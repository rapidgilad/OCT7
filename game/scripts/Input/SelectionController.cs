using System.Collections.Generic;
using Godot;
using OCT7.Game.UI;
using OCT7.Game.Views;
using OCT7.Sim;

namespace OCT7.Game.Input
{
    /// <summary>
    /// Left click / drag selects the local player's squads (Shift adds), Ctrl+A selects all,
    /// right click orders a move. Uses screen-space projection, so no physics colliders are needed.
    /// </summary>
    public partial class SelectionController : Node
    {
        private const float ClickThresholdPixels = 6f;
        private const float ClickPickRadiusPixels = 28f;

        private readonly HashSet<int> _selected = new HashSet<int>();
        private readonly List<int> _orderBuffer = new List<int>();

        private Main _main;
        private RtsCamera _camera;
        private DebugHud _hud;
        private bool _dragging;
        private Vector2 _dragStart;
        private MeshInstance3D _moveMarker;
        private double _markerTime;

        public ICollection<int> Selected => _selected;

        public void Initialize(Main main, RtsCamera camera, DebugHud hud)
        {
            _main = main;
            _camera = camera;
            _hud = hud;

            _moveMarker = new MeshInstance3D
            {
                Name = "MoveMarker",
                Mesh = new CylinderMesh { TopRadius = 1.6f, BottomRadius = 1.6f, Height = 0.06f, RadialSegments = 32 },
                MaterialOverride = new StandardMaterial3D
                {
                    AlbedoColor = new Color(TeamColors.Selection, 0.8f),
                    Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                    ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                },
                Visible = false,
            };
            main.AddChild(_moveMarker);
        }

        public void SelectAllOwn()
        {
            _selected.Clear();
            foreach (var s in _main.Sim.World.Squads)
            {
                if (s.OwnerId == Main.LocalPlayerId)
                {
                    _selected.Add(s.Id);
                }
            }
        }

        public override void _Process(double delta)
        {
            // Forget squads that no longer exist.
            _selected.RemoveWhere(id => _main.Sim.World.GetSquad(id) == null);

            if (_moveMarker.Visible)
            {
                _markerTime += delta;
                float t = (float)(_markerTime / 0.8);
                _moveMarker.Scale = new Vector3(1f - t * 0.7f, 1f, 1f - t * 0.7f);
                _moveMarker.Visible = t < 1f;
            }
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            switch (@event)
            {
                case InputEventMouseButton mb when mb.ButtonIndex == MouseButton.Left:
                    if (mb.Pressed)
                    {
                        _dragging = true;
                        _dragStart = mb.Position;
                    }
                    else if (_dragging)
                    {
                        _dragging = false;
                        _hud.SetSelectionRect(null);
                        FinishSelection(_dragStart, mb.Position, mb.ShiftPressed);
                    }

                    break;

                case InputEventMouseButton mb when mb.ButtonIndex == MouseButton.Right && mb.Pressed:
                    OrderMove(mb.Position);
                    break;

                case InputEventMouseMotion motion when _dragging:
                    if (motion.Position.DistanceTo(_dragStart) > ClickThresholdPixels)
                    {
                        _hud.SetSelectionRect(new Rect2(_dragStart, motion.Position - _dragStart).Abs());
                    }

                    break;

                case InputEventKey key when key.Pressed && !key.Echo && key.CtrlPressed && key.PhysicalKeycode == Key.A:
                    SelectAllOwn();
                    break;
            }
        }

        private void FinishSelection(Vector2 start, Vector2 end, bool additive)
        {
            if (!additive)
            {
                _selected.Clear();
            }

            if (start.DistanceTo(end) <= ClickThresholdPixels)
            {
                int picked = PickNearest(end);
                if (picked >= 0)
                {
                    _selected.Add(picked);
                }

                return;
            }

            var rect = new Rect2(start, end - start).Abs();
            foreach (var s in _main.Sim.World.Squads)
            {
                if (s.OwnerId != Main.LocalPlayerId)
                {
                    continue;
                }

                var world = new Vector3(s.Position.X, 0.8f, s.Position.Y);
                if (!_camera.Camera.IsPositionBehind(world) && rect.HasPoint(_camera.Camera.UnprojectPosition(world)))
                {
                    _selected.Add(s.Id);
                }
            }
        }

        private int PickNearest(Vector2 screen)
        {
            int best = -1;
            float bestDistance = ClickPickRadiusPixels;
            foreach (var s in _main.Sim.World.Squads)
            {
                if (s.OwnerId != Main.LocalPlayerId)
                {
                    continue;
                }

                var world = new Vector3(s.Position.X, 0.8f, s.Position.Y);
                if (_camera.Camera.IsPositionBehind(world))
                {
                    continue;
                }

                float d = _camera.Camera.UnprojectPosition(world).DistanceTo(screen);
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = s.Id;
                }
            }

            return best;
        }

        private void OrderMove(Vector2 screen)
        {
            if (_selected.Count == 0 || !_camera.TryScreenToGround(screen, out var ground))
            {
                return;
            }

            _orderBuffer.Clear();
            _orderBuffer.AddRange(_selected);
            _main.IssueMove(_orderBuffer, new Vec2(ground.X, ground.Z));

            _moveMarker.Position = new Vector3(ground.X, 0.05f, ground.Z);
            _moveMarker.Scale = Vector3.One;
            _moveMarker.Visible = true;
            _markerTime = 0;
        }
    }
}
