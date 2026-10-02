using Godot;
using OCT7.Sim.Data;
using OCT7.Sim.World;

namespace OCT7.Game.Visual
{
    /// <summary>Parts of a procedural soldier that the squad view animates.</summary>
    public sealed class SoldierRig
    {
        public Node3D Root;
        public Node3D Pose;
        public Node3D UpperBody;
        public Node3D LegL;
        public Node3D LegR;
        public Node3D ArmL;
        public Node3D ArmR;

        /// <summary>Approximate muzzle position in Root space (for muzzle flashes).</summary>
        public Vector3 Muzzle;

        /// <summary>Uniform scale of <see cref="Root"/> when fully shown (reinforcement scale-in animates to it).</summary>
        public float BaseScale = 1f;
    }

    /// <summary>
    /// Procedural low-poly models for units, structures and map dressing ("rough graphics, not boxes").
    /// Any model can be replaced by an artist asset: drop res://assets/models/&lt;id&gt;.glb (or .tscn) and it is used instead.
    /// </summary>
    public static class ModelFactory
    {
        private static readonly Color Gunmetal = new Color(0.13f, 0.13f, 0.13f);
        private static readonly Color Sand = new Color(0.72f, 0.64f, 0.47f);
        private static readonly Color Concrete = new Color(0.66f, 0.64f, 0.60f);

        /// <summary>Artist override: res://assets/models/{id}.glb or .tscn, if present.</summary>
        public static Node3D TryLoadAsset(string id)
        {
            foreach (var ext in new[] { ".glb", ".tscn" })
            {
                string path = $"res://assets/models/{id}{ext}";
                if (ResourceLoader.Exists(path) && GD.Load(path) is PackedScene scene && scene.Instantiate() is Node3D node)
                {
                    return node;
                }
            }

            return null;
        }

        // ================================================================ soldiers

        public static SoldierRig BuildSoldier(UnitDef unit, WeaponKind kind, Color team, int index, bool carriesSpecialWeapon) =>
            SoldierModels.Build(unit, kind, team, index, carriesSpecialWeapon);

        /// <summary>Tripod-mounted ATGM launcher placed next to the team (Kornet, Spike).</summary>
        public static Node3D BuildAtgmLauncher(Color team)
        {
            var root = new Node3D();
            MeshKit.Cyl(root, 0.03f, 0.03f, 0.75f, new Vector3(-0.22f, 0.32f, 0f), Gunmetal, new Vector3(0f, 0f, 20f), 5);
            MeshKit.Cyl(root, 0.03f, 0.03f, 0.75f, new Vector3(0.22f, 0.32f, 0f), Gunmetal, new Vector3(0f, 0f, -20f), 5);
            MeshKit.Cyl(root, 0.03f, 0.03f, 0.75f, new Vector3(0f, 0.32f, -0.22f), Gunmetal, new Vector3(-20f, 0f, 0f), 5);
            MeshKit.Cyl(root, 0.09f, 0.09f, 1.25f, new Vector3(0f, 0.72f, 0.15f), new Color(0.25f, 0.28f, 0.18f), new Vector3(85f, 0f, 0f), 8);
            MeshKit.Box(root, new Vector3(0.22f, 0.2f, 0.25f), new Vector3(0.18f, 0.72f, -0.2f), Gunmetal);
            MeshKit.Box(root, new Vector3(0.04f, 0.06f, 0.5f), new Vector3(-0.12f, 0.74f, 0.1f), team);
            return root;
        }

        // ================================================================ vehicles

        public static Node3D BuildVehicle(UnitDef unit, Color team, out Vector3 muzzle, out Node3D turret)
        {
            var asset = TryLoadAsset(unit.Id);
            muzzle = new Vector3(0f, 2f, 4f);
            turret = null;
            if (asset != null)
            {
                return asset;
            }

            string id = unit.Id;
            if (id.Contains("merkava"))
            {
                return VehicleModels.Merkava(team, out muzzle, out turret);
            }

            if (id.Contains("namer"))
            {
                return VehicleModels.Namer(team, out muzzle, out turret);
            }

            if (id.Contains("zu23") || id.Contains("technical"))
            {
                return VehicleModels.Technical(team, out muzzle, out turret);
            }

            var root = new Node3D();
            MeshKit.Box(root, new Vector3(3.2f, 1.6f, 6.5f), new Vector3(0f, 1.1f, 0f), Sand);
            MeshKit.Box(root, new Vector3(3.22f, 0.15f, 6.0f), new Vector3(0f, 1.6f, 0f), team);
            return root;
        }

        // ================================================================ structures

        /// <summary>Structure model with origin at the footprint center (ground level); see <see cref="StructureModels"/>.</summary>
        public static Node3D BuildStructure(StructureDef def, float width, float depth, Color team)
        {
            var asset = TryLoadAsset(def.Id);
            if (asset != null)
            {
                return asset;
            }

            var root = new Node3D();
            if (def.Kind == StructureKind.Sandbags)
            {
                StructureModels.Sandbags(root, width, depth);
            }
            else
            {
                StructureModels.Build(root, def, width, depth, team);
            }

            return root;
        }

        // ================================================================ map dressing

        public static Node3D BuildObstacle(MapObstacle o, float cellSize, int index)
        {
            var root = new Node3D();
            float w = (o.MaxX - o.MinX + 1) * cellSize;
            float d = (o.MaxY - o.MinY + 1) * cellSize;
            switch (o.Kind)
            {
                case ObstacleKind.Building:
                    Building(root, w, o.Height, d, index);
                    break;
                case ObstacleKind.Wall:
                    var stone = MeshKit.Vary(new Color(0.7f, 0.65f, 0.54f), 0.05f, index);
                    MeshKit.BoxT(root, new Vector3(w * 0.9f, o.Height, d * 0.9f), new Vector3(0f, o.Height * 0.5f, 0f), stone, Tex.Stone);
                    MeshKit.BoxT(root, new Vector3(w * 0.95f, 0.12f, d * 0.95f), new Vector3(0f, o.Height, 0f), stone.Darkened(0.15f), Tex.Concrete);
                    break;
                case ObstacleKind.Fence:
                    Fence(root, w, d);
                    break;
                default:
                    Rock(root, w, o.Height, d, index);
                    break;
            }

            return root;
        }

        /// <summary>
        /// Map building: plastered or bare concrete block with windows, a parapet, and a varied roofscape (water tanks,
        /// stairwell, AC units, satellite dish, rebar); some have balconies or a shell-damaged corner.
        /// </summary>
        private static void Building(Node3D root, float w, float h, float d, int index)
        {
            int style = index % 4;
            var color = style == 0 ? new Color(0.8f, 0.74f, 0.62f) : style == 1 ? Concrete : style == 2 ? new Color(0.76f, 0.68f, 0.54f) : new Color(0.78f, 0.76f, 0.7f);
            var plaster = MeshKit.Vary(color, 0.05f, index);
            var tex = style == 1 ? Tex.Concrete : Tex.Plaster;
            bool damaged = MeshKit.Hash01(index, 7) > 0.72f && h > 5f;
            if (damaged)
            {
                // Top floor partly collapsed at one corner.
                MeshKit.BoxT(root, new Vector3(w, h - 3f, d), new Vector3(0f, (h - 3f) * 0.5f, 0f), plaster, tex);
                MeshKit.BoxT(root, new Vector3(w * 0.6f, 3f, d), new Vector3(-w * 0.2f, h - 1.5f, 0f), plaster, tex);
                MeshKit.BoxT(root, new Vector3(w * 0.4f, 3f, d * 0.5f), new Vector3(w * 0.3f, h - 1.5f, -d * 0.25f), plaster, tex);
                for (int i = 0; i < 4; i++)
                {
                    MeshKit.Beam(root, new Vector3(w * 0.1f + i * 0.5f, h - 3f, d * 0.4f), new Vector3(w * 0.12f + i * 0.55f, h - 2f + (i % 2) * 0.5f, d * 0.45f), 0.03f, new Color(0.35f, 0.22f, 0.15f), 3);
                }
            }
            else
            {
                MeshKit.BoxT(root, new Vector3(w, h, d), new Vector3(0f, h * 0.5f, 0f), plaster, tex);
                MeshKit.BoxT(root, new Vector3(w + 0.2f, 0.4f, d + 0.2f), new Vector3(0f, h + 0.1f, 0f), plaster.Darkened(0.15f), tex);
            }

            var window = new Color(0.12f, 0.13f, 0.15f);
            var shutter = MeshKit.Hash01(index, 9) > 0.5f ? new Color(0.27f, 0.42f, 0.45f) : new Color(0.45f, 0.33f, 0.22f);
            int floors = Mathf.Max(1, (int)(h / 3f));
            for (int f = 0; f < floors; f++)
            {
                float y = 1.6f + f * 3f;
                if (y > h - 0.8f)
                {
                    break;
                }

                for (float x = -w * 0.5f + 1.5f; x <= w * 0.5f - 1.2f; x += 2.6f)
                {
                    bool shut = MeshKit.Hash01(index * 31 + f, (int)(x * 10f)) > 0.75f;
                    var c = shut ? shutter : window;
                    MeshKit.Box(root, new Vector3(1.0f, 1.2f, 0.08f), new Vector3(x, y, d * 0.5f + 0.02f), c);
                    MeshKit.Box(root, new Vector3(1.0f, 1.2f, 0.08f), new Vector3(x, y, -d * 0.5f - 0.02f), c);
                    MeshKit.Box(root, new Vector3(1.2f, 0.08f, 0.16f), new Vector3(x, y - 0.66f, d * 0.5f + 0.06f), plaster.Darkened(0.12f)); // sill
                }

                for (float z = -d * 0.5f + 1.5f; z <= d * 0.5f - 1.2f; z += 2.6f)
                {
                    MeshKit.Box(root, new Vector3(0.08f, 1.2f, 1.0f), new Vector3(w * 0.5f + 0.02f, y, z), window);
                    MeshKit.Box(root, new Vector3(0.08f, 1.2f, 1.0f), new Vector3(-w * 0.5f - 0.02f, y, z), window);
                }

                if (f > 0 && MeshKit.Hash01(index, f + 40) > 0.6f)
                {
                    MeshKit.BoxT(root, new Vector3(2.6f, 0.15f, 1.0f), new Vector3(0f, y - 0.75f, d * 0.5f + 0.5f), plaster.Darkened(0.1f), tex); // balcony
                    MeshKit.Box(root, new Vector3(2.6f, 0.7f, 0.06f), new Vector3(0f, y - 0.35f, d * 0.5f + 0.98f), new Color(0.2f, 0.2f, 0.2f));
                }
            }

            MeshKit.Box(root, new Vector3(1.1f, 2.1f, 0.08f), new Vector3(w * 0.3f, 1.05f, d * 0.5f + 0.03f), new Color(0.25f, 0.2f, 0.16f)); // door
            float roof = h + 0.3f;
            if (!damaged && MeshKit.Hash01(index, 4) > 0.4f)
            {
                MeshKit.BoxT(root, new Vector3(2.2f, 2.4f, 2.2f), new Vector3(-w * 0.28f, roof + 1.2f, -d * 0.22f), plaster.Darkened(0.05f), tex); // stairwell
            }

            if (MeshKit.Hash01(index, 5) > 0.3f)
            {
                MeshKit.Cyl(root, 0.55f, 0.55f, 1.1f, new Vector3(w * 0.25f, roof + 0.85f, -d * 0.2f), new Color(0.15f, 0.15f, 0.16f), null, 10); // water tank
                MeshKit.Box(root, new Vector3(1.1f, 0.3f, 1.1f), new Vector3(w * 0.25f, roof + 0.15f, -d * 0.2f), new Color(0.3f, 0.3f, 0.3f));
            }

            if (MeshKit.Hash01(index, 6) > 0.5f)
            {
                MeshKit.Box(root, new Vector3(1.0f, 0.6f, 0.8f), new Vector3(-w * 0.15f, roof + 0.3f, d * 0.25f), new Color(0.78f, 0.78f, 0.75f)); // AC unit
            }

            if (MeshKit.Hash01(index, 8) > 0.55f)
            {
                var dish = MeshKit.Sphere(root, 0.5f, new Vector3(w * 0.3f, roof + 0.6f, d * 0.3f), new Color(0.88f, 0.88f, 0.85f), true, 10, new Vector3(1f, 0.35f, 1f));
                dish.RotationDegrees = new Vector3(140f, 30f, 0f);
            }
        }

        private static void Fence(Node3D root, float w, float d)
        {
            bool alongX = w >= d;
            float len = alongX ? w : d;
            var wood = new Color(0.45f, 0.36f, 0.25f);
            for (float t = -len * 0.5f; t <= len * 0.5f + 0.01f; t += 2f)
            {
                var p = alongX ? new Vector3(t, 0.55f, 0f) : new Vector3(0f, 0.55f, t);
                MeshKit.Box(root, new Vector3(0.12f, 1.1f, 0.12f), p, wood);
            }

            foreach (float y in new[] { 0.45f, 0.9f })
            {
                var size = alongX ? new Vector3(len, 0.08f, 0.06f) : new Vector3(0.06f, 0.08f, len);
                MeshKit.Box(root, size, new Vector3(0f, y, 0f), wood.Lightened(0.1f));
            }
        }

        /// <summary>Rock outcrop: a cluster of faceted boulders.</summary>
        private static void Rock(Node3D root, float w, float h, float d, int index)
        {
            var color = MeshKit.Vary(new Color(0.56f, 0.53f, 0.47f), 0.08f, index);
            int count = 3 + (int)(MeshKit.Hash01(index, 1) * 3f);
            for (int i = 0; i < count; i++)
            {
                float ox = (MeshKit.Hash01(index, i * 3) - 0.5f) * w * 0.55f;
                float oz = (MeshKit.Hash01(index, i * 3 + 1) - 0.5f) * d * 0.55f;
                float r = Mathf.Min(w, d) * (0.3f + MeshKit.Hash01(index, i * 3 + 2) * 0.25f);
                MeshKit.Faceted(root, r, new Vector3(ox, r * 0.35f, oz), i % 2 == 0 ? color : color.Darkened(0.08f), index * 7 + i, new Vector3(1f, Mathf.Clamp(h / (r * 1.6f), 0.5f, 1.4f), 1f), Tex.Stone, 0.3f, 7);
            }
        }

        public static Node3D BuildTree(int seed) => NatureModels.Build(PlantKind.Olive, seed);

        public static Node3D BuildBush(int seed) => NatureModels.Build(PlantKind.Bush, seed);
    }
}
