using Godot;
using OCT7.Game.Visual;

namespace OCT7.Game.Views
{
    /// <summary>Falls over, lies still, then sinks away.</summary>
    public partial class CorpseAnimator : Node
    {
        private readonly SoldierRig _rig;
        private readonly float _lifetime;
        private readonly bool _forward;
        private float _t;

        public CorpseAnimator()
        {
        }

        public CorpseAnimator(SoldierRig rig, float lifetime, bool forward)
        {
            _rig = rig;
            _lifetime = lifetime;
            _forward = forward;
        }

        public override void _Process(double delta)
        {
            _t += (float)delta;
            float fall = Mathf.Min(1f, _t / 0.55f);
            float ease = fall * fall;
            _rig.Pose.RotationDegrees = new Vector3((_forward ? 86f : -86f) * ease, 0f, 0f);
            _rig.Pose.Position = new Vector3(0f, 0.14f * ease, 0f);
            _rig.LegL.RotationDegrees = new Vector3(-15f * ease, 0f, 0f);
            _rig.LegR.RotationDegrees = new Vector3(20f * ease, 0f, 0f);
            if (_t > _lifetime)
            {
                float sink = (_t - _lifetime) * 0.4f;
                _rig.Root.Position -= new Vector3(0f, sink * (float)delta * 2f, 0f);
                if (_t > _lifetime + 2f)
                {
                    _rig.Root.QueueFree();
                }
            }
        }
    }
}
