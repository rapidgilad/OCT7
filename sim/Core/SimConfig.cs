namespace OCT7.Sim
{
    /// <summary>Global simulation constants. The sim advances in fixed ticks; presentation interpolates between them.</summary>
    public static class SimConfig
    {
        public const int TicksPerSecond = 10;
        public const float TickSeconds = 1f / TicksPerSecond;
        public const int TicksPerMinute = TicksPerSecond * 60;
    }
}
