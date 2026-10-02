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

        /// <summary>Lit material with a procedural detail texture in world-space triplanar mapping (no UVs needed).</summary>
        public static StandardMaterial3D TexMat(Color c, Tex tex, float roughness = 0.9f)
        {
            if (tex == Tex.None)
            {
                return Mat(c, roughness);
            }

            string key = $"t{c.ToHtml()}{tex}{roughness:0.00}";
            if (!Materials.TryGetValue(key, out var m))
            {
                float scale = 1f / Textures.Scale(tex);
                m = new StandardMaterial3D
                {
                    AlbedoColor = c,
                    AlbedoTexture = Textures.Get(tex),
                    Roughness = roughness,
                    Uv1Triplanar = true,
                    Uv1WorldTriplanar = true,
                    Uv1Scale = new Vector3(scale, scale, scale),
                    TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic,
                };
                Materials[key] = m;
            }

            return m;
        }

        /// <summary>Like <see cref="TexMat"/> but in object space, so the texture moves with the model (units, vehicles).</summary>
        public static StandardMaterial3D TexMatLocal(Color c, Tex tex, float metersPerRepeat, float roughness = 0.85f)
        {
            string key = $"tl{c.ToHtml()}{tex}{metersPerRepeat:0.00}{roughness:0.00}";
            if (!Materials.TryGetValue(key, out var m))
            {
                float scale = 1f / metersPerRepeat;
                m = new StandardMaterial3D
                {
                    AlbedoColor = c,
                    AlbedoTexture = Textures.Get(tex),
                    Roughness = roughness,
                    Uv1Triplanar = true,
                    Uv1WorldTriplanar = false,
                    Uv1Scale = new Vector3(scale, scale, scale),
                    TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmaps,
                };
                Materials[key] = m;
            }

            return m;
        }

        public static MeshInstance3D BoxT(Node3D parent, Vector3 size, Vector3 pos, Color c, Tex tex, Vector3? rotDeg = null) =>
            Add(parent, BoxMesh(size), pos, TexMat(c, tex), rotDeg);

        public static MeshInstance3D PrismT(Node3D parent, Vector3 size, Vector3 pos, Color c, Tex tex, Vector3? rotDeg = null) =>
            Add(parent, PrismMesh(size, 0.5f), pos, TexMat(c, tex), rotDeg);

        public static MeshInstance3D CylT(Node3D parent, float rTop, float rBottom, float h, Vector3 pos, Color c, Tex tex, Vector3? rotDeg = null, int seg = 12) =>
            Add(parent, CylMesh(rTop, rBottom, h, seg), pos, TexMat(c, tex), rotDeg);

        /// <summary>A child node at <paramref name="pos"/> rotated by <paramref name="yawDeg"/>, for placing multi-part props.</summary>
        public static Node3D Group(Node3D parent, Vector3 pos, float yawDeg = 0f)
        {
            var g = new Node3D { Position = pos, RotationDegrees = new Vector3(0f, yawDeg, 0f) };
            parent.AddChild(g);
            return g;
        }

        /// <summary>A cylinder spanning two points (braces, guy wires, ladder rails, pipes).</summary>
        public static MeshInstance3D Beam(Node3D parent, Vector3 from, Vector3 to, float radius, Color c, int seg = 5)
        {
            var dir = to - from;
            float len = dir.Length();
            var up = len > 1e-5f ? dir / len : Vector3.Up;
            var axis = Vector3.Up.Cross(up);
            var basis = axis.LengthSquared() < 1e-8f
                ? (up.Y < 0f ? new Basis(Vector3.Right, Mathf.Pi) : Basis.Identity)
                : new Basis(axis.Normalized(), Mathf.Acos(Mathf.Clamp(Vector3.Up.Dot(up), -1f, 1f)));
            var mi = new MeshInstance3D { Mesh = CylMesh(radius, radius, len, seg), MaterialOverride = Mat(c) };
            mi.Transform = new Transform3D(basis, (from + to) * 0.5f);
            parent.AddChild(mi);
            return mi;
        }

        public static MeshInstance3D Tire(Node3D parent, Vector3 pos, float radius, Vector3? rotDeg = null) =>
            Add(parent, Cached($"tire{radius:0.00}", () => new TorusMesh { InnerRadius = radius * 0.5f, OuterRadius = radius, Rings = 12, RingSegments = 6 }), pos, Mat(new Color(0.09f, 0.09f, 0.09f), 0.95f), rotDeg);

        /// <summary>
        /// Low-poly faceted blob (rocks, earth mounds, foliage): a coarse sphere with per-vertex radial noise and flat
        /// normals. Seams are displaced consistently because the noise is keyed on the vertex direction.
        /// </summary>
        public static Mesh FacetedMesh(int seed, float roughness = 0.22f, int segments = 7) =>
            Cached($"fac{seed}{roughness:0.00}{segments}", () =>
            {
                var sphere = new SphereMesh { Radius = 1f, Height = 2f, RadialSegments = segments, Rings = System.Math.Max(3, segments / 2 + 1) };
                var arrays = sphere.GetMeshArrays();
                var verts = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
                var index = arrays[(int)Mesh.ArrayType.Index].AsInt32Array();
                var st = new SurfaceTool();
                st.Begin(Mesh.PrimitiveType.Triangles);
                st.SetSmoothGroup(uint.MaxValue);
                foreach (int i in index)
                {
                    var v = verts[i];
                    int hx = Mathf.RoundToInt(v.X * 100f), hy = Mathf.RoundToInt(v.Y * 100f), hz = Mathf.RoundToInt(v.Z * 100f);
                    float n = Hash01(hx * 31 + hz * 7 + seed * 977, hy * 13 + seed);
                    st.AddVertex(v * (1f - roughness * 0.5f + n * roughness));
                }

                st.GenerateNormals();
                return st.Commit();
            });

        public static MeshInstance3D Faceted(Node3D parent, float radius, Vector3 pos, Color c, int seed, Vector3? scale = null, Tex tex = Tex.None, float roughness = 0.22f, int segments = 7)
        {
            var mi = Add(parent, FacetedMesh(seed % 16, roughness, segments), pos, TexMat(c, tex), new Vector3(0f, Hash01(seed, 77) * 360f, 0f));
            mi.Scale = (scale ?? Vector3.One) * radius;
            return mi;
        }

        public static Mesh BoxMesh(Vector3 size) => Cached($"box{size}", () => new BoxMesh { Size = size });

        public static Mesh CylMesh(float top, float bottom, float h, int seg) =>
            Cached($"cyl{top:0.000}{bottom:0.000}{h:0.000}{seg}", () => new CylinderMesh { TopRadius = top, BottomRadius = bottom, Height = h, RadialSegments = seg, Rings = 1 });

        /// <summary>Flat annulus in the XZ plane (selection rings, capture radius), facing up.</summary>
        public static Mesh RingMesh(float radius, float width, int seg) =>
            Cached($"ring{radius:0.000}{width:0.000}{seg}", () =>
            {
                var verts = new Vector3[(seg + 1) * 2];
                var normals = new Vector3[verts.Length];
                var indices = new int[seg * 6];
                float inner = Mathf.Max(0f, radius - width);
                for (int i = 0; i <= seg; i++)
                {
                    float a = Mathf.Tau * i / seg;
                    var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    verts[i * 2] = dir * inner;
                    verts[i * 2 + 1] = dir * radius;
                    normals[i * 2] = Vector3.Up;
                    normals[i * 2 + 1] = Vector3.Up;
                }

                for (int i = 0; i < seg; i++)
                {
                    int k = i * 6, v = i * 2;
                    indices[k] = v;
                    indices[k + 1] = v + 1;
                    indices[k + 2] = v + 2;
                    indices[k + 3] = v + 1;
                    indices[k + 4] = v + 3;
                    indices[k + 5] = v + 2;
                }

                var arrays = new Godot.Collections.Array();
                arrays.Resize((int)Mesh.ArrayType.Max);
                arrays[(int)Mesh.ArrayType.Vertex] = verts;
                arrays[(int)Mesh.ArrayType.Normal] = normals;
                arrays[(int)Mesh.ArrayType.Index] = indices;
                var mesh = new ArrayMesh();
                mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
                return mesh;
            });

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
