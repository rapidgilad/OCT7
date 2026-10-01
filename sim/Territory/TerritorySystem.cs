using System;
using System.Collections.Generic;
using OCT7.Sim.Data;

namespace OCT7.Sim.Territory
{
    /// <summary>A capture point and the territory around it.</summary>
    public sealed class Sector
    {
        internal Sector(int index, SectorDef def)
        {
            Index = index;
            Def = def;
            Position = new Vec2(def.X, def.Y);
        }

        public int Index { get; }
        public SectorDef Def { get; }
        public Vec2 Position { get; }
        public SectorType Type => Def.Type;
        public bool IsCapturable => Def.Type != SectorType.Hq;

        /// <summary>Owning player, or -1 for neutral.</summary>
        public int OwnerId { get; internal set; } = -1;

        /// <summary>Player currently capturing a neutral sector (-1 = none).</summary>
        public int CapturingPlayerId { get; internal set; } = -1;

        /// <summary>Owner's hold (0..1) when owned, or the capturer's progress (0..1) when neutral.</summary>
        public float Progress { get; internal set; }

        /// <summary>Players present at the point last tick (bitmask by player id).</summary>
        public int PresenceMask { get; internal set; }

        public bool IsContested => PresenceMask != 0 && (PresenceMask & (PresenceMask - 1)) != 0;

        internal List<int> NeighborList { get; } = new List<int>();
        public IReadOnlyList<int> Neighbors => NeighborList;

        internal bool[] ConnectedFor { get; set; }

        /// <summary>True when this sector is owned by its owner and linked to the owner's HQ through owned sectors.</summary>
        public bool IsConnected => OwnerId >= 0 && ConnectedFor != null && ConnectedFor[OwnerId];
    }

    /// <summary>
    /// Sectors, capture, supply connectivity and victory-point ticket drain (docs/02 §2).
    /// Territory is the Voronoi partition of the capture points; sectors touching each other are neighbours.
    /// Maps without sectors (the sandbox) simply disable the system.
    /// </summary>
    public sealed class TerritorySystem
    {
        private readonly Simulation _sim;
        private readonly List<Sector> _sectors = new List<Sector>();
        private int[] _cellSector;
        private int[] _presence = new int[Simulation.MaxPlayers];

        public TerritorySystem(Simulation sim)
        {
            _sim = sim;
        }

        public IReadOnlyList<Sector> Sectors => _sectors;
        public bool Enabled => _sectors.Count > 0;

        internal void Initialize()
        {
            _sectors.Clear();
            var defs = _sim.Map.Sectors;
            for (int i = 0; i < defs.Count; i++)
            {
                var sector = new Sector(i, defs[i]) { ConnectedFor = new bool[Simulation.MaxPlayers] };
                if (defs[i].Owner >= 0 && defs[i].Owner < _sim.Players.Count)
                {
                    sector.OwnerId = defs[i].Owner;
                    sector.Progress = 1f;
                }

                _sectors.Add(sector);
            }

            if (!Enabled)
            {
                return;
            }

            BuildVoronoi();
            UpdateConnectivity();
        }

        /// <summary>Sector whose territory contains the position, or null (no sectors / out of bounds).</summary>
        public Sector SectorAt(Vec2 position)
        {
            if (!Enabled)
            {
                return null;
            }

            var grid = _sim.Map.Grid;
            var cell = grid.WorldToCell(position);
            return grid.InBounds(cell) ? _sectors[_cellSector[grid.ToIndex(cell)]] : null;
        }

        public int SectorIndexAt(GridPos cell) => Enabled && _sim.Map.Grid.InBounds(cell) ? _cellSector[_sim.Map.Grid.ToIndex(cell)] : -1;

        public int VictoryPointsOf(int playerId)
        {
            int n = 0;
            foreach (var s in _sectors)
            {
                if (s.Type == SectorType.Victory && s.OwnerId == playerId)
                {
                    n++;
                }
            }

            return n;
        }

        /// <summary>Connected, owned sectors by type (income sources).</summary>
        public void CountConnected(int playerId, out int standard, out int munitions, out int fuel)
        {
            standard = munitions = fuel = 0;
            foreach (var s in _sectors)
            {
                if (s.OwnerId != playerId || !s.ConnectedFor[playerId])
                {
                    continue;
                }

                switch (s.Type)
                {
                    case SectorType.Standard: standard++; break;
                    case SectorType.Munitions: munitions++; break;
                    case SectorType.Fuel: fuel++; break;
                }
            }
        }

        public void Tick()
        {
            if (!Enabled)
            {
                return;
            }

            var rules = _sim.Rules;
            float dt = SimConfig.TickSeconds;
            float r2 = rules.CaptureRadius * rules.CaptureRadius;
            int players = _sim.Players.Count;
            if (_presence.Length < players)
            {
                _presence = new int[players];
            }

            foreach (var sector in _sectors)
            {
                if (!sector.IsCapturable)
                {
                    continue;
                }

                Array.Clear(_presence, 0, _presence.Length);
                foreach (var squad in _sim.World.Squads)
                {
                    if (squad.IsAlive && squad.Def.CanCapture && !squad.IsRetreating && squad.OwnerId < players
                        && Vec2.DistanceSquared(squad.Position, sector.Position) <= r2)
                    {
                        _presence[squad.OwnerId]++;
                    }
                }

                int mask = 0;
                int present = -1;
                int count = 0;
                for (int p = 0; p < players; p++)
                {
                    if (_presence[p] > 0)
                    {
                        mask |= 1 << p;
                        present = p;
                        count++;
                    }
                }

                sector.PresenceMask = mask;
                if (count == 0)
                {
                    if (sector.OwnerId >= 0 && sector.Progress < 1f)
                    {
                        sector.Progress = Math.Min(1f, sector.Progress + 0.5f * dt / rules.CaptureSeconds);
                    }

                    continue;
                }

                if (count > 1)
                {
                    continue; // contested
                }

                float multiplier = Math.Min(rules.MaxCaptureMultiplier, 1f + rules.ExtraCapturerBonus * (_presence[present] - 1));
                float rate = dt / rules.CaptureSeconds * multiplier;
                Advance(sector, present, rate);
            }

            UpdateConnectivity();
            DrainTickets(dt);
        }

        private void Advance(Sector sector, int player, float rate)
        {
            if (sector.OwnerId == player)
            {
                sector.Progress = Math.Min(1f, sector.Progress + rate);
                return;
            }

            if (sector.OwnerId >= 0)
            {
                sector.Progress -= rate;
                if (sector.Progress <= 0f)
                {
                    int previous = sector.OwnerId;
                    sector.OwnerId = -1;
                    sector.CapturingPlayerId = player;
                    sector.Progress = 0f;
                    _sim.Emit(new SimEvent { Type = SimEventType.SectorNeutralized, PlayerId = player, TargetId = sector.Index, Value = previous, DefId = sector.Def.Id, To = sector.Position });
                }

                return;
            }

            if (sector.CapturingPlayerId != player && sector.CapturingPlayerId >= 0 && sector.Progress > 0f)
            {
                sector.Progress -= rate;
                if (sector.Progress <= 0f)
                {
                    sector.Progress = 0f;
                    sector.CapturingPlayerId = player;
                }

                return;
            }

            sector.CapturingPlayerId = player;
            sector.Progress += rate;
            if (sector.Progress >= 1f)
            {
                sector.Progress = 1f;
                sector.OwnerId = player;
                _sim.Emit(new SimEvent { Type = SimEventType.SectorCaptured, PlayerId = player, TargetId = sector.Index, DefId = sector.Def.Id, To = sector.Position });
            }
        }

        private void DrainTickets(float dt)
        {
            var players = _sim.Players;
            for (int p = 0; p < players.Count; p++)
            {
                int own = VictoryPointsOf(p);
                int bestOther = 0;
                for (int q = 0; q < players.Count; q++)
                {
                    if (q != p)
                    {
                        bestOther = Math.Max(bestOther, VictoryPointsOf(q));
                    }
                }

                int diff = bestOther - own;
                if (diff > 0)
                {
                    players[p].Tickets = Math.Max(0.0, players[p].Tickets - diff * _sim.Rules.TicketDrainPerVpPerSecond * dt);
                }
            }
        }

        private void UpdateConnectivity()
        {
            var queue = new Queue<int>();
            for (int p = 0; p < _sim.Players.Count; p++)
            {
                foreach (var s in _sectors)
                {
                    s.ConnectedFor[p] = false;
                }

                queue.Clear();
                foreach (var s in _sectors)
                {
                    if (s.Type == SectorType.Hq && s.OwnerId == p)
                    {
                        s.ConnectedFor[p] = true;
                        queue.Enqueue(s.Index);
                    }
                }

                while (queue.Count > 0)
                {
                    var s = _sectors[queue.Dequeue()];
                    foreach (var n in s.NeighborList)
                    {
                        var ns = _sectors[n];
                        if (ns.OwnerId == p && !ns.ConnectedFor[p])
                        {
                            ns.ConnectedFor[p] = true;
                            queue.Enqueue(n);
                        }
                    }
                }
            }
        }

        private void BuildVoronoi()
        {
            var grid = _sim.Map.Grid;
            _cellSector = new int[grid.Width * grid.Height];
            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    var center = grid.CellCenter(new GridPos(x, y));
                    int best = 0;
                    float bestD = float.MaxValue;
                    for (int i = 0; i < _sectors.Count; i++)
                    {
                        float d = Vec2.DistanceSquared(center, _sectors[i].Position);
                        if (d < bestD)
                        {
                            bestD = d;
                            best = i;
                        }
                    }

                    _cellSector[y * grid.Width + x] = best;
                }
            }

            var adjacent = new bool[_sectors.Count, _sectors.Count];
            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    int a = _cellSector[y * grid.Width + x];
                    if (x + 1 < grid.Width)
                    {
                        int b = _cellSector[y * grid.Width + x + 1];
                        adjacent[a, b] = adjacent[b, a] = adjacent[a, b] || a != b;
                    }

                    if (y + 1 < grid.Height)
                    {
                        int b = _cellSector[(y + 1) * grid.Width + x];
                        adjacent[a, b] = adjacent[b, a] = adjacent[a, b] || a != b;
                    }
                }
            }

            for (int i = 0; i < _sectors.Count; i++)
            {
                for (int j = 0; j < _sectors.Count; j++)
                {
                    if (i != j && adjacent[i, j])
                    {
                        _sectors[i].NeighborList.Add(j);
                    }
                }
            }
        }
    }
}
