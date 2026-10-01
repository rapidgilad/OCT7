namespace OCT7.Sim.Economy
{
    /// <summary>
    /// Per-player match state: faction, resources, income, tickets, HQ location.
    /// Resources are doubles so per-tick income adds up to the per-minute rate without visible drift.
    /// </summary>
    public sealed class PlayerState
    {
        internal PlayerState(int id, string factionId, Vec2 hqPosition)
        {
            Id = id;
            FactionId = factionId;
            HqPosition = hqPosition;
        }

        public int Id { get; }
        public string FactionId { get; }
        public Vec2 HqPosition { get; }

        public double Manpower { get; internal set; }
        public double Munitions { get; internal set; }
        public double Fuel { get; internal set; }

        /// <summary>Current income per minute (after upkeep), for the HUD and AI.</summary>
        public double ManpowerIncome { get; internal set; }

        public double MunitionsIncome { get; internal set; }
        public double FuelIncome { get; internal set; }

        /// <summary>Victory tickets; reaching 0 loses the match (docs/02 §2).</summary>
        public double Tickets { get; internal set; }

        /// <summary>True once an HQ structure was placed; losing it then loses the match.</summary>
        public bool HadHq { get; internal set; }

        public bool IsDefeated { get; internal set; }
    }
}
