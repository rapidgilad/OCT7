using System.Globalization;
using Godot;
using OCT7.Game.Match;

namespace OCT7.Game
{
    /// <summary>
    /// Command-line options passed after "--", e.g.
    ///   godot --path game -- --quick --p0 hezbollah --difficulty hard
    ///   godot --path game -- --demo --overview --screenshot /tmp/shot.png
    ///   godot --headless --path game -- --smoke-test 600
    ///   godot --headless --path game -- --smoke-test 30000 --full-match
    /// Any option that only makes sense in a match skips the main menu.
    /// </summary>
    public sealed class LaunchOptions
    {
        public ulong Seed { get; private set; } = 1;
        public string Faction0 { get; private set; } = "idf";
        public string Faction1 { get; private set; } = "hamas";
        public string Difficulty { get; private set; } = "normal";
        public string MapId { get; private set; }

        /// <summary>Skip the main menu and start a skirmish with the given (or default) settings.</summary>
        public bool Quick { get; private set; }

        /// <summary>Run N ticks headless with AI on both sides, print the state hash, quit.</summary>
        public int SmokeTestTicks { get; private set; }

        /// <summary>
        /// Play until the match is decided. With --smoke-test, N becomes the tick cap and the run fails if no side won;
        /// in a windowed run the match is fast-forwarded to its end so the end screen shows.
        /// </summary>
        public bool FullMatch { get; private set; }

        /// <summary>Let AI control the local player too (attract/demo mode). Fog is off unless --fog is given.</summary>
        public bool Demo { get; private set; }

        /// <summary>Keep the local player's fog of war in demo mode.</summary>
        public bool Fog { get; private set; }

        /// <summary>Simulate N ticks before the first frame (useful for screenshots mid-match).</summary>
        public int FastForwardTicks { get; private set; }

        /// <summary>Start with the camera framing the whole map.</summary>
        public bool Overview { get; private set; }

        /// <summary>Start with the camera centered on the local player's squads.</summary>
        public bool FocusArmy { get; private set; }

        /// <summary>Initial camera distance in meters (0 = default). Handy for close-up screenshots.</summary>
        public float CameraDistance { get; private set; }

        /// <summary>Initial camera focus "x,z" in meters (overrides the default framing).</summary>
        public Vector2? LookAt { get; private set; }

        /// <summary>Initial camera yaw in degrees (0 = looking toward -Z).</summary>
        public float? CameraYaw { get; private set; }

        /// <summary>Initial selection for screenshots: "all" squads, "one" squad, "hq" (production card) or "build" (engineers' build menu).</summary>
        public string Select { get; private set; }

        /// <summary>Show the main menu even when other options would skip it (menu screenshots).</summary>
        public bool ForceMenu { get; private set; }

        /// <summary>Art gallery instead of a match: "idf", "hamas", "hezbollah" or "all" (see MatchSetup.CreateShowcase).</summary>
        public string Showcase { get; private set; }

        /// <summary>Play sample explosions, fire, tracers and rockets in front of the camera (effects review).</summary>
        public bool VfxTest { get; private set; }

        /// <summary>Write the synthesized sound effects as WAV files to this directory and quit.</summary>
        public string ExportSoundsDir { get; private set; }

        public string ScreenshotPath { get; private set; }
        public int ScreenshotAfterFrames { get; private set; } = 60;

        public bool SkipMenu => !ForceMenu && (
            Quick || Demo || FullMatch || SmokeTestTicks > 0 || FastForwardTicks > 0 || !string.IsNullOrEmpty(ScreenshotPath));

        public static LaunchOptions Parse(string[] args)
        {
            var o = new LaunchOptions();
            for (int i = 0; i < args.Length; i++)
            {
                string Next() => i + 1 < args.Length ? args[++i] : string.Empty;
                switch (args[i])
                {
                    case "--seed": o.Seed = ulong.Parse(Next(), CultureInfo.InvariantCulture); o.Quick = true; break;
                    case "--p0": o.Faction0 = Next(); o.Quick = true; break;
                    case "--p1": o.Faction1 = Next(); o.Quick = true; break;
                    case "--difficulty": o.Difficulty = Next(); o.Quick = true; break;
                    case "--map": o.MapId = Next(); o.Quick = true; break;
                    case "--quick": o.Quick = true; break;
                    case "--menu": o.ForceMenu = true; break;
                    case "--export-sounds": o.ExportSoundsDir = Next(); break;
                    case "--vfx-test": o.VfxTest = true; break;
                    case "--showcase": o.Showcase = Next(); o.Quick = true; break;
                    case "--smoke-test": o.SmokeTestTicks = int.Parse(Next(), CultureInfo.InvariantCulture); break;
                    case "--full-match": o.FullMatch = true; break;
                    case "--demo": o.Demo = true; break;
                    case "--fog": o.Fog = true; break;
                    case "--fast-forward": o.FastForwardTicks = int.Parse(Next(), CultureInfo.InvariantCulture); break;
                    case "--overview": o.Overview = true; break;
                    case "--focus-army": o.FocusArmy = true; break;
                    case "--select-all": o.Select = "all"; break;
                    case "--select": o.Select = Next(); break;
                    case "--distance": o.CameraDistance = float.Parse(Next(), CultureInfo.InvariantCulture); break;
                    case "--yaw": o.CameraYaw = float.Parse(Next(), CultureInfo.InvariantCulture); break;
                    case "--look":
                        var xz = Next().Split(',');
                        o.LookAt = new Vector2(float.Parse(xz[0], CultureInfo.InvariantCulture), float.Parse(xz[1], CultureInfo.InvariantCulture));
                        break;
                    case "--screenshot": o.ScreenshotPath = Next(); break;
                    case "--after-frames": o.ScreenshotAfterFrames = int.Parse(Next(), CultureInfo.InvariantCulture); break;
                    default: GD.PushWarning($"Unknown launch option '{args[i]}'"); break;
                }
            }

            return o;
        }

        /// <summary>Copies the match setup into <see cref="MatchSettings"/> for the match scene.</summary>
        public void ApplyTo()
        {
            MatchSettings.PlayerFaction = Faction0;
            MatchSettings.EnemyFaction = Faction1;
            MatchSettings.Difficulty = Difficulty;
            MatchSettings.Seed = Seed;
            MatchSettings.MapId = MapId;
            MatchSettings.Demo = Demo;
            MatchSettings.Showcase = Showcase;
        }
    }
}
