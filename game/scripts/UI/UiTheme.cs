using Godot;

namespace OCT7.Game.UI
{
    /// <summary>Shared look for all UI: dark olive panels, sand accents, compact military-style buttons.</summary>
    public static class UiTheme
    {
        public static readonly Color Panel = new Color(0.07f, 0.08f, 0.065f, 0.9f);
        public static readonly Color PanelLight = new Color(0.13f, 0.145f, 0.115f, 0.95f);
        public static readonly Color Accent = new Color(0.86f, 0.76f, 0.5f);
        public static readonly Color Text = new Color(0.92f, 0.93f, 0.88f);
        public static readonly Color Dim = new Color(0.62f, 0.64f, 0.58f);
        public static readonly Color Good = new Color(0.55f, 0.9f, 0.5f);
        public static readonly Color Bad = new Color(1f, 0.45f, 0.38f);

        private static Theme _theme;

        public static Theme Create()
        {
            if (_theme != null)
            {
                return _theme;
            }

            var t = new Theme { DefaultFontSize = 14 };
            t.SetStylebox("panel", "PanelContainer", Box(Panel, 4, 8));
            t.SetStylebox("panel", "Panel", Box(Panel, 4, 8));
            t.SetStylebox("normal", "Button", Box(PanelLight, 3, 6, new Color(0.3f, 0.32f, 0.25f)));
            t.SetStylebox("hover", "Button", Box(new Color(0.2f, 0.22f, 0.16f, 0.98f), 3, 6, Accent));
            t.SetStylebox("pressed", "Button", Box(new Color(0.28f, 0.27f, 0.18f, 0.98f), 3, 6, Accent));
            t.SetStylebox("disabled", "Button", Box(new Color(0.09f, 0.095f, 0.085f, 0.9f), 3, 6, new Color(0.2f, 0.2f, 0.18f)));
            t.SetStylebox("focus", "Button", new StyleBoxEmpty());
            t.SetColor("font_color", "Button", Text);
            t.SetColor("font_hover_color", "Button", Accent);
            t.SetColor("font_pressed_color", "Button", Accent);
            t.SetColor("font_disabled_color", "Button", new Color(0.45f, 0.46f, 0.42f));
            t.SetFontSize("font_size", "Button", 13);
            t.SetColor("font_color", "Label", Text);
            t.SetStylebox("normal", "OptionButton", Box(PanelLight, 3, 8, new Color(0.3f, 0.32f, 0.25f)));
            t.SetStylebox("hover", "OptionButton", Box(new Color(0.2f, 0.22f, 0.16f), 3, 8, Accent));
            t.SetStylebox("pressed", "OptionButton", Box(new Color(0.2f, 0.22f, 0.16f), 3, 8, Accent));
            t.SetStylebox("focus", "OptionButton", new StyleBoxEmpty());
            t.SetStylebox("panel", "TooltipPanel", Box(new Color(0.05f, 0.055f, 0.045f, 0.97f), 3, 8, Accent));
            t.SetColor("font_color", "TooltipLabel", Text);
            _theme = t;
            return t;
        }

        public static StyleBoxFlat Box(Color bg, int radius, float margin, Color? border = null)
        {
            var sb = new StyleBoxFlat { BgColor = bg };
            sb.SetCornerRadiusAll(radius);
            sb.SetContentMarginAll(margin);
            if (border.HasValue)
            {
                sb.BorderColor = border.Value;
                sb.SetBorderWidthAll(1);
            }

            return sb;
        }

        public static Label Label(string text, int size = 14, Color? color = null)
        {
            var l = new Label { Text = text, MouseFilter = Control.MouseFilterEnum.Ignore };
            l.AddThemeFontSizeOverride("font_size", size);
            if (color.HasValue)
            {
                l.AddThemeColorOverride("font_color", color.Value);
            }

            l.AddThemeColorOverride("font_shadow_color", new Color(0f, 0f, 0f, 0.7f));
            l.AddThemeConstantOverride("shadow_offset_x", 1);
            l.AddThemeConstantOverride("shadow_offset_y", 1);
            return l;
        }

        public static ProgressBar Bar(Color fill, float width, float height)
        {
            var bar = new ProgressBar { ShowPercentage = false, CustomMinimumSize = new Vector2(width, height), MinValue = 0, MaxValue = 1, MouseFilter = Control.MouseFilterEnum.Ignore };
            bar.AddThemeStyleboxOverride("fill", Box(fill, 2, 0));
            bar.AddThemeStyleboxOverride("background", Box(new Color(0f, 0f, 0f, 0.55f), 2, 0));
            return bar;
        }
    }
}
