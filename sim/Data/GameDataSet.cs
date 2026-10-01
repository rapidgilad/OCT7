using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OCT7.Sim.Data
{
    /// <summary>All static game data for a match. Read-only after loading.</summary>
    public sealed class GameDataSet
    {
        private readonly Dictionary<string, UnitDef> _unitsById = new Dictionary<string, UnitDef>();
        private readonly Dictionary<string, FactionDef> _factionsById = new Dictionary<string, FactionDef>();
        private readonly Dictionary<string, WeaponDef> _weaponsById = new Dictionary<string, WeaponDef>();
        private readonly Dictionary<string, StructureDef> _structuresById = new Dictionary<string, StructureDef>();
        private readonly Dictionary<string, MapDef> _mapsById = new Dictionary<string, MapDef>();

        public GameDataSet(
            EconomyDef economy,
            IReadOnlyList<FactionDef> factions,
            IReadOnlyList<UnitDef> units,
            IReadOnlyList<WeaponDef> weapons = null,
            IReadOnlyList<StructureDef> structures = null,
            RulesDef rules = null,
            IReadOnlyList<MapDef> maps = null,
            AiDef ai = null)
        {
            Economy = economy ?? throw new ArgumentNullException(nameof(economy));
            Factions = factions ?? throw new ArgumentNullException(nameof(factions));
            Units = units ?? throw new ArgumentNullException(nameof(units));
            Weapons = weapons ?? new List<WeaponDef>();
            Structures = structures ?? new List<StructureDef>();
            Rules = rules ?? new RulesDef();
            Maps = maps ?? new List<MapDef>();
            Ai = ai ?? new AiDef();

            Index(factions, f => f?.Id, _factionsById);
            Index(units, u => u?.Id, _unitsById);
            Index(Weapons, w => w?.Id, _weaponsById);
            Index(Structures, s => s?.Id, _structuresById);
            Index(Maps, m => m?.Id, _mapsById);
        }

        public EconomyDef Economy { get; }
        public IReadOnlyList<FactionDef> Factions { get; }
        public IReadOnlyList<UnitDef> Units { get; }
        public IReadOnlyList<WeaponDef> Weapons { get; }
        public IReadOnlyList<StructureDef> Structures { get; }
        public RulesDef Rules { get; }
        public IReadOnlyList<MapDef> Maps { get; }
        public AiDef Ai { get; }

        public UnitDef GetUnit(string id) => Get(_unitsById, id, "unit");
        public FactionDef GetFaction(string id) => Get(_factionsById, id, "faction");
        public WeaponDef GetWeapon(string id) => Get(_weaponsById, id, "weapon");
        public StructureDef GetStructure(string id) => Get(_structuresById, id, "structure");
        public MapDef GetMap(string id) => Get(_mapsById, id, "map");

        public bool HasUnit(string id) => id != null && _unitsById.ContainsKey(id);
        public bool HasFaction(string id) => id != null && _factionsById.ContainsKey(id);
        public bool HasWeapon(string id) => id != null && _weaponsById.ContainsKey(id);
        public bool HasStructure(string id) => id != null && _structuresById.ContainsKey(id);
        public bool HasMap(string id) => id != null && _mapsById.ContainsKey(id);

        /// <summary>Units of a faction in file order.</summary>
        public List<UnitDef> UnitsOfFaction(string factionId)
        {
            var result = new List<UnitDef>();
            foreach (var u in Units)
            {
                if (u.FactionId == factionId)
                {
                    result.Add(u);
                }
            }

            return result;
        }

        /// <summary>Structures a faction's engineers can build (own + shared), in file order. Excludes HQs.</summary>
        public List<StructureDef> BuildableStructures(string factionId)
        {
            var result = new List<StructureDef>();
            foreach (var s in Structures)
            {
                if (s.Kind != StructureKind.Hq && (s.FactionId == factionId || s.IsShared))
                {
                    result.Add(s);
                }
            }

            return result;
        }

        public AiDifficultyDef GetDifficulty(string id)
        {
            foreach (var d in Ai.Difficulties)
            {
                if (d.Id == id)
                {
                    return d;
                }
            }

            throw new KeyNotFoundException($"Unknown AI difficulty '{id}'.");
        }

        public AiFactionPlanDef GetAiPlan(string factionId)
        {
            foreach (var p in Ai.Factions)
            {
                if (p.FactionId == factionId)
                {
                    return p;
                }
            }

            return null;
        }

        private static void Index<T>(IReadOnlyList<T> items, Func<T, string> key, Dictionary<string, T> target)
        {
            foreach (var item in items)
            {
                var k = key(item);
                if (k != null && !target.ContainsKey(k))
                {
                    target.Add(k, item);
                }
            }
        }

        private static T Get<T>(Dictionary<string, T> map, string id, string kind) =>
            id != null && map.TryGetValue(id, out var v) ? v : throw new KeyNotFoundException($"Unknown {kind} id '{id}'.");
    }

    /// <summary>Raw JSON text of every data file. Hosts fill it from res:// (Godot) or the file system (tools).</summary>
    public sealed class GameDataFiles
    {
        public string Economy;
        public string Factions;
        public string Units;
        public string Weapons;
        public string Structures;
        public string Rules;
        public string Ai;

        /// <summary>Contents of game/data/maps/*.json in the order of maps/maps.json.</summary>
        public List<string> Maps = new List<string>();
    }

    /// <summary>
    /// Parses the JSON data files (camelCase, comments and trailing commas allowed).
    /// File I/O is left to the host (Godot uses res://, tools use the file system).
    /// </summary>
    public static class GameDataLoader
    {
        private static readonly JsonSerializerOptions Options = CreateOptions();

        public static GameDataSet FromJson(GameDataFiles files)
        {
            var economy = Parse<EconomyDef>(files.Economy, "economy.json");
            var factions = Parse<FactionList>(files.Factions, "factions.json");
            var units = Parse<UnitList>(files.Units, "units.json");
            var weapons = string.IsNullOrWhiteSpace(files.Weapons) ? new WeaponList() : Parse<WeaponList>(files.Weapons, "weapons.json");
            var structures = string.IsNullOrWhiteSpace(files.Structures) ? new StructureList() : Parse<StructureList>(files.Structures, "structures.json");
            var rules = string.IsNullOrWhiteSpace(files.Rules) ? new RulesDef() : Parse<RulesDef>(files.Rules, "rules.json");
            var ai = string.IsNullOrWhiteSpace(files.Ai) ? new AiDef() : Parse<AiDef>(files.Ai, "ai.json");

            var maps = new List<MapDef>();
            foreach (var mapJson in files.Maps)
            {
                maps.Add(Parse<MapDef>(mapJson, "map"));
            }

            return new GameDataSet(economy, factions.Factions, units.Units, weapons.Weapons, structures.Structures, rules, maps, ai);
        }

        public static MapIndex ParseMapIndex(string json) => Parse<MapIndex>(json, "maps/maps.json");

        private static T Parse<T>(string json, string file)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new InvalidOperationException($"{file} is missing or empty.");
            }

            try
            {
                return JsonSerializer.Deserialize<T>(json, Options) ?? throw new InvalidOperationException($"{file} is empty.");
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException($"{file}: {ex.Message}", ex);
            }
        }

        private static JsonSerializerOptions CreateOptions()
        {
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = false,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            };
            options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
            return options;
        }
    }
}
