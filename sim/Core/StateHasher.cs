using System;

namespace OCT7.Sim
{
    /// <summary>
    /// FNV-1a hash of the full simulation state. Two runs with the same seed and commands must produce
    /// the same hash every tick; tests and MatchRunner use this to catch nondeterminism.
    /// Any new simulation state must be mixed in here.
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
            Mix(ref h, sim.IsOver ? 1 : 0);
            Mix(ref h, sim.WinnerId);

            foreach (var p in sim.Players)
            {
                Mix(ref h, p.Id);
                Mix(ref h, p.Manpower);
                Mix(ref h, p.Munitions);
                Mix(ref h, p.Fuel);
                Mix(ref h, p.Tickets);
                Mix(ref h, p.IsDefeated ? 1 : 0);
            }

            foreach (var s in sim.World.Squads)
            {
                Mix(ref h, s.Id);
                Mix(ref h, s.OwnerId);
                Mix(ref h, s.Models);
                Mix(ref h, s.Position.X);
                Mix(ref h, s.Position.Y);
                Mix(ref h, s.Facing.X);
                Mix(ref h, s.Facing.Y);
                Mix(ref h, s.CurrentWaypointIndex);
                Mix(ref h, s.Waypoints.Count);
                Mix(ref h, s.Suppression);
                Mix(ref h, s.TargetId);
                Mix(ref h, s.ForcedTarget ? 1 : 0);
                Mix(ref h, s.IsRetreating ? 1 : 0);
                Mix(ref h, s.ReinforcePending);
                Mix(ref h, s.BuildTargetId);
                Mix(ref h, s.LastDamagedTick);
                Mix(ref h, s.LastSuppressedTick);
                for (int i = 0; i < s.Def.SquadSize; i++)
                {
                    Mix(ref h, s.GetModelHealth(i));
                }
            }

            foreach (var st in sim.World.Structures)
            {
                Mix(ref h, st.Id);
                Mix(ref h, st.OwnerId);
                Mix(ref h, st.Health);
                Mix(ref h, st.BuildProgress);
                Mix(ref h, st.Queue.Count);
                foreach (var item in st.Queue)
                {
                    Mix(ref h, item.Elapsed);
                }
            }

            foreach (var sector in sim.Territory.Sectors)
            {
                Mix(ref h, sector.OwnerId);
                Mix(ref h, sector.CapturingPlayerId);
                Mix(ref h, sector.Progress);
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
