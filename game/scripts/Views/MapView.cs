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

        private readonly List<(Node3D node, Vec2 pos)> _props = new List<(Node3D, Vec2)>();
        private readonly List<(MultiMesh multi, List<Vec2> positions)> _scatter = new List<(MultiMesh, List<Vec2>)>();

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

            if (map.Id == "showcase")
            {
                ShowcaseProps(map);
            }
            else
            {
                ScatterProps(map);
            }

            ScatterGrassAndPebbles(map);
        }

        /// <summary>Hides trees, bushes, grass and pebbles inside a rectangle (a structure was placed there).</summary>
        public void ClearArea(Vec2 min, Vec2 max)
        {
            bool Inside(Vec2 p) => p.X >= min.X && p.X <= max.X && p.Y >= min.Y && p.Y <= max.Y;
            foreach (var (node, pos) in _props)
            {
                if (Inside(pos))
                {
                    node.Visible = false;
                }
            }

            foreach (var (multi, positions) in _scatter)
            {
                for (int i = 0; i < positions.Count; i++)
                {
                    if (Inside(positions[i]))
                    {
                        multi.SetInstanceTransform(i, new Transform3D(Basis.Identity.Scaled(Vector3.One * 0.0001f), new Vector3(positions[i].X, -1f, positions[i].Y)));
                    }
                }
            }
        }

        /// <summary>Gallery mode: one of each vegetation prop in a row next to the obstacle samples.</summary>
        private void ShowcaseProps(MapDefinition map)
        {
            var props = new Node3D { Name = "Props" };
            AddChild(props);
            var kinds = new[] { PlantKind.Olive, PlantKind.Cypress, PlantKind.Palm, PlantKind.Bush, PlantKind.DryShrub };
            for (int i = 0; i < 10; i++)
            {
                var prop = NatureModels.Build(kinds[i % kinds.Length], i);
                prop.Position = new Vector3(134f + (i % 5) * 9f, 0f, 6f + (i / 5) * 10f);
                props.AddChild(prop);
            }
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
                MaterialOverride = GroundMaterial(GroundTexture, grid),
                Position = new Vector3(grid.WorldWidth * 0.5f, 0f, grid.WorldHeight * 0.5f),
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            });
        }

        private const string GroundShader = @"
shader_type spatial;
render_mode diffuse_lambert, specular_disabled;
uniform sampler2D ground : source_color, filter_linear_mipmap;
uniform sampler2D detail : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform vec2 detail_repeats = vec2(75.0, 75.0);
uniform float detail_strength = 0.4;
void fragment() {
    vec3 base = texture(ground, UV).rgb;
    vec3 d1 = texture(detail, UV * detail_repeats).rgb;
    vec3 d2 = texture(detail, UV * detail_repeats * 0.23 + vec2(0.37, 0.71)).rgb;
    vec3 d = mix(vec3(1.0), d1 * d2 * 1.12, detail_strength);
    ALBEDO = base * d;
    ROUGHNESS = 1.0;
}";

        /// <summary>Ground map plus a tiling pebble / crack detail texture (about 4 m per repeat) for close-up texture.</summary>
        private static ShaderMaterial GroundMaterial(Texture2D ground, MapGrid grid)
        {
            var material = new ShaderMaterial { Shader = new Shader { Code = GroundShader } };
            material.SetShaderParameter("ground", ground);
            material.SetShaderParameter("detail", Textures.Get(Tex.GroundDetail));
            material.SetShaderParameter("detail_repeats", new Vector2(grid.WorldWidth / 4f, grid.WorldHeight / 4f));
            return material;
        }

        /// <summary>
        /// Grass tufts (thick on farmland and in grassy patches, none on tracks and rock) and pebbles (mostly on rock),
        /// each as one MultiMesh so thousands of instances cost a single draw call.
        /// </summary>
        private void ScatterGrassAndPebbles(MapDefinition map)
        {
            var grid = map.Grid;
            var tracks = BuildTrackSegments(map);
            var grass = new List<(Transform3D, Color, Vec2)>();
            var pebbles = new List<(Transform3D, Color, Vec2)>();
            int tries = (int)(grid.WorldWidth * grid.WorldHeight / 7f);
            for (int i = 0; i < tries; i++)
            {
                var pos = new Vec2(MeshKit.Hash01(i, 811) * grid.WorldWidth, MeshKit.Hash01(i, 812) * grid.WorldHeight);
                var cell = grid.WorldToCell(pos);
                if (!grid.IsWalkable(cell) || grid.GetCoverSource(cell.X, cell.Y) != CoverType.None)
                {
                    continue;
                }

                var ground = grid.GetGround(cell);
                float px = pos.X / grid.CellSize * PixelsPerCell, py = pos.Y / grid.CellSize * PixelsPerCell;
                float patches = ValueNoise(px / 46f, py / 46f, 7);
                float onTrack = DistanceToTracks(pos, tracks);
                float roll = MeshKit.Hash01(i, 813);
                float yaw = MeshKit.Hash01(i, 814) * Mathf.Tau;
                if (ground == GroundType.Rock || (onTrack < 2.2f && roll < 0.08f))
                {
                    if (roll < 0.18f)
                    {
                        float size = 0.12f + MeshKit.Hash01(i, 815) * 0.28f;
                        var basis = new Basis(Vector3.Up, yaw).Scaled(new Vector3(size, size * 0.6f, size));
                        pebbles.Add((new Transform3D(basis, new Vector3(pos.X, size * 0.2f, pos.Y)), MeshKit.Vary(new Color(0.4f, 0.37f, 0.32f), 0.08f, i), pos));
                    }

                    continue;
                }

                if (onTrack < 2.4f)
                {
                    continue;
                }

                float density = ground == GroundType.MudFarmland ? 0.85f : Mathf.SmoothStep(0.45f, 0.8f, patches) * 0.75f + 0.08f;
                if (roll > density)
                {
                    continue;
                }

                float scale = 1.3f + MeshKit.Hash01(i, 816) * 1.1f;
                var tint = ground == GroundType.MudFarmland
                    ? new Color(0.42f, 0.5f, 0.24f).Lerp(new Color(0.6f, 0.56f, 0.3f), MeshKit.Hash01(i, 817) * 0.6f)
                    : new Color(0.62f, 0.55f, 0.32f).Lerp(new Color(0.46f, 0.5f, 0.26f), MeshKit.Hash01(i, 817) * 0.6f);
                grass.Add((new Transform3D(new Basis(Vector3.Up, yaw).Scaled(Vector3.One * scale), new Vector3(pos.X, 0f, pos.Y)), tint, pos));
            }

            var grassMaterial = new StandardMaterial3D { VertexColorUseAsAlbedo = true, CullMode = BaseMaterial3D.CullModeEnum.Disabled, Roughness = 1f };
            AddScatter("Grass", NatureModels.GrassTuft(), grassMaterial, grass, false);
            var pebbleMaterial = new StandardMaterial3D { VertexColorUseAsAlbedo = true, Roughness = 1f };
            AddScatter("Pebbles", MeshKit.FacetedMesh(5, 0.4f, 5), pebbleMaterial, pebbles, true);
        }

        private void AddScatter(string name, Mesh mesh, Material material, List<(Transform3D t, Color c, Vec2 p)> items, bool shadows)
        {
            var multi = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseColors = true, Mesh = mesh, InstanceCount = items.Count };
            var positions = new List<Vec2>(items.Count);
            for (int i = 0; i < items.Count; i++)
            {
                multi.SetInstanceTransform(i, items[i].t);
                multi.SetInstanceColor(i, items[i].c);
                positions.Add(items[i].p);
            }

            AddChild(new MultiMeshInstance3D
            {
                Name = name,
                Multimesh = multi,
                MaterialOverride = material,
                CastShadow = shadows ? GeometryInstance3D.ShadowCastingSetting.On : GeometryInstance3D.ShadowCastingSetting.Off,
            });
            _scatter.Add((multi, positions));
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
            var tracks = BuildTrackSegments(map);
            int placed = 0;
            for (int i = 0; i < 3000 && placed < 260; i++)
            {
                int x = (int)(MeshKit.Hash01(i, 101) * grid.Width);
                int y = (int)(MeshKit.Hash01(i, 202) * grid.Height);
                var cell = new GridPos(x, y);
                var pos = grid.CellCenter(cell);
                if (!IsClear(grid, cell, 2) || NearPoint(map, pos, 16f) || DistanceToTracks(pos, tracks) < 3f)
                {
                    continue;
                }

                var prop = NatureModels.Build(PlantFor(grid.GetGround(cell), MeshKit.Hash01(i, 303)), i);
                var at = new Vec2(pos.X + (MeshKit.Hash01(i, 404) - 0.5f) * 1.5f, pos.Y + (MeshKit.Hash01(i, 505) - 0.5f) * 1.5f);
                prop.Position = new Vector3(at.X, 0f, at.Y);
                prop.Rotation = new Vector3(0f, MeshKit.Hash01(i, 606) * Mathf.Tau, 0f);
                props.AddChild(prop);
                _props.Add((prop, at));
                placed++;
            }
        }

        /// <summary>Olive groves on farmland, palms and shrubs around the town, dry scrub on rock.</summary>
        private static PlantKind PlantFor(GroundType ground, float roll)
        {
            switch (ground)
            {
                case GroundType.MudFarmland:
                    return roll < 0.65f ? PlantKind.Olive : roll < 0.78f ? PlantKind.Cypress : PlantKind.Bush;
                case GroundType.Rock:
                    return roll < 0.6f ? PlantKind.DryShrub : PlantKind.Bush;
                default:
                    return roll < 0.22f ? PlantKind.Olive : roll < 0.4f ? PlantKind.Palm : roll < 0.5f ? PlantKind.Cypress : roll < 0.75f ? PlantKind.Bush : PlantKind.DryShrub;
            }
        }

        private static bool IsClear(MapGrid grid, GridPos c, int r)
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
