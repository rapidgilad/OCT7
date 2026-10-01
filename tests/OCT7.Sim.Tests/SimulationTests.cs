using System;
using System.Collections.Generic;
using System.Linq;
using OCT7.Sim.AI;
using OCT7.Sim.Data;
using OCT7.Sim.Match;
using OCT7.Tools;
using Xunit;

namespace OCT7.Sim.Tests
{
    public class MovementTests
    {
        [Fact]
        public void Squad_ArrivesInExpectedTicks_OnOpenGround()
        {
            var sim = TestMatch.OpenMap();
            var def = sim.Data.GetUnit("idf_golani");
            var squad = sim.World.SpawnSquad(0, def, new Vec2(10f, 10f));
            var target = new Vec2(70f, 10f);
            sim.Enqueue(new MoveSquadsCommand(0, new[] { squad.Id }, target));

            float distance = Vec2.Distance(squad.Position, target);
            int expectedTicks = (int)Math.Ceiling(distance / (def.MoveSpeed * SimConfig.TickSeconds));

            int ticks = 0;
            sim.Step(); // applies the command and moves on the same tick
            ticks++;
            while (squad.IsMoving && ticks < 1000)
            {
                sim.Step();
                ticks++;
            }

            Assert.False(squad.IsMoving);
            Assert.True(Vec2.Distance(squad.Position, target) < 0.01f);
            Assert.InRange(ticks, expectedTicks - 1, expectedTicks + 1);
        }

        [Fact]
        public void Squad_IgnoresCommandsFromOtherPlayers()
        {
            var sim = TestMatch.OpenMap();
            var squad = sim.World.SpawnSquad(0, sim.Data.GetUnit("idf_golani"), new Vec2(10f, 10f));
            sim.Enqueue(new MoveSquadsCommand(1, new[] { squad.Id }, new Vec2(60f, 60f)));
            sim.Step();
            Assert.False(squad.IsMoving);
        }

        [Fact]
        public void MultipleSquads_SpreadIntoFormation()
        {
            var sim = TestMatch.OpenMap();
            var def = sim.Data.GetUnit("idf_golani");
            var ids = new List<int>();
            for (int i = 0; i < 4; i++)
            {
                ids.Add(sim.World.SpawnSquad(0, def, new Vec2(10f + i * 3f, 10f)).Id);
            }

            sim.Enqueue(new MoveSquadsCommand(0, ids, new Vec2(80f, 80f)));
            for (int i = 0; i < 600; i++)
            {
                sim.Step();
            }

            var finals = ids.Select(id => sim.World.GetSquad(id).Position).ToList();
            for (int i = 0; i < finals.Count; i++)
            {
                for (int j = i + 1; j < finals.Count; j++)
                {
                    Assert.True(Vec2.Distance(finals[i], finals[j]) >= 5f, "Squads should not stack on the same spot");
                }
            }
        }

        [Fact]
        public void PrevPosition_TracksLastTick_ForInterpolation()
        {
            var sim = TestMatch.OpenMap();
            var squad = sim.World.SpawnSquad(0, sim.Data.GetUnit("idf_golani"), new Vec2(10f, 10f));
            sim.Enqueue(new MoveSquadsCommand(0, new[] { squad.Id }, new Vec2(60f, 10f)));
            sim.Step();
            var before = squad.Position;
            sim.Step();
            Assert.Equal(before, squad.PrevPosition);
            Assert.NotEqual(squad.PrevPosition, squad.Position);
        }
    }

    public class DeterminismTests
    {
        private static ulong RunSandbox(ulong seed, int ticks, Action<Simulation> extraCommands = null)
        {
            var sim = MatchSetup.CreateSandboxMatch(TestMatch.Data, "idf", "hamas", seed);
            var ais = new List<IAiController> { new AdvanceAi(0, seed), new AdvanceAi(1, seed) };
            var buffer = new List<Command>();
            extraCommands?.Invoke(sim);
            for (int i = 0; i < ticks; i++)
            {
                SimLoop.Step(sim, ais, buffer);
            }

            return sim.ComputeStateHash();
        }

        [Fact]
        public void IdenticalRuns_ProduceIdenticalHashes()
        {
            Assert.Equal(RunSandbox(7, 1200), RunSandbox(7, 1200));
        }

        [Fact]
        public void DifferentCommands_ProduceDifferentHashes()
        {
            ulong baseline = RunSandbox(7, 300);
            ulong changed = RunSandbox(7, 300, sim =>
            {
                var first = sim.World.Squads.First(s => s.OwnerId == 0);
                sim.Enqueue(new MoveSquadsCommand(0, new[] { first.Id }, new Vec2(120f, 30f)) { Tick = 5 });
            });
            Assert.NotEqual(baseline, changed);
        }

        [Fact]
        public void AdvanceAi_MovesForcesTowardEachOther()
        {
            var sim = MatchSetup.CreateSandboxMatch(TestMatch.Data, "idf", "hamas", 3);
            var ais = new List<IAiController> { new AdvanceAi(0, 3), new AdvanceAi(1, 3) };
            var buffer = new List<Command>();

            float Spread() => Vec2.Distance(Centroid(sim, 0), Centroid(sim, 1));
            float start = Spread();
            for (int i = 0; i < 1500; i++)
            {
                SimLoop.Step(sim, ais, buffer);
            }

            Assert.True(Spread() < start * 0.6f, $"Forces should close distance (start {start}, now {Spread()})");
        }

        private static Vec2 Centroid(Simulation sim, int player)
        {
            var squads = sim.World.Squads.Where(s => s.OwnerId == player).ToList();
            var sum = Vec2.Zero;
            foreach (var s in squads)
            {
                sum += s.Position;
            }

            return sum / squads.Count;
        }
    }

    public class GameDataTests
    {
        [Fact]
        public void RealData_LoadsAndValidates()
        {
            var data = RepoData.Load();
            var errors = GameDataValidator.Validate(data);
            Assert.True(errors.Count == 0, string.Join(Environment.NewLine, errors));
        }

        [Theory]
        [InlineData("idf")]
        [InlineData("hamas")]
        [InlineData("hezbollah")]
        public void EachFaction_HasCoreRosterAndOneHero(string factionId)
        {
            var units = TestMatch.Data.UnitsOfFaction(factionId);
            Assert.Equal(1, units.Count(u => u.Category == UnitCategory.Hero));
            Assert.True(units.Count(u => u.Category != UnitCategory.Hero) >= 9, $"{factionId} needs >= 9 core units");
            Assert.NotEmpty(TestMatch.Data.GetFaction(factionId).StartingUnits);
            Assert.NotEmpty(TestMatch.Data.GetFaction(factionId).SandboxUnits);
        }

        [Fact]
        public void Validator_CatchesBrokenData()
        {
            var economy = new EconomyDef { PopCap = 0 };
            var factions = new List<FactionDef> { new FactionDef { Id = "a", DisplayNameKey = "k", SandboxUnits = new List<string> { "missing" } } };
            var units = new List<UnitDef>
            {
                new UnitDef { Id = "u1", FactionId = "a", SquadSize = 1, Pop = 1, MoveSpeed = 1 },
                new UnitDef { Id = "u1", FactionId = "nope", SquadSize = 0, Pop = 0, MoveSpeed = 0, Manpower = -5 },
            };
            var errors = GameDataValidator.Validate(new GameDataSet(economy, factions, units));
            Assert.Contains(errors, e => e.Contains("popCap"));
            Assert.Contains(errors, e => e.Contains("Duplicate unit id"));
            Assert.Contains(errors, e => e.Contains("unknown faction"));
            Assert.Contains(errors, e => e.Contains("negative cost"));
            Assert.Contains(errors, e => e.Contains("unknown unit 'missing'"));
        }

        [Theory]
        [InlineData("idf", "hamas")]
        [InlineData("idf", "hezbollah")]
        [InlineData("hamas", "hezbollah")]
        [InlineData("hamas", "hamas")]
        public void AnyMatchup_CanBeCreated(string f0, string f1)
        {
            var sim = MatchSetup.CreateSandboxMatch(TestMatch.Data, f0, f1, 1);
            Assert.Equal(2, sim.Players.Count);
            Assert.Contains(sim.World.Squads, s => s.OwnerId == 0);
            Assert.Contains(sim.World.Squads, s => s.OwnerId == 1);
            foreach (var s in sim.World.Squads)
            {
                Assert.True(sim.Map.Grid.IsWalkable(sim.Map.Grid.WorldToCell(s.Position)), $"{s.Def.Id} spawned inside an obstacle");
            }
        }
    }
}
