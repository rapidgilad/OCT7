using System;
using System.Collections.Generic;
using OCT7.Sim.Data;
using OCT7.Sim.Units;

namespace OCT7.Sim.Economy
{
    /// <summary>
    /// Per-tick resource income. V0: HQ base income and manpower upkeep (docs/02 §1).
    /// Sector income arrives with territory control.
    /// </summary>
    public sealed class EconomySystem
    {
        private readonly EconomyDef _def;
        private readonly SimWorld _world;
        private readonly IReadOnlyList<PlayerState> _players;

        public EconomySystem(EconomyDef def, SimWorld world, IReadOnlyList<PlayerState> players)
        {
            _def = def;
            _world = world;
            _players = players;
        }

        public void InitializePlayer(PlayerState player)
        {
            player.Manpower = _def.StartManpower;
            player.Munitions = _def.StartMunitions;
            player.Fuel = _def.StartFuel;
            player.Tickets = _def.StartingTickets;
        }

        /// <summary>Manpower income per minute after upkeep (never negative).</summary>
        public float ManpowerIncomePerMinute(PlayerState player)
        {
            int pop = _world.PopulationOf(player.Id);
            float upkeep = Math.Max(0, pop - _def.UpkeepFreePop) * _def.UpkeepManpowerPerPopPerMinute;
            return Math.Max(0f, _def.HqManpowerPerMinute - upkeep);
        }

        public void Tick()
        {
            const double perTick = 1.0 / SimConfig.TicksPerMinute;
            for (int i = 0; i < _players.Count; i++)
            {
                var p = _players[i];
                p.Manpower += ManpowerIncomePerMinute(p) * perTick;
                p.Munitions += _def.HqMunitionsPerMinute * perTick;
                p.Fuel += _def.HqFuelPerMinute * perTick;
            }
        }
    }
}
