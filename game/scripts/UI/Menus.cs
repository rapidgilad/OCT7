using Godot;
using OCT7.Game.Match;

namespace OCT7.Game.UI
{
    /// <summary>End-of-match overlay: VICTORY / DEFEAT, how it ended, a few stats, and Play again / Main menu.</summary>
    public partial class EndScreen : Control
    {
        private Label _title;
        private Label _reason;
        private Label _stats;

        public override void _Ready()
        {
            Theme = UiTheme.Create();
            SetAnchorsPreset(LayoutPreset.FullRect);
            MouseFilter = MouseFilterEnum.Stop;
            AddChild(new ColorRect { Color = new Color(0f, 0f, 0f, 0.55f), MouseFilter = MouseFilterEnum.Ignore, AnchorRight = 1f, AnchorBottom = 1f });
            var center = new CenterContainer { AnchorRight = 1f, AnchorBottom = 1f };
            AddChild(center);
            var panel = new PanelContainer { CustomMinimumSize = new Vector2(460f, 0f) };
            panel.AddThemeStyleboxOverride("panel", UiTheme.Box(new Color(0.07f, 0.08f, 0.065f, 0.96f), 6, 24, UiTheme.Accent));
            center.AddChild(panel);
            var box = new VBoxContainer();
            box.AddThemeConstantOverride("separation", 12);
            panel.AddChild(box);
            _title = UiTheme.Label("", 48, UiTheme.Accent);
            _title.HorizontalAlignment = HorizontalAlignment.Center;
            _reason = UiTheme.Label("", 18);
            _reason.HorizontalAlignment = HorizontalAlignment.Center;
            _stats = UiTheme.Label("", 14, UiTheme.Dim);
            _stats.HorizontalAlignment = HorizontalAlignment.Center;
            box.AddChild(_title);
            box.AddChild(_reason);
            box.AddChild(_stats);
            var buttons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
            buttons.AddThemeConstantOverride("separation", 16);
            box.AddChild(buttons);
            var again = new Button { Text = "Play again", CustomMinimumSize = new Vector2(150f, 44f), FocusMode = FocusModeEnum.None };
            again.Pressed += () =>
            {
                MatchSettings.Seed = (ulong)Time.GetTicksUsec();
                GetTree().ReloadCurrentScene();
            };
            var menu = new Button { Text = "Main menu", CustomMinimumSize = new Vector2(150f, 44f), FocusMode = FocusModeEnum.None };
            menu.Pressed += () => GetTree().ChangeSceneToFile("res://scenes/menu.tscn");
            buttons.AddChild(again);
            buttons.AddChild(menu);
            Visible = false;
        }

        public void Show(bool victory, string reason, string stats)
        {
            _title.Text = victory ? "VICTORY" : "DEFEAT";
            _title.AddThemeColorOverride("font_color", victory ? UiTheme.Good : UiTheme.Bad);
            _reason.Text = reason;
            _stats.Text = stats;
            Visible = true;
        }
    }

    /// <summary>Pause overlay (Esc / Menu): Resume, Surrender, Main menu.</summary>
    public partial class PauseMenu : Control
    {
        private MatchController _match;

        public void Initialize(MatchController match)
        {
            _match = match;
            Theme = UiTheme.Create();
            SetAnchorsPreset(LayoutPreset.FullRect);
            MouseFilter = MouseFilterEnum.Stop;
            AddChild(new ColorRect { Color = new Color(0f, 0f, 0f, 0.45f), MouseFilter = MouseFilterEnum.Ignore, AnchorRight = 1f, AnchorBottom = 1f });
            var center = new CenterContainer { AnchorRight = 1f, AnchorBottom = 1f };
            AddChild(center);
            var panel = new PanelContainer { CustomMinimumSize = new Vector2(300f, 0f) };
            panel.AddThemeStyleboxOverride("panel", UiTheme.Box(new Color(0.07f, 0.08f, 0.065f, 0.96f), 6, 20, UiTheme.Accent));
            center.AddChild(panel);
            var box = new VBoxContainer();
            box.AddThemeConstantOverride("separation", 10);
            panel.AddChild(box);
            var title = UiTheme.Label("PAUSED", 28, UiTheme.Accent);
            title.HorizontalAlignment = HorizontalAlignment.Center;
            box.AddChild(title);
            AddButton(box, "Resume", () => _match.TogglePause());
            AddButton(box, "Surrender", () => _match.Surrender());
            AddButton(box, "Main menu", () => GetTree().ChangeSceneToFile("res://scenes/menu.tscn"));
            Visible = false;
        }

        private static void AddButton(VBoxContainer box, string text, System.Action action)
        {
            var b = new Button { Text = text, CustomMinimumSize = new Vector2(0f, 40f), FocusMode = FocusModeEnum.None };
            b.Pressed += action;
            box.AddChild(b);
        }
    }
}
