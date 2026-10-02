using Godot;

namespace OCT7.Game.Visual
{
    /// <summary>
    /// Procedural vehicles: Merkava Mk4 (long front-engine hull, wedge turret, chain curtain, Trophy panels),
    /// Namer heavy APC (Merkava chassis, tall box hull, remote weapon station) and the ZU-23-2 technical
    /// (light pickup with a twin anti-aircraft cannon in the bed). Local +Z is forward; muzzle is in root space.
    /// </summary>
    internal static class VehicleModels
    {
        private static readonly Color Tan = new Color(0.66f, 0.6f, 0.45f);
        private static readonly Color TrackDark = new Color(0.12f, 0.11f, 0.1f);
        private static readonly Color Wheel = new Color(0.2f, 0.19f, 0.18f);
        private static readonly Color Gunmetal = new Color(0.14f, 0.14f, 0.13f);
        private static readonly Color Glass = new Color(0.12f, 0.16f, 0.2f);

        private static Material Armor(Color c) => MeshKit.TexMatLocal(c, Tex.Metal, 0.75f);

        private static MeshInstance3D Plate(Node3D parent, Vector3 size, Vector3 pos, Color c, Vector3? rot = null) =>
            MeshKit.Add(parent, MeshKit.BoxMesh(size), pos, Armor(c), rot);

        private static void Running(Node3D root, float width, float length, Color hull, Color team)
        {
            foreach (float side in new[] { -1f, 1f })
            {
                float x = side * (width * 0.5f - 0.33f);
                MeshKit.Box(root, new Vector3(0.62f, 0.75f, length), new Vector3(x, 0.4f, 0f), TrackDark);
                for (int i = 0; i < 6; i++)
                {
                    MeshKit.Cyl(root, 0.34f, 0.34f, 0.66f, new Vector3(x, 0.38f, -length * 0.4f + i * length * 0.16f), Wheel, new Vector3(0f, 0f, 90f), 10);
                }

                MeshKit.Cyl(root, 0.42f, 0.42f, 0.64f, new Vector3(x, 0.55f, length * 0.47f), Wheel, new Vector3(0f, 0f, 90f), 10); // drive sprocket
                // Segmented side skirts with a team stripe.
                for (int i = 0; i < 5; i++)
                {
                    Plate(root, new Vector3(0.1f, 0.62f, length * 0.19f), new Vector3(side * (width * 0.5f + 0.03f), 0.86f, -length * 0.38f + i * length * 0.19f), hull.Darkened(0.06f * (i % 2)));
                }

                MeshKit.Box(root, new Vector3(0.02f, 0.14f, length * 0.86f), new Vector3(side * (width * 0.5f + 0.09f), 0.98f, 0f), team);
            }
        }

        public static Node3D Merkava(Color team, out Vector3 muzzle, out Node3D turret)
        {
            var root = new Node3D();
            var tan = Tan;
            Running(root, 3.7f, 8.4f, tan, team);
            Plate(root, new Vector3(3.1f, 0.9f, 7.6f), new Vector3(0f, 1.2f, -0.2f), tan);
            Plate(root, new Vector3(3.1f, 0.18f, 2.8f), new Vector3(0f, 1.46f, 3.55f), tan.Darkened(0.04f), new Vector3(14f, 0f, 0f)); // long glacis
            Plate(root, new Vector3(1.6f, 0.25f, 1.2f), new Vector3(0.6f, 1.72f, 2.4f), tan.Darkened(0.08f)); // engine deck
            Plate(root, new Vector3(2.8f, 0.7f, 0.5f), new Vector3(0f, 1.35f, -4.05f), tan.Darkened(0.1f)); // rear door
            MeshKit.Box(root, new Vector3(2.6f, 0.06f, 0.6f), new Vector3(0f, 1.75f, -4.2f), Gunmetal); // rear basket floor

            turret = new Node3D { Position = new Vector3(0f, 1.66f, -1.0f) };
            root.AddChild(turret);
            Plate(turret, new Vector3(2.6f, 0.85f, 3.0f), new Vector3(0f, 0.43f, -0.5f), tan);
            Plate(turret, new Vector3(2.2f, 0.62f, 1.8f), new Vector3(0f, 0.4f, 1.6f), tan.Darkened(0.03f), new Vector3(-20f, 0f, 0f)); // wedge nose
            Plate(turret, new Vector3(2.2f, 0.7f, 1.4f), new Vector3(0f, 0.4f, -2.6f), tan.Darkened(0.06f)); // rear bustle
            // Ball-and-chain curtain under the bustle.
            for (int i = 0; i < 9; i++)
            {
                float x = -1.0f + i * 0.25f;
                MeshKit.Beam(turret, new Vector3(x, 0.08f, -3.3f), new Vector3(x, -0.45f, -3.32f), 0.012f, Gunmetal, 3);
                MeshKit.Sphere(turret, 0.06f, new Vector3(x, -0.5f, -3.32f), Gunmetal, false, 5);
            }

            // Trophy radar panels and launchers.
            foreach (float side in new[] { -1f, 1f })
            {
                Plate(turret, new Vector3(0.12f, 0.55f, 0.75f), new Vector3(side * 1.36f, 0.55f, 0.3f), tan.Darkened(0.12f), new Vector3(0f, side * 12f, 0f));
                MeshKit.Cyl(turret, 0.13f, 0.13f, 0.35f, new Vector3(side * 1.25f, 1.0f, -0.6f), Gunmetal, null, 8);
            }

            MeshKit.Box(turret, new Vector3(0.02f, 0.3f, 1.0f), new Vector3(1.31f, 0.45f, -2.5f), team);
            MeshKit.Box(turret, new Vector3(0.02f, 0.3f, 1.0f), new Vector3(-1.31f, 0.45f, -2.5f), team);
            // Main gun with thermal sleeve.
            MeshKit.Cyl(turret, 0.13f, 0.15f, 2.6f, new Vector3(0f, 0.5f, 3.4f), tan.Darkened(0.15f), new Vector3(90f, 0f, 0f), 10);
            MeshKit.Cyl(turret, 0.11f, 0.12f, 2.6f, new Vector3(0f, 0.5f, 5.9f), tan.Darkened(0.15f), new Vector3(90f, 0f, 0f), 10);
            MeshKit.Cyl(turret, 0.15f, 0.15f, 0.35f, new Vector3(0f, 0.5f, 4.7f), tan.Darkened(0.22f), new Vector3(90f, 0f, 0f), 10);
            // Commander's cupola with MG.
            MeshKit.Cyl(turret, 0.32f, 0.36f, 0.3f, new Vector3(0.65f, 1.0f, -0.6f), tan.Darkened(0.1f), null, 10);
            MeshKit.Beam(turret, new Vector3(0.65f, 1.25f, -0.4f), new Vector3(0.65f, 1.3f, 0.6f), 0.03f, Gunmetal, 5);
            MeshKit.Beam(turret, new Vector3(-0.7f, 0.9f, 0f), new Vector3(-0.7f, 2.4f, -0.2f), 0.012f, Gunmetal, 3); // antenna
            muzzle = new Vector3(0f, 1.66f + 0.5f, -1.0f + 7.2f);
            return root;
        }

        public static Node3D Namer(Color team, out Vector3 muzzle, out Node3D turret)
        {
            var root = new Node3D();
            var tan = Tan.Lightened(0.02f);
            Running(root, 3.7f, 7.8f, tan, team);
            Plate(root, new Vector3(3.2f, 1.75f, 6.6f), new Vector3(0f, 1.55f, -0.4f), tan);
            Plate(root, new Vector3(3.1f, 0.2f, 1.9f), new Vector3(0f, 1.95f, 3.3f), tan.Darkened(0.04f), new Vector3(26f, 0f, 0f));
            Plate(root, new Vector3(3.0f, 0.9f, 1.2f), new Vector3(0f, 1.0f, 3.4f), tan.Darkened(0.06f));
            Plate(root, new Vector3(1.5f, 1.25f, 0.12f), new Vector3(0f, 1.3f, -3.75f), tan.Darkened(0.15f)); // rear ramp
            foreach (float x in new[] { -0.8f, 0.8f })
            {
                MeshKit.Cyl(root, 0.32f, 0.32f, 0.1f, new Vector3(x, 2.47f, -1.8f), tan.Darkened(0.12f), null, 10); // roof hatches
            }

            foreach (float side in new[] { -1f, 1f })
            {
                for (int i = 0; i < 3; i++)
                {
                    MeshKit.Cyl(root, 0.06f, 0.06f, 0.3f, new Vector3(side * 1.5f, 2.3f, 2.0f - i * 0.15f), Gunmetal, new Vector3(-40f, 0f, side * 30f), 6); // smoke launchers
                }
            }

            MeshKit.Box(root, new Vector3(3.22f, 0.18f, 0.02f), new Vector3(0f, 2.2f, -3.82f), team);
            turret = new Node3D { Position = new Vector3(0.5f, 2.42f, 0.6f) };
            root.AddChild(turret);
            Plate(turret, new Vector3(0.8f, 0.5f, 0.9f), new Vector3(0f, 0.25f, 0f), tan.Darkened(0.12f));
            MeshKit.Box(turret, new Vector3(0.3f, 0.25f, 0.2f), new Vector3(-0.25f, 0.6f, 0.2f), Glass); // sight block
            MeshKit.Cyl(turret, 0.05f, 0.05f, 1.4f, new Vector3(0.15f, 0.3f, 1.0f), Gunmetal, new Vector3(90f, 0f, 0f), 6);
            MeshKit.Box(turret, new Vector3(0.25f, 0.2f, 0.3f), new Vector3(0.4f, 0.25f, 0f), new Color(0.3f, 0.33f, 0.2f)); // ammo box
            muzzle = new Vector3(0.65f, 2.72f, 2.3f);
            return root;
        }

        public static Node3D Technical(Color team, out Vector3 muzzle, out Node3D turret)
        {
            var root = new Node3D();
            var paint = new Color(0.86f, 0.84f, 0.78f);
            var body = MeshKit.TexMatLocal(paint, Tex.Metal, 0.6f);
            MeshKit.Add(root, MeshKit.BoxMesh(new Vector3(1.9f, 0.5f, 5.0f)), new Vector3(0f, 0.85f, 0f), body, null); // chassis
            MeshKit.Add(root, MeshKit.BoxMesh(new Vector3(1.86f, 0.35f, 1.4f)), new Vector3(0f, 1.27f, 1.85f), body, new Vector3(4f, 0f, 0f)); // hood
            MeshKit.Add(root, MeshKit.BoxMesh(new Vector3(1.8f, 0.9f, 1.5f)), new Vector3(0f, 1.55f, 0.55f), body, null); // cab
            MeshKit.Box(root, new Vector3(1.7f, 0.5f, 0.05f), new Vector3(0f, 1.65f, 1.3f), Glass, new Vector3(-18f, 0f, 0f));
            MeshKit.Box(root, new Vector3(1.82f, 0.4f, 1.1f), new Vector3(0f, 1.68f, 0.5f), Glass);
            MeshKit.Box(root, new Vector3(1.95f, 0.2f, 0.15f), new Vector3(0f, 0.75f, 2.55f), Gunmetal); // bumper
            MeshKit.Box(root, new Vector3(1.84f, 0.06f, 1.2f), new Vector3(0f, 2.02f, 0.55f), team); // roof marking
            foreach (float side in new[] { -1f, 1f })
            {
                MeshKit.Add(root, MeshKit.BoxMesh(new Vector3(0.06f, 0.45f, 2.3f)), new Vector3(side * 0.93f, 1.32f, -1.35f), body, null); // bed walls
                foreach (float z in new[] { 1.7f, -1.6f })
                {
                    MeshKit.Tire(root, new Vector3(side * 0.9f, 0.45f, z), 0.45f, new Vector3(0f, 0f, 90f));
                    MeshKit.Cyl(root, 0.2f, 0.2f, 0.26f, new Vector3(side * 0.9f, 0.45f, z), new Color(0.6f, 0.6f, 0.58f), new Vector3(0f, 0f, 90f), 8);
                }
            }

            MeshKit.Box(root, new Vector3(0.06f, 0.45f, 1.9f), new Vector3(0f, 1.32f, -2.48f), paint.Darkened(0.1f)); // tailgate
            // ZU-23-2 on a pedestal: two long barrels with flash hiders, ammo boxes either side, a gunner's seat.
            turret = new Node3D { Position = new Vector3(0f, 1.2f, -1.3f) };
            root.AddChild(turret);
            var olive = new Color(0.32f, 0.36f, 0.24f);
            MeshKit.Cyl(turret, 0.25f, 0.35f, 0.4f, new Vector3(0f, 0.2f, 0f), olive, null, 10);
            MeshKit.Box(turret, new Vector3(1.0f, 0.35f, 0.9f), new Vector3(0f, 0.55f, 0f), olive);
            foreach (float x in new[] { -0.32f, 0.32f })
            {
                MeshKit.Box(turret, new Vector3(0.25f, 0.4f, 0.5f), new Vector3(x * 2f, 0.6f, 0.1f), olive.Darkened(0.1f)); // ammo boxes
            }

            foreach (float x in new[] { -0.15f, 0.15f })
            {
                MeshKit.Beam(turret, new Vector3(x, 0.75f, 0.2f), new Vector3(x, 1.25f, 2.3f), 0.045f, Gunmetal, 6);
                MeshKit.Cyl(turret, 0.07f, 0.07f, 0.25f, new Vector3(x, 1.27f, 2.38f), Gunmetal, new Vector3(76f, 0f, 0f), 6);
            }

            MeshKit.Box(turret, new Vector3(0.4f, 0.08f, 0.4f), new Vector3(0f, 0.85f, -0.5f), Gunmetal); // seat
            muzzle = new Vector3(0f, 1.2f + 1.27f, -1.3f + 2.45f);
            return root;
        }
    }
}
