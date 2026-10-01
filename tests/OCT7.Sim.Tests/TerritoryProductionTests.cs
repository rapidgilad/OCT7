using System.Linq;
using OCT7.Sim.Data;
using OCT7.Sim.Match;
using OCT7.Sim.Territory;
using OCT7.Sim.World;
using Xunit;

namespace OCT7.Sim.Tests
{
    public class TerritoryTests
    {
        private static Simulation Skirmish() => MatchSetup.CreateSkirmish(TestMatch.Data, "idf", "hamas", 5);

        private static Sector SectorById(Simulation sim, string id) => sim.Territory.Sectors.First(s => s.Def.Id == id);

        [Fact]
        public void HqSectorsStartOwned_OthersNeutral()
        {
            var sim = Skirmish();
            foreach (var s in sim.Territory.Sectors)
            {
                if (s.Type == SectorType.Hq)
                {
                    Assert.True(s.OwnerId >= 0);
                }
                else
                {
                    Assert.Equal(-1, s.OwnerId);
                }
            }
        }

        [Fact]
        public void NeutralSector_IsCapturedInCaptureSeconds()
        {
            var sim = Skirmish();
            var sector = SectorById(sim, "s_west");
            var squad = sim.World.SpawnSquad(0, sim.Data.GetUnit("idf_golani"), sector.Position);
            int expected = (int)(sim.Rules.CaptureSeconds * SimConfig.TicksPerSecond);
            TestArena.Run(sim, expected - 5);
            Assert.Equal(-1, sector.OwnerId);
            var events = TestArena.Run(sim, 10);
            Assert.Equal(0, sector.OwnerId);
            Assert.Contains(events, e => e.Type == SimEventType.SectorCaptured && e.DefId == "s_west");
        }

        [Fact]
        public void EnemySector_IsNeutralizedFirst_ThenCaptured()
        {
            var sim = Skirmish();
            var sector = SectorById(sim, "s_west");
            sector.OwnerId = 1;
            sector.Progress = 1f;
            sim.World.SpawnSquad(0, sim.Data.GetUnit("idf_golani"), sector.Position);
            int capture = (int)(sim.Rules.CaptureSeconds * SimConfig.TicksPerSecond);
            TestArena.Run(sim, capture + 5);
            Assert.Equal(-1, sector.OwnerId);
            TestArena.Run(sim, capture + 5);
            Assert.Equal(0, sector.OwnerId);
        }

        [Fact]
        public void ContestedSector_DoesNotProgress_AndVehiclesCannotCapture()
        {
            var sim = Skirmish();
            var sector = SectorById(sim, "s_west");
            sim.World.SpawnSquad(0, sim.Data.GetUnit("idf_merkava"), sector.Position);
            TestArena.Run(sim, 50);
            Assert.Equal(0f, sector.Progress);

            var a = sim.World.SpawnSquad(0, sim.Data.GetUnit("idf_golani"), sector.Position);
            var b = sim.World.SpawnSquad(1, sim.Data.GetUnit("hamas_fighters"), sector.Position + new Vec2(2f, 0f));
            a.Suppression = 0f;
            sim.Step();
            Assert.True(sector.IsContested);
        }

        [Fact]
        public void Income_ComesOnlyFromConnectedSectors()
        {
            var sim = Skirmish();
            var fuel = sim.Territory.Sectors.First(s => s.Type == SectorType.Fuel && Vec2.Distance(s.Position, sim.Players[1].HqPosition) > 150f);
            fuel.OwnerId = 0;
            fuel.Progress = 1f;
            sim.Step();
            sim.Territory.CountConnected(0, out _, out _, out int connectedFuel);
            bool connected = fuel.IsConnected;
            Assert.Equal(connected ? 1 : 0, connectedFuel);

            // Own everything on player 0's half: the fuel sector must now be connected and pay out.
            foreach (var s in sim.Territory.Sectors.Where(s => s.Type != SectorType.Hq && Vec2.Distance(s.Position, sim.Players[0].HqPosition) < Vec2.Distance(s.Position, sim.Players[1].HqPosition)))
            {
                s.OwnerId = 0;
                s.Progress = 1f;
            }

            sim.Step();
            Assert.True(fuel.IsConnected);
            Assert.True(sim.Players[0].FuelIncome > sim.Data.Economy.HqFuelPerMinute);
        }

        [Fact]
        public void Tickets_Drain_ForTheSideWithFewerVictoryPoints()
        {
            var sim = Skirmish();
            var vp = sim.Territory.Sectors.First(s => s.Type == SectorType.Victory);
            vp.OwnerId = 0;
            vp.Progress = 1f;
            TestArena.Run(sim, 100);
            Assert.Equal(sim.Data.Economy.StartingTickets, sim.Players[0].Tickets, 3);
            Assert.Equal(sim.Data.Economy.StartingTickets - 10 * sim.Rules.TicketDrainPerVpPerSecond, sim.Players[1].Tickets, 2);
        }

        [Fact]
        public void Victory_WhenTicketsRunOut()
        {
            var sim = Skirmish();
            sim.Players[1].Tickets = 0.01;
            var vp = sim.Territory.Sectors.First(s => s.Type == SectorType.Victory);
            vp.OwnerId = 0;
            var events = TestArena.Run(sim, 5);
            Assert.True(sim.IsOver);
            Assert.Equal(0, sim.WinnerId);
            Assert.Contains(events, e => e.Type == SimEventType.MatchEnded && e.Value == 0);
        }

        [Fact]
        public void Victory_WhenEnemyHqIsDestroyed()
        {
            var sim = Skirmish();
            var hq = sim.World.FindHq(1);
            hq.Health = 0f;
            sim.Production.DestroyStructure(hq);
            sim.Step();
            Assert.True(sim.IsOver);
            Assert.Equal(0, sim.WinnerId);
        }
    }

    public class ProductionTests
    {
        private static Simulation Skirmish(string f0 = "idf", string f1 = "hamas") => MatchSetup.CreateSkirmish(TestMatch.Data, f0, f1, 5);

        [Fact]
        public void Produce_ReservesCost_AndSpawnsAfterBuildTime()
        {
            var sim = Skirmish();
            var hq = sim.World.FindHq(0);
            var player = sim.Players[0];
            var engineers = sim.Data.GetUnit("idf_engineers");
            double mp = player.Manpower;
            int squads = sim.World.Squads.Count(s => s.OwnerId == 0);
            sim.Enqueue(new ProduceCommand(0, hq.Id, engineers.Id));
            sim.Step();
            Assert.Equal(mp - engineers.Manpower + player.ManpowerIncome / SimConfig.TicksPerMinute, player.Manpower, 1);
            Assert.Single(hq.Queue);

            var events = TestArena.Run(sim, (int)(engineers.BuildTime * SimConfig.TicksPerSecond) + 2);
            Assert.Contains(events, e => e.Type == SimEventType.UnitProduced && e.DefId == engineers.Id);
            Assert.Equal(squads + 1, sim.World.Squads.Count(s => s.OwnerId == 0));
            Assert.Empty(hq.Queue);
        }

        [Fact]
        public void Produce_IsRejected_WhenLockedPoorOrQueueFull()
        {
            var sim = Skirmish();
            var hq = sim.World.FindHq(0);
            Assert.Equal(RejectReason.NotUnlocked, sim.Production.TryEnqueue(0, hq.Id, "idf_golani"));   // T1 unit at HQ
            Assert.Equal(RejectReason.NotOwner, sim.Production.TryEnqueue(1, hq.Id, "idf_engineers"));
            sim.Players[0].Manpower = 10;
            Assert.Equal(RejectReason.NotEnoughResources, sim.Production.TryEnqueue(0, hq.Id, "idf_engineers"));
            sim.Players[0].Manpower = 100000;
            for (int i = 0; i < sim.Rules.MaxQueueLength; i++)
            {
                Assert.Equal(RejectReason.None, sim.Production.TryEnqueue(0, hq.Id, "idf_engineers"));
            }

            Assert.Equal(RejectReason.QueueFull, sim.Production.TryEnqueue(0, hq.Id, "idf_engineers"));
        }

        [Fact]
        public void PopCap_BlocksProduction()
        {
            var sim = Skirmish();
            var hq = sim.World.FindHq(0);
            sim.Players[0].Manpower = 100000;
            for (int i = 0; i < 20; i++)
            {
                sim.World.SpawnSquad(0, sim.Data.GetUnit("idf_merkava"), new Vec2(40f, 60f));
                if (sim.World.PopulationOf(0) > sim.Data.Economy.PopCap - 5)
                {
                    break;
                }
            }

            Assert.Equal(RejectReason.PopCap, sim.Production.TryEnqueue(0, hq.Id, "idf_engineers"));
        }

        [Fact]
        public void Cancel_RefundsTheFullCost()
        {
            var sim = Skirmish();
            var hq = sim.World.FindHq(0);
            double mp = sim.Players[0].Manpower;
            Assert.Equal(RejectReason.None, sim.Production.TryEnqueue(0, hq.Id, "idf_engineers"));
            Assert.True(sim.Production.Cancel(0, hq.Id, 0));
            Assert.Equal(mp, sim.Players[0].Manpower, 6);
        }

        [Fact]
        public void Engineers_BuildT1_WhichUnlocksItsUnits()
        {
            var sim = Skirmish();
            var engineers = sim.World.Squads.First(s => s.OwnerId == 0 && s.Def.Engineer);
            var site = sim.Map.Grid.WorldToCell(new Vec2(52f, 30f));
            sim.Enqueue(new ConstructCommand(0, new[] { engineers.Id }, "idf_t1", site));
            var events = TestArena.Run(sim, 5);
            Assert.DoesNotContain(events, e => e.Type == SimEventType.CommandRejected);
            var t1 = sim.World.Structures.First(s => s.Def.Id == "idf_t1");
            Assert.False(t1.IsComplete);
            Assert.False(sim.Map.Grid.IsWalkable(site), "the footprint blocks movement immediately");

            var buildTicks = (int)(t1.Def.BuildTime * SimConfig.TicksPerSecond) + 300;
            events = TestArena.Run(sim, buildTicks);
            Assert.True(t1.IsComplete);
            Assert.Contains(events, e => e.Type == SimEventType.StructureCompleted && e.TargetId == t1.Id);
            Assert.Equal(t1.MaxHealth, t1.Health, 0);
            Assert.Equal(RejectReason.None, sim.Production.TryEnqueue(0, t1.Id, "idf_golani"));
        }

        [Fact]
        public void Placement_IsRejected_OnObstacles_EnemyTerritory_OrWithoutTech()
        {
            var sim = Skirmish();
            var t1 = sim.Data.GetStructure("idf_t1");
            var t2 = sim.Data.GetStructure("idf_t2");
            Assert.Equal(RejectReason.InvalidPlacement, sim.Production.CanPlace(0, t1, new GridPos(65, 67), 0));    // inside a building
            Assert.Equal(RejectReason.InvalidPlacement, sim.Production.CanPlace(0, t1, sim.Map.Grid.WorldToCell(new Vec2(250f, 250f)), 0)); // enemy base
            Assert.Equal(RejectReason.NotUnlocked, sim.Production.CanPlace(0, t2, sim.Map.Grid.WorldToCell(new Vec2(52f, 30f)), 0));
            Assert.Equal(RejectReason.NotOwner, sim.Production.CanPlace(0, sim.Data.GetStructure("hamas_t1"), sim.Map.Grid.WorldToCell(new Vec2(52f, 30f)), 0));
        }

        [Fact]
        public void Sandbags_AreWalkable_AndGiveHeavyCover()
        {
            var sim = Skirmish();
            var engineers = sim.World.Squads.First(s => s.OwnerId == 0 && s.Def.Engineer);
            var site = sim.Map.Grid.WorldToCell(new Vec2(60f, 50f));
            Assert.Equal(CoverType.None, sim.Cover.GetCover(new GridPos(site.X, site.Y + 1)));
            sim.Enqueue(new ConstructCommand(0, new[] { engineers.Id }, "sandbags", site, 0));
            TestArena.Run(sim, 400);
            var bags = sim.World.Structures.First(s => s.Def.Id == "sandbags");
            Assert.True(bags.IsComplete);
            Assert.True(sim.Map.Grid.IsWalkable(site));
            Assert.Equal(CoverType.Heavy, sim.Cover.GetCover(new GridPos(site.X, site.Y + 1)));
        }

        [Fact]
        public void DestroyedStructures_FreeTheirCells()
        {
            var sim = Skirmish();
            var hq = sim.World.FindHq(1);
            var cell = sim.Map.Grid.WorldToCell(hq.Center);
            Assert.False(sim.Map.Grid.IsWalkable(cell));
            sim.Production.DestroyStructure(hq);
            Assert.True(sim.Map.Grid.IsWalkable(cell));
            Assert.Equal(0, sim.World.StructureAt(cell));
        }

        [Theory]
        [InlineData("idf", "hamas")]
        [InlineData("hamas", "hezbollah")]
        [InlineData("hezbollah", "idf")]
        public void Skirmish_StartsWithHqs_StartingUnits_AndHqSectors(string f0, string f1)
        {
            var sim = Skirmish(f0, f1);
            for (int p = 0; p < 2; p++)
            {
                Assert.NotNull(sim.World.FindHq(p));
                Assert.Equal(sim.Data.GetFaction(sim.Players[p].FactionId).StartingUnits.Count, sim.World.Squads.Count(s => s.OwnerId == p));
                Assert.All(sim.World.Squads, s => Assert.True(sim.Map.Grid.IsWalkable(sim.Map.Grid.WorldToCell(s.Position))));
            }
        }
    }
}
