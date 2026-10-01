using System.Collections.Generic;
using System.Linq;
using OCT7.Sim.AI;
using OCT7.Sim.Data;
using OCT7.Sim.Match;
using Xunit;

namespace OCT7.Sim.Tests
{
    public class AiTests
    {
        private static (Simulation sim, List<IAiController> ais) Match(string f0, string f1, string d0, string d1, ulong seed)
        {
            var sim = MatchSetup.CreateSkirmish(TestMatch.Data, f0, f1, seed);
            var ais = new List<IAiController> { SkirmishAi.Create(sim, 0, d0), SkirmishAi.Create(sim, 1, d1) };
            return (sim, ais);
        }

        private static List<SimEvent> Run(Simulation sim, List<IAiController> ais, int ticks)
        {
            var buffer = new List<Command>();
            var events = new List<SimEvent>();
            for (int i = 0; i < ticks && !sim.IsOver; i++)
            {
                SimLoop.Step(sim, ais, buffer);
                events.AddRange(sim.Events);
            }

            return events;
        }

        [Theory]
        [InlineData("idf", "hamas")]
        [InlineData("hezbollah", "idf")]
        [InlineData("hamas", "hezbollah")]
        public void Ai_TechesUp_Captures_AndFights(string f0, string f1)
        {
            var (sim, ais) = Match(f0, f1, "normal", "normal", 3);
            var events = Run(sim, ais, 8 * SimConfig.TicksPerMinute);
            for (int p = 0; p < 2; p++)
            {
                Assert.Contains(sim.World.Structures, s => s.OwnerId == p && s.Def.Tier == 1 && s.IsComplete);
                Assert.Contains(events, e => e.Type == SimEventType.UnitProduced && e.PlayerId == p);
                Assert.Contains(events, e => e.Type == SimEventType.SectorCaptured && e.PlayerId == p);
            }

            Assert.Contains(events, e => e.Type == SimEventType.ModelKilled);
        }

        [Fact]
        public void Ai_PlaysAFullMatch_ToVictory()
        {
            var (sim, ais) = Match("idf", "hamas", "normal", "normal", 1);
            Run(sim, ais, 45 * SimConfig.TicksPerMinute);
            Assert.True(sim.IsOver, $"match still running after 45 min (tickets {sim.Players[0].Tickets:0}/{sim.Players[1].Tickets:0})");
            Assert.InRange(sim.WinnerId, 0, 1);
        }

        [Fact]
        public void Ai_Matches_AreDeterministic()
        {
            var (a, aisA) = Match("hamas", "hezbollah", "hard", "easy", 9);
            var (b, aisB) = Match("hamas", "hezbollah", "hard", "easy", 9);
            Run(a, aisA, 5 * SimConfig.TicksPerMinute);
            Run(b, aisB, 5 * SimConfig.TicksPerMinute);
            Assert.Equal(a.ComputeStateHash(), b.ComputeStateHash());
        }

        [Fact]
        public void Ai_RarelyIssuesRejectedCommands()
        {
            var (sim, ais) = Match("idf", "hezbollah", "hard", "hard", 4);
            var events = Run(sim, ais, 10 * SimConfig.TicksPerMinute);
            int rejected = events.Count(e => e.Type == SimEventType.CommandRejected);
            int produced = events.Count(e => e.Type == SimEventType.UnitProduced);
            Assert.True(rejected <= produced / 2 + 2, $"rejected {rejected} vs produced {produced}");
        }

        [Fact]
        public void AllDifficulties_Exist_InData()
        {
            foreach (var id in new[] { "easy", "normal", "hard" })
            {
                Assert.NotNull(TestMatch.Data.GetDifficulty(id));
            }
        }
    }
}
