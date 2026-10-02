using Godot;
using OCT7.Sim.Data;

namespace OCT7.Game.Visual
{
    /// <summary>
    /// Procedural soldiers with readable roles and faction looks.
    /// Roles: rifleman, engineer / digger, machine gunner + ammo bearers, sniper + spotter, anti-tank gunner + loaders, elite.
    /// Factions: IDF olive with covered helmets and bullpup rifles; Hamas mixed civilian / dark clothing, balaclavas,
    /// headbands and AKs; Hezbollah woodland camo, caps, AKs; elites wear plate carriers and helmets.
    /// Team color appears as an armband; the ground ring carries the rest.
    /// </summary>
    internal static class SoldierModels
    {
        public const float BaseScale = 1.35f;

        private enum Role
        {
            Rifleman,
            Engineer,
            Digger,
            Gunner,
            AmmoBearer,
            Sniper,
            Spotter,
            AtGunner,
            AtLoader,
            Elite,
        }

        private enum Head
        {
            CoveredHelmet,
            Helmet,
            Cap,
            Boonie,
            Balaclava,
            Headband,
            HardHat,
        }

        private static readonly Color Skin1 = new Color(0.78f, 0.6f, 0.45f);
        private static readonly Color Skin2 = new Color(0.6f, 0.44f, 0.32f);
        private static readonly Color Boots = new Color(0.15f, 0.12f, 0.1f);
        private static readonly Color Gunmetal = new Color(0.12f, 0.12f, 0.12f);
        private static readonly Color Furniture = new Color(0.42f, 0.27f, 0.16f);
        private static readonly Color Polymer = new Color(0.22f, 0.22f, 0.19f);

        public static SoldierRig Build(UnitDef unit, WeaponKind kind, Color team, int index, bool carriesSpecialWeapon)
        {
            string faction = unit.FactionId;
            var role = RoleOf(unit, kind, index, carriesSpecialWeapon);
            int seed = index * 7 + unit.Id.Length * 13;
            var rig = new SoldierRig { Root = new Node3D { Scale = Vector3.One * BaseScale }, BaseScale = BaseScale };
            rig.Pose = new Node3D();
            rig.Root.AddChild(rig.Pose);

            Look(faction, role, index, seed, out var uniform, out var pants, out var vest, out var headColor, out var head, out var camo);
            var skin = MeshKit.Hash01(seed) > 0.5f ? Skin1 : Skin2;
            Material Cloth(Color c) => camo ? MeshKit.TexMatLocal(c, Tex.Camo, 0.6f) : MeshKit.Mat(c);

            // Legs (pivot at the hip) with knee pads for regular troops.
            rig.LegL = Leg(rig.Pose, -0.1f, Cloth(pants), faction == "idf" || role == Role.Elite);
            rig.LegR = Leg(rig.Pose, 0.1f, Cloth(pants), faction == "idf" || role == Role.Elite);

            rig.UpperBody = new Node3D { Position = new Vector3(0f, 0.84f, 0f) };
            rig.Pose.AddChild(rig.UpperBody);
            var ub = rig.UpperBody;
            MeshKit.Add(ub, MeshKit.BoxMesh(new Vector3(0.42f, 0.56f, 0.24f)), new Vector3(0f, 0.3f, 0f), Cloth(uniform), null);
            bool plate = role == Role.Elite || (faction == "idf" && role != Role.Sniper);
            if (role != Role.Digger)
            {
                MeshKit.Box(ub, new Vector3(plate ? 0.48f : 0.46f, plate ? 0.42f : 0.36f, plate ? 0.33f : 0.29f), new Vector3(0f, 0.32f, 0.01f), vest);
                // Magazine pouches across the chest.
                for (int i = 0; i < 3; i++)
                {
                    MeshKit.Box(ub, new Vector3(0.1f, 0.13f, 0.06f), new Vector3(-0.13f + i * 0.13f, 0.24f, 0.18f), vest.Darkened(0.18f));
                }
            }

            MeshKit.Sphere(ub, 0.12f, new Vector3(0f, 0.72f, 0f), head == Head.Balaclava ? headColor : skin, false, 8);
            Headgear(ub, head, headColor, faction);

            rig.ArmL = Arm(ub, -0.27f, Cloth(uniform), team, false);
            rig.ArmR = Arm(ub, 0.27f, Cloth(uniform), team, true);

            Backpack(ub, role, faction, vest);
            Weapon(rig, faction, role, unit);
            return rig;
        }

        private static Role RoleOf(UnitDef unit, WeaponKind kind, int index, bool special)
        {
            if (unit.Engineer)
            {
                return unit.Id.Contains("digger") ? Role.Digger : Role.Engineer;
            }

            if (unit.Id.Contains("elite") || unit.Id.Contains("radwan"))
            {
                return Role.Elite;
            }

            switch (kind)
            {
                case WeaponKind.MachineGun: return index == 0 ? Role.Gunner : Role.AmmoBearer;
                case WeaponKind.Sniper: return index == 0 ? Role.Sniper : Role.Spotter;
                case WeaponKind.AntiTank: return special ? Role.AtGunner : Role.AtLoader;
            }

            return Role.Rifleman;
        }

        private static void Look(string faction, Role role, int index, int seed, out Color uniform, out Color pants, out Color vest, out Color headColor, out Head head, out bool camo)
        {
            camo = false;
            switch (faction)
            {
                case "hamas":
                    {
                        var shirts = new[] { new Color(0.42f, 0.4f, 0.31f), new Color(0.2f, 0.23f, 0.17f), new Color(0.14f, 0.14f, 0.14f), new Color(0.36f, 0.3f, 0.24f), new Color(0.3f, 0.32f, 0.36f) };
                        uniform = shirts[(index + seed) % shirts.Length];
                        pants = index % 2 == 0 ? new Color(0.14f, 0.15f, 0.13f) : new Color(0.27f, 0.25f, 0.2f);
                        vest = new Color(0.2f, 0.22f, 0.15f);
                        headColor = new Color(0.08f, 0.08f, 0.08f);
                        head = index % 3 == 2 ? Head.Headband : Head.Balaclava;
                        if (role == Role.Elite)
                        {
                            uniform = new Color(0.1f, 0.1f, 0.1f);
                            pants = uniform;
                            vest = new Color(0.17f, 0.19f, 0.14f);
                            head = index % 2 == 0 ? Head.Helmet : Head.Balaclava;
                            headColor = new Color(0.12f, 0.13f, 0.11f);
                        }
                        else if (role == Role.Digger)
                        {
                            uniform = MeshKit.Vary(new Color(0.48f, 0.43f, 0.35f), 0.08f, seed);
                            pants = new Color(0.3f, 0.27f, 0.22f);
                            head = Head.HardHat;
                            headColor = new Color(0.72f, 0.6f, 0.22f);
                        }
                        else if (role == Role.Sniper || role == Role.Spotter)
                        {
                            head = Head.Headband;
                        }

                        return;
                    }

                case "hezbollah":
                    {
                        camo = true;
                        uniform = MeshKit.Vary(new Color(0.36f, 0.4f, 0.27f), 0.05f, seed);
                        pants = uniform.Darkened(0.06f);
                        vest = new Color(0.23f, 0.26f, 0.17f);
                        headColor = new Color(0.27f, 0.3f, 0.19f);
                        head = index % 3 == 0 ? Head.Helmet : Head.Cap;
                        if (role == Role.Elite)
                        {
                            uniform = new Color(0.24f, 0.27f, 0.2f);
                            pants = uniform;
                            vest = new Color(0.16f, 0.18f, 0.13f);
                            head = Head.Helmet;
                            headColor = new Color(0.2f, 0.22f, 0.16f);
                        }
                        else if (role == Role.Sniper || role == Role.Spotter)
                        {
                            head = Head.Boonie;
                        }

                        return;
                    }

                default:
                    {
                        uniform = MeshKit.Vary(new Color(0.36f, 0.38f, 0.25f), 0.03f, seed);
                        pants = uniform.Darkened(0.05f);
                        vest = new Color(0.3f, 0.31f, 0.2f);
                        headColor = new Color(0.52f, 0.5f, 0.36f);
                        head = Head.CoveredHelmet;
                        if (role == Role.Sniper || role == Role.Spotter)
                        {
                            head = Head.Boonie;
                            headColor = new Color(0.4f, 0.4f, 0.28f);
                        }

                        return;
                    }
            }
        }

        private static void Headgear(Node3D ub, Head head, Color c, string faction)
        {
            switch (head)
            {
                case Head.CoveredHelmet:
                    // IDF helmet with the bulging cloth cover.
                    MeshKit.Sphere(ub, 0.155f, new Vector3(0f, 0.75f, 0f), c.Darkened(0.15f), true, 8);
                    MeshKit.Sphere(ub, 0.15f, new Vector3(0f, 0.86f, -0.01f), c, false, 7, new Vector3(1.05f, 0.62f, 1.1f));
                    break;
                case Head.Helmet:
                    MeshKit.Sphere(ub, 0.155f, new Vector3(0f, 0.75f, 0f), c, true, 8);
                    MeshKit.Box(ub, new Vector3(0.06f, 0.05f, 0.06f), new Vector3(0f, 0.85f, 0.13f), Gunmetal); // NVG mount
                    break;
                case Head.Cap:
                    MeshKit.Cyl(ub, 0.13f, 0.135f, 0.09f, new Vector3(0f, 0.81f, 0f), c, null, 8);
                    MeshKit.Box(ub, new Vector3(0.18f, 0.02f, 0.13f), new Vector3(0f, 0.775f, 0.13f), c.Darkened(0.1f));
                    break;
                case Head.Boonie:
                    MeshKit.Cyl(ub, 0.12f, 0.13f, 0.1f, new Vector3(0f, 0.82f, 0f), c, null, 8);
                    MeshKit.Cyl(ub, 0.24f, 0.24f, 0.02f, new Vector3(0f, 0.775f, 0f), c.Darkened(0.08f), null, 10);
                    break;
                case Head.Balaclava:
                    MeshKit.Sphere(ub, 0.128f, new Vector3(0f, 0.74f, 0f), c, false, 8);
                    MeshKit.Box(ub, new Vector3(0.12f, 0.03f, 0.03f), new Vector3(0f, 0.74f, 0.115f), new Color(0.7f, 0.55f, 0.42f)); // eye slit
                    if (faction == "hamas")
                    {
                        MeshKit.Cyl(ub, 0.13f, 0.13f, 0.035f, new Vector3(0f, 0.8f, 0f), new Color(0.2f, 0.42f, 0.18f), null, 10); // headband
                    }

                    break;
                case Head.Headband:
                    MeshKit.Sphere(ub, 0.125f, new Vector3(0f, 0.77f, -0.02f), new Color(0.1f, 0.08f, 0.07f), true, 8); // hair
                    MeshKit.Cyl(ub, 0.128f, 0.128f, 0.04f, new Vector3(0f, 0.78f, 0f), new Color(0.2f, 0.42f, 0.18f), null, 10);
                    break;
                case Head.HardHat:
                    MeshKit.Sphere(ub, 0.15f, new Vector3(0f, 0.76f, 0f), c, true, 8);
                    MeshKit.Cyl(ub, 0.19f, 0.19f, 0.02f, new Vector3(0f, 0.765f, 0.02f), c.Darkened(0.1f), null, 10);
                    MeshKit.Add(ub, MeshKit.BoxMesh(new Vector3(0.07f, 0.05f, 0.04f)), new Vector3(0f, 0.82f, 0.15f), MeshKit.Emissive(new Color(1f, 0.9f, 0.6f), 2.5f), null); // headlamp
                    break;
            }
        }

        private static Node3D Leg(Node3D parent, float x, Material pants, bool kneePads)
        {
            var pivot = new Node3D { Position = new Vector3(x, 0.84f, 0f) };
            parent.AddChild(pivot);
            MeshKit.Add(pivot, MeshKit.BoxMesh(new Vector3(0.15f, 0.8f, 0.17f)), new Vector3(0f, -0.4f, 0f), pants, null);
            MeshKit.Box(pivot, new Vector3(0.16f, 0.1f, 0.26f), new Vector3(0f, -0.79f, 0.04f), Boots);
            if (kneePads)
            {
                MeshKit.Box(pivot, new Vector3(0.14f, 0.12f, 0.05f), new Vector3(0f, -0.46f, 0.1f), new Color(0.2f, 0.21f, 0.17f));
            }

            return pivot;
        }

        private static Node3D Arm(Node3D upperBody, float x, Material sleeve, Color team, bool armband)
        {
            var pivot = new Node3D { Position = new Vector3(x, 0.53f, 0.02f), RotationDegrees = new Vector3(-65f, 0f, x > 0 ? 8f : -12f) };
            upperBody.AddChild(pivot);
            MeshKit.Add(pivot, MeshKit.BoxMesh(new Vector3(0.11f, 0.5f, 0.12f)), new Vector3(0f, -0.22f, 0f), sleeve, null);
            MeshKit.Box(pivot, new Vector3(0.1f, 0.1f, 0.1f), new Vector3(0f, -0.5f, 0f), new Color(0.2f, 0.18f, 0.15f)); // glove
            if (armband)
            {
                MeshKit.Box(pivot, new Vector3(0.14f, 0.12f, 0.15f), new Vector3(0f, -0.1f, 0f), team);
            }

            return pivot;
        }

        private static void Backpack(Node3D ub, Role role, string faction, Color vest)
        {
            var pack = vest.Darkened(0.15f);
            switch (role)
            {
                case Role.Engineer:
                    // Big rucksack with a shovel and a bedroll: engineers read at a glance.
                    MeshKit.Box(ub, new Vector3(0.36f, 0.5f, 0.24f), new Vector3(0f, 0.32f, -0.26f), pack);
                    MeshKit.Cyl(ub, 0.08f, 0.08f, 0.4f, new Vector3(0f, 0.62f, -0.26f), new Color(0.45f, 0.42f, 0.3f), new Vector3(0f, 0f, 90f), 6);
                    MeshKit.Box(ub, new Vector3(0.04f, 0.8f, 0.04f), new Vector3(-0.15f, 0.55f, -0.36f), Furniture, new Vector3(0f, 0f, 12f));
                    MeshKit.Box(ub, new Vector3(0.16f, 0.2f, 0.03f), new Vector3(-0.23f, 0.97f, -0.36f), Gunmetal, new Vector3(0f, 0f, 12f));
                    break;
                case Role.Digger:
                    MeshKit.Box(ub, new Vector3(0.04f, 0.9f, 0.04f), new Vector3(0.12f, 0.5f, -0.2f), Furniture, new Vector3(0f, 0f, -15f));
                    MeshKit.Box(ub, new Vector3(0.36f, 0.05f, 0.05f), new Vector3(0.24f, 0.93f, -0.2f), Gunmetal, new Vector3(0f, 0f, -15f)); // pickaxe head
                    break;
                case Role.AmmoBearer:
                    MeshKit.Box(ub, new Vector3(0.3f, 0.36f, 0.2f), new Vector3(0f, 0.34f, -0.24f), pack);
                    MeshKit.Box(ub, new Vector3(0.24f, 0.18f, 0.12f), new Vector3(0f, 0.6f, -0.24f), new Color(0.3f, 0.33f, 0.2f)); // ammo can
                    break;
                case Role.AtLoader:
                    // Spare rockets sticking out of a carrier.
                    MeshKit.Box(ub, new Vector3(0.32f, 0.38f, 0.18f), new Vector3(0f, 0.32f, -0.24f), pack);
                    for (int i = 0; i < 3; i++)
                    {
                        float x = -0.1f + i * 0.1f;
                        MeshKit.Cyl(ub, 0.04f, 0.04f, 0.3f, new Vector3(x, 0.62f, -0.26f), new Color(0.28f, 0.32f, 0.2f), null, 6);
                        MeshKit.Cyl(ub, 0.0f, 0.06f, 0.16f, new Vector3(x, 0.85f, -0.26f), new Color(0.35f, 0.38f, 0.24f), null, 6);
                    }

                    break;
                case Role.Sniper:
                case Role.Spotter:
                    // Ghillie-style shaggy shoulders.
                    for (int i = 0; i < 6; i++)
                    {
                        float a = i / 6f * Mathf.Tau;
                        MeshKit.Cyl(ub, 0f, 0.07f, 0.22f, new Vector3(Mathf.Cos(a) * 0.2f, 0.55f, Mathf.Sin(a) * 0.14f - 0.04f), new Color(0.32f, 0.34f, 0.2f).Darkened(i % 2 * 0.15f), new Vector3(20f * Mathf.Sin(a), 0f, -20f * Mathf.Cos(a)), 4);
                    }

                    break;
                case Role.Elite:
                    MeshKit.Box(ub, new Vector3(0.3f, 0.32f, 0.14f), new Vector3(0f, 0.36f, -0.24f), pack);
                    MeshKit.Beam(ub, new Vector3(0.1f, 0.5f, -0.26f), new Vector3(0.14f, 1.05f, -0.28f), 0.012f, Gunmetal, 3); // radio antenna
                    break;
                default:
                    MeshKit.Box(ub, new Vector3(0.3f, 0.32f, 0.15f), new Vector3(0f, 0.36f, -0.21f), pack);
                    break;
            }
        }

        private static void Weapon(SoldierRig rig, string faction, Role role, UnitDef unit)
        {
            var ub = rig.UpperBody;
            switch (role)
            {
                case Role.Gunner:
                    // Belt-fed MG with a bipod and an ammo box.
                    MeshKit.Box(ub, new Vector3(0.1f, 0.14f, 0.7f), new Vector3(0.08f, 0.37f, 0.25f), Gunmetal);
                    MeshKit.Beam(ub, new Vector3(0.08f, 0.39f, 0.55f), new Vector3(0.08f, 0.39f, 1.15f), 0.025f, Gunmetal, 6);
                    MeshKit.Box(ub, new Vector3(0.08f, 0.12f, 0.24f), new Vector3(0.08f, 0.32f, -0.15f), faction == "idf" ? Polymer : Furniture);
                    MeshKit.Box(ub, new Vector3(0.16f, 0.16f, 0.18f), new Vector3(0.18f, 0.27f, 0.2f), new Color(0.27f, 0.3f, 0.18f));
                    MeshKit.Beam(ub, new Vector3(0.06f, 0.36f, 0.9f), new Vector3(0.0f, 0.24f, 1.0f), 0.012f, Gunmetal, 3);
                    MeshKit.Beam(ub, new Vector3(0.1f, 0.36f, 0.9f), new Vector3(0.16f, 0.24f, 1.0f), 0.012f, Gunmetal, 3);
                    rig.Muzzle = new Vector3(0.08f, 0.84f + 0.39f, 1.15f);
                    return;
                case Role.Sniper:
                    // Heavy anti-materiel rifle: long barrel, big scope, bipod, muzzle brake.
                    MeshKit.Box(ub, new Vector3(0.08f, 0.12f, 0.75f), new Vector3(0.08f, 0.38f, 0.2f), faction == "idf" ? Polymer : new Color(0.3f, 0.3f, 0.26f));
                    MeshKit.Beam(ub, new Vector3(0.08f, 0.4f, 0.5f), new Vector3(0.08f, 0.4f, 1.3f), 0.02f, Gunmetal, 6);
                    MeshKit.Box(ub, new Vector3(0.06f, 0.06f, 0.1f), new Vector3(0.08f, 0.4f, 1.32f), Gunmetal);
                    MeshKit.Cyl(ub, 0.04f, 0.04f, 0.36f, new Vector3(0.08f, 0.5f, 0.2f), new Color(0.05f, 0.05f, 0.05f), new Vector3(90f, 0f, 0f), 6);
                    rig.Muzzle = new Vector3(0.08f, 0.84f + 0.4f, 1.35f);
                    return;
                case Role.Spotter:
                    Rifle(rig, faction, true);
                    MeshKit.Box(ub, new Vector3(0.16f, 0.06f, 0.1f), new Vector3(0f, 0.7f, 0.16f), Gunmetal); // binoculars
                    return;
                case Role.AtGunner:
                    // Shoulder-fired launcher; Hezbollah's RPG-29 is long and slim, the others carry a cone warhead.
                    bool rpg29 = faction == "hezbollah";
                    var tube = rpg29 ? new Color(0.2f, 0.25f, 0.16f) : new Color(0.3f, 0.33f, 0.22f);
                    float len = rpg29 ? 1.5f : 1.05f;
                    MeshKit.Cyl(ub, 0.06f, 0.06f, len, new Vector3(0.2f, 0.62f, 0.05f), tube, new Vector3(90f, 0f, 0f), 8);
                    MeshKit.Box(ub, new Vector3(0.06f, 0.16f, 0.08f), new Vector3(0.2f, 0.5f, 0.15f), Furniture); // grip
                    if (!rpg29)
                    {
                        MeshKit.Cyl(ub, 0.0f, 0.11f, 0.3f, new Vector3(0.2f, 0.62f, len * 0.5f + 0.2f), new Color(0.38f, 0.4f, 0.26f), new Vector3(90f, 0f, 0f), 8);
                        MeshKit.Cyl(ub, 0.11f, 0.11f, 0.1f, new Vector3(0.2f, 0.62f, len * 0.5f + 0.02f), new Color(0.38f, 0.4f, 0.26f), new Vector3(90f, 0f, 0f), 8);
                    }

                    rig.Muzzle = new Vector3(0.2f, 0.84f + 0.62f, len * 0.5f + 0.3f);
                    return;
                case Role.Elite:
                    Rifle(rig, faction, false);
                    if (unit.Id.Contains("radwan") || unit.Id.Contains("elite"))
                    {
                        // Elites carry a launcher slung across the back.
                        MeshKit.Cyl(ub, 0.05f, 0.05f, 1.0f, new Vector3(0.05f, 0.45f, -0.32f), new Color(0.26f, 0.3f, 0.2f), new Vector3(0f, 0f, 60f), 6);
                    }

                    return;
                default:
                    Rifle(rig, faction, role == Role.Engineer || role == Role.Digger || role == Role.AmmoBearer || role == Role.AtLoader);
                    return;
            }
        }

        /// <summary>IDF: bullpup Tavor with optic; others: AK with wooden furniture and curved magazine.</summary>
        private static void Rifle(SoldierRig rig, string faction, bool carbine)
        {
            var ub = rig.UpperBody;
            if (faction == "idf")
            {
                float len = carbine ? 0.55f : 0.66f;
                MeshKit.Box(ub, new Vector3(0.07f, 0.13f, len), new Vector3(0.08f, 0.37f, 0.28f), Polymer);
                MeshKit.Box(ub, new Vector3(0.05f, 0.13f, 0.07f), new Vector3(0.08f, 0.27f, 0.08f), Gunmetal); // magazine behind the grip
                MeshKit.Box(ub, new Vector3(0.05f, 0.06f, 0.1f), new Vector3(0.08f, 0.47f, 0.3f), Gunmetal); // optic
                MeshKit.Beam(ub, new Vector3(0.08f, 0.38f, 0.28f + len * 0.5f), new Vector3(0.08f, 0.38f, 0.38f + len * 0.5f), 0.018f, Gunmetal, 5);
                rig.Muzzle = new Vector3(0.08f, 0.84f + 0.38f, 0.4f + len * 0.5f);
                return;
            }

            float body = carbine ? 0.42f : 0.5f;
            MeshKit.Box(ub, new Vector3(0.06f, 0.1f, body), new Vector3(0.08f, 0.38f, 0.33f), Gunmetal);
            MeshKit.Box(ub, new Vector3(0.06f, 0.11f, 0.26f), new Vector3(0.08f, 0.35f, 0.0f), Furniture); // stock
            MeshKit.Box(ub, new Vector3(0.06f, 0.07f, 0.18f), new Vector3(0.08f, 0.37f, 0.44f), Furniture); // handguard
            MeshKit.Box(ub, new Vector3(0.05f, 0.16f, 0.06f), new Vector3(0.08f, 0.26f, 0.36f), Gunmetal, new Vector3(-20f, 0f, 0f)); // curved magazine
            MeshKit.Beam(ub, new Vector3(0.08f, 0.39f, 0.33f + body * 0.5f), new Vector3(0.08f, 0.39f, 0.45f + body * 0.5f), 0.015f, Gunmetal, 5);
            rig.Muzzle = new Vector3(0.08f, 0.84f + 0.39f, 0.47f + body * 0.5f);
        }
    }
}
