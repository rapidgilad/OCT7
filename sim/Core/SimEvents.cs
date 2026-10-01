namespace OCT7.Sim
{
    public enum SimEventType
    {
        ShotFired,
        ModelKilled,
        SquadDestroyed,
        Deflected,
        StructurePlaced,
        StructureCompleted,
        StructureDestroyed,
        UnitProduced,
        ModelReinforced,
        SectorCaptured,
        SectorNeutralized,
        CommandRejected,
        MatchEnded,
    }

    /// <summary>Why a command was ignored (sent to the UI via <see cref="SimEventType.CommandRejected"/>).</summary>
    public enum RejectReason
    {
        None = 0,
        NotEnoughResources = 1,
        PopCap = 2,
        QueueFull = 3,
        NotUnlocked = 4,
        InvalidPlacement = 5,
        NotOwner = 6,
        NoEngineer = 7,
    }

    /// <summary>
    /// Something that happened during a tick. Output only: presentation (tracers, sounds, UI messages) and
    /// statistics read it; nothing in the simulation depends on it, so it never affects determinism.
    /// </summary>
    public struct SimEvent
    {
        public SimEventType Type;
        public int PlayerId;
        public int SourceId;
        public int TargetId;
        public string DefId;
        public Vec2 From;
        public Vec2 To;

        /// <summary>Event-specific number: hits in a volley, model index, reject reason, winner id.</summary>
        public int Value;

        public override string ToString() => $"{Type} p{PlayerId} {SourceId}->{TargetId} {DefId} v{Value}";
    }
}
