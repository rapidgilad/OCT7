using Godot;
using OCT7.Sim.Data;

namespace OCT7.Game.UI
{
    public enum IconKind
    {
        None,
        Manpower,
        Munitions,
        Fuel,
        Population,
        Rifleman,
        Elite,
        Engineer,
        MachineGun,
        Sniper,
        AntiTank,
        Tank,
        Apc,
        Technical,
        Headquarters,
        Barracks,
        Factory,
        Sandbags,
        Retreat,
        Reinforce,
        Stop,
        Build,
        Back,
        VictoryPoint,
    }

    /// <summary>
    /// Small vector icons drawn in code (no image assets): resources, unit roles, structures and orders.
    /// All shapes are defined in a 0..1 box and scaled to the target rectangle.
    /// </summary>
    public static class Icons
    {
        public static IconKind ForUnit(UnitDef u, WeaponKind primary)
        {
            if (u.IsVehicle)
            {
                return primary == WeaponKind.TankGun ? IconKind.Tank : primary == WeaponKind.Autocannon && u.Id.Contains("zu23") ? IconKind.Technical : IconKind.Apc;
            }

            if (u.Engineer)
            {
                return IconKind.Engineer;
            }

            switch (primary)
            {
                case WeaponKind.MachineGun: return IconKind.MachineGun;
                case WeaponKind.Sniper: return IconKind.Sniper;
                case WeaponKind.AntiTank: return IconKind.AntiTank;
            }

            return u.Weapons.Count > 1 ? IconKind.Elite : IconKind.Rifleman;
        }

        public static IconKind ForStructure(StructureDef d) =>
            d.Kind == StructureKind.Hq ? IconKind.Headquarters : d.Kind == StructureKind.Sandbags ? IconKind.Sandbags : d.Tier >= 3 ? IconKind.Factory : IconKind.Barracks;

        public static void Draw(CanvasItem ci, IconKind kind, Rect2 r, Color c)
        {
            Vector2 P(float x, float y) => r.Position + new Vector2(x * r.Size.X, y * r.Size.Y);
            float s = Mathf.Min(r.Size.X, r.Size.Y);
            float w = Mathf.Max(1.5f, s * 0.09f);
            void Line(float x1, float y1, float x2, float y2, float width = -1f) => ci.DrawLine(P(x1, y1), P(x2, y2), c, width < 0f ? w : width * s, true);
            void Poly(params float[] xy)
            {
                var pts = new Vector2[xy.Length / 2];
                for (int i = 0; i < pts.Length; i++)
                {
                    pts[i] = P(xy[i * 2], xy[i * 2 + 1]);
                }

                ci.DrawColoredPolygon(pts, c);
            }

            void Circle(float x, float y, float radius, bool filled = true)
            {
                if (filled)
                {
                    ci.DrawCircle(P(x, y), radius * s, c);
                }
                else
                {
                    ci.DrawArc(P(x, y), radius * s, 0f, Mathf.Tau, 24, c, w, true);
                }
            }

            void Rifle(float y)
            {
                Line(0.08f, y + 0.12f, 0.92f, y - 0.12f, 0.1f);
                Poly(0.08f, y + 0.06f, 0.3f, y, 0.32f, y + 0.12f, 0.1f, y + 0.22f);
                Line(0.48f, y - 0.02f, 0.5f, y + 0.16f, 0.07f);
            }

            switch (kind)
            {
                case IconKind.Manpower:
                    Circle(0.5f, 0.24f, 0.17f);
                    Poly(0.2f, 0.92f, 0.28f, 0.5f, 0.72f, 0.5f, 0.8f, 0.92f);
                    break;
                case IconKind.Munitions:
                    Poly(0.36f, 0.4f, 0.5f, 0.08f, 0.64f, 0.4f);
                    Poly(0.36f, 0.42f, 0.64f, 0.42f, 0.64f, 0.92f, 0.36f, 0.92f);
                    break;
                case IconKind.Fuel:
                    Poly(0.5f, 0.06f, 0.78f, 0.56f, 0.22f, 0.56f);
                    Circle(0.5f, 0.64f, 0.29f);
                    break;
                case IconKind.Population:
                    Circle(0.3f, 0.32f, 0.12f);
                    Circle(0.7f, 0.32f, 0.12f);
                    Poly(0.1f, 0.85f, 0.16f, 0.52f, 0.44f, 0.52f, 0.5f, 0.85f);
                    Poly(0.5f, 0.85f, 0.56f, 0.52f, 0.84f, 0.52f, 0.9f, 0.85f);
                    break;
                case IconKind.Rifleman:
                    Rifle(0.5f);
                    break;
                case IconKind.Elite:
                    Rifle(0.62f);
                    Poly(0.5f, 0.06f, 0.58f, 0.24f, 0.78f, 0.24f, 0.62f, 0.36f, 0.68f, 0.54f, 0.5f, 0.43f, 0.32f, 0.54f, 0.38f, 0.36f, 0.22f, 0.24f, 0.42f, 0.24f);
                    break;
                case IconKind.Engineer:
                    Line(0.2f, 0.85f, 0.68f, 0.32f, 0.11f);
                    Circle(0.74f, 0.26f, 0.16f, false);
                    Poly(0.12f, 0.12f, 0.42f, 0.12f, 0.42f, 0.24f, 0.3f, 0.24f, 0.3f, 0.5f, 0.24f, 0.5f, 0.24f, 0.24f, 0.12f, 0.24f);
                    break;
                case IconKind.MachineGun:
                    Line(0.06f, 0.42f, 0.95f, 0.42f, 0.11f);
                    Poly(0.06f, 0.36f, 0.28f, 0.36f, 0.3f, 0.6f, 0.08f, 0.62f);
                    Poly(0.36f, 0.46f, 0.52f, 0.46f, 0.52f, 0.64f, 0.36f, 0.64f);
                    Line(0.74f, 0.44f, 0.62f, 0.86f);
                    Line(0.74f, 0.44f, 0.86f, 0.86f);
                    break;
                case IconKind.Sniper:
                    Circle(0.5f, 0.5f, 0.34f, false);
                    Line(0.5f, 0.06f, 0.5f, 0.34f);
                    Line(0.5f, 0.66f, 0.5f, 0.94f);
                    Line(0.06f, 0.5f, 0.34f, 0.5f);
                    Line(0.66f, 0.5f, 0.94f, 0.5f);
                    Circle(0.5f, 0.5f, 0.05f);
                    break;
                case IconKind.AntiTank:
                    Poly(0.62f, 0.24f, 0.94f, 0.5f, 0.62f, 0.76f);
                    Poly(0.18f, 0.4f, 0.62f, 0.4f, 0.62f, 0.6f, 0.18f, 0.6f);
                    Poly(0.04f, 0.26f, 0.2f, 0.4f, 0.2f, 0.6f, 0.04f, 0.74f);
                    break;
                case IconKind.Tank:
                    Poly(0.04f, 0.62f, 0.96f, 0.62f, 0.86f, 0.86f, 0.14f, 0.86f);
                    Poly(0.26f, 0.38f, 0.62f, 0.38f, 0.68f, 0.6f, 0.2f, 0.6f);
                    Line(0.62f, 0.46f, 0.98f, 0.4f, 0.08f);
                    break;
                case IconKind.Apc:
                    Poly(0.06f, 0.36f, 0.84f, 0.36f, 0.96f, 0.56f, 0.96f, 0.76f, 0.06f, 0.76f);
                    Circle(0.24f, 0.8f, 0.1f);
                    Circle(0.5f, 0.8f, 0.1f);
                    Circle(0.76f, 0.8f, 0.1f);
                    Poly(0.56f, 0.2f, 0.7f, 0.2f, 0.7f, 0.36f, 0.56f, 0.36f);
                    break;
                case IconKind.Technical:
                    Poly(0.04f, 0.5f, 0.96f, 0.5f, 0.96f, 0.72f, 0.04f, 0.72f);
                    Poly(0.6f, 0.5f, 0.66f, 0.3f, 0.9f, 0.3f, 0.96f, 0.5f);
                    Circle(0.24f, 0.76f, 0.1f);
                    Circle(0.78f, 0.76f, 0.1f);
                    Line(0.22f, 0.5f, 0.5f, 0.12f, 0.06f);
                    Line(0.3f, 0.5f, 0.58f, 0.16f, 0.06f);
                    break;
                case IconKind.Headquarters:
                    Line(0.24f, 0.08f, 0.24f, 0.94f, 0.08f);
                    Poly(0.27f, 0.1f, 0.86f, 0.24f, 0.27f, 0.42f);
                    Poly(0.08f, 0.88f, 0.92f, 0.88f, 0.92f, 0.96f, 0.08f, 0.96f);
                    break;
                case IconKind.Barracks:
                    Poly(0.1f, 0.48f, 0.5f, 0.14f, 0.9f, 0.48f);
                    Poly(0.18f, 0.48f, 0.82f, 0.48f, 0.82f, 0.9f, 0.18f, 0.9f);
                    break;
                case IconKind.Factory:
                    Poly(0.06f, 0.9f, 0.06f, 0.42f, 0.32f, 0.58f, 0.32f, 0.42f, 0.58f, 0.58f, 0.58f, 0.42f, 0.84f, 0.58f, 0.84f, 0.12f, 0.94f, 0.12f, 0.94f, 0.9f);
                    break;
                case IconKind.Sandbags:
                    foreach (var (x, y) in new[] { (0.24f, 0.74f), (0.5f, 0.74f), (0.76f, 0.74f), (0.37f, 0.5f), (0.63f, 0.5f), (0.5f, 0.27f) })
                    {
                        ci.DrawSetTransform(P(x, y), 0f, new Vector2(1f, 0.55f));
                        ci.DrawCircle(Vector2.Zero, s * 0.15f, c);
                        ci.DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
                    }

                    break;
                case IconKind.Retreat:
                    Poly(0.06f, 0.5f, 0.42f, 0.16f, 0.42f, 0.36f, 0.92f, 0.36f, 0.92f, 0.64f, 0.42f, 0.64f, 0.42f, 0.84f);
                    break;
                case IconKind.Reinforce:
                    Poly(0.4f, 0.1f, 0.6f, 0.1f, 0.6f, 0.4f, 0.9f, 0.4f, 0.9f, 0.6f, 0.6f, 0.6f, 0.6f, 0.9f, 0.4f, 0.9f, 0.4f, 0.6f, 0.1f, 0.6f, 0.1f, 0.4f, 0.4f, 0.4f);
                    break;
                case IconKind.Stop:
                    Poly(0.2f, 0.2f, 0.8f, 0.2f, 0.8f, 0.8f, 0.2f, 0.8f);
                    break;
                case IconKind.Build:
                    Line(0.24f, 0.9f, 0.6f, 0.4f, 0.12f);
                    Poly(0.42f, 0.24f, 0.7f, 0.06f, 0.94f, 0.36f, 0.68f, 0.52f);
                    break;
                case IconKind.Back:
                    Line(0.62f, 0.14f, 0.26f, 0.5f, 0.12f);
                    Line(0.26f, 0.5f, 0.62f, 0.86f, 0.12f);
                    break;
                case IconKind.VictoryPoint:
                    Poly(0.5f, 0.06f, 0.94f, 0.5f, 0.5f, 0.94f, 0.06f, 0.5f);
                    break;
            }
        }
    }
}
