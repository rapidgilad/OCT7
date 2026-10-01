using System.Linq;
using OCT7.Sim.Combat;
using OCT7.Sim.Data;
using OCT7.Sim.Units;
using OCT7.Sim.World;
using Xunit;

namespace OCT7.Sim.Tests
{
    public class CombatTests
    {
        [Fact]
        public void InfantrySquads_AutoEngage_AndLoseModels()
        {
            var sim = TestArena.Open();
            var golani = TestArena.Spawn(sim, 0, "idf_golani", 40f, 60f);
            var fighters = TestArena.Spawn(sim, 1, "hamas_fighters", 65f, 60f);
            var events = TestArena.Run(sim, 200);

            Assert.Contains(events, e => e.Type == SimEventType.ShotFired && e.SourceId == golani.Id);
            Assert.Contains(events, e => e.Type == SimEventType.ShotFired && e.SourceId == fighters.Id);
            Assert.Contains(events, e => e.Type == SimEventType.ModelKilled);
        }

        [Fact]
        public void HeavyCover_HalvesHitChance_OnlyFromTheCoverSide()
        {
            // Building wall at x = 30 (cells 30..31 → world 60..64). Target hugs its west face.
            var sim = TestArena.Open(7, new MapObstacle(30, 10, 31, 50, 6f, ObstacleKind.Building));
            var target = TestArena.Spawn(sim, 1, "hamas_fighters", 59f, 60f);
            var eastShooter = TestArena.Spawn(sim, 0, "idf_golani", 77f, 60f);   // beyond the wall, 18 m (mid band)
            var westShooter = TestArena.Spawn(sim, 0, "idf_golani", 41f, 60f);   // same side as the target, 18 m
            var tavor = TestMatch.Data.GetWeapon("tavor");

            float covered = sim.Combat.HitChance(eastShooter, tavor, target);
            float open = sim.Combat.HitChance(westShooter, tavor, target);
            Assert.Equal(CoverType.Heavy, sim.Cover.CoverAgainst(target.Position, eastShooter.Position, sim.Rules.CoverDirectionThreshold));
            Assert.Equal(tavor.AccuracyMid * sim.Rules.HeavyCoverMultiplier, covered, 3);
            Assert.Equal(tavor.AccuracyMid, open, 3);
        }

        [Fact]
        public void RetreatingTargets_AreHarderToHit()
        {
            var sim = TestArena.Open();
            var shooter = TestArena.Spawn(sim, 0, "idf_golani", 40f, 60f);
            var target = TestArena.Spawn(sim, 1, "hamas_fighters", 60f, 60f);
            var w = TestMatch.Data.GetWeapon("tavor");
            float normal = sim.Combat.HitChance(shooter, w, target);
            target.IsRetreating = true;
            Assert.Equal(normal * sim.Rules.RetreatReceivedAccuracy, sim.Combat.HitChance(shooter, w, target), 4);
        }

        [Fact]
        public void MachineGun_SuppressesInfantry_AndPinnedSquadsCannotMove()
        {
            var sim = TestArena.Open();
            var mg = TestArena.Spawn(sim, 0, "idf_mag_team", 40f, 60f);
            var target = TestArena.Spawn(sim, 1, "hamas_fighters", 60f, 60f);
            TestArena.Run(sim, 60);
            Assert.True(target.Suppression >= sim.Rules.SuppressedThreshold, $"suppression {target.Suppression}");

            target.Suppression = 1f;
            sim.Step();
            Assert.Equal(SuppressionState.Pinned, target.SuppressionState);
            Assert.Equal(0f, sim.Movement.SpeedOf(target));
            target.IsRetreating = true;
            Assert.True(sim.Movement.SpeedOf(target) > 0f, "retreat ignores pinning");
        }

        [Fact]
        public void SetupWeapons_FireOnlyAfterStandingStill()
        {
            var sim = TestArena.Open();
            var mg = TestArena.Spawn(sim, 0, "idf_mag_team", 40f, 60f);
            TestArena.Spawn(sim, 1, "hamas_fighters", 60f, 60f);
            var setup = TestMatch.Data.GetWeapon("mag").SetupTime;
            var events = TestArena.Run(sim, (int)(setup * SimConfig.TicksPerSecond) - 2);
            Assert.DoesNotContain(events, e => e.Type == SimEventType.ShotFired && e.SourceId == mg.Id);
            events = TestArena.Run(sim, 10);
            Assert.Contains(events, e => e.Type == SimEventType.ShotFired && e.SourceId == mg.Id);
        }

        [Fact]
        public void TargetsBehindBuildings_AreNotEngaged()
        {
            var sim = TestArena.Open(7, new MapObstacle(25, 20, 27, 40, 8f, ObstacleKind.Building));
            var a = TestArena.Spawn(sim, 0, "idf_golani", 44f, 60f);
            var b = TestArena.Spawn(sim, 1, "hamas_fighters", 62f, 60f);
            var events = TestArena.Run(sim, 100);
            Assert.DoesNotContain(events, e => e.Type == SimEventType.ShotFired);
        }

        [Fact]
        public void VehicleArmor_FrontStopsMore_RearIsWeak_SmallArmsUseless()
        {
            var sim = TestArena.Open();
            var tank = TestArena.Spawn(sim, 0, "idf_merkava", 60f, 60f);
            TestArena.Face(tank, new Vec2(1f, 0f));
            var rpg = TestMatch.Data.GetWeapon("yassin105");
            var rifle = TestMatch.Data.GetWeapon("ak47");
            float front = CombatSystem.PenetrationChance(rpg, tank, new Vec2(90f, 60f));
            float rear = CombatSystem.PenetrationChance(rpg, tank, new Vec2(30f, 60f));
            Assert.Equal(rpg.Penetration / tank.Def.ArmorFront, front, 3);
            Assert.Equal(1f, rear);
            Assert.True(CombatSystem.PenetrationChance(rifle, tank, new Vec2(30f, 60f)) < 0.01f);
        }

        [Fact]
        public void RifleSquads_DoNotFireAtTanks()
        {
            var sim = TestArena.Open();
            TestArena.Spawn(sim, 0, "idf_merkava", 60f, 60f).Weapons.ToList(); // tank exists
            var fighters = TestArena.Spawn(sim, 1, "hamas_fighters", 80f, 60f);
            var events = TestArena.Run(sim, 50);
            Assert.DoesNotContain(events, e => e.Type == SimEventType.ShotFired && e.SourceId == fighters.Id);
        }

        [Fact]
        public void AntiTank_PrefersVehicles_AndKillsThem()
        {
            var sim = TestArena.Open(11);
            var tank = TestArena.Spawn(sim, 0, "idf_d9r", 60f, 60f);       // unarmed, so it can't kill the team first
            TestArena.Spawn(sim, 0, "idf_golani", 58f, 50f);               // infantry also in Kornet range
            TestArena.Face(tank, new Vec2(-1f, 0f)); // rear toward the Kornet
            var kornet = TestArena.Spawn(sim, 1, "hamas_kornet_team", 95f, 60f);
            var events = TestArena.Run(sim, 600);
            Assert.All(events.Where(e => e.Type == SimEventType.ShotFired && e.SourceId == kornet.Id), e => Assert.Equal(tank.Id, e.TargetId));
            Assert.Contains(events, e => e.Type == SimEventType.SquadDestroyed && e.TargetId == tank.Id);
        }

        [Fact]
        public void WipedSquads_AreRemoved_AndCreditTheKiller()
        {
            var sim = TestArena.Open(3);
            var golani = TestArena.Spawn(sim, 0, "idf_golani", 50f, 60f);
            var victim = TestArena.Spawn(sim, 1, "hamas_ghoul_sniper", 60f, 60f);
            victim.ModelHealth[0] = 1f;
            var events = TestArena.Run(sim, 100);
            Assert.Contains(events, e => e.Type == SimEventType.SquadDestroyed && e.TargetId == victim.Id);
            Assert.Null(sim.World.GetSquad(victim.Id));
            Assert.Equal(1, golani.Kills);
        }

        [Fact]
        public void AttackCommand_ClosesDistance_ThenFires()
        {
            var sim = TestArena.Open();
            var golani = TestArena.Spawn(sim, 0, "idf_golani", 10f, 60f);
            var target = TestArena.Spawn(sim, 1, "hamas_diggers", 100f, 60f);
            sim.Enqueue(new AttackCommand(0, new[] { golani.Id }, target.Id));
            var events = TestArena.Run(sim, 400);
            Assert.True(golani.Position.X > 50f, "should have advanced toward the target");
            Assert.Contains(events, e => e.Type == SimEventType.ShotFired && e.SourceId == golani.Id && e.TargetId == target.Id);
        }

        [Fact]
        public void MoveCommand_SnapsInfantryToNearbyCover()
        {
            var sim = TestArena.Open(7, new MapObstacle(30, 10, 31, 50, 6f, ObstacleKind.Building));
            var golani = TestArena.Spawn(sim, 0, "idf_golani", 20f, 60f);
            sim.Enqueue(new MoveSquadsCommand(0, new[] { golani.Id }, new Vec2(57f, 60f)));
            TestArena.Run(sim, 200);
            Assert.Equal(CoverType.Heavy, sim.Cover.GetCover(sim.Map.Grid.WorldToCell(golani.Position)));
        }
    }

    public class SupportTests
    {
        [Fact]
        public void Retreat_ReturnsToHq_ThenEnds()
        {
            var sim = MatchSetupSkirmish();
            var squad = sim.World.Squads.First(s => s.OwnerId == 0 && !s.Def.Engineer);
            squad.Position = new Vec2(150f, 150f);
            sim.Enqueue(new RetreatCommand(0, new[] { squad.Id }));
            sim.Step();
            Assert.True(squad.IsRetreating);
            TestArena.Run(sim, 900);
            Assert.False(squad.IsRetreating);
            Assert.True(Vec2.Distance(squad.Position, sim.World.FindHq(0).Center) <= sim.Rules.RetreatArriveDistance + 2f);
        }

        [Fact]
        public void Reinforce_RefillsModels_NearBase_ForManpower()
        {
            var sim = MatchSetupSkirmish();
            var squad = sim.World.Squads.First(s => s.OwnerId == 0 && !s.Def.Engineer);
            squad.ModelHealth[0] = 0f;
            squad.ModelHealth[1] = 0f;
            squad.Models -= 2;
            var player = sim.Players[0];
            double before = player.Manpower;
            sim.Enqueue(new ReinforceCommand(0, new[] { squad.Id }));
            TestArena.Run(sim, (int)(2 * sim.Rules.ReinforceSecondsPerModel * SimConfig.TicksPerSecond) + 5);
            Assert.Equal(squad.Def.SquadSize, squad.Models);
            double spent = before + player.ManpowerIncome * (sim.ElapsedSeconds / 60.0) - player.Manpower;
            Assert.InRange(spent, 2 * squad.Def.ReinforceCostPerModel - 5, 2 * squad.Def.ReinforceCostPerModel + 5);
        }

        [Fact]
        public void Reinforce_DoesNothing_FarFromBase()
        {
            var sim = MatchSetupSkirmish();
            var squad = sim.World.Squads.First(s => s.OwnerId == 0 && !s.Def.Engineer);
            squad.Position = new Vec2(150f, 150f);
            squad.ModelHealth[0] = 0f;
            squad.Models -= 1;
            sim.Enqueue(new ReinforceCommand(0, new[] { squad.Id }));
            TestArena.Run(sim, 100);
            Assert.Equal(squad.Def.SquadSize - 1, squad.Models);
        }

        [Fact]
        public void Squads_HealNearHq_AfterDelay()
        {
            var sim = MatchSetupSkirmish();
            var squad = sim.World.Squads.First(s => s.OwnerId == 0 && !s.Def.Engineer);
            squad.ModelHealth[0] = 10f;
            TestArena.Run(sim, 200);
            Assert.True(squad.ModelHealth[0] > 10f);
        }

        internal static Simulation MatchSetupSkirmish() => Match.MatchSetup.CreateSkirmish(TestMatch.Data, "idf", "hamas", 5);
    }
}
