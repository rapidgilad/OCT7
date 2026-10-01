using Godot;
using OCT7.Game.Match;
using OCT7.Game.Tools;

namespace OCT7.Game.UI
{
    /// <summary>
    /// Main menu: choose your faction, the enemy faction and AI difficulty, then start a skirmish.
    /// Command-line options (--quick, --demo, --smoke-test, --screenshot …) skip straight into a match.
    /// </summary>
    public partial class MainMenu : Control
    {
        private static readonly string[] FactionIds = { "idf", "hamas", "hezbollah" };
        private static readonly string[] FactionNames = { "IDF", "Hamas", "Hezbollah" };
        private static readonly string[] DifficultyIds = { "easy", "normal", "hard" };
        private static readonly string[] DifficultyNames = { "Recruit (Easy)", "Veteran (Normal)", "Elite (Hard)" };

        private OptionButton _player;
        private OptionButton _enemy;
        private OptionButton _difficulty;

        public override void _Ready()
        {
            LaunchOptions options = null;
            if (!MatchSettings.LaunchHandled)
            {
                MatchSettings.LaunchHandled = true;
                options = LaunchOptions.Parse(OS.GetCmdlineUserArgs());
                if (options.SkipMenu)
                {
                    options.ApplyTo();
                    MatchSettings.Launch = options;
                    CallDeferred(MethodName.StartMatch);
                    return;
                }
            }

            Theme = UiTheme.Create();
            SetAnchorsPreset(LayoutPreset.FullRect);
            AddChild(new ColorRect { Color = new Color(0.09f, 0.1f, 0.08f), AnchorRight = 1f, AnchorBottom = 1f, MouseFilter = MouseFilterEnum.Ignore });
            AddChild(new ColorRect { Color = new Color(0.62f, 0.55f, 0.38f, 0.08f), AnchorRight = 1f, AnchorTop = 0.62f, AnchorBottom = 1f, MouseFilter = MouseFilterEnum.Ignore });

            var center = new CenterContainer { AnchorRight = 1f, AnchorBottom = 1f };
            AddChild(center);
            var box = new VBoxContainer { CustomMinimumSize = new Vector2(420f, 0f) };
            box.AddThemeConstantOverride("separation", 12);
            center.AddChild(box);

            var title = UiTheme.Label("IRON SWORDS", 54, UiTheme.Accent);
            title.HorizontalAlignment = HorizontalAlignment.Center;
            var subtitle = UiTheme.Label("FRONTLINES", 26, UiTheme.Text);
            subtitle.HorizontalAlignment = HorizontalAlignment.Center;
            var version = UiTheme.Label("v0.1 · Skirmish vs AI · Border Ridge Outpost", 13, UiTheme.Dim);
            version.HorizontalAlignment = HorizontalAlignment.Center;
            box.AddChild(title);
            box.AddChild(subtitle);
            box.AddChild(version);
            box.AddChild(new Control { CustomMinimumSize = new Vector2(0f, 18f) });

            _player = Option(box, "Your faction", FactionNames, System.Array.IndexOf(FactionIds, MatchSettings.PlayerFaction));
            _enemy = Option(box, "Enemy faction", FactionNames, System.Array.IndexOf(FactionIds, MatchSettings.EnemyFaction));
            _difficulty = Option(box, "AI difficulty", DifficultyNames, System.Array.IndexOf(DifficultyIds, MatchSettings.Difficulty));

            box.AddChild(new Control { CustomMinimumSize = new Vector2(0f, 10f) });
            var start = new Button { Text = "START SKIRMISH", CustomMinimumSize = new Vector2(0f, 50f), FocusMode = FocusModeEnum.None };
            start.AddThemeFontSizeOverride("font_size", 18);
            start.Pressed += OnStart;
            box.AddChild(start);
            var quit = new Button { Text = "Quit", CustomMinimumSize = new Vector2(0f, 36f), FocusMode = FocusModeEnum.None };
            quit.Pressed += () => GetTree().Quit();
            box.AddChild(quit);

            var hint = UiTheme.Label("Capture sectors for income · hold victory points to drain enemy tickets · destroy the enemy HQ to win outright", 12, UiTheme.Dim);
            hint.HorizontalAlignment = HorizontalAlignment.Center;
            hint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            box.AddChild(hint);

            if (!string.IsNullOrEmpty(options?.ScreenshotPath))
            {
                AddChild(new ScreenshotTool(options.ScreenshotPath, options.ScreenshotAfterFrames) { Name = "Screenshot" });
            }
        }

        private static OptionButton Option(VBoxContainer box, string label, string[] items, int selected)
        {
            box.AddChild(UiTheme.Label(label, 13, UiTheme.Dim));
            var option = new OptionButton { CustomMinimumSize = new Vector2(0f, 36f), FocusMode = FocusModeEnum.None };
            foreach (var item in items)
            {
                option.AddItem(item);
            }

            option.Selected = Mathf.Max(0, selected);
            box.AddChild(option);
            return option;
        }

        private void OnStart()
        {
            MatchSettings.PlayerFaction = FactionIds[_player.Selected];
            MatchSettings.EnemyFaction = FactionIds[_enemy.Selected];
            MatchSettings.Difficulty = DifficultyIds[_difficulty.Selected];
            MatchSettings.Seed = (ulong)Time.GetTicksUsec();
            MatchSettings.Demo = false;
            StartMatch();
        }

        private void StartMatch() => GetTree().ChangeSceneToFile("res://scenes/match.tscn");
    }
}
