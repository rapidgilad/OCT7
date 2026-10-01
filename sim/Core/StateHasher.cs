using System;

namespace OCT7.Sim
{
    /// <summary>
    /// FNV-1a hash of the full simulation state. Two runs with the same seed and commands must produce
    /// the same hash every tick; tests and MatchRunner use this to catch nondeterminism.
    /// </summary>
    public static class StateHasher
    {
        private const ulong OffsetBasis = 14695981039346656037UL;
        private const ulong Prime = 1099511628211UL;

        public static ulong Hash(Simulation sim)
        {
            ulong h = OffsetBasis;
            Mix(ref h, sim.Tick);
            Mix(ref h, sim.Random.State0);
            Mix(ref h, sim.Random.State1);

            var players = sim.Players;
            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];
                Mix(ref h, p.Id);
                Mix(ref h, p.Manpower);
                Mix(ref h, p.Munitions);
                Mix(ref h, p.Fuel);
                Mix(ref h, p.Tickets);
            }

            var squads = sim.World.Squads;
            for (int i = 0; i < squads.Count; i++)
            {
                var s = squads[i];
                Mix(ref h, s.Id);
                Mix(ref h, s.OwnerId);
                Mix(ref h, s.Models);
                Mix(ref h, s.Position.X);
                Mix(ref h, s.Position.Y);
                Mix(ref h, s.CurrentWaypointIndex);
                Mix(ref h, s.Waypoints.Count);
            }

            return h;
        }

        private static void Mix(ref ulong h, float value) => Mix(ref h, BitConverter.SingleToInt32Bits(value));

        private static void Mix(ref ulong h, double value) => Mix(ref h, unchecked((ulong)BitConverter.DoubleToInt64Bits(value)));

        private static void Mix(ref ulong h, int value) => Mix(ref h, unchecked((ulong)(uint)value));

        private static void Mix(ref ulong h, ulong value)
        {
            for (int i = 0; i < 8; i++)
            {
                h ^= (value >> (i * 8)) & 0xFF;
                h *= Prime;
            }
        }
    }
}
