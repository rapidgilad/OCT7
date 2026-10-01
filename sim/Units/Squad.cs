using System.Collections.Generic;
using OCT7.Sim.Data;

namespace OCT7.Sim.Units
{
    public enum SuppressionState
    {
        None,
        Suppressed,
        Pinned,
    }

    /// <summary>
    /// A squad (or single vehicle) on the map. The simulation tracks the squad as one point with per-model health;
    /// presentation spreads its models around that point.
    /// </summary>
    public sealed class Squad
    {
        internal Squad(int id, int ownerId, UnitDef def, IReadOnlyList<WeaponDef> weapons, Vec2 position)
        {
            Id = id;
            OwnerId = ownerId;
            Def = def;
            Position = position;
            PrevPosition = position;
            Facing = new Vec2(0f, 1f);
            ModelHealth = new float[def.SquadSize];
            for (int i = 0; i < ModelHealth.Length; i++)
            {
                ModelHealth[i] = def.HealthPerModel;
            }

            Models = def.SquadSize;
            Weapons = weapons;
            Cooldowns = new float[weapons.Count];
        }

        public int Id { get; }
        public int OwnerId { get; }
        public UnitDef Def { get; }

        /// <summary>Resolved weapons, parallel to <see cref="UnitDef.Weapons"/>.</summary>
        public IReadOnlyList<WeaponDef> Weapons { get; }

        /// <summary>Position at the end of the last tick.</summary>
        public Vec2 Position { get; internal set; }

        /// <summary>Position at the end of the tick before; presentation interpolates between the two.</summary>
        public Vec2 PrevPosition { get; internal set; }

        /// <summary>Unit-length heading of the last movement (or of the current target).</summary>
        public Vec2 Facing { get; internal set; }

        /// <summary>Models alive in the squad.</summary>
        public int Models { get; internal set; }

        /// <summary>Per-model health; 0 means the model is dead.</summary>
        internal float[] ModelHealth { get; }

        public float GetModelHealth(int index) => ModelHealth[index];

        public float Health
        {
            get
            {
                float sum = 0f;
                for (int i = 0; i < ModelHealth.Length; i++)
                {
                    sum += ModelHealth[i];
                }

                return sum;
            }
        }

        public float MaxHealth => Def.HealthPerModel * Def.SquadSize;
        public float HealthFraction => MaxHealth > 0f ? Health / MaxHealth : 0f;

        // --- combat state ---
        public float Suppression { get; internal set; }
        public SuppressionState SuppressionState { get; internal set; }

        /// <summary>Current target entity id (squad or structure); 0 = none.</summary>
        public int TargetId { get; internal set; }

        /// <summary>True when the player explicitly ordered the attack.</summary>
        public bool ForcedTarget { get; internal set; }

        internal float StationarySeconds { get; set; }
        internal float[] Cooldowns { get; }
        public int LastDamagedTick { get; internal set; } = -100000;
        public int LastSuppressedTick { get; internal set; } = -100000;
        public int LastFiredTick { get; internal set; } = -100000;
        public int Kills { get; internal set; }

        // --- orders ---
        public bool IsRetreating { get; internal set; }

        /// <summary>Models still to reinforce (set by the Reinforce command).</summary>
        public int ReinforcePending { get; internal set; }

        internal float ReinforceTimer { get; set; }

        /// <summary>Structure this engineer squad is constructing; 0 = none.</summary>
        public int BuildTargetId { get; internal set; }

        internal int RepathTick { get; set; }

        internal List<Vec2> Path { get; } = new List<Vec2>();
        internal int PathIndex { get; set; }

        public bool IsMoving => PathIndex < Path.Count;

        /// <summary>Remaining waypoints (read-only view for debug drawing).</summary>
        public IReadOnlyList<Vec2> Waypoints => Path;
        public int CurrentWaypointIndex => PathIndex;

        /// <summary>Final destination, or current position when idle.</summary>
        public Vec2 Destination => Path.Count > 0 ? Path[Path.Count - 1] : Position;

        public bool IsAlive => Models > 0;
        public bool InCombat(int tick) => tick - LastDamagedTick < 30 || tick - LastFiredTick < 30;

        /// <summary>Longest weapon range (target acquisition radius).</summary>
        public float MaxRange
        {
            get
            {
                float r = 0f;
                foreach (var w in Weapons)
                {
                    if (w.Range > r)
                    {
                        r = w.Range;
                    }
                }

                return r;
            }
        }

        internal void ClearOrders()
        {
            TargetId = 0;
            ForcedTarget = false;
            BuildTargetId = 0;
            IsRetreating = false;
        }
    }
}
