using Godot;
using OCT7.Sim.Data;

namespace OCT7.Game.Visual
{
    /// <summary>
    /// Procedural structure models: one distinct silhouette per building, with faction palettes.
    /// IDF = tan/olive prefab, containers and concrete T-walls; Hamas = grey urban concrete, rebar, rooftop water
    /// tanks and rusty corrugated metal; Hezbollah = limestone, red tile roofs, earth berms and rock.
    /// Origin is the footprint center at ground level; the front faces +Z.
    /// </summary>
    internal static class StructureModels
    {
        private static readonly Color IdfTan = new Color(0.66f, 0.6f, 0.45f);
        private static readonly Color IdfOlive = new Color(0.38f, 0.4f, 0.27f);
        private static readonly Color ConcreteGray = new Color(0.64f, 0.63f, 0.6f);
        private static readonly Color GazaConcrete = new Color(0.7f, 0.68f, 0.63f);
        private static readonly Color Plaster = new Color(0.82f, 0.76f, 0.62f);
        private static readonly Color Limestone = new Color(0.84f, 0.78f, 0.64f);
        private static readonly Color RoofRed = new Color(0.68f, 0.32f, 0.22f);
        private static readonly Color Rust = new Color(0.58f, 0.4f, 0.28f);
        private static readonly Color EarthColor = new Color(0.52f, 0.44f, 0.32f);
        private static readonly Color RockColor = new Color(0.58f, 0.55f, 0.49f);
        private static readonly Color NetGreen = new Color(0.4f, 0.44f, 0.27f);
        private static readonly Color Dark = new Color(0.07f, 0.07f, 0.07f);
        private static readonly Color Steel = new Color(0.28f, 0.3f, 0.28f);
        private static readonly Color Wood = new Color(0.46f, 0.35f, 0.23f);
        private static readonly Color Burlap = new Color(0.66f, 0.6f, 0.45f);
        private static readonly Color Pole = new Color(0.3f, 0.3f, 0.29f);
        private static readonly Color Window = new Color(0.1f, 0.11f, 0.13f);

        public static void Build(Node3D root, StructureDef def, float w, float d, Color team)
        {
            switch (def.Id)
            {
                case "idf_hq": IdfHq(root, w, d, team); return;
                case "idf_t1": IdfOutpost(root, w, d, team); return;
                case "idf_t2": IdfSupportBase(root, w, d, team); return;
                case "idf_t3": IdfArmorYard(root, w, d, team); return;
                case "hamas_hq": HamasBunkerHouse(root, w, d, team); return;
                case "hamas_t1": HamasWorkshop(root, w, d, team); return;
                case "hamas_t2": HamasArmsLab(root, w, d, team); return;
                case "hamas_t3": HamasCompound(root, w, d, team); return;
                case "hzb_hq": HzbFarmhouse(root, w, d, team); return;
                case "hzb_t1": HzbTrainingCamp(root, w, d, team); return;
                case "hzb_t2": HzbFortification(root, w, d, team); return;
                case "hzb_t3": HzbTunnelPortal(root, w, d, team); return;
            }

            // Unknown structure: a plain block by faction palette.
            var color = def.FactionId == "idf" ? IdfTan : def.FactionId == "hamas" ? GazaConcrete : Limestone;
            MeshKit.BoxT(root, new Vector3(w * 0.7f, def.Height, d * 0.7f), new Vector3(0f, def.Height * 0.5f, 0f), color, Tex.Concrete);
            Flag(root, new Vector3(w * 0.4f, 0f, d * 0.4f), team, 6f);
        }

        // ================================================================ shared props

        public static void Flag(Node3D root, Vector3 pos, Color team, float height = 7f)
        {
            MeshKit.Cyl(root, 0.05f, 0.08f, height, pos + new Vector3(0f, height * 0.5f, 0f), Pole, null, 6);
            MeshKit.Sphere(root, 0.1f, pos + new Vector3(0f, height + 0.05f, 0f), new Color(0.75f, 0.7f, 0.45f), false, 6);
            // Two panels at slightly different angles read as cloth in the wind.
            var flag = MeshKit.Group(root, pos + new Vector3(0f, height - 0.55f, 0f));
            MeshKit.Box(flag, new Vector3(0.04f, 0.95f, 0.85f), new Vector3(0f, 0f, 0.45f), team, new Vector3(0f, 8f, 0f));
            MeshKit.Box(flag, new Vector3(0.04f, 0.9f, 0.8f), new Vector3(0.12f, -0.03f, 1.22f), team.Darkened(0.12f), new Vector3(0f, -14f, 0f));
        }

        public static void SandbagRow(Node3D root, Vector3 from, Vector3 to, int layers = 2, float yBase = 0f)
        {
            var dir = to - from;
            dir.Y = 0f;
            float len = dir.Length();
            int n = Mathf.Max(1, (int)(len / 0.8f));
            float yaw = Mathf.Atan2(dir.X, dir.Z);
            for (int l = 0; l < layers; l++)
            {
                for (int i = 0; i < n; i++)
                {
                    float t = (i + 0.5f + (l % 2) * 0.5f) / n;
                    if (t > 1f)
                    {
                        continue;
                    }

                    var p = from + (to - from) * t + new Vector3(0f, yBase + 0.19f + l * 0.3f, 0f);
                    var bag = MeshKit.Sphere(root, 0.44f, p, MeshKit.Vary(Burlap, 0.07f, i * 7 + l * 31 + (int)(from.X * 10f)), false, 6, new Vector3(1.0f, 0.42f, 0.62f));
                    bag.MaterialOverride = MeshKit.TexMat(((StandardMaterial3D)bag.MaterialOverride).AlbedoColor, Tex.Canvas);
                    bag.Rotation = new Vector3(0f, yaw + Mathf.Pi * 0.5f + (MeshKit.Hash01(i, l) - 0.5f) * 0.25f, 0f);
                }
            }
        }

        /// <summary>Placed sandbag wall (shared engineer structure): a chunky three-layer row along the long axis.</summary>
        public static void Sandbags(Node3D root, float width, float depth)
        {
            bool alongX = width >= depth;
            float len = (alongX ? width : depth) * 0.5f - 0.25f;
            var from = alongX ? new Vector3(-len, 0f, 0f) : new Vector3(0f, 0f, -len);
            var to = alongX ? new Vector3(len, 0f, 0f) : new Vector3(0f, 0f, len);
            SandbagRow(root, from, to, 3);
            var side = alongX ? new Vector3(0f, 0f, 0.3f) : new Vector3(0.3f, 0f, 0f);
            SandbagRow(root, from + side, to + side, 1);
        }

        /// <summary>Worn ground under a building: a soft-edged ellipse slightly darker than the terrain.</summary>
        private static void Pad(Node3D root, float w, float d, Color color, Tex tex = Tex.Earth)
        {
            var tint = color.Lerp(new Color(0.55f, 0.49f, 0.38f), 0.55f);
            var outer = MeshKit.Add(root, MeshKit.CylMesh(0.5f, 0.5f, 0.04f, 24), new Vector3(0f, 0.02f, 0f), MeshKit.TexMat(tint.Lerp(new Color(0.6f, 0.54f, 0.42f), 0.5f), Tex.Earth), null);
            outer.Scale = new Vector3(w * 1.12f, 1f, d * 1.12f);
            var inner = MeshKit.Add(root, MeshKit.CylMesh(0.5f, 0.5f, 0.05f, 24), new Vector3(0f, 0.03f, 0f), MeshKit.TexMat(tint, tex), null);
            inner.Scale = new Vector3(w * 0.95f, 1f, d * 0.95f);
        }

        /// <summary>Shipping container (6 x 2.4 x 2.6 m) along local X, doors at +X, team stripe on both sides.</summary>
        private static void Container(Node3D root, Vector3 pos, float yaw, Color color, Color team, float length = 6f)
        {
            var g = MeshKit.Group(root, pos, yaw);
            MeshKit.BoxT(g, new Vector3(length, 2.6f, 2.4f), new Vector3(0f, 1.3f, 0f), color, Tex.Corrugated);
            MeshKit.Box(g, new Vector3(0.05f, 2.4f, 2.2f), new Vector3(length * 0.5f + 0.02f, 1.3f, 0f), color.Darkened(0.25f));
            MeshKit.Box(g, new Vector3(0.06f, 2.2f, 0.06f), new Vector3(length * 0.5f + 0.05f, 1.3f, -0.4f), Steel);
            MeshKit.Box(g, new Vector3(0.06f, 2.2f, 0.06f), new Vector3(length * 0.5f + 0.05f, 1.3f, 0.4f), Steel);
            foreach (float z in new[] { -1.22f, 1.22f })
            {
                MeshKit.Box(g, new Vector3(length * 0.85f, 0.2f, 0.02f), new Vector3(0f, 2.2f, z), team);
            }
        }

        /// <summary>Concrete T-wall blast barrier segment (IDF bases).</summary>
        private static void TWall(Node3D root, Vector3 pos, float yaw)
        {
            var g = MeshKit.Group(root, pos, yaw);
            var c = MeshKit.Vary(ConcreteGray, 0.04f, (int)(pos.X * 13f + pos.Z * 7f));
            MeshKit.BoxT(g, new Vector3(1.25f, 3.3f, 0.26f), new Vector3(0f, 1.75f, 0f), c, Tex.Concrete);
            MeshKit.BoxT(g, new Vector3(1.25f, 0.4f, 1.1f), new Vector3(0f, 0.2f, 0f), c.Darkened(0.06f), Tex.Concrete);
        }

        private static void TWallRow(Node3D root, Vector3 from, Vector3 to)
        {
            var dir = to - from;
            int n = Mathf.Max(1, Mathf.RoundToInt(dir.Length() / 1.3f));
            float yaw = Mathf.RadToDeg(Mathf.Atan2(dir.Z, -dir.X)) + 180f;
            for (int i = 0; i < n; i++)
            {
                TWall(root, from + dir * ((i + 0.5f) / n), yaw);
            }
        }

        private static void Antenna(Node3D root, Vector3 pos, float height)
        {
            MeshKit.Cyl(root, 0.06f, 0.12f, height, pos + new Vector3(0f, height * 0.5f, 0f), Pole, null, 6);
            for (float y = 2f; y < height - 1f; y += 2.2f)
            {
                MeshKit.Box(root, new Vector3(0.9f, 0.05f, 0.05f), pos + new Vector3(0f, y, 0f), Pole);
            }

            MeshKit.Add(root, MeshKit.SphereMesh(0.12f, 6, false), pos + new Vector3(0f, height + 0.1f, 0f), MeshKit.Emissive(new Color(1f, 0.15f, 0.1f), 3f), null);
            foreach (var corner in new[] { new Vector3(2.8f, 0f, 0f), new Vector3(-1.4f, 0f, 2.4f), new Vector3(-1.4f, 0f, -2.4f) })
            {
                MeshKit.Beam(root, pos + new Vector3(0f, height * 0.75f, 0f), pos + corner, 0.015f, Pole, 3);
            }
        }

        private static void Dish(Node3D root, Vector3 pos, float radius, float tilt = 35f)
        {
            var g = MeshKit.Group(root, pos, 30f);
            MeshKit.Cyl(g, 0.07f, 0.09f, 0.8f, new Vector3(0f, 0.4f, 0f), Pole, null, 6);
            var dish = MeshKit.Sphere(g, radius, new Vector3(0f, 0.9f, 0f), new Color(0.88f, 0.88f, 0.85f), true, 12, new Vector3(1f, 0.35f, 1f));
            dish.RotationDegrees = new Vector3(180f - tilt, 0f, 0f);
            MeshKit.Beam(g, new Vector3(0f, 0.9f, 0f), new Vector3(0f, 0.9f + radius * 0.8f * Mathf.Cos(Mathf.DegToRad(tilt)), radius * 0.8f * Mathf.Sin(Mathf.DegToRad(tilt))), 0.03f, Pole, 4);
        }

        private static void CamoNet(Node3D root, Vector3 center, Vector2 size, float height)
        {
            MeshKit.BoxT(root, new Vector3(size.X, 0.06f, size.Y), center + new Vector3(0f, height, 0f), NetGreen, Tex.Camo, new Vector3(2f, 0f, -1.5f));
            MeshKit.BoxT(root, new Vector3(size.X * 0.4f, 0.05f, size.Y * 0.6f), center + new Vector3(size.X * 0.2f, height + 0.15f, 0f), NetGreen.Darkened(0.08f), Tex.Camo);
            foreach (var (sx, sz) in new[] { (-1f, -1f), (1f, -1f), (-1f, 1f), (1f, 1f) })
            {
                var top = center + new Vector3(sx * size.X * 0.45f, height, sz * size.Y * 0.45f);
                MeshKit.Beam(root, top, new Vector3(top.X, 0f, top.Z), 0.05f, Wood, 4);
            }
        }

        private static void Crate(Node3D root, Vector3 pos, Vector3 size, Color color, float yaw = 0f)
        {
            var g = MeshKit.Group(root, pos, yaw);
            MeshKit.Box(g, size, new Vector3(0f, size.Y * 0.5f, 0f), color);
            MeshKit.Box(g, new Vector3(size.X + 0.02f, 0.04f, size.Z + 0.02f), new Vector3(0f, size.Y * 0.8f, 0f), color.Darkened(0.25f));
        }

        private static void Barrel(Node3D root, Vector3 pos, Color color)
        {
            MeshKit.Cyl(root, 0.3f, 0.3f, 0.9f, pos + new Vector3(0f, 0.45f, 0f), color, null, 10);
            MeshKit.Cyl(root, 0.31f, 0.31f, 0.05f, pos + new Vector3(0f, 0.7f, 0f), color.Darkened(0.3f), null, 10);
        }

        private static void WaterTank(Node3D root, Vector3 pos)
        {
            MeshKit.Box(root, new Vector3(1.1f, 0.4f, 1.1f), pos + new Vector3(0f, 0.2f, 0f), Steel);
            MeshKit.Cyl(root, 0.55f, 0.55f, 1.1f, pos + new Vector3(0f, 0.95f, 0f), new Color(0.12f, 0.12f, 0.13f), null, 12);
        }

        private static void Rebar(Node3D root, Vector3 pos, int seed)
        {
            for (int i = 0; i < 3; i++)
            {
                var off = new Vector3((MeshKit.Hash01(seed, i) - 0.5f) * 0.3f, 0f, (MeshKit.Hash01(seed, i + 9) - 0.5f) * 0.3f);
                var top = pos + off + new Vector3((MeshKit.Hash01(seed, i + 3) - 0.5f) * 0.4f, 0.9f + MeshKit.Hash01(seed, i + 5) * 0.6f, 0f);
                MeshKit.Beam(root, pos + off, top, 0.025f, new Color(0.35f, 0.22f, 0.15f), 3);
            }
        }

        private static void Windows(Node3D root, float width, float y, float z, float spacing, Vector2 size)
        {
            for (float x = -width * 0.5f + spacing * 0.6f; x <= width * 0.5f - spacing * 0.4f; x += spacing)
            {
                MeshKit.Box(root, new Vector3(size.X, size.Y, 0.06f), new Vector3(x, y, z), Window);
            }
        }

        private static void Rubble(Node3D root, Vector3 pos, float size, int seed, Color color)
        {
            for (int i = 0; i < 5; i++)
            {
                var p = pos + new Vector3((MeshKit.Hash01(seed, i) - 0.5f) * size * 1.6f, 0f, (MeshKit.Hash01(seed, i + 7) - 0.5f) * size * 1.6f);
                float r = size * (0.25f + MeshKit.Hash01(seed, i + 3) * 0.35f);
                MeshKit.Faceted(root, r, p + new Vector3(0f, r * 0.3f, 0f), MeshKit.Vary(color, 0.08f, seed + i), seed + i, new Vector3(1f, 0.6f, 1f), Tex.Concrete, 0.35f, 5);
            }
        }

        // ================================================================ IDF

        private static void IdfHq(Node3D root, float w, float d, Color team)
        {
            Pad(root, w, d, new Color(0.58f, 0.56f, 0.52f));
            Container(root, new Vector3(-1.4f, 0f, -3.3f), 0f, IdfTan, team, 6.5f);
            Container(root, new Vector3(-2.2f, 2.6f, -3.3f), 0f, IdfOlive, team, 4.5f);
            Container(root, new Vector3(3.6f, 0f, 0.2f), 90f, IdfOlive, team, 6f);
            CamoNet(root, new Vector3(-1.4f, 0f, 1.0f), new Vector2(6.5f, 4.2f), 3.0f);
            Crate(root, new Vector3(-3.8f, 0f, 2.2f), new Vector3(1.3f, 1.0f, 0.9f), IdfOlive.Darkened(0.1f)); // generator
            MeshKit.Beam(root, new Vector3(-3.4f, 1f, 2.2f), new Vector3(-3.4f, 2.3f, 2.2f), 0.06f, Steel);
            Crate(root, new Vector3(-0.6f, 0f, 1.6f), new Vector3(2.2f, 0.8f, 0.9f), new Color(0.5f, 0.46f, 0.36f)); // map table
            TWallRow(root, new Vector3(-5.6f, 0f, -5.4f), new Vector3(-5.6f, 0f, 4.4f));
            TWallRow(root, new Vector3(-4.4f, 0f, -5.6f), new Vector3(5.2f, 0f, -5.6f));
            Antenna(root, new Vector3(4.7f, 0f, -4.6f), 13f);
            Dish(root, new Vector3(-3.4f, 5.2f, -3.3f), 0.9f);
            Flag(root, new Vector3(1.6f, 0f, 4.8f), team, 7.5f);
            SandbagRow(root, new Vector3(-4.6f, 0f, 5.4f), new Vector3(-1.2f, 0f, 5.4f));
            SandbagRow(root, new Vector3(3.0f, 0f, 5.4f), new Vector3(5.2f, 0f, 5.4f));
        }

        private static void IdfOutpost(Node3D root, float w, float d, Color team)
        {
            Pad(root, w, d, new Color(0.56f, 0.5f, 0.4f));
            // Watchtower: the outpost's landmark.
            var t = MeshKit.Group(root, new Vector3(-2.6f, 0f, -2.6f));
            foreach (var (x, z) in new[] { (-1.1f, -1.1f), (1.1f, -1.1f), (-1.1f, 1.1f), (1.1f, 1.1f) })
            {
                MeshKit.Beam(t, new Vector3(x * 1.15f, 0f, z * 1.15f), new Vector3(x, 5.3f, z), 0.1f, Steel, 4);
            }

            MeshKit.Beam(t, new Vector3(-1.2f, 0.3f, 1.2f), new Vector3(1.1f, 2.8f, 1.1f), 0.04f, Steel, 3);
            MeshKit.Beam(t, new Vector3(1.2f, 0.3f, 1.2f), new Vector3(-1.1f, 2.8f, 1.1f), 0.04f, Steel, 3);
            MeshKit.Beam(t, new Vector3(-1.2f, 2.8f, -1.2f), new Vector3(-1.1f, 5.2f, 1.1f), 0.04f, Steel, 3);
            MeshKit.BoxT(t, new Vector3(3.0f, 0.25f, 3.0f), new Vector3(0f, 5.35f, 0f), Wood, Tex.Canvas);
            SandbagRow(t, new Vector3(-1.4f, 0f, 1.4f), new Vector3(1.4f, 0f, 1.4f), 2, 5.45f);
            SandbagRow(t, new Vector3(1.4f, 0f, -1.4f), new Vector3(1.4f, 0f, 1.4f), 2, 5.45f);
            SandbagRow(t, new Vector3(-1.4f, 0f, -1.4f), new Vector3(-1.4f, 0f, 1.4f), 2, 5.45f);
            foreach (var (x, z) in new[] { (-1.3f, -1.3f), (1.3f, -1.3f), (-1.3f, 1.3f), (1.3f, 1.3f) })
            {
                MeshKit.Beam(t, new Vector3(x, 5.4f, z), new Vector3(x, 7.0f, z), 0.05f, Wood, 4);
            }

            MeshKit.PrismT(t, new Vector3(3.4f, 0.9f, 3.4f), new Vector3(0f, 7.4f, 0f), IdfOlive, Tex.Canvas);
            for (float y = 0.5f; y < 5.2f; y += 0.45f)
            {
                MeshKit.Box(t, new Vector3(0.6f, 0.05f, 0.05f), new Vector3(0f, y, 1.25f), Steel);
            }

            // Barracks tent.
            MeshKit.PrismT(root, new Vector3(4.2f, 2.9f, 6.2f), new Vector3(2.3f, 1.45f, 0.2f), IdfOlive, Tex.Canvas);
            MeshKit.Box(root, new Vector3(1.0f, 1.6f, 0.05f), new Vector3(2.3f, 0.8f, 3.32f), Dark);
            // MG nest.
            var nest = new Vector3(-2.6f, 0f, 3.2f);
            SandbagRow(root, nest + new Vector3(-1.3f, 0f, -0.6f), nest + new Vector3(-1.3f, 0f, 1.0f));
            SandbagRow(root, nest + new Vector3(-1.3f, 0f, 1.0f), nest + new Vector3(1.3f, 0f, 1.0f));
            SandbagRow(root, nest + new Vector3(1.3f, 0f, 1.0f), nest + new Vector3(1.3f, 0f, -0.6f));
            MeshKit.Beam(root, nest + new Vector3(0f, 0.8f, 0.2f), nest + new Vector3(0f, 0.95f, 1.6f), 0.05f, Dark);
            Crate(root, new Vector3(4.2f, 0f, 3.8f), new Vector3(1.0f, 0.5f, 0.6f), IdfOlive.Darkened(0.1f), 15f);
            Crate(root, new Vector3(4.0f, 0.5f, 3.8f), new Vector3(1.0f, 0.5f, 0.6f), IdfOlive.Darkened(0.05f), 5f);
            Flag(root, new Vector3(-4.4f, 0f, 0.6f), team, 6f);
        }

        private static void IdfSupportBase(Node3D root, float w, float d, Color team)
        {
            Pad(root, w, d, ConcreteGray.Darkened(0.05f), Tex.Concrete);
            var building = MeshKit.Group(root, new Vector3(-1.4f, 0f, -2.2f));
            MeshKit.BoxT(building, new Vector3(6.2f, 3.2f, 4.4f), new Vector3(0f, 1.6f, 0f), new Color(0.78f, 0.75f, 0.68f), Tex.Concrete);
            MeshKit.BoxT(building, new Vector3(6.4f, 0.3f, 4.6f), new Vector3(0f, 3.3f, 0f), new Color(0.62f, 0.6f, 0.55f), Tex.Concrete);
            Windows(building, 6.2f, 2.0f, 2.23f, 1.6f, new Vector2(0.9f, 0.7f));
            MeshKit.Box(building, new Vector3(1.1f, 2.1f, 0.06f), new Vector3(2.2f, 1.05f, 2.23f), Steel);
            MeshKit.Box(building, new Vector3(6.22f, 0.25f, 0.02f), new Vector3(0f, 2.75f, 2.21f), team);
            Dish(building, new Vector3(-1.6f, 3.4f, 0f), 1.0f);
            Crate(building, new Vector3(1.6f, 3.45f, -0.8f), new Vector3(0.9f, 0.6f, 0.7f), new Color(0.8f, 0.8f, 0.78f)); // AC unit
            // Radome on a short tower: the base's landmark.
            MeshKit.CylT(root, 0.55f, 0.7f, 2.4f, new Vector3(3.3f, 1.2f, -3.0f), ConcreteGray, Tex.Concrete, null, 10);
            MeshKit.Sphere(root, 1.55f, new Vector3(3.3f, 3.75f, -3.0f), new Color(0.93f, 0.93f, 0.9f), false, 14);
            // Missile crates under a camo net.
            for (int i = 0; i < 6; i++)
            {
                Crate(root, new Vector3(2.4f + (i % 2) * 0.1f, (i / 2) * 0.42f, 1.6f + (i % 2) * 0.7f), new Vector3(2.4f, 0.4f, 0.55f), IdfOlive.Darkened(0.05f * (i % 3)));
            }

            CamoNet(root, new Vector3(2.4f, 0f, 2.0f), new Vector2(3.6f, 3.0f), 2.2f);
            TWallRow(root, new Vector3(-4.6f, 0f, 4.4f), new Vector3(-0.8f, 0f, 4.4f));
            Flag(root, new Vector3(-4.4f, 0f, 1.2f), team, 6.5f);
        }

        private static void IdfArmorYard(Node3D root, float w, float d, Color team)
        {
            Pad(root, w, d, ConcreteGray.Darkened(0.1f), Tex.Concrete);
            // Quonset hangar: half cylinder along Z, open toward +Z.
            var hangarPos = new Vector3(-0.8f, 0f, -1.2f);
            MeshKit.CylT(root, 4.1f, 4.1f, 8.6f, hangarPos, IdfTan, Tex.Corrugated, new Vector3(90f, 0f, 0f), 18);
            MeshKit.Cyl(root, 3.5f, 3.5f, 0.06f, hangarPos + new Vector3(0f, 0f, 4.33f), Dark, new Vector3(90f, 0f, 0f), 18);
            MeshKit.Box(root, new Vector3(8.3f, 0.3f, 0.08f), hangarPos + new Vector3(0f, 0.15f, 4.34f), Dark);
            foreach (float z in new[] { -3.6f, 0f, 3.6f })
            {
                MeshKit.Cyl(root, 4.16f, 4.16f, 0.15f, hangarPos + new Vector3(0f, 0f, z), IdfTan.Darkened(0.15f), new Vector3(90f, 0f, 0f), 18);
            }

            MeshKit.Box(root, new Vector3(0.1f, 0.5f, 6f), hangarPos + new Vector3(-4.12f, 0.3f, 0f), team);
            // Fuel bowser.
            var fuel = MeshKit.Group(root, new Vector3(4.5f, 0f, 2.8f), 90f);
            MeshKit.Cyl(fuel, 0.85f, 0.85f, 3.6f, new Vector3(0f, 1.35f, 0f), IdfOlive, new Vector3(0f, 0f, 90f), 12);
            MeshKit.Box(fuel, new Vector3(1.4f, 1.3f, 1.4f), new Vector3(2.3f, 1.0f, 0f), IdfOlive.Darkened(0.1f));
            foreach (float x in new[] { -1.2f, 1.2f, 2.3f })
            {
                foreach (float z in new[] { -0.75f, 0.75f })
                {
                    MeshKit.Tire(fuel, new Vector3(x, 0.4f, z), 0.4f, new Vector3(90f, 0f, 0f));
                }
            }

            // Spare road wheels and a track section.
            for (int i = 0; i < 3; i++)
            {
                MeshKit.Cyl(root, 0.4f, 0.4f, 0.3f, new Vector3(4.6f + i * 0.2f, 0.15f + i * 0.3f, -2.8f), Steel, null, 10);
            }

            TWallRow(root, new Vector3(-5.6f, 0f, -5.6f), new Vector3(5.6f, 0f, -5.6f));
            Flag(root, new Vector3(5.0f, 0f, 5.0f), team, 7f);
        }

        // ================================================================ Hamas

        private static void HamasBunkerHouse(Node3D root, float w, float d, Color team)
        {
            Pad(root, w, d, new Color(0.6f, 0.57f, 0.52f));
            var house = MeshKit.Group(root, new Vector3(-1.0f, 0f, -1.8f));
            MeshKit.BoxT(house, new Vector3(7.4f, 3.2f, 5.8f), new Vector3(0f, 1.6f, 0f), GazaConcrete, Tex.Concrete);
            MeshKit.BoxT(house, new Vector3(7.6f, 0.35f, 6.0f), new Vector3(0f, 3.35f, 0f), GazaConcrete.Darkened(0.12f), Tex.Concrete);
            MeshKit.Box(house, new Vector3(1.1f, 2.2f, 0.06f), new Vector3(1.6f, 1.1f, 2.93f), Dark);
            MeshKit.Box(house, new Vector3(1.0f, 0.9f, 0.06f), new Vector3(-1.4f, 2.0f, 2.93f), Window);
            MeshKit.Box(house, new Vector3(1.0f, 0.9f, 0.06f), new Vector3(-3.0f, 2.0f, 2.93f), Window);
            MeshKit.Box(house, new Vector3(2.0f, 1.0f, 0.04f), new Vector3(-2.2f, 2.85f, 2.95f), team); // banner
            SandbagRow(house, new Vector3(-3.6f, 0f, 3.3f), new Vector3(-0.8f, 0f, 3.3f), 3);
            // Roofscape: column stubs with rebar, water tanks, a solar panel.
            foreach (var (x, z) in new[] { (-3.5f, -2.7f), (3.5f, -2.7f), (-3.5f, 2.7f), (3.5f, 2.7f) })
            {
                MeshKit.BoxT(house, new Vector3(0.4f, 0.6f, 0.4f), new Vector3(x, 3.8f, z), GazaConcrete, Tex.Concrete);
                Rebar(house, new Vector3(x, 4.1f, z), (int)(x * 10f + z));
            }

            WaterTank(house, new Vector3(-1.8f, 3.5f, -1.4f));
            WaterTank(house, new Vector3(-0.4f, 3.5f, -1.4f));
            MeshKit.Box(house, new Vector3(2.0f, 0.06f, 1.2f), new Vector3(1.8f, 4.0f, 0.4f), new Color(0.2f, 0.26f, 0.36f), new Vector3(-28f, 0f, 0f));
            MeshKit.Beam(house, new Vector3(1.8f, 3.5f, 0.9f), new Vector3(1.8f, 3.75f, 0.9f), 0.04f, Steel);
            // Tunnel shaft with a winch frame: the faction's signature.
            var shaft = MeshKit.Group(root, new Vector3(3.6f, 0f, 3.4f));
            MeshKit.BoxT(shaft, new Vector3(2.0f, 0.5f, 2.0f), new Vector3(0f, 0.25f, 0f), GazaConcrete.Darkened(0.1f), Tex.Concrete);
            MeshKit.Box(shaft, new Vector3(1.4f, 0.05f, 1.4f), new Vector3(0f, 0.51f, 0f), new Color(0.02f, 0.02f, 0.02f));
            MeshKit.Beam(shaft, new Vector3(-0.9f, 0.5f, 0f), new Vector3(-0.6f, 2.4f, 0f), 0.06f, Steel);
            MeshKit.Beam(shaft, new Vector3(0.9f, 0.5f, 0f), new Vector3(0.6f, 2.4f, 0f), 0.06f, Steel);
            MeshKit.Beam(shaft, new Vector3(-0.7f, 2.4f, 0f), new Vector3(0.7f, 2.4f, 0f), 0.07f, Steel);
            MeshKit.Beam(shaft, new Vector3(0f, 2.4f, 0f), new Vector3(0f, 0.6f, 0f), 0.015f, Dark, 3);
            Rubble(root, new Vector3(-4.4f, 0f, 3.8f), 1.2f, 5, GazaConcrete);
            Flag(house, new Vector3(3.0f, 3.5f, -2.2f), team, 4f);
        }

        private static void HamasWorkshop(Node3D root, float w, float d, Color team)
        {
            Pad(root, w, d, new Color(0.5f, 0.45f, 0.38f));
            var g = MeshKit.Group(root, new Vector3(-0.4f, 0f, -1.6f));
            MeshKit.BoxT(g, new Vector3(6.8f, 3.2f, 5.4f), new Vector3(0f, 1.6f, 0f), GazaConcrete.Darkened(0.05f), Tex.Concrete);
            MeshKit.BoxT(g, new Vector3(7.3f, 0.12f, 6.1f), new Vector3(0f, 3.45f, 0f), Rust, Tex.Corrugated, new Vector3(7f, 0f, 0f));
            MeshKit.BoxT(g, new Vector3(4.0f, 2.6f, 0.08f), new Vector3(-0.6f, 1.6f, 2.72f), Rust.Darkened(0.05f), Tex.Corrugated); // roll-up door, half open
            MeshKit.Box(g, new Vector3(4.0f, 0.3f, 0.06f), new Vector3(-0.6f, 0.15f, 2.72f), Dark);
            MeshKit.Box(g, new Vector3(6.82f, 0.2f, 0.02f), new Vector3(0f, 3.0f, 2.71f), team);
            // Lean-to on the right.
            MeshKit.BoxT(root, new Vector3(2.6f, 0.1f, 5.0f), new Vector3(4.4f, 2.4f, -1.4f), Rust.Lightened(0.05f), Tex.Corrugated, new Vector3(0f, 0f, -10f));
            MeshKit.Beam(root, new Vector3(5.6f, 0f, 0.9f), new Vector3(5.6f, 2.2f, 0.9f), 0.06f, Steel);
            MeshKit.Beam(root, new Vector3(5.6f, 0f, -3.7f), new Vector3(5.6f, 2.2f, -3.7f), 0.06f, Steel);
            // Clutter: tires, oil drums, a bundle of steel pipes.
            for (int i = 0; i < 4; i++)
            {
                MeshKit.Tire(root, new Vector3(-3.8f, 0.12f + i * 0.24f, 3.0f), 0.42f);
            }

            MeshKit.Tire(root, new Vector3(-2.8f, 0.42f, 3.6f), 0.42f, new Vector3(80f, 20f, 0f));
            Barrel(root, new Vector3(3.6f, 0f, 2.6f), new Color(0.2f, 0.32f, 0.55f));
            Barrel(root, new Vector3(4.3f, 0f, 2.9f), Rust);
            Barrel(root, new Vector3(3.9f, 0f, 3.5f), new Color(0.3f, 0.4f, 0.25f));
            for (int i = 0; i < 7; i++)
            {
                float x = 0.6f + (i % 4) * 0.18f, y = 0.1f + (i / 4) * 0.16f;
                MeshKit.Cyl(root, 0.08f, 0.08f, 3.2f, new Vector3(x - 1.0f, y, 3.6f), new Color(0.42f, 0.42f, 0.4f), new Vector3(0f, 0f, 90f), 6);
            }

            Crate(root, new Vector3(1.6f, 0f, 2.2f), new Vector3(1.6f, 0.9f, 0.7f), Wood); // workbench
            Flag(root, new Vector3(-4.4f, 0f, -3.8f), team, 5.5f);
        }

        private static void HamasArmsLab(Node3D root, float w, float d, Color team)
        {
            Pad(root, w, d, new Color(0.58f, 0.55f, 0.5f));
            var g = MeshKit.Group(root, new Vector3(-0.8f, 0f, -1.6f));
            var c = GazaConcrete.Lightened(0.04f);
            MeshKit.BoxT(g, new Vector3(6.4f, 3.0f, 5.4f), new Vector3(0f, 1.5f, 0f), c, Tex.Concrete);
            // Second floor with a shell-damaged front-right corner.
            MeshKit.BoxT(g, new Vector3(6.4f, 2.8f, 3.1f), new Vector3(0f, 4.4f, -1.15f), c, Tex.Concrete);
            MeshKit.BoxT(g, new Vector3(3.6f, 2.8f, 2.3f), new Vector3(-1.4f, 4.4f, 1.55f), c, Tex.Concrete);
            MeshKit.BoxT(g, new Vector3(2.8f, 1.0f, 2.3f), new Vector3(1.8f, 3.5f, 1.55f), c.Darkened(0.05f), Tex.Concrete);
            Rebar(g, new Vector3(0.5f, 4.0f, 2.6f), 3);
            Rebar(g, new Vector3(3.1f, 4.0f, 0.5f), 4);
            MeshKit.BoxT(g, new Vector3(6.6f, 0.3f, 5.6f), new Vector3(0f, 3.0f, 0f), c.Darkened(0.12f), Tex.Concrete);
            Windows(g, 6.4f, 1.8f, 2.73f, 2.0f, new Vector2(1.0f, 0.9f));
            MeshKit.Box(g, new Vector3(1.1f, 1.0f, 0.06f), new Vector3(-2.2f, 4.6f, 2.73f), new Color(0.32f, 0.42f, 0.52f)); // tarp over window
            MeshKit.Box(g, new Vector3(1.1f, 1.0f, 0.06f), new Vector3(-0.6f, 4.6f, 2.73f), team);
            WaterTank(g, new Vector3(-2.0f, 5.8f, -1.6f));
            Rubble(root, new Vector3(2.6f, 0f, 1.8f), 1.1f, 9, c);
            // Rocket rack.
            var rack = MeshKit.Group(root, new Vector3(3.6f, 0f, -2.2f), -90f);
            MeshKit.Beam(rack, new Vector3(-0.8f, 0f, 0f), new Vector3(-0.8f, 1.6f, -0.6f), 0.06f, Steel);
            MeshKit.Beam(rack, new Vector3(0.8f, 0f, 0f), new Vector3(0.8f, 1.6f, -0.6f), 0.06f, Steel);
            for (int i = 0; i < 4; i++)
            {
                float x = -0.6f + i * 0.4f;
                MeshKit.Beam(rack, new Vector3(x, 0.3f, 1.2f), new Vector3(x, 1.7f, -0.9f), 0.11f, new Color(0.35f, 0.38f, 0.3f), 8);
            }

            Crate(root, new Vector3(3.8f, 0f, 2.8f), new Vector3(1.1f, 0.6f, 0.7f), new Color(0.32f, 0.36f, 0.24f));
            Flag(root, new Vector3(-4.5f, 0f, 3.6f), team, 5.5f);
        }

        private static void HamasCompound(Node3D root, float w, float d, Color team)
        {
            Pad(root, w, d, new Color(0.6f, 0.56f, 0.48f));
            float hw = w * 0.46f, hd = d * 0.46f;
            var wall = Plaster.Darkened(0.05f);
            MeshKit.BoxT(root, new Vector3(w * 0.92f, 2.3f, 0.35f), new Vector3(0f, 1.15f, -hd), wall, Tex.Plaster);
            MeshKit.BoxT(root, new Vector3(0.35f, 2.3f, d * 0.92f), new Vector3(-hw, 1.15f, 0f), wall, Tex.Plaster);
            MeshKit.BoxT(root, new Vector3(0.35f, 2.3f, d * 0.92f), new Vector3(hw, 1.15f, 0f), wall, Tex.Plaster);
            MeshKit.BoxT(root, new Vector3(w * 0.3f, 2.3f, 0.35f), new Vector3(-hw + w * 0.15f, 1.15f, hd), wall, Tex.Plaster);
            MeshKit.BoxT(root, new Vector3(w * 0.3f, 2.3f, 0.35f), new Vector3(hw - w * 0.15f, 1.15f, hd), wall, Tex.Plaster);
            // Steel gate, one leaf ajar.
            MeshKit.BoxT(root, new Vector3(1.6f, 2.1f, 0.08f), new Vector3(-0.9f, 1.05f, hd), new Color(0.2f, 0.32f, 0.24f), Tex.Metal);
            MeshKit.BoxT(root, new Vector3(1.6f, 2.1f, 0.08f), new Vector3(0.95f, 1.05f, hd + 0.6f), new Color(0.2f, 0.32f, 0.24f), Tex.Metal, new Vector3(0f, 40f, 0f));
            // Main building with a pergola porch.
            MeshKit.BoxT(root, new Vector3(5.2f, 3.0f, 3.2f), new Vector3(-0.6f, 1.5f, -2.4f), Plaster.Lightened(0.06f), Tex.Plaster);
            MeshKit.BoxT(root, new Vector3(5.4f, 0.25f, 3.4f), new Vector3(-0.6f, 3.1f, -2.4f), Plaster.Darkened(0.1f), Tex.Plaster);
            Windows(root, 5.2f, 1.9f, -0.78f, 1.5f, new Vector2(0.8f, 0.9f));
            foreach (float x in new[] { -2.9f, -0.6f, 1.7f })
            {
                MeshKit.Beam(root, new Vector3(x, 0f, 0.6f), new Vector3(x, 2.6f, 0.6f), 0.08f, Wood);
            }

            for (int i = 0; i < 7; i++)
            {
                MeshKit.Box(root, new Vector3(0.12f, 0.1f, 1.8f), new Vector3(-3.0f + i * 0.78f, 2.65f, -0.2f), Wood);
            }

            // Team-colored shade tarp and a corner lookout.
            MeshKit.BoxT(root, new Vector3(3.2f, 0.05f, 2.6f), new Vector3(2.6f, 2.5f, 1.8f), team.Darkened(0.15f), Tex.Canvas, new Vector3(-8f, 0f, 6f));
            MeshKit.Beam(root, new Vector3(4.0f, 0f, 3.0f), new Vector3(4.0f, 2.6f, 3.0f), 0.05f, Steel);
            var post = MeshKit.Group(root, new Vector3(hw - 0.6f, 2.3f, -hd + 0.6f));
            SandbagRow(post, new Vector3(-0.9f, 0f, 0.9f), new Vector3(0.9f, 0f, 0.9f));
            SandbagRow(post, new Vector3(-0.9f, 0f, -0.9f), new Vector3(-0.9f, 0f, 0.9f));
            WaterTank(root, new Vector3(-2.4f, 3.2f, -3.2f));
            Dish(root, new Vector3(1.0f, 3.2f, -3.0f), 0.55f);
            Flag(root, new Vector3(-hw + 0.6f, 0f, hd - 0.6f), team, 6f);
        }

        // ================================================================ Hezbollah

        private static void HzbFarmhouse(Node3D root, float w, float d, Color team)
        {
            Pad(root, w, d, new Color(0.55f, 0.5f, 0.4f));
            var house = MeshKit.Group(root, new Vector3(-1.6f, 0f, -2.0f));
            MeshKit.BoxT(house, new Vector3(6.8f, 3.3f, 4.8f), new Vector3(0f, 1.65f, 0f), Limestone, Tex.Stone);
            MeshKit.PrismT(house, new Vector3(5.4f, 1.7f, 7.4f), new Vector3(0f, 4.15f, 0f), RoofRed, Tex.Tiles, new Vector3(0f, 90f, 0f));
            // Arched door and shuttered windows.
            MeshKit.Box(house, new Vector3(1.2f, 1.8f, 0.06f), new Vector3(0.8f, 0.9f, 2.42f), Dark);
            MeshKit.Cyl(house, 0.6f, 0.6f, 0.06f, new Vector3(0.8f, 1.8f, 2.42f), Dark, new Vector3(90f, 0f, 0f), 12);
            foreach (float x in new[] { -2.2f, 2.4f })
            {
                MeshKit.Box(house, new Vector3(0.8f, 1.0f, 0.06f), new Vector3(x, 2.0f, 2.42f), Window);
                MeshKit.Box(house, new Vector3(0.35f, 1.0f, 0.05f), new Vector3(x - 0.6f, 2.0f, 2.44f), new Color(0.25f, 0.42f, 0.4f));
                MeshKit.Box(house, new Vector3(0.35f, 1.0f, 0.05f), new Vector3(x + 0.6f, 2.0f, 2.44f), new Color(0.25f, 0.42f, 0.4f));
            }

            MeshKit.Box(house, new Vector3(1.6f, 0.9f, 0.04f), new Vector3(-0.8f, 2.95f, 2.45f), team); // banner
            // Command bunker dug into an earth mound behind the house.
            MeshKit.Faceted(root, 3.4f, new Vector3(3.4f, 0f, -3.2f), EarthColor, 3, new Vector3(1.1f, 0.62f, 0.9f), Tex.Earth, 0.25f, 8);
            MeshKit.BoxT(root, new Vector3(2.2f, 2.0f, 1.0f), new Vector3(3.4f, 1.0f, -0.6f), ConcreteGray, Tex.Concrete);
            MeshKit.Box(root, new Vector3(1.3f, 1.6f, 0.06f), new Vector3(3.4f, 0.8f, -0.08f), Steel);
            Antenna(root, new Vector3(4.8f, 0f, 4.2f), 11f);
            // Terraced stone wall in front.
            MeshKit.BoxT(root, new Vector3(4.6f, 0.9f, 0.5f), new Vector3(-3.0f, 0.45f, 5.2f), Limestone.Darkened(0.08f), Tex.Stone);
            MeshKit.BoxT(root, new Vector3(3.0f, 0.9f, 0.5f), new Vector3(3.4f, 0.45f, 5.2f), Limestone.Darkened(0.08f), Tex.Stone);
            Flag(house, new Vector3(-3.0f, 3.3f, -2.0f), team, 3.6f);
        }

        private static void HzbTrainingCamp(Node3D root, float w, float d, Color team)
        {
            Pad(root, w, d, new Color(0.52f, 0.47f, 0.37f));
            // Open-sided canopy under a camo net.
            var canopy = new Vector3(-0.8f, 0f, -2.2f);
            foreach (var (x, z) in new[] { (-3.2f, -1.9f), (0f, -1.9f), (3.2f, -1.9f), (-3.2f, 1.9f), (0f, 1.9f), (3.2f, 1.9f) })
            {
                MeshKit.Beam(root, canopy + new Vector3(x, 0f, z), canopy + new Vector3(x, 3.0f, z), 0.09f, Wood);
            }

            MeshKit.BoxT(root, new Vector3(7.2f, 0.07f, 4.6f), canopy + new Vector3(0f, 3.05f, 0f), NetGreen, Tex.Camo, new Vector3(3f, 0f, 0f));
            MeshKit.Box(root, new Vector3(7.2f, 0.5f, 0.04f), canopy + new Vector3(0f, 2.75f, 2.32f), team); // banner
            for (int i = 0; i < 3; i++)
            {
                MeshKit.Box(root, new Vector3(2.0f, 0.45f, 0.4f), canopy + new Vector3(-2.0f + i * 2.0f, 0.22f, 0.2f), Wood.Darkened(0.1f)); // benches
            }

            // Climbing A-frame.
            var frame = MeshKit.Group(root, new Vector3(-3.0f, 0f, 2.8f));
            foreach (float x in new[] { -1.0f, 1.0f })
            {
                MeshKit.Beam(frame, new Vector3(x, 0f, -1.1f), new Vector3(x, 2.6f, 0f), 0.08f, Wood);
                MeshKit.Beam(frame, new Vector3(x, 0f, 1.1f), new Vector3(x, 2.6f, 0f), 0.08f, Wood);
            }

            for (int i = 1; i < 6; i++)
            {
                float t = i / 6f;
                MeshKit.Beam(frame, new Vector3(-1.0f, 2.6f * t, -1.1f * (1f - t)), new Vector3(1.0f, 2.6f * t, -1.1f * (1f - t)), 0.04f, Wood, 4);
            }

            // Tire run and a log wall.
            for (int i = 0; i < 6; i++)
            {
                MeshKit.Tire(root, new Vector3(1.0f + (i / 2) * 1.0f, 0.12f, 2.4f + (i % 2) * 1.0f), 0.42f);
            }

            for (int i = 0; i < 3; i++)
            {
                MeshKit.Cyl(root, 0.16f, 0.16f, 2.8f, new Vector3(3.6f, 0.16f + i * 0.3f, 0.6f), Wood, new Vector3(90f, 0f, 0f), 8);
            }

            // Targets.
            foreach (float z in new[] { -3.6f, -2.0f })
            {
                MeshKit.Beam(root, new Vector3(4.6f, 0f, z), new Vector3(4.6f, 1.6f, z), 0.05f, Wood);
                MeshKit.Box(root, new Vector3(0.05f, 0.9f, 0.9f), new Vector3(4.6f, 1.6f, z), new Color(0.88f, 0.86f, 0.8f));
                MeshKit.Box(root, new Vector3(0.06f, 0.3f, 0.3f), new Vector3(4.58f, 1.6f, z), new Color(0.15f, 0.15f, 0.15f));
            }

            Flag(root, new Vector3(-4.4f, 0f, 4.2f), team, 6f);
        }

        private static void HzbFortification(Node3D root, float w, float d, Color team)
        {
            Pad(root, w, d, new Color(0.5f, 0.44f, 0.34f));
            MeshKit.Faceted(root, 4.6f, new Vector3(0f, 0f, -1.8f), EarthColor, 6, new Vector3(1.05f, 0.55f, 0.82f), Tex.Earth, 0.22f, 9);
            var facade = new Color(0.56f, 0.57f, 0.5f);
            MeshKit.BoxT(root, new Vector3(6.2f, 2.6f, 0.9f), new Vector3(0f, 1.3f, 1.4f), facade, Tex.Concrete);
            MeshKit.BoxT(root, new Vector3(6.6f, 0.4f, 1.3f), new Vector3(0f, 2.7f, 1.3f), facade.Darkened(0.1f), Tex.Concrete);
            foreach (float x in new[] { -2.2f, 0f, 2.2f })
            {
                MeshKit.Box(root, new Vector3(1.1f, 0.2f, 0.06f), new Vector3(x, 2.05f, 1.86f), Dark); // firing slits
            }

            MeshKit.BoxT(root, new Vector3(1.2f, 1.7f, 0.06f), new Vector3(-1.1f, 0.85f, 1.87f), new Color(0.22f, 0.3f, 0.22f), Tex.Metal);
            MeshKit.Box(root, new Vector3(1.8f, 0.5f, 0.04f), new Vector3(1.6f, 0.9f, 1.87f), team); // painted marking
            MeshKit.BoxT(root, new Vector3(5.6f, 0.06f, 4.0f), new Vector3(0.4f, 2.9f, -2.0f), NetGreen, Tex.Camo, new Vector3(-12f, 0f, 4f));
            // Trench in front, lined with sandbags.
            MeshKit.BoxT(root, new Vector3(8.0f, 0.04f, 1.0f), new Vector3(0f, 0.07f, 3.6f), new Color(0.2f, 0.17f, 0.13f), Tex.Earth);
            SandbagRow(root, new Vector3(-4.0f, 0f, 3.0f), new Vector3(4.0f, 0f, 3.0f), 1);
            SandbagRow(root, new Vector3(-4.0f, 0f, 4.2f), new Vector3(4.0f, 0f, 4.2f), 1);
            MeshKit.Beam(root, new Vector3(2.4f, 1.6f, -2.6f), new Vector3(2.4f, 5.0f, -2.6f), 0.05f, Pole); // periscope mast
            Flag(root, new Vector3(-4.2f, 0f, -0.6f), team, 5.5f);
        }

        private static void HzbTunnelPortal(Node3D root, float w, float d, Color team)
        {
            Pad(root, w, d, new Color(0.48f, 0.44f, 0.38f));
            // A rocky hill with a concrete tunnel portal: the signature "underground" headquarters.
            var hill = new[] { (0f, -3.0f, 3.9f, 1.0f), (-3.4f, -2.4f, 2.8f, 0.9f), (3.4f, -2.6f, 3.0f, 0.85f), (-1.8f, -4.4f, 2.6f, 1.2f), (2.2f, -4.6f, 2.4f, 1.1f), (-4.4f, 0.2f, 1.8f, 0.6f), (4.6f, 0.4f, 1.6f, 0.6f) };
            for (int i = 0; i < hill.Length; i++)
            {
                var (x, z, r, h) = hill[i];
                MeshKit.Faceted(root, r, new Vector3(x, r * 0.25f, z), MeshKit.Vary(RockColor, 0.06f, i), 20 + i, new Vector3(1f, h, 1f), Tex.Stone, 0.3f, 7);
            }

            var portal = new Vector3(0f, 0f, 0.2f);
            var concrete = ConcreteGray.Darkened(0.05f);
            MeshKit.BoxT(root, new Vector3(0.9f, 4.2f, 1.8f), portal + new Vector3(-2.6f, 2.1f, 0f), concrete, Tex.Concrete);
            MeshKit.BoxT(root, new Vector3(0.9f, 4.2f, 1.8f), portal + new Vector3(2.6f, 2.1f, 0f), concrete, Tex.Concrete);
            MeshKit.BoxT(root, new Vector3(6.1f, 1.0f, 1.9f), portal + new Vector3(0f, 4.4f, 0f), concrete, Tex.Concrete);
            MeshKit.Box(root, new Vector3(4.3f, 3.9f, 0.06f), portal + new Vector3(0f, 1.95f, 0.1f), new Color(0.02f, 0.02f, 0.02f));
            MeshKit.BoxT(root, new Vector3(2.0f, 3.6f, 0.2f), portal + new Vector3(-1.6f, 1.8f, 1.1f), Steel, Tex.Metal, new Vector3(0f, -25f, 0f)); // blast doors, open
            MeshKit.BoxT(root, new Vector3(2.0f, 3.6f, 0.2f), portal + new Vector3(1.6f, 1.8f, 1.1f), Steel, Tex.Metal, new Vector3(0f, 25f, 0f));
            MeshKit.Box(root, new Vector3(6.12f, 0.25f, 0.02f), portal + new Vector3(0f, 4.0f, 0.96f), team);
            MeshKit.BoxT(root, new Vector3(5.0f, 0.04f, 4.4f), new Vector3(0f, 0.07f, 3.4f), new Color(0.36f, 0.33f, 0.3f), Tex.Earth); // access road
            MeshKit.BoxT(root, new Vector3(7.0f, 0.06f, 3.0f), portal + new Vector3(0f, 5.0f, -0.6f), NetGreen, Tex.Camo, new Vector3(-10f, 0f, 0f));
            // Guard post with a lamp.
            var guard = MeshKit.Group(root, new Vector3(4.2f, 0f, 4.2f));
            SandbagRow(guard, new Vector3(-0.9f, 0f, 0.8f), new Vector3(0.9f, 0f, 0.8f), 2);
            SandbagRow(guard, new Vector3(0.9f, 0f, -0.8f), new Vector3(0.9f, 0f, 0.8f), 2);
            MeshKit.Beam(guard, new Vector3(-0.8f, 0f, -0.8f), new Vector3(-0.8f, 3.0f, -0.8f), 0.05f, Pole);
            MeshKit.Add(guard, MeshKit.SphereMesh(0.16f, 6, false), new Vector3(-0.8f, 3.05f, -0.8f), MeshKit.Emissive(new Color(1f, 0.85f, 0.55f), 2.5f), null);
            Flag(root, new Vector3(-1.0f, 4.2f, -3.2f), team, 4f);
        }
    }
}
