using System;
using System.IO;
using OCT7.Sim.Data;

namespace OCT7.Tools
{
    /// <summary>Loads game/data from the repository for tests and tools (outside Godot).</summary>
    public static class RepoData
    {
        public static string FindRepoRoot(string startDirectory = null)
        {
            var dir = new DirectoryInfo(startDirectory ?? AppContext.BaseDirectory);
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "OCT7.sln")))
                {
                    return dir.FullName;
                }

                dir = dir.Parent;
            }

            throw new DirectoryNotFoundException("Could not find the repo root (OCT7.sln) above " + (startDirectory ?? AppContext.BaseDirectory));
        }

        public static string DataDirectory(string repoRoot = null) =>
            Path.Combine(repoRoot ?? FindRepoRoot(), "game", "data");

        public static GameDataSet Load(string dataDirectory = null)
        {
            var dir = dataDirectory ?? DataDirectory();
            string Read(string file) => File.ReadAllText(Path.Combine(dir, file));
            string ReadOptional(string file) => File.Exists(Path.Combine(dir, file)) ? Read(file) : null;

            var files = new GameDataFiles
            {
                Economy = Read("economy.json"),
                Factions = Read("factions.json"),
                Units = Read("units.json"),
                Weapons = ReadOptional("weapons.json"),
                Structures = ReadOptional("structures.json"),
                Rules = ReadOptional("rules.json"),
                Ai = ReadOptional("ai.json"),
            };

            var mapIndexPath = Path.Combine(dir, "maps", "maps.json");
            if (File.Exists(mapIndexPath))
            {
                foreach (var id in GameDataLoader.ParseMapIndex(File.ReadAllText(mapIndexPath)).Maps)
                {
                    files.Maps.Add(File.ReadAllText(Path.Combine(dir, "maps", id + ".json")));
                }
            }

            return GameDataLoader.FromJson(files);
        }
    }
}
