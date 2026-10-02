namespace OCT7.Game.Match
{
    /// <summary>Skirmish setup chosen in the main menu (or from the command line) and read by the match scene.</summary>
    public static class MatchSettings
    {
        public static string PlayerFaction = "idf";
        public static string EnemyFaction = "hamas";
        public static string Difficulty = "normal";
        public static ulong Seed = 1;
        public static string MapId;

        /// <summary>AI plays both sides; the camera spectates with fog of war disabled.</summary>
        public static bool Demo;

        /// <summary>Art gallery mode ("idf", "hamas", "hezbollah" or "all"); null for a normal match.</summary>
        public static string Showcase;

        /// <summary>True once the command line has been read, so returning to the menu doesn't re-launch the same match.</summary>
        public static bool LaunchHandled;

        /// <summary>Command-line options for the next match only (screenshot, fast-forward, smoke test …); null for menu starts.</summary>
        public static LaunchOptions Launch;
    }
}
