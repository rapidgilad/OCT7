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

        public GameDataSet(EconomyDef economy, IReadOnlyList<FactionDef> factions, IReadOnlyList<UnitDef> units)
        {
            Economy = economy ?? throw new ArgumentNullException(nameof(economy));
            Factions = factions ?? throw new ArgumentNullException(nameof(factions));
            Units = units ?? throw new ArgumentNullException(nameof(units));

            foreach (var f in factions)
            {
                if (f?.Id != null && !_factionsById.ContainsKey(f.Id))
                {
                    _factionsById.Add(f.Id, f);
                }
            }

            foreach (var u in units)
            {
                if (u?.Id != null && !_unitsById.ContainsKey(u.Id))
                {
                    _unitsById.Add(u.Id, u);
                }
            }
        }

        public EconomyDef Economy { get; }
        public IReadOnlyList<FactionDef> Factions { get; }
        public IReadOnlyList<UnitDef> Units { get; }

        public UnitDef GetUnit(string id) =>
            id != null && _unitsById.TryGetValue(id, out var u) ? u : throw new KeyNotFoundException($"Unknown unit id '{id}'.");

        public FactionDef GetFaction(string id) =>
            id != null && _factionsById.TryGetValue(id, out var f) ? f : throw new KeyNotFoundException($"Unknown faction id '{id}'.");

        public bool HasUnit(string id) => id != null && _unitsById.ContainsKey(id);
        public bool HasFaction(string id) => id != null && _factionsById.ContainsKey(id);

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
    }

    /// <summary>
    /// Parses the JSON data files (camelCase, comments and trailing commas allowed).
    /// File I/O is left to the host (Godot uses res://, tools use the file system).
    /// </summary>
    public static class GameDataLoader
    {
        private static readonly JsonSerializerOptions Options = CreateOptions();

        public static GameDataSet FromJson(string economyJson, string factionsJson, string unitsJson)
        {
            var economy = JsonSerializer.Deserialize<EconomyDef>(economyJson, Options);
            var factions = JsonSerializer.Deserialize<FactionList>(factionsJson, Options);
            var units = JsonSerializer.Deserialize<UnitList>(unitsJson, Options);
            return new GameDataSet(
                economy ?? throw new InvalidOperationException("economy.json is empty."),
                factions?.Factions ?? throw new InvalidOperationException("factions.json has no 'factions' array."),
                units?.Units ?? throw new InvalidOperationException("units.json has no 'units' array."));
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
