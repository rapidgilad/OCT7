using System.Collections.Generic;
using System.Linq;
using OCT7.Sim.Data;
using OCT7.Sim.Units;
using OCT7.Sim.World;

namespace OCT7.Sim.Tests
{
    /// <summary>Small hand-built battlefields for combat/support tests.</summary>
    internal static class TestArena
    {
        /// <summary>Open 120 x 120 m map, IDF (player 0) vs Hamas (player 1), no HQs, started.</summary>
        public static Simulation Open(ulong seed = 7, params MapObstacle[] obstacles)
        {
            var grid = new MapGrid(60, 60, 2f);
            foreach (var o in obstacles)
            {
                MapFactory.ApplyObstacle(grid, o, 2.5f);
            }

            var map = new MapDefinition("arena", "Arena", grid, obstacles.ToList(), new[] { new Vec2(10f, 10f), new Vec2(110f, 110f) });
            var sim = new Simulation(TestMatch.Data, map, seed);
            sim.AddPlayer("idf", map.HqPositions[0]);
            sim.AddPlayer("hamas", map.HqPositions[1]);
            sim.Start();
            return sim;
        }

        public static Squad Spawn(Simulation sim, int player, string unitId, float x, float y)
        {
            var s = sim.World.SpawnSquad(player, TestMatch.Data.GetUnit(unitId), new Vec2(x, y));
            return s;
        }

        /// <summary>Steps the sim, collecting every event.</summary>
        public static List<SimEvent> Run(Simulation sim, int ticks)
        {
            var events = new List<SimEvent>();
            for (int i = 0; i < ticks; i++)
            {
                sim.Step();
                events.AddRange(sim.Events);
            }

            return events;
        }

        public static void Face(Squad s, Vec2 direction) => s.Facing = direction.Normalized();
    }
}
