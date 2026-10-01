using System.Collections.Generic;
using Godot;

namespace OCT7.Game.Visual
{
    /// <summary>
    /// Tiny helpers for building low-poly models from primitives. Meshes and materials are cached by
    /// parameters, so thousands of soldier parts share a handful of resources.
    /// </summary>
    public static class MeshKit
    {
        private static readonly Dictionary<string, Mesh> Meshes = new Dictionary<string, Mesh>();
        private static readonly Dictionary<string, StandardMaterial3D> Materials = new Dictionary<string, StandardMaterial3D>();

        public static StandardMaterial3D Mat(Color c, float roughness = 0.85f, float metallic = 0f)
        {
            string key = $"m{c.ToHtml()}{roughness:0.00}{metallic:0.00}";
            if (!Materials.TryGetValue(key, out var m))
            {
                m = new StandardMaterial3D { AlbedoColor = c, Roughness = roughness, Metallic = metallic };
                Materials[key] = m;
            }

            return m;
        }

        public static StandardMaterial3D Emissive(Color c, float energy = 2f)
        {
            string key = $"e{c.ToHtml()}{energy:0.0}";
            if (!Materials.TryGetValue(key, out var m))
            {
                m = new StandardMaterial3D
                {
                    AlbedoColor = c,
                    EmissionEnabled = true,
                    Emission = c,
                    EmissionEnergyMultiplier = energy,
                    ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                };
                Materials[key] = m;
            }

            return m;
        }

        /// <summary>Unshaded, optionally transparent material (overlays, rings). Not cached when <paramref name="unique"/>.</summary>
        public static StandardMaterial3D Flat(Color c, bool unique = false)
        {
            string key = $"f{c.ToHtml()}";
            if (!unique && Materials.TryGetValue(key, out var cached))
            {
                return cached;
            }

            var m = new StandardMaterial3D
            {
                AlbedoColor = c,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                Transparency = c.A < 0.999f || unique ? BaseMaterial3D.TransparencyEnum.Alpha : BaseMaterial3D.TransparencyEnum.Disabled,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            };
            if (!unique)
            {
                Materials[key] = m;
            }

            return m;
        }

        public static Mesh BoxMesh(Vector3 size) => Cached($"box{size}", () => new BoxMesh { Size = size });

        public static Mesh CylMesh(float top, float bottom, float h, int seg) =>
            Cached($"cyl{top:0.000}{bottom:0.000}{h:0.000}{seg}", () => new CylinderMesh { TopRadius = top, BottomRadius = bottom, Height = h, RadialSegments = seg, Rings = 1 });

        public static Mesh SphereMesh(float r, int seg, bool hemi) =>
            Cached($"sph{r:0.000}{seg}{hemi}", () => new SphereMesh { Radius = r, Height = hemi ? r : r * 2f, RadialSegments = seg, Rings = System.Math.Max(2, seg / 2), IsHemisphere = hemi });

        public static Mesh PrismMesh(Vector3 size, float leftToRight) =>
            Cached($"pri{size}{leftToRight:0.00}", () => new PrismMesh { Size = size, LeftToRight = leftToRight });

        public static MeshInstance3D Box(Node3D parent, Vector3 size, Vector3 pos, Color c, Vector3? rotDeg = null, float roughness = 0.85f) =>
            Add(parent, BoxMesh(size), pos, Mat(c, roughness), rotDeg);

        public static MeshInstance3D Cyl(Node3D parent, float rTop, float rBottom, float h, Vector3 pos, Color c, Vector3? rotDeg = null, int seg = 8) =>
            Add(parent, CylMesh(rTop, rBottom, h, seg), pos, Mat(c), rotDeg);

        public static MeshInstance3D Sphere(Node3D parent, float r, Vector3 pos, Color c, bool hemi = false, int seg = 8, Vector3? scale = null)
        {
            var mi = Add(parent, SphereMesh(r, seg, hemi), pos, Mat(c), null);
            if (scale.HasValue)
            {
                mi.Scale = scale.Value;
            }

            return mi;
        }

        public static MeshInstance3D Prism(Node3D parent, Vector3 size, Vector3 pos, Color c, Vector3? rotDeg = null, float leftToRight = 0.5f) =>
            Add(parent, PrismMesh(size, leftToRight), pos, Mat(c), rotDeg);

        public static MeshInstance3D Add(Node3D parent, Mesh mesh, Vector3 pos, Material material, Vector3? rotDeg)
        {
            var mi = new MeshInstance3D { Mesh = mesh, MaterialOverride = material, Position = pos };
            if (rotDeg.HasValue)
            {
                mi.RotationDegrees = rotDeg.Value;
            }

            parent.AddChild(mi);
            return mi;
        }

        /// <summary>Deterministic 0..1 hash for per-index visual variation.</summary>
        public static float Hash01(int a, int b = 0)
        {
            unchecked
            {
                uint h = (uint)(a * 73856093) ^ (uint)(b * 19349663) ^ 0x9E3779B9u;
                h ^= h >> 13;
                h *= 0x5bd1e995;
                h ^= h >> 15;
                return (h & 0xFFFFFF) / (float)0x1000000;
            }
        }

        public static Color Vary(Color c, float amount, int seed) =>
            new Color(
                Mathf.Clamp(c.R + (Hash01(seed, 1) - 0.5f) * amount, 0f, 1f),
                Mathf.Clamp(c.G + (Hash01(seed, 2) - 0.5f) * amount, 0f, 1f),
                Mathf.Clamp(c.B + (Hash01(seed, 3) - 0.5f) * amount, 0f, 1f));

        private static Mesh Cached(string key, System.Func<Mesh> create)
        {
            if (!Meshes.TryGetValue(key, out var m))
            {
                m = create();
                Meshes[key] = m;
            }

            return m;
        }
    }
}
