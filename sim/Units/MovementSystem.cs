using OCT7.Sim.World;

namespace OCT7.Sim.Units
{
    /// <summary>
    /// Plans paths for move orders and advances squads along them each tick, applying retreat, suppression,
    /// pinning and ground-type speed modifiers (docs/02 §3).
    /// </summary>
    public sealed class MovementSystem
    {
        private readonly Simulation _sim;

        public MovementSystem(Simulation sim)
        {
            _sim = sim;
        }

        /// <summary>Plans a path to the target. Returns false (and stops the squad) if no path exists.</summary>
        public bool OrderMove(Squad squad, Vec2 target)
        {
            squad.Path.Clear();
            squad.PathIndex = 0;
            squad.RepathTick = _sim.Tick;
            return _sim.Pathfinder.FindPath(squad.Position, target, squad.Path);
        }

        public void Stop(Squad squad)
        {
            squad.Path.Clear();
            squad.PathIndex = 0;
        }

        /// <summary>Re-plans every moving squad to its destination (after the grid changed).</summary>
        public void RepathMovingSquads()
        {
            foreach (var s in _sim.World.Squads)
            {
                if (s.IsMoving)
                {
                    OrderMove(s, s.Destination);
                }
            }
        }

        public float SpeedOf(Squad s)
        {
            var rules = _sim.Rules;
            float speed = s.Def.MoveSpeed;
            if (s.IsRetreating)
            {
                speed *= rules.RetreatSpeedMultiplier;
            }
            else if (s.SuppressionState == SuppressionState.Pinned)
            {
                return 0f;
            }
            else if (s.SuppressionState == SuppressionState.Suppressed)
            {
                speed *= rules.SuppressedSpeedMultiplier;
            }

            if (s.Def.IsVehicle)
            {
                var ground = _sim.Map.Grid.GetGround(_sim.Map.Grid.WorldToCell(s.Position));
                if (ground == GroundType.Rock)
                {
                    speed *= rules.RockVehicleSpeedMultiplier;
                }
                else if (ground == GroundType.MudFarmland)
                {
                    speed *= rules.MudVehicleSpeedMultiplier;
                }
            }

            return speed;
        }

        public void Tick()
        {
            var squads = _sim.World.Squads;
            for (int i = 0; i < squads.Count; i++)
            {
                var s = squads[i];
                s.PrevPosition = s.Position;
                if (!s.IsMoving)
                {
                    continue;
                }

                float remaining = SpeedOf(s) * SimConfig.TickSeconds;
                while (remaining > 0f && s.IsMoving)
                {
                    var waypoint = s.Path[s.PathIndex];
                    var delta = waypoint - s.Position;
                    float dist = delta.Length;
                    if (dist <= remaining)
                    {
                        s.Position = waypoint;
                        remaining -= dist;
                        s.PathIndex++;
                        if (dist > 1e-4f)
                        {
                            s.Facing = delta / dist;
                        }
                    }
                    else
                    {
                        var dir = delta / dist;
                        s.Position += dir * remaining;
                        s.Facing = dir;
                        remaining = 0f;
                    }
                }

                if (!s.IsMoving)
                {
                    s.Path.Clear();
                    s.PathIndex = 0;
                }
            }
        }
    }
}
