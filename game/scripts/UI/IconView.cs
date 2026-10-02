using Godot;

namespace OCT7.Game.UI
{
    /// <summary>A Control that draws one <see cref="IconKind"/>.</summary>
    public partial class IconView : Control
    {
        private IconKind _kind;
        private Color _color = Colors.White;

        public IconView()
        {
            MouseFilter = MouseFilterEnum.Ignore;
        }

        public IconView(IconKind kind, Color color, float size) : this()
        {
            _kind = kind;
            _color = color;
            CustomMinimumSize = new Vector2(size, size);
        }

        public IconKind Kind
        {
            get => _kind;
            set
            {
                _kind = value;
                QueueRedraw();
            }
        }

        public Color IconColor
        {
            get => _color;
            set
            {
                _color = value;
                QueueRedraw();
            }
        }

        public override void _Draw() => Icons.Draw(this, _kind, new Rect2(Vector2.Zero, Size), _color);
    }
}
