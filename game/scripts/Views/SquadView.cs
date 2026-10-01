using Godot;
using OCT7.Sim.Data;
using OCT7.Sim.Units;

namespace OCT7.Game.Views
{
    /// <summary>
    /// Placeholder visuals for one squad: a capsule per soldier (or a box for vehicles), a team-colored base disc
    /// and a selection ring. Local +Z is the squad's forward direction.
    /// </summary>
    public partial class SquadView : Node3D
    {
        private const float SoldierSpacing = 1.7f;

        private MeshInstance3D _selectionRing;
        private Node3D _models;
        private int _shownModels = -1;
        private float _footprintRadius;

        public int SquadId { get; private set; }

        public void Build(Squad squad, Color teamColor)
        {
            SquadId = squad.Id;
            Name = $"Squad{squad.Id}_{squad.Def.Id}";
            _models = new Node3D { Name = "Models" };
            AddChild(_models);

            var def = squad.Def;
            if (def.Category == UnitCategory.Vehicle)
            {
                BuildVehicle(def, teamColor);
            }
            else
            {
                BuildInfantry(def, teamColor, squad.Models);
            }

            var disc = new MeshInstance3D
            {
                Name = "TeamDisc",
                Mesh = new CylinderMesh { TopRadius = _footprintRadius, BottomRadius = _footprintRadius, Height = 0.04f, RadialSegments = 32 },
                MaterialOverride = new StandardMaterial3D
                {
                    AlbedoColor = new Color(teamColor, 0.35f),
                    Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                    ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                },
                Position = new Vector3(0f, 0.03f, 0f),
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            };
            AddChild(disc);

            float ring = _footprintRadius + 0.5f;
            _selectionRing = new MeshInstance3D
            {
                Name = "SelectionRing",
                Mesh = new CylinderMesh { TopRadius = ring, BottomRadius = ring, Height = 0.05f, RadialSegments = 40 },
                MaterialOverride = new StandardMaterial3D
                {
                    AlbedoColor = new Color(TeamColors.Selection, 0.55f),
                    Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                    ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                },
                Position = new Vector3(0f, 0.02f, 0f),
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                Visible = false,
            };
            AddChild(_selectionRing);
        }

        public void SetSelected(bool selected) => _selectionRing.Visible = selected;

        /// <summary>Hide models of soldiers the squad has lost (combat comes later; this keeps visuals in sync).</summary>
        public void SetAliveModels(int alive)
        {
            if (alive == _shownModels)
            {
                return;
            }

            _shownModels = alive;
            int i = 0;
            foreach (Node child in _models.GetChildren())
            {
                if (child is Node3D n && n.HasMeta("soldier"))
                {
                    n.Visible = i < alive;
                    i++;
                }
            }
        }

        private void BuildInfantry(UnitDef def, Color teamColor, int count)
        {
            bool hero = def.Category == UnitCategory.Hero;
            bool support = def.Category == UnitCategory.Support;
            var uniform = new StandardMaterial3D { AlbedoColor = teamColor.Lerp(new Color(0.35f, 0.38f, 0.28f), 0.45f) };
            var leaderMat = new StandardMaterial3D { AlbedoColor = hero ? new Color(0.95f, 0.78f, 0.25f) : teamColor };

            int columns = count <= 3 ? count : 3;
            int rows = (count + columns - 1) / columns;
            for (int i = 0; i < count; i++)
            {
                int row = i / columns;
                int col = i % columns;
                float x = (col - (columns - 1) * 0.5f) * SoldierSpacing;
                float z = ((rows - 1) * 0.5f - row) * SoldierSpacing;
                var soldier = new MeshInstance3D
                {
                    Mesh = new CapsuleMesh { Radius = 0.32f, Height = 1.75f },
                    MaterialOverride = i == 0 ? leaderMat : uniform,
                    Position = new Vector3(x, 0.875f, z),
                };
                soldier.SetMeta("soldier", true);
                _models.AddChild(soldier);
            }

            if (support)
            {
                // Crew-served weapon (MG / ATGM / launcher) in front of the team.
                _models.AddChild(new MeshInstance3D
                {
                    Mesh = new BoxMesh { Size = new Vector3(0.5f, 0.45f, 1.6f) },
                    MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.18f, 0.18f, 0.16f) },
                    Position = new Vector3(0f, 0.3f, rows * SoldierSpacing * 0.5f + 0.9f),
                });
            }

            _footprintRadius = Mathf.Max(columns, rows) * SoldierSpacing * 0.5f + 0.8f;
            _shownModels = count;
        }

        private void BuildVehicle(UnitDef def, Color teamColor)
        {
            // Size class from pop cost: heavy armor, medium (APC/dozer), light (technical/truck).
            Vector3 hull = def.Pop >= 14 ? new Vector3(3.7f, 1.9f, 8.6f)
                         : def.Pop >= 8 ? new Vector3(3.6f, 2.3f, 7.4f)
                         : new Vector3(2.1f, 1.6f, 5.0f);
            var hullMat = new StandardMaterial3D { AlbedoColor = teamColor.Lerp(new Color(0.42f, 0.42f, 0.34f), 0.55f) };
            _models.AddChild(new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = hull },
                MaterialOverride = hullMat,
                Position = new Vector3(0f, hull.Y * 0.5f + 0.25f, 0f),
            });

            if (def.Pop >= 14)
            {
                // Turret + gun for main battle tanks.
                var turretMat = new StandardMaterial3D { AlbedoColor = hullMat.AlbedoColor.Darkened(0.15f) };
                _models.AddChild(new MeshInstance3D
                {
                    Mesh = new BoxMesh { Size = new Vector3(2.6f, 0.9f, 3.6f) },
                    MaterialOverride = turretMat,
                    Position = new Vector3(0f, hull.Y + 0.7f, -0.6f),
                });
                _models.AddChild(new MeshInstance3D
                {
                    Mesh = new CylinderMesh { TopRadius = 0.12f, BottomRadius = 0.14f, Height = 5f },
                    MaterialOverride = turretMat,
                    Position = new Vector3(0f, hull.Y + 0.75f, 2.6f),
                    RotationDegrees = new Vector3(90f, 0f, 0f),
                });
            }
            else if (def.Pop < 8)
            {
                // Pickup / truck: cab at the front.
                _models.AddChild(new MeshInstance3D
                {
                    Mesh = new BoxMesh { Size = new Vector3(2.0f, 1.0f, 1.6f) },
                    MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.85f, 0.85f, 0.82f) },
                    Position = new Vector3(0f, hull.Y + 0.75f, 1.4f),
                });
            }

            _footprintRadius = hull.Z * 0.5f + 0.4f;
        }
    }
}
