using System.Collections.Generic;
using Godot;
using OCT7.Game.Visual;
using OCT7.Sim;
using OCT7.Sim.Data;
using OCT7.Sim.World;

namespace OCT7.Game.Views
{
    /// <summary>
    /// Map visuals: textured ground (ground types, dirt tracks, noise), procedural buildings / walls / fences /
    /// rocks, and scattered olive trees and bushes (visual only).
    /// </summary>
    public partial class MapView : Node3D
    {
        private const int PixelsPerCell = 4;
        private static readonly Color Sand = new Color(0.76f, 0.69f, 0.53f);
        private static readonly Color Rock = new Color(0.58f, 0.55f, 0.50f);
        private static readonly Color Farmland = new Color(0.50f, 0.55f, 0.34f);
        private static readonly Color Track = new Color(0.62f, 0.54f, 0.41f);

        public ImageTexture GroundTexture { get; private set; }

        public void Build(MapDefinition map)
        {
            var grid = map.Grid;
            BuildOuterGround(grid);
            BuildGround(map);
            for (int i = 0; i < map.Obstacles.Count; i++)
            {
                var o = map.Obstacles[i];
                var node = ModelFactory.BuildObstacle(o, grid.CellSize, i);
                float cs = grid.CellSize;
                node.Position = new Vector3((o.MinX + (o.MaxX - o.MinX + 1) * 0.5f) * cs, 0f, (o.MinY + (o.MaxY - o.MinY + 1) * 0.5f) * cs);
                AddChild(node);
            }

            ScatterProps(map);
        }

        private void BuildOuterGround(MapGrid grid)
        {
            AddChild(new MeshInstance3D
            {
                Name = "OuterGround",
                Mesh = new PlaneMesh { Size = new Vector2(grid.WorldWidth * 5f, grid.WorldHeight * 5f) },
                MaterialOverride = MeshKit.Mat(new Color(0.55f, 0.5f, 0.4f), 1f),
                Position = new Vector3(grid.WorldWidth * 0.5f, -0.06f, grid.WorldHeight * 0.5f),
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            });
        }

        private void BuildGround(MapDefinition map)
        {
            var grid = map.Grid;
            int w = grid.Width * PixelsPerCell;
            int h = grid.Height * PixelsPerCell;
            var bytes = new byte[w * h * 3];
            var trackMask = BuildTrackMask(map);
            for (int py = 0; py < h; py++)
            {
                for (int px = 0; px < w; px++)
                {
                    int cx = px / PixelsPerCell, cy = py / PixelsPerCell;
                    var ground = grid.GetGround(new GridPos(cx, cy));
                    var c = ground == GroundType.Rock ? Rock : ground == GroundType.MudFarmland ? Farmland : Sand;
                    if (trackMask[cy * grid.Width + cx])
                    {
                        c = c.Lerp(Track, 0.7f);
                    }

                    float n = (MeshKit.Hash01(px / 2, py / 2) - 0.5f) * 0.07f + (MeshKit.Hash01(px / 9, py / 9 + 999) - 0.5f) * 0.06f;
                    int i = (py * w + px) * 3;
                    bytes[i] = (byte)(Mathf.Clamp(c.R + n, 0f, 1f) * 255f);
                    bytes[i + 1] = (byte)(Mathf.Clamp(c.G + n, 0f, 1f) * 255f);
                    bytes[i + 2] = (byte)(Mathf.Clamp(c.B + n * 0.8f, 0f, 1f) * 255f);
                }
            }

            var image = Image.CreateFromData(w, h, false, Image.Format.Rgb8, bytes);
            image.GenerateMipmaps();
            GroundTexture = ImageTexture.CreateFromImage(image);
            AddChild(new MeshInstance3D
            {
                Name = "Ground",
                Mesh = new PlaneMesh { Size = new Vector2(grid.WorldWidth, grid.WorldHeight) },
                MaterialOverride = new StandardMaterial3D { AlbedoTexture = GroundTexture, Roughness = 1f },
                Position = new Vector3(grid.WorldWidth * 0.5f, 0f, grid.WorldHeight * 0.5f),
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            });
        }

        /// <summary>Dirt tracks from each HQ to the victory points and between neighbouring capture points.</summary>
        private static bool[] BuildTrackMask(MapDefinition map)
        {
            var grid = map.Grid;
            var mask = new bool[grid.Width * grid.Height];
            var points = new List<Vec2>();
            foreach (var s in map.Sectors)
            {
                points.Add(new Vec2(s.X, s.Y));
            }

            for (int i = 0; i < points.Count; i++)
            {
                // connect each point to its two nearest neighbours
                int best1 = -1, best2 = -1;
                float d1 = float.MaxValue, d2 = float.MaxValue;
                for (int j = 0; j < points.Count; j++)
                {
                    if (i == j)
                    {
                        continue;
                    }

                    float d = Vec2.Distance(points[i], points[j]);
                    if (d < d1)
                    {
                        d2 = d1;
                        best2 = best1;
                        d1 = d;
                        best1 = j;
                    }
                    else if (d < d2)
                    {
                        d2 = d;
                        best2 = j;
                    }
                }

                if (best1 >= 0) Paint(mask, grid, points[i], points[best1]);
                if (best2 >= 0) Paint(mask, grid, points[i], points[best2]);
            }

            return mask;
        }

        private static void Paint(bool[] mask, MapGrid grid, Vec2 a, Vec2 b)
        {
            float len = Vec2.Distance(a, b);
            int steps = (int)(len / (grid.CellSize * 0.5f));
            for (int s = 0; s <= steps; s++)
            {
                var p = Vec2.Lerp(a, b, s / (float)Mathf.Max(1, steps));
                var c = grid.WorldToCell(p);
                for (int dy = 0; dy <= 1; dy++)
                {
                    for (int dx = 0; dx <= 1; dx++)
                    {
                        var cell = new GridPos(c.X + dx, c.Y + dy);
                        if (grid.IsWalkable(cell))
                        {
                            mask[grid.ToIndex(cell)] = true;
                        }
                    }
                }
            }
        }

        private void ScatterProps(MapDefinition map)
        {
            var grid = map.Grid;
            var props = new Node3D { Name = "Props" };
            AddChild(props);
            int placed = 0;
            for (int i = 0; i < 1400 && placed < 170; i++)
            {
                int x = (int)(MeshKit.Hash01(i, 101) * grid.Width);
                int y = (int)(MeshKit.Hash01(i, 202) * grid.Height);
                var cell = new GridPos(x, y);
                var pos = grid.CellCenter(cell);
                if (!ClearArea(grid, cell, 2) || NearPoint(map, pos, 16f))
                {
                    continue;
                }

                bool farmland = grid.GetGround(cell) == GroundType.MudFarmland;
                var prop = MeshKit.Hash01(i, 303) < (farmland ? 0.75f : 0.45f) ? ModelFactory.BuildTree(i) : ModelFactory.BuildBush(i);
                prop.Position = new Vector3(pos.X + (MeshKit.Hash01(i, 404) - 0.5f) * 1.5f, 0f, pos.Y + (MeshKit.Hash01(i, 505) - 0.5f) * 1.5f);
                prop.Rotation = new Vector3(0f, MeshKit.Hash01(i, 606) * Mathf.Tau, 0f);
                props.AddChild(prop);
                placed++;
            }
        }

        private static bool ClearArea(MapGrid grid, GridPos c, int r)
        {
            for (int dy = -r; dy <= r; dy++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    if (!grid.IsWalkable(c.X + dx, c.Y + dy) || grid.GetCoverSource(c.X + dx, c.Y + dy) != CoverType.None)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static bool NearPoint(MapDefinition map, Vec2 pos, float radius)
        {
            foreach (var s in map.Sectors)
            {
                if (Vec2.Distance(pos, new Vec2(s.X, s.Y)) < (s.Type == SectorType.Hq ? radius * 2.2f : radius))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
