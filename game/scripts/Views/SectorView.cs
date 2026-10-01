using System.Collections.Generic;
using Godot;
using OCT7.Game.Visual;
using OCT7.Sim;
using OCT7.Sim.Data;
using OCT7.Sim.Territory;

namespace OCT7.Game.Views
{
    /// <summary>
    /// Capture points: a pole with a flag that climbs with capture progress and takes the owner's color,
    /// a type marker on top (VP star, fuel barrel, munitions crate) and a capture-radius ring.
    /// </summary>
    public partial class SectorView : Node3D
    {
        private readonly List<(Sector sector, MeshInstance3D flag, MeshInstance3D ring)> _points = new List<(Sector, MeshInstance3D, MeshInstance3D)>();

        public void Build(Simulation sim)
        {
            foreach (var s in sim.Territory.Sectors)
            {
                if (s.Type == SectorType.Hq)
                {
                    continue;
                }

                var root = new Node3D { Position = new Vector3(s.Position.X, 0f, s.Position.Y), Name = $"Sector_{s.Def.Id}" };
                AddChild(root);
                MeshKit.Cyl(root, 0.35f, 0.45f, 0.3f, new Vector3(0f, 0.15f, 0f), new Color(0.4f, 0.38f, 0.34f), null, 8);
                MeshKit.Cyl(root, 0.06f, 0.07f, 6f, new Vector3(0f, 3f, 0f), new Color(0.3f, 0.3f, 0.3f), null, 6);
                switch (s.Type)
                {
                    case SectorType.Victory:
                        MeshKit.Sphere(root, 0.32f, new Vector3(0f, 6.25f, 0f), new Color(0.95f, 0.78f, 0.25f), false, 8);
                        break;
                    case SectorType.Fuel:
                        MeshKit.Cyl(root, 0.3f, 0.3f, 0.8f, new Vector3(0.6f, 0.4f, 0.4f), new Color(0.75f, 0.25f, 0.15f), null, 10);
                        MeshKit.Cyl(root, 0.3f, 0.3f, 0.8f, new Vector3(0.2f, 0.4f, 0.9f), new Color(0.75f, 0.25f, 0.15f), null, 10);
                        break;
                    case SectorType.Munitions:
                        MeshKit.Box(root, new Vector3(1.0f, 0.5f, 0.6f), new Vector3(0.7f, 0.25f, 0.5f), new Color(0.3f, 0.34f, 0.22f));
                        MeshKit.Box(root, new Vector3(1.0f, 0.5f, 0.6f), new Vector3(0.7f, 0.75f, 0.5f), new Color(0.28f, 0.32f, 0.2f));
                        break;
                }

                var flag = new MeshInstance3D
                {
                    Mesh = MeshKit.BoxMesh(new Vector3(0.05f, 0.8f, 1.3f)),
                    MaterialOverride = MeshKit.Flat(Colors.White, unique: true),
                    Position = new Vector3(0f, 1f, 0.68f),
                };
                root.AddChild(flag);

                float r = sim.Rules.CaptureRadius;
                var ring = new MeshInstance3D
                {
                    Mesh = MeshKit.CylMesh(r, r, 0.03f, 40),
                    MaterialOverride = MeshKit.Flat(new Color(1f, 1f, 1f, 0.1f), unique: true),
                    Position = new Vector3(0f, 0.035f, 0f),
                    CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                };
                root.AddChild(ring);
                _points.Add((s, flag, ring));
            }
        }

        public void UpdateVisual()
        {
            foreach (var (s, flag, ring) in _points)
            {
                Color c;
                float height;
                if (s.OwnerId >= 0)
                {
                    c = TeamColors.For(s.OwnerId);
                    height = 1f + 4.7f * s.Progress;
                }
                else
                {
                    c = s.CapturingPlayerId >= 0 && s.Progress > 0f ? TeamColors.For(s.CapturingPlayerId).Lerp(Colors.White, 0.5f) : new Color(0.92f, 0.92f, 0.88f);
                    height = 1f + 4.7f * s.Progress;
                }

                ((StandardMaterial3D)flag.MaterialOverride).AlbedoColor = c;
                flag.Position = new Vector3(0f, height, 0.68f);
                var ringColor = s.OwnerId >= 0 ? TeamColors.For(s.OwnerId) : Colors.White;
                ((StandardMaterial3D)ring.MaterialOverride).AlbedoColor = new Color(ringColor, s.IsContested ? 0.28f : 0.12f);
            }
        }
    }
}
