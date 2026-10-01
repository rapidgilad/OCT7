using System.Collections.Generic;
using OCT7.Sim.Data;

namespace OCT7.Sim.Units
{
    /// <summary>
    /// A squad (or single vehicle) on the map. The simulation tracks the squad as one point;
    /// presentation spreads its models around that point.
    /// </summary>
    public sealed class Squad
    {
        internal Squad(int id, int ownerId, UnitDef def, Vec2 position)
        {
            Id = id;
            OwnerId = ownerId;
            Def = def;
            Position = position;
            PrevPosition = position;
            Models = def.SquadSize;
            Facing = new Vec2(0f, 1f);
        }

        public int Id { get; }
        public int OwnerId { get; }
        public UnitDef Def { get; }

        /// <summary>Position at the end of the last tick.</summary>
        public Vec2 Position { get; internal set; }

        /// <summary>Position at the end of the tick before; presentation interpolates between the two.</summary>
        public Vec2 PrevPosition { get; internal set; }

        /// <summary>Unit-length heading of the last movement.</summary>
        public Vec2 Facing { get; internal set; }

        /// <summary>Models alive in the squad.</summary>
        public int Models { get; internal set; }

        internal List<Vec2> Path { get; } = new List<Vec2>();
        internal int PathIndex { get; set; }

        public bool IsMoving => PathIndex < Path.Count;

        /// <summary>Remaining waypoints (read-only view for debug drawing).</summary>
        public IReadOnlyList<Vec2> Waypoints => Path;
        public int CurrentWaypointIndex => PathIndex;

        /// <summary>Final destination, or current position when idle.</summary>
        public Vec2 Destination => Path.Count > 0 ? Path[Path.Count - 1] : Position;
    }
}
