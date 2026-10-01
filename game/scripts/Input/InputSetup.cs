using Godot;

namespace OCT7.Game.Input
{
    /// <summary>
    /// Registers input actions in code (instead of hand-editing project.godot). Physical keycodes keep
    /// WASD in the same place on non-QWERTY layouts. Rebinding UI comes later.
    /// </summary>
    public static class InputSetup
    {
        public const string CamLeft = "cam_left";
        public const string CamRight = "cam_right";
        public const string CamForward = "cam_forward";
        public const string CamBack = "cam_back";
        public const string CamRotateLeft = "cam_rotate_left";
        public const string CamRotateRight = "cam_rotate_right";

        public static void EnsureActions()
        {
            Add(CamLeft, Key.A, Key.Left);
            Add(CamRight, Key.D, Key.Right);
            Add(CamForward, Key.W, Key.Up);
            Add(CamBack, Key.S, Key.Down);
            Add(CamRotateLeft, Key.Q);
            Add(CamRotateRight, Key.E);
        }

        private static void Add(string action, params Key[] keys)
        {
            if (InputMap.HasAction(action))
            {
                return;
            }

            InputMap.AddAction(action);
            foreach (var key in keys)
            {
                InputMap.ActionAddEvent(action, new InputEventKey { PhysicalKeycode = key });
            }
        }
    }
}
