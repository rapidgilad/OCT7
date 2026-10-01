using Godot;

namespace OCT7.Game.Input
{
    /// <summary>
    /// CoH-style RTS camera: WASD / arrow / edge pan, mouse-wheel zoom, Q/E rotate.
    /// The rig orbits a focus point on the ground at a fixed pitch.
    /// </summary>
    public partial class RtsCamera : Node3D
    {
        private const float PitchDegrees = 55f;
        private const float MinDistance = 20f;
        private const float MaxDistance = 260f;
        private const float EdgePanMargin = 8f;
        private const float RotateSpeedDegrees = 90f;

        private Vector3 _focus;
        private float _yawDegrees = 225f;
        private float _distance = 75f;
        private float _targetDistance = 75f;
        private float _boundsX = 256f;
        private float _boundsZ = 256f;

        public Camera3D Camera { get; private set; }

        /// <summary>Disabled for automated screenshots (the virtual mouse would otherwise pan the camera).</summary>
        public bool EdgePanEnabled { get; set; } = true;

        public override void _Ready()
        {
            Camera = new Camera3D { Name = "Camera", Fov = 50f, Near = 0.5f, Far = 2000f, Current = true };
            AddChild(Camera);
            UpdateTransform();
        }

        public void SetBounds(float worldWidth, float worldHeight)
        {
            _boundsX = worldWidth;
            _boundsZ = worldHeight;
        }

        public void SetView(Vector3 focus, float distance, float yawDegrees)
        {
            _focus = focus;
            _distance = _targetDistance = Mathf.Clamp(distance, MinDistance, MaxDistance);
            _yawDegrees = yawDegrees;
            UpdateTransform();
        }

        public override void _Process(double delta)
        {
            float dt = (float)delta;
            var pan = Vector2.Zero;
            if (Godot.Input.IsActionPressed(InputSetup.CamLeft)) pan.X -= 1f;
            if (Godot.Input.IsActionPressed(InputSetup.CamRight)) pan.X += 1f;
            if (Godot.Input.IsActionPressed(InputSetup.CamForward)) pan.Y += 1f;
            if (Godot.Input.IsActionPressed(InputSetup.CamBack)) pan.Y -= 1f;

            if (EdgePanEnabled && DisplayServer.WindowIsFocused())
            {
                var mouse = GetViewport().GetMousePosition();
                var size = GetViewport().GetVisibleRect().Size;
                bool inside = mouse.X >= 0 && mouse.Y >= 0 && mouse.X <= size.X && mouse.Y <= size.Y;
                if (inside)
                {
                    if (mouse.X <= EdgePanMargin) pan.X -= 1f;
                    if (mouse.X >= size.X - EdgePanMargin) pan.X += 1f;
                    if (mouse.Y <= EdgePanMargin) pan.Y += 1f;
                    if (mouse.Y >= size.Y - EdgePanMargin) pan.Y -= 1f;
                }
            }

            if (Godot.Input.IsActionPressed(InputSetup.CamRotateLeft)) _yawDegrees -= RotateSpeedDegrees * dt;
            if (Godot.Input.IsActionPressed(InputSetup.CamRotateRight)) _yawDegrees += RotateSpeedDegrees * dt;

            if (pan != Vector2.Zero)
            {
                float yaw = Mathf.DegToRad(_yawDegrees);
                var forward = new Vector3(-Mathf.Sin(yaw), 0f, -Mathf.Cos(yaw));
                var right = new Vector3(Mathf.Cos(yaw), 0f, -Mathf.Sin(yaw));
                float speed = 0.9f * _distance + 20f;
                _focus += (right * pan.X + forward * pan.Y).Normalized() * speed * dt;
            }

            _focus = new Vector3(Mathf.Clamp(_focus.X, 0f, _boundsX), 0f, Mathf.Clamp(_focus.Z, 0f, _boundsZ));
            _distance = Mathf.Lerp(_distance, _targetDistance, 1f - Mathf.Exp(-12f * dt));
            UpdateTransform();
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            if (@event is InputEventMouseButton mb && mb.Pressed)
            {
                if (mb.ButtonIndex == MouseButton.WheelUp)
                {
                    _targetDistance = Mathf.Clamp(_targetDistance * 0.88f, MinDistance, MaxDistance);
                }
                else if (mb.ButtonIndex == MouseButton.WheelDown)
                {
                    _targetDistance = Mathf.Clamp(_targetDistance * 1.12f, MinDistance, MaxDistance);
                }
            }
        }

        /// <summary>Ray from the camera through a screen point, intersected with the ground plane (y = 0).</summary>
        public bool TryScreenToGround(Vector2 screenPosition, out Vector3 ground)
        {
            var origin = Camera.ProjectRayOrigin(screenPosition);
            var direction = Camera.ProjectRayNormal(screenPosition);
            var hit = new Plane(Vector3.Up, 0f).IntersectsRay(origin, direction);
            ground = hit ?? Vector3.Zero;
            return hit.HasValue;
        }

        private void UpdateTransform()
        {
            if (Camera == null)
            {
                return;
            }

            float yaw = Mathf.DegToRad(_yawDegrees);
            float pitch = Mathf.DegToRad(PitchDegrees);
            float horizontal = Mathf.Cos(pitch) * _distance;
            var offset = new Vector3(Mathf.Sin(yaw) * horizontal, Mathf.Sin(pitch) * _distance, Mathf.Cos(yaw) * horizontal);
            var position = _focus + offset;
            Camera.GlobalTransform = new Transform3D(Basis.Identity, position).LookingAt(_focus, Vector3.Up);
        }
    }
}
