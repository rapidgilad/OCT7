using System.Collections.Generic;

namespace OCT7.Sim.Data
{
    /// <summary>
    /// One difficulty preset. Easy / Normal / Hard are parameter sets of the same AI (docs/04-ai-and-difficulty.md).
    /// </summary>
    public sealed class AiDifficultyDef
    {
        public string Id { get; set; }
        public string Name { get; set; }

        /// <summary>Seconds between strategic decisions (reaction speed).</summary>
        public float ThinkIntervalSeconds { get; set; } = 2f;

        /// <summary>Squads retreat when their health fraction falls below this.</summary>
        public float RetreatHealthFraction { get; set; } = 0.3f;

        /// <summary>Attack when own army value ≥ this × known enemy army value nearby.</summary>
        public float AttackStrengthRatio { get; set; } = 1.2f;

        /// <summary>Minimum army value (MP-equivalent) before the first attack wave.</summary>
        public float FirstAttackArmyValue { get; set; } = 900f;

        public int MaxCappers { get; set; } = 2;

        /// <summary>Extra delay (seconds) added before each production/build decision.</summary>
        public float ProductionDelaySeconds { get; set; }

        public bool Reinforce { get; set; } = true;

        /// <summary>Sends squads into cover near objectives and focuses fire.</summary>
        public bool Micro { get; set; } = true;

        /// <summary>Builds sandbags at held victory points.</summary>
        public bool BuildDefenses { get; set; } = true;
    }

    /// <summary>One opening step: build a structure or produce a unit.</summary>
    public sealed class AiBuildStepDef
    {
        public string Build { get; set; }
        public string Produce { get; set; }
        public int Count { get; set; } = 1;
    }

    public sealed class AiUnitWeightDef
    {
        public string Unit { get; set; }
        public float Weight { get; set; } = 1f;
    }

    /// <summary>Per-faction AI plan: scripted opening, then a weighted unit mix.</summary>
    public sealed class AiFactionPlanDef
    {
        public string FactionId { get; set; }
        public List<AiBuildStepDef> Opening { get; set; } = new List<AiBuildStepDef>();
        public List<AiUnitWeightDef> Mix { get; set; } = new List<AiUnitWeightDef>();
    }

    /// <summary>Loaded from game/data/ai.json.</summary>
    public sealed class AiDef
    {
        public List<AiDifficultyDef> Difficulties { get; set; } = new List<AiDifficultyDef>();
        public List<AiFactionPlanDef> Factions { get; set; } = new List<AiFactionPlanDef>();
    }
}
