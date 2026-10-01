namespace OCT7.Sim.Economy
{
    /// <summary>Per-player match state: faction, resources, HQ location.
    /// Resources are doubles so per-tick income adds up to the per-minute rate without visible drift.</summary>
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
        public int Tickets { get; internal set; }
    }
}
