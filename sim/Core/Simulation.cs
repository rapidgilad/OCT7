using System;
using System.Collections.Generic;
using OCT7.Sim.Data;
using OCT7.Sim.Economy;
using OCT7.Sim.Pathfinding;
using OCT7.Sim.Units;
using OCT7.Sim.World;

namespace OCT7.Sim
{
    /// <summary>
    /// The authoritative match state and fixed-tick loop. Hosts (Godot, MatchRunner, tests) only:
    ///   1) enqueue commands, 2) call <see cref="Step"/> at <see cref="SimConfig.TicksPerSecond"/>, 3) read state.
    /// </summary>
    public sealed class Simulation
    {
        private readonly CommandQueue _queue = new CommandQueue();
        private readonly List<Command> _due = new List<Command>();
        private readonly List<PlayerState> _players = new List<PlayerState>();

        public Simulation(GameDataSet data, MapDefinition map, ulong seed)
        {
            Data = data ?? throw new ArgumentNullException(nameof(data));
            Map = map ?? throw new ArgumentNullException(nameof(map));
            Seed = seed;
            Random = new DeterministicRandom(seed);
            World = new SimWorld(map);
            Pathfinder = new GridPathfinder(map.Grid);
            Movement = new MovementSystem(World, Pathfinder);
            Economy = new EconomySystem(data.Economy, World, _players);
        }

        public GameDataSet Data { get; }
        public MapDefinition Map { get; }
        public ulong Seed { get; }

        /// <summary>The next tick to be simulated (number of completed ticks).</summary>
        public int Tick { get; private set; }

        public float ElapsedSeconds => Tick * SimConfig.TickSeconds;

        public DeterministicRandom Random { get; }
        public SimWorld World { get; }
        public GridPathfinder Pathfinder { get; }
        public MovementSystem Movement { get; }
        public EconomySystem Economy { get; }
        public IReadOnlyList<PlayerState> Players => _players;

        /// <summary>Number of commands executed so far (diagnostics).</summary>
        public int ExecutedCommandCount { get; private set; }

        public PlayerState AddPlayer(string factionId, Vec2 hqPosition)
        {
            if (Tick != 0)
            {
                throw new InvalidOperationException("Players must be added before the first tick.");
            }

            Data.GetFaction(factionId); // throws on unknown faction
            var player = new PlayerState(_players.Count, factionId, hqPosition);
            Economy.InitializePlayer(player);
            _players.Add(player);
            return player;
        }

        public PlayerState GetPlayer(int id) => id >= 0 && id < _players.Count ? _players[id] : null;

        /// <summary>Queues a command. Commands targeting a past tick run on the next <see cref="Step"/>.</summary>
        public void Enqueue(Command command)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            if (command.Tick < Tick)
            {
                command.Tick = Tick;
            }

            _queue.Enqueue(command);
        }

        /// <summary>Advances the match by exactly one tick.</summary>
        public void Step()
        {
            _due.Clear();
            _queue.TakeDue(Tick, _due);
            for (int i = 0; i < _due.Count; i++)
            {
                _due[i].Apply(this);
            }

            ExecutedCommandCount += _due.Count;

            Movement.Tick();
            Economy.Tick();
            Tick++;
        }

        public ulong ComputeStateHash() => StateHasher.Hash(this);
    }
}
