using System.Collections.Generic;
using OCT7.Sim.Data;
using OCT7.Sim.Production;
using OCT7.Sim.World;

namespace OCT7.Sim.Units
{
    /// <summary>
    /// Entity store for squads and structures. Both live in lists ordered by id (ids only grow), so iteration order
    /// is deterministic. Dictionaries are for lookups only and are never iterated.
    /// </summary>
    public sealed class SimWorld
    {
        private readonly GameDataSet _data;
        private readonly List<Squad> _squads = new List<Squad>();
        private readonly List<Structure> _structures = new List<Structure>();
        private readonly Dictionary<int, Squad> _squadsById = new Dictionary<int, Squad>();
        private readonly Dictionary<int, Structure> _structuresById = new Dictionary<int, Structure>();
        private readonly int[] _structureAt;
        private int _nextId = 1;

        public SimWorld(MapDefinition map, GameDataSet data)
        {
            Map = map;
            _data = data;
            _structureAt = new int[map.Grid.Width * map.Grid.Height];
        }

        public MapDefinition Map { get; }
        public IReadOnlyList<Squad> Squads => _squads;
        public IReadOnlyList<Structure> Structures => _structures;

        public Squad SpawnSquad(int ownerId, UnitDef def, Vec2 position)
        {
            var weapons = new List<WeaponDef>();
            foreach (var w in def.Weapons)
            {
                if (_data != null && _data.HasWeapon(w.Weapon))
                {
                    weapons.Add(_data.GetWeapon(w.Weapon));
                }
            }

            var squad = new Squad(_nextId++, ownerId, def, weapons, position);
            _squads.Add(squad);
            _squadsById.Add(squad.Id, squad);
            return squad;
        }

        public Squad GetSquad(int id) => _squadsById.TryGetValue(id, out var s) ? s : null;

        public Structure GetStructure(int id) => _structuresById.TryGetValue(id, out var s) ? s : null;

        public bool RemoveSquad(int id)
        {
            if (!_squadsById.TryGetValue(id, out var squad))
            {
                return false;
            }

            _squadsById.Remove(id);
            _squads.Remove(squad);
            return true;
        }

        /// <summary>Structure id occupying a cell, or 0.</summary>
        public int StructureAt(GridPos cell) => Map.Grid.InBounds(cell) ? _structureAt[Map.Grid.ToIndex(cell)] : 0;

        /// <summary>
        /// Adds a structure and stamps its footprint into the grid (blocking, line of sight, cover source).
        /// Callers are responsible for validation and for rebuilding cover/paths afterwards.
        /// </summary>
        internal Structure AddStructure(int ownerId, StructureDef def, GridPos minCell, int sizeX, int sizeY, float losBlockHeight)
        {
            var s = new Structure(_nextId++, ownerId, def, minCell, sizeX, sizeY, Map.Grid.CellSize);
            _structures.Add(s);
            _structuresById.Add(s.Id, s);
            Stamp(s, true, losBlockHeight);
            return s;
        }

        internal void RemoveStructure(Structure s, float losBlockHeight)
        {
            if (!_structuresById.Remove(s.Id))
            {
                return;
            }

            _structures.Remove(s);
            Stamp(s, false, losBlockHeight);
        }

        public int PopulationOf(int playerId)
        {
            int pop = 0;
            foreach (var s in _squads)
            {
                if (s.OwnerId == playerId)
                {
                    pop += s.Def.Pop;
                }
            }

            return pop;
        }

        /// <summary>Population reserved by units waiting in production queues.</summary>
        public int QueuedPopulationOf(int playerId)
        {
            int pop = 0;
            foreach (var s in _structures)
            {
                if (s.OwnerId == playerId)
                {
                    foreach (var item in s.Queue)
                    {
                        pop += item.Unit.Pop;
                    }
                }
            }

            return pop;
        }

        public Structure FindHq(int playerId)
        {
            foreach (var s in _structures)
            {
                if (s.OwnerId == playerId && s.Def.Kind == StructureKind.Hq)
                {
                    return s;
                }
            }

            return null;
        }

        public bool OwnsComplete(int playerId, string structureDefId)
        {
            foreach (var s in _structures)
            {
                if (s.OwnerId == playerId && s.Def.Id == structureDefId && s.IsComplete)
                {
                    return true;
                }
            }

            return false;
        }

        private void Stamp(Structure s, bool add, float losBlockHeight)
        {
            var grid = Map.Grid;
            bool blocks = s.Def.BlocksMovement;
            bool blocksLos = blocks && s.Def.Height >= losBlockHeight;
            var cover = s.Def.Kind == StructureKind.Sandbags || blocks ? CoverType.Heavy : CoverType.None;
            for (int y = s.MinCell.Y; y < s.MinCell.Y + s.SizeY; y++)
            {
                for (int x = s.MinCell.X; x < s.MinCell.X + s.SizeX; x++)
                {
                    var c = new GridPos(x, y);
                    if (!grid.InBounds(c))
                    {
                        continue;
                    }

                    _structureAt[grid.ToIndex(c)] = add ? s.Id : 0;
                    if (blocks)
                    {
                        grid.SetBlocked(c, add);
                    }

                    if (blocksLos)
                    {
                        grid.SetLosBlocked(c, add);
                    }

                    grid.SetCoverSource(c, add ? cover : CoverType.None);
                }
            }
        }
    }
}
