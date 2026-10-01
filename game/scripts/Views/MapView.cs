using System.Collections.Generic;
using Godot;
using OCT7.Sim;
using OCT7.Sim.Economy;
using OCT7.Sim.World;

namespace OCT7.Game.Views
{
    /// <summary>Placeholder map visuals: ground colored by ground type, grey building blocks, HQ pads.</summary>
    public partial class MapView : Node3D
    {
        private static readonly Color Sand = new Color(0.77f, 0.70f, 0.54f);
        private static readonly Color Rock = new Color(0.56f, 0.54f, 0.51f);
        private static readonly Color Farmland = new Color(0.47f, 0.54f, 0.33f);

        public void Build(MapDefinition map, IReadOnlyList<PlayerState> players)
        {
            var grid = map.Grid;
            BuildOuterGround(grid);
            BuildGround(grid);
            for (int i = 0; i < map.Obstacles.Count; i++)
            {
                BuildObstacle(grid, map.Obstacles[i], i);
            }

            foreach (var p in players)
            {
                BuildHq(p);
            }
        }

        private void BuildOuterGround(MapGrid grid)
        {
            var outer = new MeshInstance3D
            {
                Name = "OuterGround",
                Mesh = new PlaneMesh { Size = new Vector2(grid.WorldWidth * 4f, grid.WorldHeight * 4f) },
                MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.42f, 0.38f, 0.30f), Roughness = 1f },
                Position = new Vector3(grid.WorldWidth * 0.5f, -0.05f, grid.WorldHeight * 0.5f),
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            };
            AddChild(outer);
        }

        private void BuildGround(MapGrid grid)
        {
            // One pixel per cell; a little deterministic noise so the ground doesn't look flat.
            var image = Image.CreateEmpty(grid.Width, grid.Height, false, Image.Format.Rgb8);
            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    var type = grid.GetGround(new GridPos(x, y));
                    var baseColor = type == GroundType.Rock ? Rock : type == GroundType.MudFarmland ? Farmland : Sand;
                    float noise = (Hash(x, y) % 1000) / 1000f * 0.08f - 0.04f;
                    image.SetPixel(x, y, new Color(baseColor.R + noise, baseColor.G + noise, baseColor.B + noise));
                }
            }

            var material = new StandardMaterial3D
            {
                AlbedoTexture = ImageTexture.CreateFromImage(image),
                TextureFilter = BaseMaterial3D.TextureFilterEnum.Linear,
                Roughness = 1f,
            };

            var ground = new MeshInstance3D
            {
                Name = "Ground",
                Mesh = new PlaneMesh { Size = new Vector2(grid.WorldWidth, grid.WorldHeight) },
                MaterialOverride = material,
                Position = new Vector3(grid.WorldWidth * 0.5f, 0f, grid.WorldHeight * 0.5f),
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            };
            AddChild(ground);
        }

        private void BuildObstacle(MapGrid grid, MapObstacle o, int index)
        {
            float cs = grid.CellSize;
            float w = (o.MaxX - o.MinX + 1) * cs;
            float d = (o.MaxY - o.MinY + 1) * cs;
            float shade = 0.55f + (index % 4) * 0.05f;
            var body = new MeshInstance3D
            {
                Name = $"Building{index}",
                Mesh = new BoxMesh { Size = new Vector3(w, o.Height, d) },
                MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(shade, shade * 0.97f, shade * 0.92f), Roughness = 0.9f },
                Position = new Vector3(o.MinX * cs + w * 0.5f, o.Height * 0.5f, o.MinY * cs + d * 0.5f),
            };
            AddChild(body);

            // Roof parapet in a darker tone for readability from the RTS camera.
            var roof = new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(w - 1f, 0.3f, d - 1f) },
                MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(shade * 0.7f, shade * 0.68f, shade * 0.64f) },
                Position = new Vector3(0f, o.Height * 0.5f + 0.15f, 0f),
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            };
            body.AddChild(roof);
        }

        private void BuildHq(PlayerState player)
        {
            var color = TeamColors.For(player.Id);
            var root = new Node3D { Name = $"HQ{player.Id}", Position = new Vector3(player.HqPosition.X, 0f, player.HqPosition.Y) };
            AddChild(root);

            root.AddChild(new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(14f, 0.3f, 14f) },
                MaterialOverride = new StandardMaterial3D { AlbedoColor = color.Darkened(0.35f) },
                Position = new Vector3(0f, 0.15f, 0f),
            });
            root.AddChild(new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(8f, 4f, 6f) },
                MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.40f, 0.42f, 0.36f) },
                Position = new Vector3(0f, 2.3f, 0f),
            });
            root.AddChild(new MeshInstance3D
            {
                Mesh = new CylinderMesh { TopRadius = 0.12f, BottomRadius = 0.12f, Height = 10f },
                MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.2f, 0.2f, 0.2f) },
                Position = new Vector3(3.5f, 5f, 2.5f),
            });
            root.AddChild(new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(2.4f, 1.4f, 0.08f) },
                MaterialOverride = new StandardMaterial3D { AlbedoColor = color },
                Position = new Vector3(4.75f, 9.2f, 2.5f),
            });
        }

        private static uint Hash(int x, int y)
        {
            unchecked
            {
                uint h = (uint)(x * 73856093) ^ (uint)(y * 19349663);
                h ^= h >> 13;
                h *= 0x5bd1e995;
                return h ^ (h >> 15);
            }
        }
    }
}
