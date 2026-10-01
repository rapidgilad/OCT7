using System.Collections.Generic;

namespace OCT7.Sim.Data
{
    /// <summary>Broad unit class; drives capture rights, cover, suppression immunity and visuals.</summary>
    public enum UnitCategory
    {
        Infantry,
        Support,
        Vehicle,
        Hero,
    }

    /// <summary>Weapon family; drives target preference and default effects.</summary>
    public enum WeaponKind
    {
        SmallArms,
        MachineGun,
        Sniper,
        AntiTank,
        TankGun,
        Autocannon,
    }

    public enum StructureKind
    {
        Hq,
        Production,
        Sandbags,
    }

    public enum ObstacleKind
    {
        /// <summary>Blocks movement and line of sight (if tall); heavy cover around it.</summary>
        Building,

        /// <summary>Blocks movement; heavy cover; blocks line of sight if tall.</summary>
        Rock,

        /// <summary>Low stone wall: blocks movement, heavy cover, does not block line of sight.</summary>
        Wall,

        /// <summary>Fence / hedge: walkable, light cover, does not block line of sight.</summary>
        Fence,
    }

    public enum SectorType
    {
        Hq,
        Standard,
        Munitions,
        Fuel,
        Victory,
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

    /// <summary>Global combat / territory rules (docs/02). Loaded from game/data/rules.json.</summary>
    public sealed class RulesDef
    {
        public float HeavyCoverMultiplier { get; set; } = 0.5f;
        public float LightCoverMultiplier { get; set; } = 0.75f;

        /// <summary>Cover counts only if dot(coverNormal, directionToAttacker) exceeds this.</summary>
        public float CoverDirectionThreshold { get; set; } = 0.2f;

        public float CoverSnapRadius { get; set; } = 4f;
        public float LosBlockHeight { get; set; } = 2.5f;
        public int VisionUpdateTicks { get; set; } = 2;

        public float SuppressedThreshold { get; set; } = 0.5f;
        public float PinnedThreshold { get; set; } = 0.9f;
        public float SuppressionDecayPerSecond { get; set; } = 0.12f;

        /// <summary>Suppression only decays after this long without incoming fire, so sustained MG fire pins.</summary>
        public float SuppressionDecayDelaySeconds { get; set; } = 1.5f;
        public float MissSuppressionFactor { get; set; } = 0.5f;
        public float SuppressedSpeedMultiplier { get; set; } = 0.5f;
        public float SuppressedAccuracyMultiplier { get; set; } = 0.75f;
        public float PinnedAccuracyMultiplier { get; set; } = 0.5f;

        public float RetreatSpeedMultiplier { get; set; } = 1.3f;
        public float RetreatReceivedAccuracy { get; set; } = 0.5f;
        public float RetreatArriveDistance { get; set; } = 14f;

        public float ReinforceRadius { get; set; } = 30f;
        public float ReinforceSecondsPerModel { get; set; } = 3f;
        public float HealRadius { get; set; } = 30f;
        public float HealFractionPerSecond { get; set; } = 0.02f;
        public float HealDelaySeconds { get; set; } = 5f;

        public float CaptureRadius { get; set; } = 10f;
        public float CaptureSeconds { get; set; } = 20f;
        public float ExtraCapturerBonus { get; set; } = 0.25f;
        public float MaxCaptureMultiplier { get; set; } = 2f;
        public float TicketDrainPerVpPerSecond { get; set; } = 0.333f;

        public float BuildRange { get; set; } = 4f;
        public int MaxQueueLength { get; set; } = 5;
        public float RockVehicleSpeedMultiplier { get; set; } = 0.9f;
        public float MudVehicleSpeedMultiplier { get; set; } = 0.8f;
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

        /// <summary>The faction's HQ structure, placed at the start position.</summary>
        public string HqStructure { get; set; }

        /// <summary>Units spawned at match start (engineers, CoH-style).</summary>
        public List<string> StartingUnits { get; set; } = new List<string>();

        /// <summary>Larger demo force used by the sandbox scene and tests.</summary>
        public List<string> SandboxUnits { get; set; } = new List<string>();
    }

    /// <summary>A weapon carried by a unit.</summary>
    public sealed class UnitWeaponDef
    {
        public string Weapon { get; set; }

        /// <summary>True: every alive model fires it (rifles). False: crew-served, fires <see cref="Count"/> times.</summary>
        public bool PerModel { get; set; }

        public int Count { get; set; } = 1;
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

        /// <summary>Seconds to produce.</summary>
        public float BuildTime { get; set; } = 20f;

        /// <summary>Vision radius in meters.</summary>
        public float SightRange { get; set; } = 35f;

        /// <summary>Armor vs penetration (vehicles). 0 = soft target, always penetrated.</summary>
        public float ArmorFront { get; set; }

        public float ArmorRear { get; set; }

        public float ReceivedAccuracy { get; set; } = 1f;

        /// <summary>Can construct structures.</summary>
        public bool Engineer { get; set; }

        /// <summary>False until the unit's special mechanics exist (deferred signature units).</summary>
        public bool Enabled { get; set; } = true;

        public List<UnitWeaponDef> Weapons { get; set; } = new List<UnitWeaponDef>();

        public bool IsVehicle => Category == UnitCategory.Vehicle;
        public bool CanCapture => Category != UnitCategory.Vehicle;
        public float ReinforceCostPerModel => SquadSize > 0 ? Manpower / SquadSize : Manpower;
    }

    /// <summary>Weapon definition. Loaded from game/data/weapons.json.</summary>
    public sealed class WeaponDef
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public WeaponKind Kind { get; set; }
        public float Range { get; set; }
        public float AccuracyNear { get; set; }
        public float AccuracyMid { get; set; }
        public float AccuracyFar { get; set; }
        public float Damage { get; set; }

        /// <summary>Seconds between volleys.</summary>
        public float Cooldown { get; set; }

        public float Penetration { get; set; } = 1f;

        /// <summary>Suppression added to an infantry target per hit (misses add a fraction).</summary>
        public float Suppression { get; set; }

        /// <summary>Seconds of standing still before the weapon can fire (MGs, ATGMs).</summary>
        public float SetupTime { get; set; }

        public float MovingAccuracy { get; set; } = 0.5f;

        /// <summary>Accuracy multiplier against infantry (AT weapons are poor at it).</summary>
        public float VsInfantryAccuracy { get; set; } = 1f;

        /// <summary>Damage per model against infantry; 0 = use <see cref="Damage"/>.</summary>
        public float VsInfantryDamage { get; set; }

        /// <summary>Models hit per successful shot against infantry (blast weapons).</summary>
        public int ModelsHitVsInfantry { get; set; } = 1;

        /// <summary>Damage multiplier against structures.</summary>
        public float VsStructure { get; set; } = 0.05f;

        public bool PrefersVehicles => Kind == WeaponKind.AntiTank || Kind == WeaponKind.TankGun;
        public bool NeedsSetup => SetupTime > 0f;
        public float InfantryDamage => VsInfantryDamage > 0f ? VsInfantryDamage : Damage;
    }

    /// <summary>Structure definition. Loaded from game/data/structures.json.</summary>
    public sealed class StructureDef
    {
        public string Id { get; set; }

        /// <summary>Faction id, or "any" for shared structures (sandbags).</summary>
        public string FactionId { get; set; }

        public string Name { get; set; }
        public StructureKind Kind { get; set; }
        public int Tier { get; set; }
        public float Manpower { get; set; }
        public float Munitions { get; set; }
        public float Fuel { get; set; }

        /// <summary>Seconds of work for one engineer squad.</summary>
        public float BuildTime { get; set; }

        public float Health { get; set; }

        /// <summary>Square footprint in cells (production/HQ), or width for sandbags.</summary>
        public int Footprint { get; set; } = 1;

        /// <summary>Length in cells along the orientation axis (sandbags). Square structures use Footprint.</summary>
        public int Length { get; set; }

        /// <summary>Visual height in meters; tall structures block line of sight.</summary>
        public float Height { get; set; } = 4f;

        public float SightRange { get; set; } = 30f;

        public List<string> Produces { get; set; } = new List<string>();

        /// <summary>Structures that must be complete before this one can be placed.</summary>
        public List<string> Requires { get; set; } = new List<string>();

        public bool IsShared => FactionId == "any";
        public bool BlocksMovement => Kind != StructureKind.Sandbags;
    }

    public sealed class PointDef
    {
        public float X { get; set; }
        public float Y { get; set; }
    }

    /// <summary>Obstacle rectangle in cells (inclusive).</summary>
    public sealed class ObstacleDef
    {
        public int MinX { get; set; }
        public int MinY { get; set; }
        public int MaxX { get; set; }
        public int MaxY { get; set; }
        public float Height { get; set; } = 6f;
        public ObstacleKind Kind { get; set; } = ObstacleKind.Building;

        /// <summary>When the map is mirrored, center pieces are not duplicated.</summary>
        public bool Center { get; set; }
    }

    public sealed class GroundZoneDef
    {
        public int MinX { get; set; }
        public int MinY { get; set; }
        public int MaxX { get; set; }
        public int MaxY { get; set; }
        public string Type { get; set; }
        public bool Center { get; set; }
    }

    /// <summary>Capture point (world meters). Territory is the Voronoi region around it.</summary>
    public sealed class SectorDef
    {
        public string Id { get; set; }
        public SectorType Type { get; set; } = SectorType.Standard;
        public float X { get; set; }
        public float Y { get; set; }

        /// <summary>Initial owner (player index) or -1 for neutral. HQ sectors are owned and cannot be captured.</summary>
        public int Owner { get; set; } = -1;

        public bool Center { get; set; }
    }

    /// <summary>Map definition. Loaded from game/data/maps/*.json.</summary>
    public sealed class MapDef
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public float CellSize { get; set; } = 2f;

        /// <summary>Rotate every non-center element 180° around the map center for a fair 1v1 layout.</summary>
        public bool Mirror { get; set; }

        public List<PointDef> Starts { get; set; } = new List<PointDef>();
        public List<ObstacleDef> Obstacles { get; set; } = new List<ObstacleDef>();
        public List<GroundZoneDef> GroundZones { get; set; } = new List<GroundZoneDef>();
        public List<SectorDef> Sectors { get; set; } = new List<SectorDef>();
    }

    public sealed class FactionList
    {
        public List<FactionDef> Factions { get; set; } = new List<FactionDef>();
    }

    public sealed class UnitList
    {
        public List<UnitDef> Units { get; set; } = new List<UnitDef>();
    }

    public sealed class WeaponList
    {
        public List<WeaponDef> Weapons { get; set; } = new List<WeaponDef>();
    }

    public sealed class StructureList
    {
        public List<StructureDef> Structures { get; set; } = new List<StructureDef>();
    }

    public sealed class MapIndex
    {
        public List<string> Maps { get; set; } = new List<string>();
    }
}
