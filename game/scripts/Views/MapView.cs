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
        private static readonly Color Sand = new Color(0.62f, 0.55f, 0.41f);
        private static readonly Color DryGrass = new Color(0.50f, 0.49f, 0.32f);
        private static readonly Color DarkEarth = new Color(0.47f, 0.40f, 0.30f);
        private static readonly Color Rock = new Color(0.50f, 0.48f, 0.44f);
        private static readonly Color Farmland = new Color(0.40f, 0.45f, 0.26f);
        private static readonly Color Track = new Color(0.55f, 0.46f, 0.34f);

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
                MaterialOverride = MeshKit.Mat(new Color(0.40f, 0.36f, 0.28f), 1f),
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
            var tracks = BuildTrackSegments(map);
            float metersPerPixel = grid.CellSize / PixelsPerCell;
            for (int py = 0; py < h; py++)
            {
                for (int px = 0; px < w; px++)
                {
                    int cx = px / PixelsPerCell, cy = py / PixelsPerCell;
                    var ground = grid.GetGround(new GridPos(cx, cy));
                    var c = ground == GroundType.Rock ? Rock : ground == GroundType.MudFarmland ? Farmland : Sand;

                    // Large soft patches of dry grass and darker earth break up the flat sand.
                    float patches = ValueNoise(px / 46f, py / 46f, 7);
                    float earth = ValueNoise(px / 23f, py / 23f, 31);
                    if (ground == GroundType.SandUrban)
                    {
                        c = c.Lerp(DryGrass, Mathf.SmoothStep(0.5f, 0.85f, patches) * 0.85f);
                        c = c.Lerp(DarkEarth, Mathf.SmoothStep(0.55f, 0.9f, earth) * 0.6f);
                    }

                    // Dirt tracks: distance to the nearest track segment, with a noisy soft edge.
                    var world = new Vec2((px + 0.5f) * metersPerPixel, (py + 0.5f) * metersPerPixel);
                    float d = DistanceToTracks(world, tracks) + (ValueNoise(px / 5f, py / 5f, 17) - 0.5f) * 1.2f;
                    float track = 1f - Mathf.SmoothStep(1.4f, 2.6f, d);
                    if (track > 0f)
                    {
                        c = c.Lerp(Track, 0.75f * track);
                    }

                    float n = (MeshKit.Hash01(px, py) - 0.5f) * 0.045f + (ValueNoise(px / 7f, py / 7f, 3) - 0.5f) * 0.07f;
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

        /// <summary>Smooth value noise in [0, 1]: bilinear interpolation of lattice hashes with smoothstep weights.</summary>
        private static float ValueNoise(float x, float y, int seed)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            float a = MeshKit.Hash01(x0 + seed * 1013, y0);
            float b = MeshKit.Hash01(x0 + 1 + seed * 1013, y0);
            float c = MeshKit.Hash01(x0 + seed * 1013, y0 + 1);
            float d = MeshKit.Hash01(x0 + 1 + seed * 1013, y0 + 1);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        /// <summary>Dirt tracks between each capture point and its two nearest neighbours (world-space segments).</summary>
        private static List<(Vec2 a, Vec2 b)> BuildTrackSegments(MapDefinition map)
        {
            var segments = new List<(Vec2, Vec2)>();
            var points = new List<Vec2>();
            foreach (var s in map.Sectors)
            {
                points.Add(new Vec2(s.X, s.Y));
            }

            for (int i = 0; i < points.Count; i++)
            {
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

                if (best1 >= 0) segments.Add((points[i], points[best1]));
                if (best2 >= 0) segments.Add((points[i], points[best2]));
            }

            return segments;
        }

        private static float DistanceToTracks(Vec2 p, List<(Vec2 a, Vec2 b)> segments)
        {
            float best = float.MaxValue;
            foreach (var (a, b) in segments)
            {
                var ab = b - a;
                float len2 = ab.X * ab.X + ab.Y * ab.Y;
                float t = len2 > 0f ? Mathf.Clamp(((p.X - a.X) * ab.X + (p.Y - a.Y) * ab.Y) / len2, 0f, 1f) : 0f;
                float dx = a.X + ab.X * t - p.X, dy = a.Y + ab.Y * t - p.Y;
                float d2 = dx * dx + dy * dy;
                if (d2 < best)
                {
                    best = d2;
                }
            }

            return Mathf.Sqrt(best);
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
