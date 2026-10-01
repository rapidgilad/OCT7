using OCT7.Sim.Pathfinding;

namespace OCT7.Sim.Units
{
    /// <summary>Plans paths for move orders and advances squads along them each tick.</summary>
    public sealed class MovementSystem
    {
        private readonly SimWorld _world;
        private readonly GridPathfinder _pathfinder;

        public MovementSystem(SimWorld world, GridPathfinder pathfinder)
        {
            _world = world;
            _pathfinder = pathfinder;
        }

        /// <summary>Plans a path to the target. Returns false (and stops the squad) if no path exists.</summary>
        public bool OrderMove(Squad squad, Vec2 target)
        {
            squad.Path.Clear();
            squad.PathIndex = 0;
            return _pathfinder.FindPath(squad.Position, target, squad.Path);
        }

        public void Stop(Squad squad)
        {
            squad.Path.Clear();
            squad.PathIndex = 0;
        }

        public void Tick()
        {
            var squads = _world.Squads;
            for (int i = 0; i < squads.Count; i++)
            {
                var s = squads[i];
                s.PrevPosition = s.Position;
                if (!s.IsMoving)
                {
                    continue;
                }

                float remaining = s.Def.MoveSpeed * SimConfig.TickSeconds;
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
