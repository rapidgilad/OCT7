using System;
using System.Collections.Generic;
using OCT7.Sim.Combat;
using OCT7.Sim.Data;
using OCT7.Sim.Economy;
using OCT7.Sim.Pathfinding;
using OCT7.Sim.Production;
using OCT7.Sim.Territory;
using OCT7.Sim.Units;
using OCT7.Sim.World;

namespace OCT7.Sim
{
    /// <summary>
    /// The authoritative match state and fixed-tick loop. Hosts (Godot, MatchRunner, tests) only:
    ///   1) enqueue commands, 2) call <see cref="Step"/> at <see cref="SimConfig.TicksPerSecond"/>, 3) read state and <see cref="Events"/>.
    /// System order per tick: commands → vision → production/construction → movement → combat → support → territory → economy → victory.
    /// </summary>
    public sealed class Simulation
    {
        public const int MaxPlayers = 8;

        private readonly CommandQueue _queue = new CommandQueue();
        private readonly List<Command> _due = new List<Command>();
        private readonly List<PlayerState> _players = new List<PlayerState>();
        private readonly List<SimEvent> _events = new List<SimEvent>();
        private bool _started;

        public Simulation(GameDataSet data, MapDefinition map, ulong seed)
        {
            Data = data ?? throw new ArgumentNullException(nameof(data));
            Map = map ?? throw new ArgumentNullException(nameof(map));
            Seed = seed;
            Random = new DeterministicRandom(seed);
            World = new SimWorld(map, data);
            Pathfinder = new GridPathfinder(map.Grid);
            Cover = new CoverGrid(map.Grid);
            Vision = new VisibilityGrid(map.Grid, MaxPlayers);
            Movement = new MovementSystem(this);
            Combat = new CombatSystem(this);
            Support = new SupportSystem(this);
            Territory = new TerritorySystem(this);
            Production = new ProductionSystem(this);
            Economy = new EconomySystem(this);
        }

        public GameDataSet Data { get; }
        public RulesDef Rules => Data.Rules;
        public MapDefinition Map { get; }
        public ulong Seed { get; }

        /// <summary>The next tick to be simulated (number of completed ticks).</summary>
        public int Tick { get; private set; }

        public float ElapsedSeconds => Tick * SimConfig.TickSeconds;

        public DeterministicRandom Random { get; }
        public SimWorld World { get; }
        public GridPathfinder Pathfinder { get; }
        public CoverGrid Cover { get; }
        public VisibilityGrid Vision { get; }
        public MovementSystem Movement { get; }
        public CombatSystem Combat { get; }
        public SupportSystem Support { get; }
        public TerritorySystem Territory { get; }
        public ProductionSystem Production { get; }
        public EconomySystem Economy { get; }
        public IReadOnlyList<PlayerState> Players => _players;

        /// <summary>Events produced by the last <see cref="Step"/>.</summary>
        public IReadOnlyList<SimEvent> Events => _events;

        public bool IsOver { get; private set; }

        /// <summary>Winning player id once <see cref="IsOver"/>; -1 for a draw or while running.</summary>
        public int WinnerId { get; private set; } = -1;

        /// <summary>Number of commands executed so far (diagnostics).</summary>
        public int ExecutedCommandCount { get; private set; }

        public PlayerState AddPlayer(string factionId, Vec2 hqPosition)
        {
            if (_started)
            {
                throw new InvalidOperationException("Players must be added before the match starts.");
            }

            if (_players.Count >= MaxPlayers)
            {
                throw new InvalidOperationException("Too many players.");
            }

            Data.GetFaction(factionId); // throws on unknown faction
            var player = new PlayerState(_players.Count, factionId, hqPosition);
            Economy.InitializePlayer(player);
            _players.Add(player);
            return player;
        }

        public PlayerState GetPlayer(int id) => id >= 0 && id < _players.Count ? _players[id] : null;

        /// <summary>Initializes territory and vision. Called automatically by the first <see cref="Step"/>.</summary>
        public void Start()
        {
            if (_started)
            {
                return;
            }

            _started = true;
            Territory.Initialize();
            UpdateVision();
        }

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
            Start();
            _events.Clear();
            if (IsOver)
            {
                Tick++;
                return;
            }

            _due.Clear();
            _queue.TakeDue(Tick, _due);
            for (int i = 0; i < _due.Count; i++)
            {
                _due[i].Apply(this);
            }

            ExecutedCommandCount += _due.Count;

            if (Tick % Math.Max(1, Rules.VisionUpdateTicks) == 0)
            {
                UpdateVision();
            }

            Production.Tick();
            Movement.Tick();
            Combat.Tick();
            Support.Tick();
            Territory.Tick();
            Economy.Tick();
            CheckVictory();
            Tick++;
        }

        public bool IsVisibleTo(int playerId, Vec2 position) => Vision.IsVisible(playerId, position);

        /// <summary>Position of a squad or structure by id; false if it no longer exists.</summary>
        public bool TryGetEntityPosition(int id, out Vec2 position)
        {
            var squad = World.GetSquad(id);
            if (squad != null)
            {
                position = squad.Position;
                return true;
            }

            var structure = World.GetStructure(id);
            if (structure != null)
            {
                position = structure.Center;
                return true;
            }

            position = Vec2.Zero;
            return false;
        }

        public ulong ComputeStateHash() => StateHasher.Hash(this);

        internal void Emit(SimEvent e) => _events.Add(e);

        internal void Reject(int playerId, RejectReason reason, string defId = null) =>
            Emit(new SimEvent { Type = SimEventType.CommandRejected, PlayerId = playerId, Value = (int)reason, DefId = defId });

        /// <summary>Call after cells changed blocking/cover state (structures placed or destroyed).</summary>
        internal void OnGridChanged(int minX, int minY, int maxX, int maxY)
        {
            Cover.RebuildRegion(minX, minY, maxX, maxY);
            Movement.RepathMovingSquads();
        }

        private void UpdateVision()
        {
            for (int p = 0; p < _players.Count; p++)
            {
                Vision.Begin(p);
            }

            foreach (var s in World.Squads)
            {
                if (s.OwnerId >= 0 && s.OwnerId < _players.Count)
                {
                    Vision.Reveal(s.OwnerId, s.Position, s.Def.SightRange);
                }
            }

            foreach (var st in World.Structures)
            {
                if (st.OwnerId >= 0 && st.OwnerId < _players.Count && st.Def.SightRange > 0f)
                {
                    Vision.Reveal(st.OwnerId, st.Center, st.Def.SightRange);
                }
            }
        }

        private void CheckVictory()
        {
            if (_players.Count < 2)
            {
                return;
            }

            int alive = 0;
            int lastAlive = -1;
            foreach (var p in _players)
            {
                if (!p.IsDefeated)
                {
                    bool lostHq = p.HadHq && World.FindHq(p.Id) == null;
                    if (p.Tickets <= 0 || lostHq)
                    {
                        p.IsDefeated = true;
                    }
                }

                if (!p.IsDefeated)
                {
                    alive++;
                    lastAlive = p.Id;
                }
            }

            if (alive <= 1 && alive < _players.Count)
            {
                IsOver = true;
                WinnerId = alive == 1 ? lastAlive : -1;
                Emit(new SimEvent { Type = SimEventType.MatchEnded, PlayerId = WinnerId, Value = WinnerId });
            }
        }
    }
}
