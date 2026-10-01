using System;
using System.Collections.Generic;
using OCT7.Sim.Combat;
using OCT7.Sim.Data;
using OCT7.Sim.Units;

namespace OCT7.Sim.Production
{
    /// <summary>
    /// Base building and unit production (docs/02 §4):
    ///   engineers place structures (cost paid on placement) and build them while adjacent;
    ///   complete structures produce queued units (cost + pop reserved when queued, refunded on cancel).
    /// </summary>
    public sealed class ProductionSystem
    {
        private readonly Simulation _sim;

        public ProductionSystem(Simulation sim)
        {
            _sim = sim;
        }

        // ---------------------------------------------------------------- placement

        /// <summary>Footprint size in cells for a structure and orientation (0 = along X, 1 = along Y).</summary>
        public static void FootprintSize(StructureDef def, int orientation, out int sizeX, out int sizeY)
        {
            if (def.Kind == StructureKind.Sandbags)
            {
                int length = Math.Max(1, def.Length);
                sizeX = orientation == 0 ? length : def.Footprint;
                sizeY = orientation == 0 ? def.Footprint : length;
            }
            else
            {
                sizeX = sizeY = def.Footprint;
            }
        }

        public static GridPos MinCellFor(GridPos center, int sizeX, int sizeY) =>
            new GridPos(center.X - sizeX / 2, center.Y - sizeY / 2);

        /// <summary>Checks every placement rule without changing anything.</summary>
        public RejectReason CanPlace(int playerId, StructureDef def, GridPos center, int orientation)
        {
            var player = _sim.GetPlayer(playerId);
            if (player == null || def == null || def.Kind == StructureKind.Hq)
            {
                return RejectReason.InvalidPlacement;
            }

            if (!def.IsShared && def.FactionId != player.FactionId)
            {
                return RejectReason.NotOwner;
            }

            foreach (var req in def.Requires)
            {
                if (!_sim.World.OwnsComplete(playerId, req))
                {
                    return RejectReason.NotUnlocked;
                }
            }

            if (player.Manpower < def.Manpower || player.Munitions < def.Munitions || player.Fuel < def.Fuel)
            {
                return RejectReason.NotEnoughResources;
            }

            FootprintSize(def, orientation, out int sx, out int sy);
            var min = MinCellFor(center, sx, sy);
            var grid = _sim.Map.Grid;
            for (int y = min.Y; y < min.Y + sy; y++)
            {
                for (int x = min.X; x < min.X + sx; x++)
                {
                    var c = new GridPos(x, y);
                    if (!grid.IsWalkable(c) || _sim.World.StructureAt(c) != 0 || grid.GetCoverSource(x, y) != World.CoverType.None)
                    {
                        return RejectReason.InvalidPlacement;
                    }
                }
            }

            if (def.BlocksMovement)
            {
                // Leave a one-cell walkable margin so structures never seal off paths completely.
                for (int y = min.Y - 1; y <= min.Y + sy; y++)
                {
                    for (int x = min.X - 1; x <= min.X + sx; x++)
                    {
                        bool border = x == min.X - 1 || y == min.Y - 1 || x == min.X + sx || y == min.Y + sy;
                        if (border && !grid.IsWalkable(x, y))
                        {
                            return RejectReason.InvalidPlacement;
                        }
                    }
                }

                foreach (var s in _sim.World.Squads)
                {
                    var cell = grid.WorldToCell(s.Position);
                    if (cell.X >= min.X && cell.Y >= min.Y && cell.X < min.X + sx && cell.Y < min.Y + sy)
                    {
                        return RejectReason.InvalidPlacement;
                    }
                }
            }

            if (_sim.Territory.Enabled)
            {
                var sector = _sim.Territory.SectorAt(grid.CellCenter(center));
                bool ok = sector != null && (sector.OwnerId == playerId || (def.Kind == StructureKind.Sandbags && sector.OwnerId < 0));
                if (!ok)
                {
                    return RejectReason.InvalidPlacement;
                }
            }

            return RejectReason.None;
        }

        public RejectReason TryPlace(int playerId, IReadOnlyList<Squad> engineers, string structureId, GridPos center, int orientation, out Structure placed)
        {
            placed = null;
            if (!_sim.Data.HasStructure(structureId))
            {
                return RejectReason.InvalidPlacement;
            }

            var def = _sim.Data.GetStructure(structureId);
            var reason = CanPlace(playerId, def, center, orientation);
            if (reason != RejectReason.None)
            {
                return reason;
            }

            var player = _sim.GetPlayer(playerId);
            player.Manpower -= def.Manpower;
            player.Munitions -= def.Munitions;
            player.Fuel -= def.Fuel;

            FootprintSize(def, orientation, out int sx, out int sy);
            var min = MinCellFor(center, sx, sy);
            placed = _sim.World.AddStructure(playerId, def, min, sx, sy, _sim.Rules.LosBlockHeight);
            placed.BuildProgress = 0f;
            placed.Health = def.Health * 0.1f;
            _sim.OnGridChanged(min.X, min.Y, min.X + sx - 1, min.Y + sy - 1);
            _sim.Emit(new SimEvent { Type = SimEventType.StructurePlaced, PlayerId = playerId, TargetId = placed.Id, DefId = def.Id, To = placed.Center });
            AssignBuilders(engineers, placed);
            return RejectReason.None;
        }

        /// <summary>Places a finished structure without cost or engineers (HQs at match start).</summary>
        public Structure PlaceComplete(int playerId, StructureDef def, GridPos center, int orientation = 0)
        {
            FootprintSize(def, orientation, out int sx, out int sy);
            var min = MinCellFor(center, sx, sy);
            var s = _sim.World.AddStructure(playerId, def, min, sx, sy, _sim.Rules.LosBlockHeight);
            s.BuildProgress = 1f;
            s.Health = def.Health;
            _sim.OnGridChanged(min.X, min.Y, min.X + sx - 1, min.Y + sy - 1);
            return s;
        }

        public void AssignBuilders(IReadOnlyList<Squad> engineers, Structure structure)
        {
            foreach (var e in engineers)
            {
                if (!e.Def.Engineer || e.OwnerId != structure.OwnerId)
                {
                    continue;
                }

                e.ClearOrders();
                e.BuildTargetId = structure.Id;
                _sim.Movement.OrderMove(e, ApproachPoint(structure, e.Position));
            }
        }

        internal void DestroyStructure(Structure st)
        {
            _sim.World.RemoveStructure(st, _sim.Rules.LosBlockHeight);
            _sim.OnGridChanged(st.MinCell.X, st.MinCell.Y, st.MinCell.X + st.SizeX - 1, st.MinCell.Y + st.SizeY - 1);
            _sim.Emit(new SimEvent { Type = SimEventType.StructureDestroyed, PlayerId = st.OwnerId, TargetId = st.Id, DefId = st.Def.Id, To = st.Center });
        }

        /// <summary>A walkable point just outside the footprint, on the side facing <paramref name="from"/>.</summary>
        public Vec2 ApproachPoint(Structure st, Vec2 from)
        {
            var nearest = CombatSystem.NearestPointOn(st, from);
            var outward = (from - nearest).Normalized();
            if (outward == Vec2.Zero)
            {
                outward = (MapCenter - st.Center).Normalized();
                if (outward == Vec2.Zero)
                {
                    outward = new Vec2(1f, 0f);
                }
            }

            return _sim.Map.Grid.ClampToWorld(nearest + outward * 2f);
        }

        private Vec2 MapCenter => new Vec2(_sim.Map.Grid.WorldWidth * 0.5f, _sim.Map.Grid.WorldHeight * 0.5f);

        // ---------------------------------------------------------------- production

        public RejectReason CanProduce(int playerId, Structure structure, string unitId)
        {
            if (structure == null || structure.OwnerId != playerId || !_sim.Data.HasUnit(unitId))
            {
                return RejectReason.NotOwner;
            }

            var unit = _sim.Data.GetUnit(unitId);
            if (!structure.IsComplete || !structure.Def.Produces.Contains(unitId) || !unit.Enabled)
            {
                return RejectReason.NotUnlocked;
            }

            if (structure.Queue.Count >= _sim.Rules.MaxQueueLength)
            {
                return RejectReason.QueueFull;
            }

            var player = _sim.GetPlayer(playerId);
            if (player.Manpower < unit.Manpower || player.Munitions < unit.Munitions || player.Fuel < unit.Fuel)
            {
                return RejectReason.NotEnoughResources;
            }

            int pop = _sim.World.PopulationOf(playerId) + _sim.World.QueuedPopulationOf(playerId) + unit.Pop;
            if (pop > _sim.Data.Economy.PopCap)
            {
                return RejectReason.PopCap;
            }

            return RejectReason.None;
        }

        public RejectReason TryEnqueue(int playerId, int structureId, string unitId)
        {
            var structure = _sim.World.GetStructure(structureId);
            var reason = CanProduce(playerId, structure, unitId);
            if (reason != RejectReason.None)
            {
                return reason;
            }

            var unit = _sim.Data.GetUnit(unitId);
            var player = _sim.GetPlayer(playerId);
            player.Manpower -= unit.Manpower;
            player.Munitions -= unit.Munitions;
            player.Fuel -= unit.Fuel;
            structure.QueueList.Add(new ProductionItem(unit));
            return RejectReason.None;
        }

        public bool Cancel(int playerId, int structureId, int index)
        {
            var structure = _sim.World.GetStructure(structureId);
            if (structure == null || structure.OwnerId != playerId || index < 0 || index >= structure.Queue.Count)
            {
                return false;
            }

            var unit = structure.Queue[index].Unit;
            var player = _sim.GetPlayer(playerId);
            player.Manpower += unit.Manpower;
            player.Munitions += unit.Munitions;
            player.Fuel += unit.Fuel;
            structure.QueueList.RemoveAt(index);
            return true;
        }

        // ---------------------------------------------------------------- tick

        public void Tick()
        {
            float dt = SimConfig.TickSeconds;
            TickConstruction(dt);
            TickQueues(dt);
        }

        private void TickConstruction(float dt)
        {
            float range = _sim.Rules.BuildRange + 0.5f;
            foreach (var e in _sim.World.Squads)
            {
                if (e.BuildTargetId == 0)
                {
                    continue;
                }

                var st = _sim.World.GetStructure(e.BuildTargetId);
                if (st == null || st.IsComplete || st.OwnerId != e.OwnerId || e.IsRetreating)
                {
                    e.BuildTargetId = 0;
                    continue;
                }

                if (st.DistanceTo(e.Position) > range)
                {
                    if (!e.IsMoving && _sim.Tick - e.RepathTick > 20)
                    {
                        _sim.Movement.OrderMove(e, ApproachPoint(st, e.Position));
                    }

                    continue;
                }

                if (e.IsMoving)
                {
                    _sim.Movement.Stop(e);
                }

                float step = dt / st.Def.BuildTime;
                st.BuildProgress = Math.Min(1f, st.BuildProgress + step);
                st.Health = Math.Min(st.MaxHealth, st.Health + st.MaxHealth * 0.9f * step);
                if (st.IsComplete)
                {
                    e.BuildTargetId = 0;
                    _sim.Emit(new SimEvent { Type = SimEventType.StructureCompleted, PlayerId = st.OwnerId, TargetId = st.Id, DefId = st.Def.Id, To = st.Center });
                }
            }
        }

        private void TickQueues(float dt)
        {
            var structures = _sim.World.Structures;
            for (int i = 0; i < structures.Count; i++)
            {
                var st = structures[i];
                if (!st.IsComplete || st.Queue.Count == 0)
                {
                    continue;
                }

                var item = st.QueueList[0];
                item.Elapsed += dt;
                if (item.Elapsed < item.Unit.BuildTime)
                {
                    continue;
                }

                st.QueueList.RemoveAt(0);
                var exit = ExitPoint(st);
                var squad = _sim.World.SpawnSquad(st.OwnerId, item.Unit, exit);
                var facing = (MapCenter - st.Center).Normalized();
                if (facing != Vec2.Zero)
                {
                    squad.Facing = facing;
                }

                if (st.HasRallyPoint)
                {
                    _sim.Movement.OrderMove(squad, st.RallyPoint);
                }

                _sim.Emit(new SimEvent { Type = SimEventType.UnitProduced, PlayerId = st.OwnerId, SourceId = st.Id, TargetId = squad.Id, DefId = item.Unit.Id, To = exit });
            }
        }

        /// <summary>Spawn point: the walkable cell just outside the footprint, on the side facing the map center.</summary>
        public Vec2 ExitPoint(Structure st)
        {
            var dir = (MapCenter - st.Center).Normalized();
            if (dir == Vec2.Zero)
            {
                dir = new Vec2(1f, 0f);
            }

            float half = Math.Max(st.SizeX, st.SizeY) * st.CellSize * 0.5f;
            var grid = _sim.Map.Grid;
            var candidate = grid.ClampToWorld(st.Center + dir * (half + 3f));
            if (grid.TryFindNearestWalkable(grid.WorldToCell(candidate), 8, out var cell))
            {
                return grid.WorldToCell(candidate) == cell ? candidate : grid.CellCenter(cell);
            }

            return candidate;
        }
    }
}
