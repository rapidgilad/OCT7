using System;
using OCT7.Sim.Data;

namespace OCT7.Game
{
    /// <summary>Reads game/data/*.json through Godot's virtual file system (works in the editor and in exports).</summary>
    public static class GodotDataSource
    {
        public const string DataDirectory = "res://data";

        public static GameDataSet Load(string directory = DataDirectory)
        {
            return GameDataLoader.FromJson(
                Read(directory, "economy.json"),
                Read(directory, "factions.json"),
                Read(directory, "units.json"));
        }

        private static string Read(string directory, string file)
        {
            string path = $"{directory}/{file}";
            if (!Godot.FileAccess.FileExists(path))
            {
                throw new InvalidOperationException($"Missing data file {path}. Exports must include data/*.json (see export_presets.cfg).");
            }

            return Godot.FileAccess.GetFileAsString(path);
        }
    }
}
