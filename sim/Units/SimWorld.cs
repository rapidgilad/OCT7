using System.Collections.Generic;
using OCT7.Sim.Data;
using OCT7.Sim.World;

namespace OCT7.Sim.Units
{
    /// <summary>
    /// Entity store. Squads are kept in a list ordered by id (ids only grow), so iteration order is deterministic.
    /// The dictionary is for lookups only and is never iterated.
    /// </summary>
    public sealed class SimWorld
    {
        private readonly List<Squad> _squads = new List<Squad>();
        private readonly Dictionary<int, Squad> _byId = new Dictionary<int, Squad>();
        private int _nextId = 1;

        public SimWorld(MapDefinition map)
        {
            Map = map;
        }

        public MapDefinition Map { get; }
        public IReadOnlyList<Squad> Squads => _squads;

        public Squad SpawnSquad(int ownerId, UnitDef def, Vec2 position)
        {
            var squad = new Squad(_nextId++, ownerId, def, position);
            _squads.Add(squad);
            _byId.Add(squad.Id, squad);
            return squad;
        }

        public Squad GetSquad(int id) => _byId.TryGetValue(id, out var s) ? s : null;

        public bool RemoveSquad(int id)
        {
            if (!_byId.TryGetValue(id, out var squad))
            {
                return false;
            }

            _byId.Remove(id);
            _squads.Remove(squad);
            return true;
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
    }
}
