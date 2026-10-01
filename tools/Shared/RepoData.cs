using System;
using System.IO;
using OCT7.Sim.Data;

namespace OCT7.Tools
{
    /// <summary>Loads game/data/*.json from the repository for tests and tools (outside Godot).</summary>
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
            return GameDataLoader.FromJson(
                File.ReadAllText(Path.Combine(dir, "economy.json")),
                File.ReadAllText(Path.Combine(dir, "factions.json")),
                File.ReadAllText(Path.Combine(dir, "units.json")));
        }
    }
}
