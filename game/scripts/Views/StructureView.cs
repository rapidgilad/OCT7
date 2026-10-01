using Godot;
using OCT7.Game.Visual;
using OCT7.Sim.Production;

namespace OCT7.Game.Views
{
    /// <summary>A structure model. Under construction it rises out of a scaffold; when selected it shows a ring.</summary>
    public partial class StructureView : Node3D
    {
        private Node3D _model;
        private Node3D _scaffold;
        private MeshInstance3D _ring;
        private float _shownProgress = -1f;

        public int StructureId { get; private set; }
        public bool EverSeen { get; set; }

        public void Build(Structure s, Color team)
        {
            StructureId = s.Id;
            Name = $"Structure{s.Id}_{s.Def.Id}";
            float w = s.SizeX * s.CellSize;
            float d = s.SizeY * s.CellSize;
            Position = new Vector3(s.Center.X, 0f, s.Center.Y);
            _model = ModelFactory.BuildStructure(s.Def, w, d, team);
            AddChild(_model);

            if (s.Def.Kind != OCT7.Sim.Data.StructureKind.Sandbags)
            {
                _scaffold = new Node3D();
                var pole = new Color(0.45f, 0.42f, 0.38f);
                for (int i = 0; i < 4; i++)
                {
                    float x = (i % 2 == 0 ? -0.5f : 0.5f) * w * 0.9f;
                    float z = (i < 2 ? -0.5f : 0.5f) * d * 0.9f;
                    MeshKit.Box(_scaffold, new Vector3(0.12f, 4f, 0.12f), new Vector3(x, 2f, z), pole);
                }

                MeshKit.Box(_scaffold, new Vector3(w * 0.9f, 0.08f, 0.12f), new Vector3(0f, 3.9f, -d * 0.45f), pole);
                MeshKit.Box(_scaffold, new Vector3(w * 0.9f, 0.08f, 0.12f), new Vector3(0f, 3.9f, d * 0.45f), pole);
                MeshKit.Box(_scaffold, new Vector3(w * 0.95f, 0.05f, d * 0.95f), new Vector3(0f, 0.03f, 0f), new Color(0.5f, 0.45f, 0.36f));
                AddChild(_scaffold);
            }

            float r = Mathf.Max(w, d) * 0.62f;
            _ring = new MeshInstance3D
            {
                Mesh = MeshKit.RingMesh(r, 0.4f, 48),
                MaterialOverride = MeshKit.Flat(new Color(0.7f, 1f, 0.6f, 0.95f)),
                Position = new Vector3(0f, 0.05f, 0f),
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                Visible = false,
            };
            AddChild(_ring);
        }

        public void SetSelected(bool selected) => _ring.Visible = selected;

        public void UpdateVisual(Structure s)
        {
            float p = s.BuildProgress;
            if (Mathf.IsEqualApprox(p, _shownProgress))
            {
                return;
            }

            _shownProgress = p;
            bool complete = s.IsComplete;
            _model.Scale = new Vector3(1f, complete ? 1f : Mathf.Max(0.04f, p), 1f);
            if (_scaffold != null)
            {
                _scaffold.Visible = !complete;
            }
        }
    }
}
