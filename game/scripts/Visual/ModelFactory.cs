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
    }

    /// <summary>
    /// Procedural low-poly models for units, structures and map dressing ("rough graphics, not boxes").
    /// Any model can be replaced by an artist asset: drop res://assets/models/&lt;id&gt;.glb (or .tscn) and it is used instead.
    /// </summary>
    public static class ModelFactory
    {
        private static readonly Color Skin1 = new Color(0.78f, 0.60f, 0.45f);
        private static readonly Color Skin2 = new Color(0.62f, 0.45f, 0.32f);
        private static readonly Color Boots = new Color(0.16f, 0.13f, 0.10f);
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

        public static SoldierRig BuildSoldier(UnitDef unit, WeaponKind kind, Color team, int index, bool carriesSpecialWeapon)
        {
            string faction = unit.FactionId;
            var rig = new SoldierRig { Root = new Node3D { Scale = Vector3.One * 1.3f } };
            rig.Pose = new Node3D();
            rig.Root.AddChild(rig.Pose);

            Color uniform, pants, vest, headgear;
            bool helmet, balaclava, cap;
            int seed = index * 7 + unit.Id.Length;
            switch (faction)
            {
                case "hamas":
                    var shirts = new[] { new Color(0.45f, 0.42f, 0.32f), new Color(0.20f, 0.24f, 0.17f), new Color(0.13f, 0.13f, 0.13f), new Color(0.36f, 0.30f, 0.24f) };
                    uniform = shirts[(index + unit.Id.Length) % shirts.Length];
                    pants = new Color(0.15f, 0.16f, 0.14f);
                    vest = new Color(0.21f, 0.23f, 0.16f);
                    headgear = new Color(0.08f, 0.08f, 0.08f);
                    helmet = false;
                    balaclava = true;
                    cap = false;
                    break;
                case "hezbollah":
                    uniform = MeshKit.Vary(new Color(0.27f, 0.31f, 0.20f), 0.06f, seed);
                    pants = uniform.Darkened(0.1f);
                    vest = new Color(0.21f, 0.24f, 0.15f);
                    headgear = new Color(0.24f, 0.27f, 0.17f);
                    helmet = index % 3 == 0;
                    balaclava = false;
                    cap = !helmet;
                    break;
                default: // idf
                    uniform = MeshKit.Vary(new Color(0.35f, 0.37f, 0.24f), 0.04f, seed);
                    pants = uniform.Darkened(0.06f);
                    vest = new Color(0.25f, 0.27f, 0.17f);
                    headgear = new Color(0.32f, 0.34f, 0.21f);
                    helmet = true;
                    balaclava = false;
                    cap = false;
                    break;
            }

            var skin = MeshKit.Hash01(seed) > 0.5f ? Skin1 : Skin2;

            // Legs (pivot at the hip).
            rig.LegL = Leg(rig.Pose, -0.1f, pants);
            rig.LegR = Leg(rig.Pose, 0.1f, pants);

            rig.UpperBody = new Node3D { Position = new Vector3(0f, 0.84f, 0f) };
            rig.Pose.AddChild(rig.UpperBody);
            var ub = rig.UpperBody;
            MeshKit.Box(ub, new Vector3(0.42f, 0.56f, 0.24f), new Vector3(0f, 0.3f, 0f), uniform);
            MeshKit.Box(ub, new Vector3(0.46f, 0.38f, 0.3f), new Vector3(0f, 0.32f, 0.01f), vest);
            MeshKit.Box(ub, new Vector3(0.3f, 0.34f, 0.16f), new Vector3(0f, 0.36f, -0.2f), vest.Darkened(0.15f)); // backpack
            MeshKit.Sphere(ub, 0.12f, new Vector3(0f, 0.72f, 0f), balaclava ? headgear : skin, false, 8);

            if (helmet)
            {
                MeshKit.Sphere(ub, 0.15f, new Vector3(0f, 0.74f, 0f), headgear, true, 8);
                MeshKit.Cyl(ub, 0.152f, 0.152f, 0.04f, new Vector3(0f, 0.755f, 0f), headgear.Darkened(0.35f), null, 10);
            }
            else if (cap)
            {
                MeshKit.Cyl(ub, 0.13f, 0.135f, 0.09f, new Vector3(0f, 0.81f, 0f), headgear, null, 8);
                MeshKit.Box(ub, new Vector3(0.18f, 0.02f, 0.12f), new Vector3(0f, 0.775f, 0.12f), headgear);
                MeshKit.Cyl(ub, 0.128f, 0.128f, 0.03f, new Vector3(0f, 0.775f, 0f), headgear.Darkened(0.35f), null, 10);
            }
            else
            {
                MeshKit.Cyl(ub, 0.125f, 0.125f, 0.04f, new Vector3(0f, 0.77f, 0f), new Color(0.22f, 0.27f, 0.14f), null, 10); // headband
            }

            // Arms hold the weapon forward (pivot at the shoulder).
            rig.ArmL = Arm(ub, -0.27f, uniform, team, false);
            rig.ArmR = Arm(ub, 0.27f, uniform, team, true);

            BuildWeapon(rig, kind, carriesSpecialWeapon, unit.Engineer);
            return rig;
        }

        private static Node3D Leg(Node3D parent, float x, Color pants)
        {
            var pivot = new Node3D { Position = new Vector3(x, 0.84f, 0f) };
            parent.AddChild(pivot);
            MeshKit.Box(pivot, new Vector3(0.15f, 0.8f, 0.17f), new Vector3(0f, -0.4f, 0f), pants);
            MeshKit.Box(pivot, new Vector3(0.16f, 0.1f, 0.25f), new Vector3(0f, -0.79f, 0.04f), Boots);
            return pivot;
        }

        private static Node3D Arm(Node3D upperBody, float x, Color sleeve, Color team, bool armband)
        {
            var pivot = new Node3D { Position = new Vector3(x, 0.53f, 0.02f), RotationDegrees = new Vector3(-65f, 0f, x > 0 ? 8f : -12f) };
            upperBody.AddChild(pivot);
            MeshKit.Box(pivot, new Vector3(0.11f, 0.5f, 0.12f), new Vector3(0f, -0.22f, 0f), sleeve);
            if (armband)
            {
                MeshKit.Box(pivot, new Vector3(0.14f, 0.11f, 0.15f), new Vector3(0f, -0.1f, 0f), team);
            }

            return pivot;
        }

        private static void BuildWeapon(SoldierRig rig, WeaponKind kind, bool special, bool engineer)
        {
            var ub = rig.UpperBody;
            if (!special || kind == WeaponKind.SmallArms)
            {
                float len = engineer ? 0.62f : 0.8f;
                MeshKit.Box(ub, new Vector3(0.06f, 0.09f, len), new Vector3(0.08f, 0.38f, 0.32f), Gunmetal);
                MeshKit.Box(ub, new Vector3(0.05f, 0.14f, 0.07f), new Vector3(0.08f, 0.3f, 0.3f), Gunmetal); // magazine
                if (engineer)
                {
                    MeshKit.Box(ub, new Vector3(0.05f, 0.75f, 0.05f), new Vector3(-0.12f, 0.42f, -0.28f), new Color(0.4f, 0.3f, 0.2f), new Vector3(0f, 0f, 20f)); // shovel
                }

                rig.Muzzle = new Vector3(0.08f, 0.84f + 0.38f, 0.32f + len * 0.5f);
                return;
            }

            switch (kind)
            {
                case WeaponKind.MachineGun:
                    MeshKit.Box(ub, new Vector3(0.09f, 0.13f, 1.1f), new Vector3(0.08f, 0.36f, 0.38f), Gunmetal);
                    MeshKit.Box(ub, new Vector3(0.14f, 0.14f, 0.16f), new Vector3(0.08f, 0.28f, 0.2f), new Color(0.25f, 0.27f, 0.17f)); // ammo box
                    rig.Muzzle = new Vector3(0.08f, 1.2f, 0.95f);
                    break;
                case WeaponKind.Sniper:
                    MeshKit.Box(ub, new Vector3(0.06f, 0.09f, 1.3f), new Vector3(0.08f, 0.38f, 0.42f), Gunmetal);
                    MeshKit.Cyl(ub, 0.035f, 0.035f, 0.3f, new Vector3(0.08f, 0.46f, 0.3f), new Color(0.05f, 0.05f, 0.05f), new Vector3(90f, 0f, 0f), 6);
                    rig.Muzzle = new Vector3(0.08f, 1.22f, 1.1f);
                    break;
                case WeaponKind.AntiTank:
                    // Shoulder tube (RPG) — launchers on tripods are added by the squad view for setup weapons.
                    MeshKit.Cyl(ub, 0.065f, 0.065f, 1.15f, new Vector3(0.2f, 0.62f, 0.05f), new Color(0.24f, 0.27f, 0.17f), new Vector3(90f, 0f, 0f), 8);
                    MeshKit.Cyl(ub, 0.11f, 0.05f, 0.3f, new Vector3(0.2f, 0.62f, 0.72f), new Color(0.3f, 0.33f, 0.2f), new Vector3(90f, 0f, 0f), 8);
                    rig.Muzzle = new Vector3(0.2f, 1.46f, 0.85f);
                    break;
                default:
                    MeshKit.Box(ub, new Vector3(0.06f, 0.09f, 0.8f), new Vector3(0.08f, 0.38f, 0.32f), Gunmetal);
                    rig.Muzzle = new Vector3(0.08f, 1.22f, 0.72f);
                    break;
            }
        }

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

            var root = new Node3D();
            string id = unit.Id;
            if (id.Contains("merkava"))
            {
                BuildMerkava(root, team, out muzzle, out turret);
            }
            else if (id.Contains("namer"))
            {
                BuildNamer(root, team, out muzzle, out turret);
            }
            else if (id.Contains("zu23") || id.Contains("technical"))
            {
                BuildTechnical(root, team, out muzzle, out turret);
            }
            else
            {
                MeshKit.Box(root, new Vector3(3.2f, 1.6f, 6.5f), new Vector3(0f, 1.1f, 0f), Sand);
                MeshKit.Box(root, new Vector3(3.22f, 0.15f, 6.0f), new Vector3(0f, 1.6f, 0f), team);
                Tracks(root, 3.4f, 6.8f);
            }

            return root;
        }

        private static void Tracks(Node3D root, float width, float length)
        {
            var dark = new Color(0.14f, 0.13f, 0.12f);
            foreach (float side in new[] { -1f, 1f })
            {
                MeshKit.Box(root, new Vector3(0.62f, 0.85f, length), new Vector3(side * (width * 0.5f - 0.31f), 0.43f, 0f), dark);
                for (int i = 0; i < 6; i++)
                {
                    float z = -length * 0.42f + i * length * 0.168f;
                    MeshKit.Cyl(root, 0.32f, 0.32f, 0.66f, new Vector3(side * (width * 0.5f - 0.31f), 0.38f, z), new Color(0.22f, 0.21f, 0.2f), new Vector3(0f, 0f, 90f), 8);
                }
            }
        }

        private static void BuildMerkava(Node3D root, Color team, out Vector3 muzzle, out Node3D turret)
        {
            var tan = new Color(0.62f, 0.57f, 0.43f);
            Tracks(root, 3.7f, 8.4f);
            MeshKit.Box(root, new Vector3(3.0f, 0.95f, 7.4f), new Vector3(0f, 1.15f, -0.2f), tan);
            MeshKit.Box(root, new Vector3(3.0f, 0.18f, 2.6f), new Vector3(0f, 1.42f, 3.65f), tan.Darkened(0.05f), new Vector3(16f, 0f, 0f)); // long glacis
            foreach (float side in new[] { -1f, 1f })
            {
                MeshKit.Box(root, new Vector3(0.1f, 0.65f, 7.8f), new Vector3(side * 1.9f, 0.82f, 0f), tan.Darkened(0.08f)); // side skirts
                MeshKit.Box(root, new Vector3(0.11f, 0.14f, 6.8f), new Vector3(side * 1.91f, 0.95f, 0f), team);
            }

            turret = new Node3D { Position = new Vector3(0f, 1.62f, -1.1f) };
            root.AddChild(turret);
            MeshKit.Box(turret, new Vector3(2.7f, 0.85f, 3.4f), new Vector3(0f, 0.43f, -0.2f), tan);
            MeshKit.Box(turret, new Vector3(2.3f, 0.7f, 1.6f), new Vector3(0f, 0.38f, 1.9f), tan.Darkened(0.04f), new Vector3(-22f, 0f, 0f)); // wedge nose
            MeshKit.Box(turret, new Vector3(2.72f, 0.14f, 3.0f), new Vector3(0f, 0.8f, -0.5f), team);
            MeshKit.Cyl(turret, 0.11f, 0.13f, 5.2f, new Vector3(0f, 0.48f, 4.3f), tan.Darkened(0.12f), new Vector3(90f, 0f, 0f), 8);
            MeshKit.Cyl(turret, 0.28f, 0.28f, 0.35f, new Vector3(0.7f, 1.0f, -0.6f), tan.Darkened(0.1f), null, 8); // commander cupola
            MeshKit.Box(turret, new Vector3(0.08f, 0.1f, 0.9f), new Vector3(-0.6f, 1.0f, 0.2f), Gunmetal);
            MeshKit.Box(turret, new Vector3(0.9f, 0.6f, 0.9f), new Vector3(1.2f, 0.5f, 0.6f), tan.Darkened(0.06f)); // Trophy panel
            MeshKit.Box(turret, new Vector3(0.9f, 0.6f, 0.9f), new Vector3(-1.2f, 0.5f, 0.6f), tan.Darkened(0.06f));
            muzzle = new Vector3(0f, 1.62f + 0.48f, -1.1f + 6.9f);
        }

        private static void BuildNamer(Node3D root, Color team, out Vector3 muzzle, out Node3D turret)
        {
            var tan = new Color(0.60f, 0.55f, 0.42f);
            Tracks(root, 3.7f, 7.8f);
            MeshKit.Box(root, new Vector3(3.2f, 1.75f, 6.8f), new Vector3(0f, 1.5f, -0.3f), tan);
            MeshKit.Box(root, new Vector3(3.1f, 0.2f, 1.9f), new Vector3(0f, 1.85f, 3.35f), tan.Darkened(0.05f), new Vector3(28f, 0f, 0f));
            MeshKit.Box(root, new Vector3(3.22f, 0.16f, 6.0f), new Vector3(0f, 2.0f, -0.4f), team);
            MeshKit.Box(root, new Vector3(1.4f, 1.2f, 0.12f), new Vector3(0f, 1.4f, -3.72f), tan.Darkened(0.15f)); // rear ramp
            turret = new Node3D { Position = new Vector3(0.6f, 2.4f, 0.4f) };
            root.AddChild(turret);
            MeshKit.Box(turret, new Vector3(0.7f, 0.45f, 0.8f), new Vector3(0f, 0.22f, 0f), tan.Darkened(0.1f));
            MeshKit.Cyl(turret, 0.05f, 0.05f, 1.5f, new Vector3(0f, 0.3f, 0.9f), Gunmetal, new Vector3(90f, 0f, 0f), 6);
            muzzle = new Vector3(0.6f, 2.7f, 2.05f);
        }

        private static void BuildTechnical(Node3D root, Color team, out Vector3 muzzle, out Node3D turret)
        {
            var paint = new Color(0.84f, 0.82f, 0.76f);
            MeshKit.Box(root, new Vector3(1.95f, 0.45f, 5.0f), new Vector3(0f, 0.75f, 0f), paint);
            MeshKit.Box(root, new Vector3(1.85f, 0.95f, 1.7f), new Vector3(0f, 1.45f, 1.25f), paint);
            MeshKit.Box(root, new Vector3(1.7f, 0.5f, 0.06f), new Vector3(0f, 1.6f, 2.11f), new Color(0.15f, 0.2f, 0.25f), new Vector3(-15f, 0f, 0f)); // windshield
            MeshKit.Box(root, new Vector3(1.96f, 0.16f, 1.2f), new Vector3(0f, 1.2f, 1.25f), team);
            foreach (float side in new[] { -1f, 1f })
            {
                MeshKit.Box(root, new Vector3(0.06f, 0.4f, 2.6f), new Vector3(side * 0.95f, 1.15f, -1.1f), paint.Darkened(0.1f)); // bed walls
                foreach (float z in new[] { 1.6f, -1.6f })
                {
                    MeshKit.Cyl(root, 0.42f, 0.42f, 0.3f, new Vector3(side * 0.95f, 0.42f, z), new Color(0.1f, 0.1f, 0.1f), new Vector3(0f, 0f, 90f), 10);
                }
            }

            turret = new Node3D { Position = new Vector3(0f, 1.15f, -1.2f) };
            root.AddChild(turret);
            MeshKit.Box(turret, new Vector3(1.1f, 0.35f, 1.0f), new Vector3(0f, 0.2f, 0f), new Color(0.3f, 0.33f, 0.22f));
            MeshKit.Box(turret, new Vector3(0.9f, 0.5f, 0.12f), new Vector3(0f, 0.65f, 0.3f), new Color(0.3f, 0.33f, 0.22f)); // shield
            foreach (float x in new[] { -0.18f, 0.18f })
            {
                MeshKit.Cyl(turret, 0.04f, 0.04f, 2.1f, new Vector3(x, 0.75f, 1.2f), Gunmetal, new Vector3(75f, 0f, 0f), 6);
            }

            muzzle = new Vector3(0f, 2.2f, 0.9f);
        }

        // ================================================================ structures

        /// <summary>Structure model with origin at the footprint center (ground level).</summary>
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
                BuildSandbags(root, width, depth);
                return root;
            }

            string f = def.FactionId;
            int tier = def.Tier;
            if (f == "idf")
            {
                if (tier == 0) IdfHq(root, width, depth, team);
                else if (tier == 1) Tent(root, width, depth, new Color(0.36f, 0.38f, 0.25f), team);
                else if (tier == 2) Compound(root, width, depth, new Color(0.7f, 0.68f, 0.62f), team, true);
                else Hangar(root, width, depth, new Color(0.62f, 0.57f, 0.43f), team);
            }
            else if (f == "hamas")
            {
                if (tier == 0) Bunker(root, width, depth, new Color(0.62f, 0.58f, 0.5f), team, false);
                else if (tier == 1) Shed(root, width, depth, new Color(0.5f, 0.38f, 0.28f), team);
                else if (tier == 2) Compound(root, width, depth, new Color(0.72f, 0.66f, 0.55f), team, false);
                else WalledCompound(root, width, depth, new Color(0.7f, 0.64f, 0.52f), team);
            }
            else
            {
                if (tier == 0) Bunker(root, width, depth, new Color(0.45f, 0.42f, 0.33f), team, true);
                else if (tier == 1) Tent(root, width, depth, new Color(0.3f, 0.34f, 0.22f), team);
                else if (tier == 2) Bunker(root, width, depth, new Color(0.55f, 0.53f, 0.47f), team, true);
                else Hangar(root, width, depth, new Color(0.38f, 0.4f, 0.3f), team);
            }

            return root;
        }

        private static void Flag(Node3D root, Vector3 pos, Color team, float height = 7f)
        {
            MeshKit.Cyl(root, 0.06f, 0.08f, height, pos + new Vector3(0f, height * 0.5f, 0f), new Color(0.25f, 0.25f, 0.25f), null, 6);
            MeshKit.Box(root, new Vector3(0.05f, 0.9f, 1.5f), pos + new Vector3(0f, height - 0.5f, 0.78f), team);
        }

        private static void SandbagRow(Node3D root, Vector3 from, Vector3 to, int layers = 2)
        {
            var color = new Color(0.68f, 0.62f, 0.47f);
            var dir = to - from;
            float len = dir.Length();
            int n = Mathf.Max(1, (int)(len / 0.85f));
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

                    var p = from + dir * t + new Vector3(0f, 0.2f + l * 0.32f, 0f);
                    var bag = MeshKit.Sphere(root, 0.42f, p, MeshKit.Vary(color, 0.06f, i + l * 31), false, 6, new Vector3(1.05f, 0.42f, 0.62f));
                    bag.Rotation = new Vector3(0f, yaw + Mathf.Pi * 0.5f, 0f);
                }
            }
        }

        private static void BuildSandbags(Node3D root, float width, float depth)
        {
            bool alongX = width >= depth;
            float len = (alongX ? width : depth) * 0.5f - 0.2f;
            var from = alongX ? new Vector3(-len, 0f, 0f) : new Vector3(0f, 0f, -len);
            var to = alongX ? new Vector3(len, 0f, 0f) : new Vector3(0f, 0f, len);
            SandbagRow(root, from, to, 3);
        }

        private static void IdfHq(Node3D root, float w, float d, Color team)
        {
            var olive = new Color(0.36f, 0.38f, 0.26f);
            var tan = new Color(0.62f, 0.58f, 0.46f);
            MeshKit.Box(root, new Vector3(w * 0.8f, 0.15f, d * 0.8f), new Vector3(0f, 0.07f, 0f), new Color(0.5f, 0.47f, 0.4f));
            MeshKit.Box(root, new Vector3(6f, 2.6f, 2.4f), new Vector3(0f, 1.3f, -2.6f), olive);
            MeshKit.Box(root, new Vector3(2.4f, 2.6f, 5f), new Vector3(-3.4f, 1.3f, 0.5f), tan);
            MeshKit.Box(root, new Vector3(2.4f, 2.6f, 5f), new Vector3(3.4f, 1.3f, 0.5f), olive.Darkened(0.05f));
            MeshKit.Box(root, new Vector3(8.5f, 0.08f, 6.5f), new Vector3(0f, 3.2f, 0f), new Color(0.24f, 0.29f, 0.17f)); // camo net
            MeshKit.Cyl(root, 0.08f, 0.12f, 11f, new Vector3(4.2f, 5.5f, -3.6f), new Color(0.3f, 0.3f, 0.3f), null, 6); // antenna
            MeshKit.Box(root, new Vector3(1.2f, 0.12f, 1.2f), new Vector3(4.2f, 9.5f, -3.6f), team);
            SandbagRow(root, new Vector3(-w * 0.42f, 0f, d * 0.42f), new Vector3(-1.5f, 0f, d * 0.42f));
            SandbagRow(root, new Vector3(1.5f, 0f, d * 0.42f), new Vector3(w * 0.42f, 0f, d * 0.42f));
            Flag(root, new Vector3(-4.5f, 0f, 3.6f), team);
        }

        private static void Tent(Node3D root, float w, float d, Color canvas, Color team)
        {
            MeshKit.Prism(root, new Vector3(w * 0.75f, 3.2f, d * 0.6f), new Vector3(0f, 1.6f, -0.5f), canvas);
            MeshKit.Box(root, new Vector3(w * 0.75f, 0.2f, 0.4f), new Vector3(0f, 0.1f, d * 0.12f), canvas.Darkened(0.2f));
            MeshKit.Box(root, new Vector3(2.4f, 2.2f, 2.0f), new Vector3(w * 0.28f, 1.1f, d * 0.3f), canvas.Darkened(0.1f));
            SandbagRow(root, new Vector3(-w * 0.45f, 0f, d * 0.42f), new Vector3(-w * 0.05f, 0f, d * 0.42f));
            Flag(root, new Vector3(-w * 0.4f, 0f, -d * 0.35f), team, 5f);
        }

        private static void Compound(Node3D root, float w, float d, Color wall, Color team, bool shed)
        {
            MeshKit.Box(root, new Vector3(w * 0.7f, 3.6f, d * 0.55f), new Vector3(-w * 0.1f, 1.8f, -d * 0.18f), wall);
            MeshKit.Box(root, new Vector3(w * 0.72f, 0.3f, d * 0.57f), new Vector3(-w * 0.1f, 3.75f, -d * 0.18f), wall.Darkened(0.2f));
            for (int i = 0; i < 3; i++)
            {
                MeshKit.Box(root, new Vector3(0.9f, 1.0f, 0.06f), new Vector3(-w * 0.35f + i * 2.4f, 2.3f, -d * 0.18f + d * 0.275f + 0.02f), new Color(0.12f, 0.13f, 0.14f));
            }

            if (shed)
            {
                MeshKit.Prism(root, new Vector3(w * 0.5f, 1.6f, d * 0.4f), new Vector3(w * 0.22f, 3.4f, d * 0.27f), new Color(0.45f, 0.45f, 0.42f));
                MeshKit.Box(root, new Vector3(0.2f, 2.6f, 0.2f), new Vector3(w * 0.45f, 1.3f, d * 0.43f), new Color(0.35f, 0.35f, 0.33f));
                MeshKit.Box(root, new Vector3(0.2f, 2.6f, 0.2f), new Vector3(-w * 0.02f, 1.3f, d * 0.43f), new Color(0.35f, 0.35f, 0.33f));
            }
            else
            {
                for (int i = 0; i < 4; i++)
                {
                    MeshKit.Cyl(root, 0.32f, 0.32f, 0.95f, new Vector3(w * 0.3f + (i % 2) * 0.7f, 0.48f, d * 0.25f + (i / 2) * 0.7f), new Color(0.3f, 0.35f, 0.25f), null, 8);
                }
            }

            Flag(root, new Vector3(w * 0.38f, 0f, -d * 0.38f), team, 5.5f);
        }

        private static void Hangar(Node3D root, float w, float d, Color color, Color team)
        {
            MeshKit.Box(root, new Vector3(w * 0.85f, 3.2f, d * 0.7f), new Vector3(0f, 1.6f, -d * 0.05f), color);
            MeshKit.Prism(root, new Vector3(w * 0.9f, 2.4f, d * 0.74f), new Vector3(0f, 4.4f, -d * 0.05f), color.Darkened(0.12f));
            MeshKit.Box(root, new Vector3(w * 0.5f, 2.8f, 0.1f), new Vector3(0f, 1.4f, d * 0.31f), new Color(0.1f, 0.1f, 0.1f)); // open doors
            MeshKit.Box(root, new Vector3(w * 0.86f, 0.2f, 0.12f), new Vector3(0f, 3.0f, d * 0.31f), team);
            Flag(root, new Vector3(w * 0.45f, 0f, d * 0.4f), team, 6f);
        }

        private static void Bunker(Node3D root, float w, float d, Color color, Color team, bool camoNet)
        {
            MeshKit.Sphere(root, w * 0.5f, new Vector3(0f, 0f, 0f), color.Darkened(0.15f), true, 10, new Vector3(1f, 0.35f, d / w)); // earth mound
            MeshKit.Box(root, new Vector3(w * 0.5f, 2.0f, d * 0.4f), new Vector3(0f, 1.0f, d * 0.18f), color);
            MeshKit.Box(root, new Vector3(1.8f, 1.6f, 0.1f), new Vector3(0f, 0.8f, d * 0.38f + 0.01f), new Color(0.08f, 0.08f, 0.08f)); // entrance
            MeshKit.Box(root, new Vector3(w * 0.52f, 0.25f, d * 0.42f), new Vector3(0f, 2.1f, d * 0.18f), color.Darkened(0.2f));
            if (camoNet)
            {
                MeshKit.Box(root, new Vector3(w * 0.75f, 0.08f, d * 0.7f), new Vector3(0f, 2.6f, 0f), new Color(0.26f, 0.31f, 0.18f));
            }

            SandbagRow(root, new Vector3(-w * 0.4f, 0f, d * 0.45f), new Vector3(-1.4f, 0f, d * 0.45f));
            SandbagRow(root, new Vector3(1.4f, 0f, d * 0.45f), new Vector3(w * 0.4f, 0f, d * 0.45f));
            MeshKit.Cyl(root, 0.06f, 0.09f, 7f, new Vector3(w * 0.3f, 3.5f, -d * 0.25f), new Color(0.3f, 0.3f, 0.3f), null, 6);
            Flag(root, new Vector3(-w * 0.38f, 0f, -d * 0.3f), team, 5.5f);
        }

        private static void Shed(Node3D root, float w, float d, Color color, Color team)
        {
            MeshKit.Box(root, new Vector3(w * 0.75f, 3.0f, d * 0.6f), new Vector3(0f, 1.5f, -d * 0.1f), color);
            MeshKit.Box(root, new Vector3(w * 0.8f, 0.12f, d * 0.68f), new Vector3(0f, 3.1f, -d * 0.1f), new Color(0.55f, 0.52f, 0.48f), new Vector3(6f, 0f, 0f));
            MeshKit.Box(root, new Vector3(2.4f, 2.4f, 0.08f), new Vector3(0f, 1.2f, d * 0.2f + 0.01f), new Color(0.12f, 0.1f, 0.08f));
            for (int i = 0; i < 3; i++)
            {
                MeshKit.Cyl(root, 0.3f, 0.3f, 0.9f, new Vector3(w * 0.32f, 0.45f, d * 0.3f - i * 0.7f), new Color(0.35f, 0.25f, 0.18f), null, 8);
            }

            Flag(root, new Vector3(-w * 0.4f, 0f, d * 0.38f), team, 5f);
        }

        private static void WalledCompound(Node3D root, float w, float d, Color color, Color team)
        {
            float hw = w * 0.45f, hd = d * 0.45f;
            MeshKit.Box(root, new Vector3(w * 0.9f, 2.2f, 0.4f), new Vector3(0f, 1.1f, -hd), color);
            MeshKit.Box(root, new Vector3(0.4f, 2.2f, d * 0.9f), new Vector3(-hw, 1.1f, 0f), color);
            MeshKit.Box(root, new Vector3(0.4f, 2.2f, d * 0.9f), new Vector3(hw, 1.1f, 0f), color);
            MeshKit.Box(root, new Vector3(w * 0.3f, 2.2f, 0.4f), new Vector3(-hw + w * 0.15f, 1.1f, hd), color);
            MeshKit.Box(root, new Vector3(w * 0.3f, 2.2f, 0.4f), new Vector3(hw - w * 0.15f, 1.1f, hd), color);
            MeshKit.Box(root, new Vector3(w * 0.45f, 3.2f, d * 0.4f), new Vector3(0f, 1.6f, -d * 0.12f), color.Darkened(0.08f));
            Flag(root, new Vector3(hw - 0.6f, 0f, -hd + 0.6f), team, 6f);
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
                    var stone = MeshKit.Vary(new Color(0.58f, 0.54f, 0.46f), 0.05f, index);
                    MeshKit.Box(root, new Vector3(w * 0.9f, o.Height, d * 0.9f), new Vector3(0f, o.Height * 0.5f, 0f), stone);
                    MeshKit.Box(root, new Vector3(w * 0.95f, 0.12f, d * 0.95f), new Vector3(0f, o.Height, 0f), stone.Darkened(0.2f));
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

        private static void Building(Node3D root, float w, float h, float d, int index)
        {
            var plaster = MeshKit.Vary(index % 3 == 0 ? new Color(0.78f, 0.74f, 0.64f) : index % 3 == 1 ? Concrete : new Color(0.72f, 0.66f, 0.55f), 0.06f, index);
            MeshKit.Box(root, new Vector3(w, h, d), new Vector3(0f, h * 0.5f, 0f), plaster);
            MeshKit.Box(root, new Vector3(w + 0.2f, 0.35f, d + 0.2f), new Vector3(0f, h + 0.1f, 0f), plaster.Darkened(0.18f)); // parapet
            var window = new Color(0.12f, 0.13f, 0.15f);
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
                    MeshKit.Box(root, new Vector3(1.0f, 1.2f, 0.08f), new Vector3(x, y, d * 0.5f + 0.02f), window);
                    MeshKit.Box(root, new Vector3(1.0f, 1.2f, 0.08f), new Vector3(x, y, -d * 0.5f - 0.02f), window);
                }

                for (float z = -d * 0.5f + 1.5f; z <= d * 0.5f - 1.2f; z += 2.6f)
                {
                    MeshKit.Box(root, new Vector3(0.08f, 1.2f, 1.0f), new Vector3(w * 0.5f + 0.02f, y, z), window);
                    MeshKit.Box(root, new Vector3(0.08f, 1.2f, 1.0f), new Vector3(-w * 0.5f - 0.02f, y, z), window);
                }
            }

            if (MeshKit.Hash01(index, 5) > 0.35f)
            {
                MeshKit.Cyl(root, 0.6f, 0.6f, 1.2f, new Vector3(w * 0.25f, h + 0.85f, -d * 0.2f), new Color(0.2f, 0.22f, 0.25f), null, 10); // water tank
            }

            if (MeshKit.Hash01(index, 6) > 0.5f)
            {
                MeshKit.Box(root, new Vector3(1.0f, 0.6f, 0.8f), new Vector3(-w * 0.25f, h + 0.55f, d * 0.2f), new Color(0.75f, 0.75f, 0.73f)); // AC unit
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

        private static void Rock(Node3D root, float w, float h, float d, int index)
        {
            var color = MeshKit.Vary(new Color(0.44f, 0.41f, 0.37f), 0.08f, index);
            for (int i = 0; i < 4; i++)
            {
                float ox = (MeshKit.Hash01(index, i * 3) - 0.5f) * w * 0.5f;
                float oz = (MeshKit.Hash01(index, i * 3 + 1) - 0.5f) * d * 0.5f;
                float r = Mathf.Min(w, d) * (0.35f + MeshKit.Hash01(index, i * 3 + 2) * 0.25f);
                var rock = MeshKit.Sphere(root, r, new Vector3(ox, h * 0.25f, oz), color, false, 6, new Vector3(1f, h / (r * 2f) + 0.2f, 1f));
                rock.Rotation = new Vector3(0f, MeshKit.Hash01(index, i) * Mathf.Tau, 0.15f);
            }
        }

        public static Node3D BuildTree(int seed)
        {
            var root = new Node3D();
            float s = 0.8f + MeshKit.Hash01(seed, 9) * 0.5f;
            MeshKit.Cyl(root, 0.12f * s, 0.2f * s, 1.8f * s, new Vector3(0f, 0.9f * s, 0f), new Color(0.35f, 0.28f, 0.2f), new Vector3(0f, 0f, 6f), 6);
            var leaf = MeshKit.Vary(new Color(0.34f, 0.42f, 0.24f), 0.08f, seed);
            MeshKit.Sphere(root, 1.3f * s, new Vector3(0f, 2.3f * s, 0f), leaf, false, 7, new Vector3(1f, 0.7f, 1f));
            MeshKit.Sphere(root, 0.9f * s, new Vector3(0.7f * s, 2.0f * s, 0.3f * s), leaf.Darkened(0.08f), false, 6);
            MeshKit.Sphere(root, 0.8f * s, new Vector3(-0.6f * s, 2.1f * s, -0.4f * s), leaf.Lightened(0.05f), false, 6);
            return root;
        }

        public static Node3D BuildBush(int seed)
        {
            var root = new Node3D();
            var leaf = MeshKit.Vary(new Color(0.36f, 0.4f, 0.24f), 0.1f, seed);
            MeshKit.Sphere(root, 0.6f, new Vector3(0f, 0.3f, 0f), leaf, false, 6, new Vector3(1.3f, 0.7f, 1f));
            return root;
        }
    }
}
