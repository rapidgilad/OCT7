using System;

namespace OCT7.Sim.Economy
{
    /// <summary>
    /// Per-tick resource income (docs/02 §1): HQ base income + connected sectors, minus manpower upkeep.
    /// </summary>
    public sealed class EconomySystem
    {
        private readonly Simulation _sim;

        public EconomySystem(Simulation sim)
        {
            _sim = sim;
        }

        public void InitializePlayer(PlayerState player)
        {
            var def = _sim.Data.Economy;
            player.Manpower = def.StartManpower;
            player.Munitions = def.StartMunitions;
            player.Fuel = def.StartFuel;
            player.Tickets = def.StartingTickets;
        }

        /// <summary>Manpower income per minute after upkeep (never negative).</summary>
        public float ManpowerIncomePerMinute(PlayerState player)
        {
            var def = _sim.Data.Economy;
            int standard = 0, munitions = 0, fuel = 0;
            _sim.Territory.CountConnected(player.Id, out standard, out munitions, out fuel);
            int pop = _sim.World.PopulationOf(player.Id);
            float upkeep = Math.Max(0, pop - def.UpkeepFreePop) * def.UpkeepManpowerPerPopPerMinute;
            return Math.Max(0f, def.HqManpowerPerMinute + standard * def.StandardSectorManpowerPerMinute - upkeep);
        }

        public void Tick()
        {
            var def = _sim.Data.Economy;
            const double perTick = 1.0 / SimConfig.TicksPerMinute;
            var players = _sim.Players;
            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];
                _sim.Territory.CountConnected(p.Id, out _, out int munitions, out int fuel);
                p.ManpowerIncome = ManpowerIncomePerMinute(p);
                p.MunitionsIncome = def.HqMunitionsPerMinute + munitions * def.MunitionsSectorPerMinute;
                p.FuelIncome = def.HqFuelPerMinute + fuel * def.FuelSectorPerMinute;
                p.Manpower += p.ManpowerIncome * perTick;
                p.Munitions += p.MunitionsIncome * perTick;
                p.Fuel += p.FuelIncome * perTick;
            }
        }
    }
}
