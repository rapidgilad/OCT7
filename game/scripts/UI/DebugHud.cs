using System.Text;
using Godot;
using OCT7.Sim;

namespace OCT7.Game.UI
{
    /// <summary>Developer HUD: tick/time, resources per player, selection count, controls help, drag rectangle.</summary>
    public partial class DebugHud : Control
    {
        private readonly StringBuilder _text = new StringBuilder();
        private Label _info;
        private Rect2? _selectionRect;

        public override void _Ready()
        {
            SetAnchorsPreset(LayoutPreset.FullRect);
            MouseFilter = MouseFilterEnum.Ignore;

            var panel = new PanelContainer { Position = new Vector2(12f, 12f), MouseFilter = MouseFilterEnum.Ignore };
            var style = new StyleBoxFlat { BgColor = new Color(0.05f, 0.06f, 0.05f, 0.72f) };
            style.SetCornerRadiusAll(6);
            style.SetContentMarginAll(10f);
            panel.AddThemeStyleboxOverride("panel", style);
            AddChild(panel);

            _info = MakeLabel(15);
            panel.AddChild(_info);

            var help = MakeLabel(13);
            help.Text = "WASD / edge: pan    Wheel: zoom    Q/E: rotate    LMB / drag: select    Shift: add    Ctrl+A: all    RMB: move";
            help.SetAnchorsPreset(LayoutPreset.BottomLeft);
            help.Position = new Vector2(14f, -30f);
            AddChild(help);
        }

        public void SetSelectionRect(Rect2? rect)
        {
            _selectionRect = rect;
            QueueRedraw();
        }

        public void Refresh(Simulation sim, int selectedCount)
        {
            _text.Clear();
            _text.Append("OCT7 SANDBOX  |  tick ").Append(sim.Tick).Append("  (").Append(sim.ElapsedSeconds.ToString("0.0")).Append(" s)  |  ")
                 .Append(Engine.GetFramesPerSecond()).Append(" fps\n");
            foreach (var p in sim.Players)
            {
                var faction = sim.Data.GetFaction(p.FactionId);
                _text.Append(p.Id == Main.LocalPlayerId ? "YOU  " : "AI   ")
                     .Append(faction.DisplayName.PadRight(10))
                     .Append(" MP ").Append(((int)p.Manpower).ToString().PadLeft(4))
                     .Append("   MU ").Append(((int)p.Munitions).ToString().PadLeft(3))
                     .Append("   FU ").Append(((int)p.Fuel).ToString().PadLeft(3))
                     .Append("   pop ").Append(sim.World.PopulationOf(p.Id)).Append('/').Append(sim.Data.Economy.PopCap)
                     .Append('\n');
            }

            _text.Append("Selected squads: ").Append(selectedCount);
            _info.Text = _text.ToString();
        }

        public override void _Draw()
        {
            if (_selectionRect.HasValue)
            {
                DrawRect(_selectionRect.Value, new Color(0.55f, 1f, 0.55f, 0.12f), true);
                DrawRect(_selectionRect.Value, new Color(0.55f, 1f, 0.55f, 0.9f), false, 1.5f);
            }
        }

        private static Label MakeLabel(int size)
        {
            var label = new Label { MouseFilter = MouseFilterEnum.Ignore };
            label.AddThemeFontSizeOverride("font_size", size);
            label.AddThemeColorOverride("font_color", new Color(0.92f, 0.94f, 0.88f));
            label.AddThemeColorOverride("font_shadow_color", new Color(0f, 0f, 0f, 0.8f));
            label.AddThemeConstantOverride("shadow_offset_x", 1);
            label.AddThemeConstantOverride("shadow_offset_y", 1);
            return label;
        }
    }
}
