using System.Collections.Generic;

namespace OCT7.Sim.Data
{
    /// <summary>Broad unit class; drives veterancy thresholds, crush/cover rules and visuals.</summary>
    public enum UnitCategory
    {
        Infantry,
        Support,
        Vehicle,
        Hero,
    }

    /// <summary>Economy tuning (docs/02-core-gameplay.md §1). Loaded from game/data/economy.json.</summary>
    public sealed class EconomyDef
    {
        public float StartManpower { get; set; }
        public float StartMunitions { get; set; }
        public float StartFuel { get; set; }
        public float HqManpowerPerMinute { get; set; }
        public float HqMunitionsPerMinute { get; set; }
        public float HqFuelPerMinute { get; set; }
        public float StandardSectorManpowerPerMinute { get; set; }
        public float MunitionsSectorPerMinute { get; set; }
        public float FuelSectorPerMinute { get; set; }
        public float OutpostBonus { get; set; }
        public int PopCap { get; set; }
        public int UpkeepFreePop { get; set; }
        public float UpkeepManpowerPerPopPerMinute { get; set; }
        public int StartingTickets { get; set; }
    }

    /// <summary>Faction definition. Loaded from game/data/factions.json.</summary>
    public sealed class FactionDef
    {
        public string Id { get; set; }

        /// <summary>Localization key; display names are swappable per region (docs/01 content rules).</summary>
        public string DisplayNameKey { get; set; }

        /// <summary>Fallback display name until localization exists.</summary>
        public string DisplayName { get; set; }

        /// <summary>intel | tunnels | rocket_stockpile</summary>
        public string UniqueResource { get; set; }

        public string VoiceLanguage { get; set; }

        /// <summary>Units spawned at match start in a real match (engineers only, CoH-style).</summary>
        public List<string> StartingUnits { get; set; } = new List<string>();

        /// <summary>Larger demo force used by the sandbox scene and MatchRunner.</summary>
        public List<string> SandboxUnits { get; set; } = new List<string>();
    }

    /// <summary>Unit definition. Loaded from game/data/units.json. Costs follow docs/factions/*.md.</summary>
    public sealed class UnitDef
    {
        public string Id { get; set; }
        public string FactionId { get; set; }
        public string Name { get; set; }

        /// <summary>0 = HQ, 1..3 = tech tiers (solo V1 folds T4 into T3).</summary>
        public int Tier { get; set; }

        public UnitCategory Category { get; set; }
        public int SquadSize { get; set; }
        public int Pop { get; set; }
        public float Manpower { get; set; }
        public float Munitions { get; set; }
        public float Fuel { get; set; }

        /// <summary>Meters per second.</summary>
        public float MoveSpeed { get; set; }

        public float HealthPerModel { get; set; }
    }

    public sealed class FactionList
    {
        public List<FactionDef> Factions { get; set; } = new List<FactionDef>();
    }

    public sealed class UnitList
    {
        public List<UnitDef> Units { get; set; } = new List<UnitDef>();
    }
}
