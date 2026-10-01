using System;
using OCT7.Sim.Data;

namespace OCT7.Game
{
    /// <summary>Reads game/data through Godot's virtual file system (works in the editor and in exports).</summary>
    public static class GodotDataSource
    {
        public const string DataDirectory = "res://data";

        public static GameDataSet Load(string directory = DataDirectory)
        {
            var files = new GameDataFiles
            {
                Economy = Read(directory, "economy.json"),
                Factions = Read(directory, "factions.json"),
                Units = Read(directory, "units.json"),
                Weapons = Read(directory, "weapons.json"),
                Structures = Read(directory, "structures.json"),
                Rules = Read(directory, "rules.json"),
                Ai = Read(directory, "ai.json"),
            };

            foreach (var id in GameDataLoader.ParseMapIndex(Read(directory, "maps/maps.json")).Maps)
            {
                files.Maps.Add(Read(directory, $"maps/{id}.json"));
            }

            return GameDataLoader.FromJson(files);
        }

        private static string Read(string directory, string file)
        {
            string path = $"{directory}/{file}";
            if (!Godot.FileAccess.FileExists(path))
            {
                throw new InvalidOperationException($"Missing data file {path}. Exports must include data/*.json and data/maps/*.json (see export_presets.cfg).");
            }

            return Godot.FileAccess.GetFileAsString(path);
        }
    }
}
