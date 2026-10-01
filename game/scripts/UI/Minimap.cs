using Godot;
using OCT7.Game.Match;
using OCT7.Game.Views;
using OCT7.Sim;
using OCT7.Sim.Data;

namespace OCT7.Game.UI
{
    /// <summary>
    /// Minimap: ground texture, obstacles, sector ownership, structures, own squads and visible enemies,
    /// fog of war and the camera frame. Left-click / drag jumps the camera; right-click orders selected squads to move.
    /// </summary>
    public partial class Minimap : Control
    {
        private MatchController _match;
        private Texture2D _ground;
        private bool _dragging;
        private Image _fogImage;
        private ImageTexture _fogTexture;
        private byte[] _fogBytes;
        private int _fogTick = -1;

        public void Initialize(MatchController match, Texture2D groundTexture)
        {
            _match = match;
            _ground = groundTexture;
            MouseFilter = MouseFilterEnum.Stop;
        }

        public override void _Process(double delta) => QueueRedraw();

        private Vector2 ToMini(Vec2 world)
        {
            var g = _match.Sim.Map.Grid;
            return new Vector2(world.X / g.WorldWidth * Size.X, world.Y / g.WorldHeight * Size.Y);
        }

        private Vec2 ToWorld(Vector2 mini)
        {
            var g = _match.Sim.Map.Grid;
            return new Vec2(Mathf.Clamp(mini.X / Size.X, 0f, 1f) * g.WorldWidth, Mathf.Clamp(mini.Y / Size.Y, 0f, 1f) * g.WorldHeight);
        }

        public override void _Draw()
        {
            if (_match?.Sim == null)
            {
                return;
            }

            var sim = _match.Sim;
            var g = sim.Map.Grid;
            int me = _match.LocalPlayerId;
            var rect = new Rect2(Vector2.Zero, Size);
            if (_ground != null)
            {
                DrawTextureRect(_ground, rect, false);
            }

            float sx = Size.X / g.Width, sy = Size.Y / g.Height;
            foreach (var o in sim.Map.Obstacles)
            {
                var c = o.Kind == ObstacleKind.Building ? new Color(0.35f, 0.33f, 0.3f) : o.Kind == ObstacleKind.Rock ? new Color(0.45f, 0.42f, 0.38f) : new Color(0.5f, 0.46f, 0.4f);
                DrawRect(new Rect2(o.MinX * sx, o.MinY * sy, (o.MaxX - o.MinX + 1) * sx, (o.MaxY - o.MinY + 1) * sy), c);
            }

            // Fog: one texel per cell, refreshed a few times per second, drawn with linear filtering.
            if (!_match.Views.RevealAll)
            {
                UpdateFogTexture(sim, me);
                DrawTextureRect(_fogTexture, rect, false);
            }

            foreach (var s in sim.Territory.Sectors)
            {
                var p = ToMini(s.Position);
                var color = s.OwnerId >= 0 ? TeamColors.For(s.OwnerId) : new Color(0.9f, 0.9f, 0.85f);
                float r = s.Type == SectorType.Victory ? 6f : s.Type == SectorType.Hq ? 0f : 4f;
                if (r > 0f)
                {
                    DrawCircle(p, r + 1.5f, new Color(0f, 0f, 0f, 0.6f));
                    DrawCircle(p, r, color);
                    if (s.Type == SectorType.Victory)
                    {
                        DrawCircle(p, 2f, new Color(0.95f, 0.8f, 0.3f));
                    }
                }
            }

            foreach (var st in sim.World.Structures)
            {
                bool shown = st.OwnerId == me || (_match.Views.TryGetStructureView(st.Id, out var v) && v.EverSeen);
                if (!shown)
                {
                    continue;
                }

                var p = ToMini(st.Center);
                float half = st.SizeX * sx * 0.5f + 1f;
                DrawRect(new Rect2(p.X - half, p.Y - half, half * 2f, half * 2f), TeamColors.For(st.OwnerId).Darkened(0.2f));
            }

            foreach (var s in sim.World.Squads)
            {
                if (!_match.Views.IsShown(sim, s))
                {
                    continue;
                }

                var p = ToMini(s.Position);
                float size = s.Def.IsVehicle ? 4f : 3f;
                DrawRect(new Rect2(p.X - size * 0.5f, p.Y - size * 0.5f, size, size), TeamColors.For(s.OwnerId).Lightened(0.25f));
            }

            // Camera frame: project the screen corners to the ground.
            var cam = _match.CameraRig;
            var vp = GetViewport().GetVisibleRect().Size;
            var corners = new[] { new Vector2(0f, 44f), new Vector2(vp.X, 44f), new Vector2(vp.X, vp.Y - 206f), new Vector2(0f, vp.Y - 206f) };
            var pts = new Vector2[5];
            bool ok = true;
            for (int i = 0; i < 4; i++)
            {
                if (!cam.TryScreenToGround(corners[i], out var ground))
                {
                    ok = false;
                    break;
                }

                var m = ToMini(new Vec2(ground.X, ground.Z));
                pts[i] = new Vector2(Mathf.Clamp(m.X, 0f, Size.X), Mathf.Clamp(m.Y, 0f, Size.Y));
            }

            if (ok)
            {
                pts[4] = pts[0];
                DrawPolyline(pts, new Color(1f, 1f, 1f, 0.8f), 1.2f);
            }

            DrawRect(rect, new Color(0.86f, 0.76f, 0.5f, 0.8f), false, 1.5f);
        }

        private void UpdateFogTexture(Simulation sim, int me)
        {
            var g = sim.Map.Grid;
            if (_fogImage == null)
            {
                _fogBytes = new byte[g.Width * g.Height * 4];
                _fogImage = Image.CreateFromData(g.Width, g.Height, false, Image.Format.Rgba8, _fogBytes);
                _fogTexture = ImageTexture.CreateFromImage(_fogImage);
                TextureFilter = TextureFilterEnum.Linear;
            }

            if (_fogTick >= 0 && sim.Tick - _fogTick < 3)
            {
                return;
            }

            _fogTick = sim.Tick;
            for (int y = 0; y < g.Height; y++)
            {
                for (int x = 0; x < g.Width; x++)
                {
                    var cell = new GridPos(x, y);
                    byte a = sim.Vision.IsVisible(me, cell) ? (byte)0 : sim.Vision.IsExplored(me, cell) ? (byte)80 : (byte)140;
                    int i = (y * g.Width + x) * 4;
                    _fogBytes[i] = 0;
                    _fogBytes[i + 1] = 0;
                    _fogBytes[i + 2] = 0;
                    _fogBytes[i + 3] = a;
                }
            }

            _fogImage.SetData(g.Width, g.Height, false, Image.Format.Rgba8, _fogBytes);
            _fogTexture.Update(_fogImage);
        }

        public override void _GuiInput(InputEvent @event)
        {
            if (_match == null)
            {
                return;
            }

            if (@event is InputEventMouseButton mb)
            {
                if (mb.ButtonIndex == MouseButton.Left)
                {
                    _dragging = mb.Pressed;
                    if (mb.Pressed)
                    {
                        Jump(mb.Position);
                    }
                }
                else if (mb.ButtonIndex == MouseButton.Right && mb.Pressed)
                {
                    var ids = System.Linq.Enumerable.ToArray(_match.Player.SelectedSquads);
                    if (ids.Length > 0)
                    {
                        _match.Enqueue(new MoveSquadsCommand(_match.LocalPlayerId, ids, ToWorld(mb.Position)));
                    }
                }

                AcceptEvent();
            }
            else if (@event is InputEventMouseMotion mm && _dragging)
            {
                Jump(mm.Position);
                AcceptEvent();
            }
        }

        private void Jump(Vector2 mini)
        {
            var w = ToWorld(mini);
            _match.CameraRig.FocusOn(new Vector3(w.X, 0f, w.Y));
        }
    }
}
