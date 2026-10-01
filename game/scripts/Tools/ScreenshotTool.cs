using Godot;

namespace OCT7.Game.Tools
{
    /// <summary>
    /// Saves a PNG of the viewport after N frames, then quits. Lets Claude Code inspect the game visually
    /// in the cloud via Xvfb:  xvfb-run godot --path game --rendering-method gl_compatibility -- --screenshot out.png
    /// </summary>
    public partial class ScreenshotTool : Node
    {
        private readonly string _path;
        private readonly int _afterFrames;
        private int _frames;

        public ScreenshotTool()
        {
        }

        public ScreenshotTool(string path, int afterFrames)
        {
            _path = path;
            _afterFrames = Mathf.Max(1, afterFrames);
        }

        public override void _Process(double delta)
        {
            if (++_frames < _afterFrames)
            {
                return;
            }

            string target = _path.StartsWith("res://") || _path.StartsWith("user://") ? ProjectSettings.GlobalizePath(_path) : _path;
            var image = GetViewport().GetTexture().GetImage();
            var error = image.SavePng(target);
            GD.Print(error == Error.Ok ? $"[screenshot] saved {target} ({image.GetWidth()}x{image.GetHeight()})" : $"[screenshot] FAILED {error} -> {target}");
            GetTree().Quit(error == Error.Ok ? 0 : 1);
            SetProcess(false);
        }
    }
}
