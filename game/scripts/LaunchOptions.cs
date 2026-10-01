using System.Globalization;
using Godot;

namespace OCT7.Game
{
    /// <summary>
    /// Command-line options passed after "--", e.g.
    ///   godot --path game -- --demo --overview --screenshot /tmp/shot.png
    ///   godot --headless --path game -- --smoke-test 600
    /// </summary>
    public sealed class LaunchOptions
    {
        public ulong Seed { get; private set; } = 1;
        public string Faction0 { get; private set; } = "idf";
        public string Faction1 { get; private set; } = "hamas";

        /// <summary>Run N ticks headless with AI on both sides, print the state hash, quit.</summary>
        public int SmokeTestTicks { get; private set; }

        /// <summary>Let AI control the local player too (attract/demo mode).</summary>
        public bool Demo { get; private set; }

        /// <summary>Simulate N ticks before the first frame (useful for screenshots mid-match).</summary>
        public int FastForwardTicks { get; private set; }

        /// <summary>Start with the camera framing the whole map.</summary>
        public bool Overview { get; private set; }

        /// <summary>Start with the camera centered on the local player's squads.</summary>
        public bool FocusArmy { get; private set; }

        /// <summary>Select all local squads on start (shows selection visuals in screenshots).</summary>
        public bool SelectAllOnStart { get; private set; }

        public string ScreenshotPath { get; private set; }
        public int ScreenshotAfterFrames { get; private set; } = 60;

        public static LaunchOptions Parse(string[] args)
        {
            var o = new LaunchOptions();
            for (int i = 0; i < args.Length; i++)
            {
                string Next() => i + 1 < args.Length ? args[++i] : string.Empty;
                switch (args[i])
                {
                    case "--seed": o.Seed = ulong.Parse(Next(), CultureInfo.InvariantCulture); break;
                    case "--p0": o.Faction0 = Next(); break;
                    case "--p1": o.Faction1 = Next(); break;
                    case "--smoke-test": o.SmokeTestTicks = int.Parse(Next(), CultureInfo.InvariantCulture); break;
                    case "--demo": o.Demo = true; break;
                    case "--fast-forward": o.FastForwardTicks = int.Parse(Next(), CultureInfo.InvariantCulture); break;
                    case "--overview": o.Overview = true; break;
                    case "--focus-army": o.FocusArmy = true; break;
                    case "--select-all": o.SelectAllOnStart = true; break;
                    case "--screenshot": o.ScreenshotPath = Next(); break;
                    case "--after-frames": o.ScreenshotAfterFrames = int.Parse(Next(), CultureInfo.InvariantCulture); break;
                    default: GD.PushWarning($"Unknown launch option '{args[i]}'"); break;
                }
            }

            return o;
        }
    }
}
