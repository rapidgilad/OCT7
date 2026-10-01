using System.Collections.Generic;
using OCT7.Sim.Data;
using OCT7.Sim.World;
using OCT7.Tools;
using Xunit;

namespace OCT7.Sim.Tests
{
    public class DeterministicRandomTests
    {
        [Fact]
        public void SameSeed_ProducesSameSequence()
        {
            var a = new DeterministicRandom(1234);
            var b = new DeterministicRandom(1234);
            for (int i = 0; i < 1000; i++)
            {
                Assert.Equal(a.NextULong(), b.NextULong());
            }
        }

        [Fact]
        public void DifferentSeeds_ProduceDifferentSequences()
        {
            var a = new DeterministicRandom(1);
            var b = new DeterministicRandom(2);
            int same = 0;
            for (int i = 0; i < 100; i++)
            {
                if (a.NextULong() == b.NextULong())
                {
                    same++;
                }
            }

            Assert.True(same < 5);
        }

        [Fact]
        public void NextInt_StaysInRange()
        {
            var r = new DeterministicRandom(99);
            for (int i = 0; i < 10000; i++)
            {
                int v = r.NextInt(-3, 7);
                Assert.InRange(v, -3, 6);
            }
        }

        [Fact]
        public void NextFloat_StaysInUnitInterval()
        {
            var r = new DeterministicRandom(7);
            for (int i = 0; i < 10000; i++)
            {
                float v = r.NextFloat();
                Assert.True(v >= 0f && v < 1f);
            }
        }
    }

    public class CommandOrderingTests
    {
        private sealed class RecordingCommand : Command
        {
            private readonly List<string> _log;
            private readonly string _name;

            public RecordingCommand(List<string> log, string name, int playerId, int tick)
            {
                _log = log;
                _name = name;
                PlayerId = playerId;
                Tick = tick;
            }

            public override void Apply(Simulation sim) => _log.Add($"{sim.Tick}:{_name}");
        }

        [Fact]
        public void Commands_RunOnTheirTick_InPlayerThenSequenceOrder()
        {
            var sim = TestMatch.EmptySandbox();
            var log = new List<string>();
            sim.Enqueue(new RecordingCommand(log, "p1-first", 1, 2));
            sim.Enqueue(new RecordingCommand(log, "p0-late", 0, 3));
            sim.Enqueue(new RecordingCommand(log, "p0-a", 0, 2));
            sim.Enqueue(new RecordingCommand(log, "p0-b", 0, 2));
            sim.Enqueue(new RecordingCommand(log, "now", 1, 0));

            for (int i = 0; i < 5; i++)
            {
                sim.Step();
            }

            Assert.Equal(new[] { "0:now", "2:p0-a", "2:p0-b", "2:p1-first", "3:p0-late" }, log);
        }

        [Fact]
        public void PastTickCommands_RunOnNextStep()
        {
            var sim = TestMatch.EmptySandbox();
            for (int i = 0; i < 10; i++)
            {
                sim.Step();
            }

            var log = new List<string>();
            sim.Enqueue(new RecordingCommand(log, "late", 0, 2));
            sim.Step();
            Assert.Equal(new[] { "10:late" }, log);
        }
    }

    public class EconomyTests
    {
        [Fact]
        public void OneMinute_GivesHqIncome()
        {
            var sim = TestMatch.EmptySandbox();
            var p = sim.Players[0];
            double startMp = p.Manpower;
            double startMu = p.Munitions;
            double startFu = p.Fuel;

            for (int i = 0; i < SimConfig.TicksPerMinute; i++)
            {
                sim.Step();
            }

            var e = sim.Data.Economy;
            Assert.Equal(startMp + e.HqManpowerPerMinute, p.Manpower, 6);
            Assert.Equal(startMu + e.HqMunitionsPerMinute, p.Munitions, 6);
            Assert.Equal(startFu + e.HqFuelPerMinute, p.Fuel, 6);
        }

        [Fact]
        public void Upkeep_ReducesManpowerIncome_AbovePopThreshold()
        {
            var sim = TestMatch.EmptySandbox();
            var golani = sim.Data.GetUnit("idf_golani"); // pop 7
            for (int i = 0; i < 5; i++)
            {
                sim.World.SpawnSquad(0, golani, new Vec2(30f + i * 4f, 30f));
            }

            // 35 pop - 20 free = 15 pop * 1.5 = 22.5 MP/min upkeep
            var e = sim.Data.Economy;
            float expected = e.HqManpowerPerMinute - (35 - e.UpkeepFreePop) * e.UpkeepManpowerPerPopPerMinute;
            Assert.Equal(expected, sim.Economy.ManpowerIncomePerMinute(sim.Players[0]), 3);
        }

        [Fact]
        public void Players_StartWithEconomyDefaults()
        {
            var sim = TestMatch.EmptySandbox();
            var e = sim.Data.Economy;
            foreach (var p in sim.Players)
            {
                Assert.Equal(e.StartManpower, p.Manpower);
                Assert.Equal(e.StartMunitions, p.Munitions);
                Assert.Equal(e.StartFuel, p.Fuel);
                Assert.Equal(e.StartingTickets, p.Tickets);
            }
        }
    }

    /// <summary>Helpers to build small matches for tests.</summary>
    internal static class TestMatch
    {
        private static GameDataSet _data;

        public static GameDataSet Data => _data ??= RepoData.Load();

        /// <summary>Sandbox map, IDF vs Hamas, no units.</summary>
        public static Simulation EmptySandbox(ulong seed = 42)
        {
            var map = MapFactory.CreateSandbox();
            var sim = new Simulation(Data, map, seed);
            sim.AddPlayer("idf", map.HqPositions[0]);
            sim.AddPlayer("hamas", map.HqPositions[1]);
            return sim;
        }

        /// <summary>Open map with no obstacles.</summary>
        public static Simulation OpenMap(int size = 64, ulong seed = 42)
        {
            var grid = new MapGrid(size, size, 2f);
            var map = new MapDefinition("open", "Open", grid, new List<MapObstacle>(), new[] { new Vec2(8f, 8f), new Vec2(size * 2f - 8f, size * 2f - 8f) });
            var sim = new Simulation(Data, map, seed);
            sim.AddPlayer("idf", map.HqPositions[0]);
            sim.AddPlayer("hamas", map.HqPositions[1]);
            return sim;
        }
    }
}
