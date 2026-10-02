using System;
using Godot;

namespace OCT7.Game.UI
{
    /// <summary>A Control that draws itself with a callback (health bars with pips, VP diamonds, progress overlays).</summary>
    public partial class DrawBox : Control
    {
        public DrawBox()
        {
            MouseFilter = MouseFilterEnum.Ignore;
        }

        public Action<DrawBox> OnDraw { get; set; }

        public override void _Process(double delta) => QueueRedraw();

        public override void _Draw() => OnDraw?.Invoke(this);
    }
}
