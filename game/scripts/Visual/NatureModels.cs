using Godot;

namespace OCT7.Game.Visual
{
    public enum PlantKind
    {
        Olive,
        Cypress,
        Palm,
        Bush,
        DryShrub,
    }

    /// <summary>
    /// Mediterranean vegetation in the faceted low-poly style: gnarled olive trees with silvery canopies, tall dark
    /// cypresses, date palms with drooping fronds, bushes and dry shrubs, plus a grass-tuft mesh for MultiMesh scattering.
    /// </summary>
    public static class NatureModels
    {
        private static readonly Color Bark = new Color(0.36f, 0.29f, 0.22f);
        private static readonly Color OliveLeaf = new Color(0.36f, 0.42f, 0.27f);
        private static readonly Color CypressLeaf = new Color(0.2f, 0.29f, 0.16f);
        private static readonly Color PalmLeaf = new Color(0.34f, 0.44f, 0.2f);
        private static readonly Color PalmTrunk = new Color(0.5f, 0.41f, 0.29f);

        public static Node3D Build(PlantKind kind, int seed)
        {
            var root = new Node3D();
            float s = 0.85f + MeshKit.Hash01(seed, 9) * 0.4f;
            switch (kind)
            {
                case PlantKind.Olive:
                    {
                        // Twisted trunk in two leaning segments, wide flat canopy of several faceted clumps.
                        var lean = new Vector3((MeshKit.Hash01(seed, 1) - 0.5f) * 0.8f, 0f, (MeshKit.Hash01(seed, 2) - 0.5f) * 0.8f) * s;
                        var mid = new Vector3(lean.X * 0.6f, 1.1f * s, lean.Z * 0.6f);
                        var top = new Vector3(-lean.X * 0.3f, 2.0f * s, -lean.Z * 0.3f);
                        MeshKit.Beam(root, Vector3.Zero, mid, 0.2f * s, Bark, 6);
                        MeshKit.Beam(root, mid, top, 0.15f * s, Bark, 6);
                        MeshKit.Beam(root, mid, mid + new Vector3(0.8f, 0.7f, 0.3f) * s, 0.09f * s, Bark, 5);
                        var leaf = MeshKit.Vary(OliveLeaf, 0.07f, seed);
                        for (int i = 0; i < 5; i++)
                        {
                            float a = i / 5f * Mathf.Tau + MeshKit.Hash01(seed, i) * 0.8f;
                            float r = (i == 0 ? 0f : 0.95f) * s;
                            var p = top + new Vector3(Mathf.Cos(a) * r, (i == 0 ? 0.35f : 0.05f) * s, Mathf.Sin(a) * r);
                            MeshKit.Faceted(root, (i == 0 ? 1.15f : 0.85f) * s, p, i % 2 == 0 ? leaf : leaf.Darkened(0.1f), seed + i, new Vector3(1f, 0.62f, 1f), Tex.None, 0.3f, 6);
                        }

                        break;
                    }

                case PlantKind.Cypress:
                    MeshKit.Beam(root, Vector3.Zero, new Vector3(0f, 0.8f * s, 0f), 0.12f * s, Bark, 5);
                    MeshKit.Faceted(root, 0.95f * s, new Vector3(0f, 3.4f * s, 0f), MeshKit.Vary(CypressLeaf, 0.05f, seed), seed, new Vector3(0.85f, 3.0f, 0.85f), Tex.None, 0.18f, 7);
                    break;
                case PlantKind.Palm:
                    {
                        // Gently curved trunk of stacked segments, crown of drooping fronds.
                        var bend = new Vector3(MeshKit.Hash01(seed, 3) - 0.5f, 0f, MeshKit.Hash01(seed, 4) - 0.5f).Normalized() * 0.18f * s;
                        var p = Vector3.Zero;
                        for (int i = 0; i < 6; i++)
                        {
                            var next = p + new Vector3(0f, 0.95f * s, 0f) + bend * i * 0.35f;
                            MeshKit.Beam(root, p, next, (0.2f - i * 0.012f) * s, i % 2 == 0 ? PalmTrunk : PalmTrunk.Darkened(0.1f), 6);
                            p = next;
                        }

                        for (int i = 0; i < 9; i++)
                        {
                            float yaw = i / 9f * 360f + MeshKit.Hash01(seed, i) * 20f;
                            var frond = MeshKit.Group(root, p, yaw);
                            bool dry = i % 4 == 3;
                            float droop = dry ? 75f : 25f + MeshKit.Hash01(seed, i + 20) * 25f;
                            MeshKit.Box(frond, new Vector3(0.5f * s, 0.04f, 2.4f * s), new Vector3(0f, -0.2f * s, 1.05f * s), dry ? new Color(0.5f, 0.42f, 0.26f) : MeshKit.Vary(PalmLeaf, 0.05f, seed + i), new Vector3(droop, 0f, 0f));
                        }

                        MeshKit.Sphere(root, 0.28f * s, p + new Vector3(0f, -0.35f * s, 0f), new Color(0.7f, 0.45f, 0.18f), false, 6); // dates
                        break;
                    }

                case PlantKind.DryShrub:
                    {
                        var c = MeshKit.Vary(new Color(0.5f, 0.46f, 0.3f), 0.08f, seed);
                        for (int i = 0; i < 3; i++)
                        {
                            var p = new Vector3((MeshKit.Hash01(seed, i) - 0.5f) * 0.9f, 0.2f, (MeshKit.Hash01(seed, i + 5) - 0.5f) * 0.9f) * s;
                            MeshKit.Faceted(root, 0.45f * s, p, i == 0 ? c : c.Darkened(0.1f), seed + i, new Vector3(1.1f, 0.6f, 1.1f), Tex.None, 0.45f, 5);
                        }

                        break;
                    }

                default:
                    {
                        var c = MeshKit.Vary(new Color(0.34f, 0.4f, 0.23f), 0.08f, seed);
                        MeshKit.Faceted(root, 0.75f * s, new Vector3(0f, 0.35f * s, 0f), c, seed, new Vector3(1.3f, 0.75f, 1.1f), Tex.None, 0.3f, 6);
                        MeshKit.Faceted(root, 0.5f * s, new Vector3(0.55f, 0.25f, 0.3f) * s, c.Darkened(0.08f), seed + 1, new Vector3(1f, 0.8f, 1f), Tex.None, 0.3f, 5);
                        break;
                    }
            }

            return root;
        }

        /// <summary>Grass tuft for MultiMesh: five thin crossed blades with vertex colors (dark base, light tips).</summary>
        public static Mesh GrassTuft()
        {
            var st = new SurfaceTool();
            st.Begin(Mesh.PrimitiveType.Triangles);
            for (int i = 0; i < 5; i++)
            {
                float a = i / 5f * Mathf.Pi + 0.3f * i;
                float lean = 0.12f + (i % 3) * 0.06f;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                var side = new Vector3(-dir.Z, 0f, dir.X) * 0.045f;
                float h = 0.38f + (i % 2) * 0.16f;
                var tip = dir * lean + new Vector3(0f, h, 0f);
                var baseColor = new Color(0.55f, 0.55f, 0.5f);
                st.SetColor(baseColor);
                st.AddVertex(-side);
                st.SetColor(Colors.White);
                st.AddVertex(tip);
                st.SetColor(baseColor);
                st.AddVertex(side);
            }

            st.GenerateNormals();
            return st.Commit();
        }
    }
}
