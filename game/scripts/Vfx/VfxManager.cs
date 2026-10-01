using System.Collections.Generic;
using Godot;
using OCT7.Game.Views;
using OCT7.Game.Visual;
using OCT7.Sim;
using OCT7.Sim.Data;

namespace OCT7.Game.Vfx
{
    /// <summary>
    /// Event-driven combat effects with pooled nodes: muzzle flashes, tracers, rockets with smoke trails,
    /// explosions, dust impacts, deflection sparks, and smoke columns from wrecks. Effects are only shown
    /// where the local player can see.
    /// </summary>
    public partial class VfxManager : Node3D
    {
        private enum Shape
        {
            Sphere,
            Streak,
        }

        private sealed class Particle
        {
            public MeshInstance3D Node;
            public StandardMaterial3D Material;
            public Shape Shape;
            public Vector3 From;
            public Vector3 To;
            public Vector3 Rise;
            public float Age;
            public float Life;
            public float StartScale;
            public float EndScale;
            public Color StartColor;
            public Color EndColor;
            public Vector3 StreakSize;
        }

        private struct SmokeEmitter
        {
            public Vector3 Position;
            public float Until;
            public float Accumulator;
            public bool Fire;
        }

        private readonly List<Particle> _active = new List<Particle>();
        private readonly Stack<Particle> _spheres = new Stack<Particle>();
        private readonly Stack<Particle> _streaks = new Stack<Particle>();
        private readonly List<SmokeEmitter> _emitters = new List<SmokeEmitter>();
        private readonly RandomNumberGenerator _rng = new RandomNumberGenerator { Seed = 1234 };
        private Simulation _sim;
        private ViewRegistry _views;
        private float _time;

        public int LocalPlayerId { get; set; }
        public bool RevealAll { get; set; }

        public void Initialize(Simulation sim, ViewRegistry views)
        {
            _sim = sim;
            _views = views;
        }

        private bool Seen(Vec2 p) => RevealAll || _sim.Vision.IsVisible(LocalPlayerId, p);

        private static Vector3 V3(Vec2 p, float y) => new Vector3(p.X, y, p.Y);

        public void OnEvent(SimEvent e)
        {
            switch (e.Type)
            {
                case SimEventType.ShotFired:
                    if (Seen(e.From) || Seen(e.To))
                    {
                        Shot(e);
                    }

                    break;
                case SimEventType.Deflected:
                    if (Seen(e.To))
                    {
                        Spawn(Shape.Sphere, V3(e.To, 1.6f) + Jitter(1.2f), Vector3.Zero, 0.12f, 0.2f, 0.7f, new Color(1f, 0.9f, 0.6f, 1f), new Color(1f, 0.6f, 0.2f, 0f));
                    }

                    break;
                case SimEventType.SquadDestroyed:
                    if (_sim.Data.HasUnit(e.DefId) && _sim.Data.GetUnit(e.DefId).IsVehicle && Seen(e.To))
                    {
                        Explosion(V3(e.To, 1.2f), 2.2f);
                        _emitters.Add(new SmokeEmitter { Position = V3(e.To, 1.8f), Until = _time + 25f, Fire = true });
                    }

                    break;
                case SimEventType.StructureDestroyed:
                    if (Seen(e.To))
                    {
                        Explosion(V3(e.To, 2f), 3.5f);
                        _emitters.Add(new SmokeEmitter { Position = V3(e.To, 2f), Until = _time + 15f });
                    }

                    break;
            }
        }

        private void Shot(SimEvent e)
        {
            var w = _sim.Data.HasWeapon(e.DefId) ? _sim.Data.GetWeapon(e.DefId) : null;
            var kind = w?.Kind ?? WeaponKind.SmallArms;
            Vector3 muzzle = V3(e.From, 1.3f);
            if (_views.TryGetSquadView(e.SourceId, out var view) && view.Visible)
            {
                muzzle = view.MuzzleWorld(_rng.RandiRange(0, System.Math.Max(0, view.SoldierCount - 1)));
            }

            bool vehicleTarget = _sim.World.GetSquad(e.TargetId)?.Def.IsVehicle ?? false;
            var target = V3(e.To, vehicleTarget ? 1.5f : 1.0f) + Jitter(vehicleTarget ? 0.8f : 1.2f);
            bool hit = e.Value > 0;

            switch (kind)
            {
                case WeaponKind.SmallArms:
                    Flash(muzzle, 0.18f);
                    if (_rng.Randf() < 0.65f)
                    {
                        Tracer(muzzle, target, new Color(1f, 0.85f, 0.45f), 0.09f, 0.035f);
                    }

                    if (!hit)
                    {
                        Dust(target - new Vector3(0f, 0.9f, 0f), 0.5f);
                    }

                    break;
                case WeaponKind.MachineGun:
                case WeaponKind.Autocannon:
                    Flash(muzzle, kind == WeaponKind.Autocannon ? 0.35f : 0.25f);
                    Tracer(muzzle, target, new Color(1f, 0.7f, 0.3f), 0.08f, 0.05f);
                    Tracer(muzzle, target + Jitter(0.8f), new Color(1f, 0.7f, 0.3f), 0.1f, 0.05f);
                    Dust(target - new Vector3(0f, 0.9f, 0f) + Jitter(1f), 0.45f);
                    break;
                case WeaponKind.Sniper:
                    Flash(muzzle, 0.3f);
                    Tracer(muzzle, target, new Color(1f, 1f, 0.8f), 0.06f, 0.05f);
                    Dust(target - new Vector3(0f, 0.9f, 0f), 0.6f);
                    break;
                case WeaponKind.AntiTank:
                    Flash(muzzle, 0.5f);
                    Spawn(Shape.Sphere, muzzle - new Vector3(0f, 0.2f, 0f), new Vector3(0f, 0.5f, 0f), 0.3f, 1.4f, 1.6f, new Color(0.85f, 0.82f, 0.75f, 0.6f), new Color(0.8f, 0.78f, 0.72f, 0f)); // backblast
                    Rocket(muzzle, target, hit);
                    break;
                case WeaponKind.TankGun:
                    Flash(muzzle, 1.0f);
                    Spawn(Shape.Sphere, muzzle, new Vector3(0f, 0.6f, 0f), 0.6f, 2.6f, 1.2f, new Color(0.8f, 0.76f, 0.68f, 0.7f), new Color(0.75f, 0.72f, 0.66f, 0f));
                    Tracer(muzzle, target, new Color(1f, 0.9f, 0.6f), 0.12f, 0.12f);
                    Explosion(target, vehicleTarget ? 1.0f : 1.6f, 0.12f);
                    break;
            }
        }

        private Vector3 Jitter(float r) => new Vector3(_rng.RandfRange(-r, r), _rng.RandfRange(-r * 0.3f, r * 0.3f), _rng.RandfRange(-r, r));

        private void Flash(Vector3 at, float size) =>
            Spawn(Shape.Sphere, at, Vector3.Zero, size, size * 0.4f, 0.06f, new Color(1f, 0.85f, 0.45f, 1f), new Color(1f, 0.6f, 0.2f, 0f));

        private void Dust(Vector3 at, float size) =>
            Spawn(Shape.Sphere, at + new Vector3(0f, 0.2f, 0f), new Vector3(0f, 0.6f, 0f), size * 0.4f, size * 1.4f, 0.8f, new Color(0.72f, 0.65f, 0.52f, 0.55f), new Color(0.7f, 0.64f, 0.52f, 0f));

        private void Tracer(Vector3 from, Vector3 to, Color color, float life, float thickness)
        {
            var p = Spawn(Shape.Streak, from, Vector3.Zero, 1f, 1f, life, color, new Color(color, 0.2f));
            p.To = to;
            p.StreakSize = new Vector3(thickness, thickness, 2.2f);
        }

        private void Rocket(Vector3 from, Vector3 to, bool hit)
        {
            var p = Spawn(Shape.Streak, from, Vector3.Zero, 1f, 1f, 0.32f, new Color(1f, 0.6f, 0.2f), new Color(1f, 0.5f, 0.2f, 1f));
            p.To = to;
            p.StreakSize = new Vector3(0.12f, 0.12f, 0.9f);
            for (int i = 1; i <= 5; i++)
            {
                var at = from.Lerp(to, i / 6f);
                Spawn(Shape.Sphere, at, new Vector3(0f, 0.4f, 0f), 0.2f, 0.9f, 1.4f + i * 0.08f, new Color(0.85f, 0.83f, 0.8f, 0.5f), new Color(0.8f, 0.8f, 0.78f, 0f));
            }

            Explosion(to, hit ? 1.3f : 0.9f, 0.3f);
        }

        private void Explosion(Vector3 at, float size, float delay = 0f)
        {
            var flash = Spawn(Shape.Sphere, at, Vector3.Zero, size * 0.3f, size * 1.6f, 0.35f + delay, new Color(1f, 0.85f, 0.4f, 1f), new Color(1f, 0.4f, 0.1f, 0f));
            flash.Age = -delay;
            for (int i = 0; i < 4; i++)
            {
                var smoke = Spawn(Shape.Sphere, at + Jitter(size * 0.6f), new Vector3(_rng.RandfRange(-0.5f, 0.5f), 1.2f, _rng.RandfRange(-0.5f, 0.5f)), size * 0.4f, size * 1.8f, 2.2f + delay, new Color(0.3f, 0.28f, 0.25f, 0.75f), new Color(0.45f, 0.43f, 0.4f, 0f));
                smoke.Age = -delay;
            }

            var dust = Spawn(Shape.Sphere, new Vector3(at.X, 0.3f, at.Z), new Vector3(0f, 0.3f, 0f), size * 0.6f, size * 2.6f, 2.5f + delay, new Color(0.72f, 0.65f, 0.52f, 0.6f), new Color(0.72f, 0.65f, 0.52f, 0f));
            dust.Age = -delay;
        }

        private Particle Spawn(Shape shape, Vector3 at, Vector3 rise, float startScale, float endScale, float life, Color startColor, Color endColor)
        {
            var stack = shape == Shape.Sphere ? _spheres : _streaks;
            Particle p;
            if (stack.Count > 0)
            {
                p = stack.Pop();
            }
            else
            {
                p = new Particle { Shape = shape };
                p.Material = new StandardMaterial3D
                {
                    ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                    Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                    CullMode = BaseMaterial3D.CullModeEnum.Disabled,
                };
                p.Node = new MeshInstance3D
                {
                    Mesh = shape == Shape.Sphere ? MeshKit.SphereMesh(0.5f, 8, false) : MeshKit.BoxMesh(Vector3.One),
                    MaterialOverride = p.Material,
                    CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                };
                AddChild(p.Node);
            }

            p.From = at;
            p.To = at;
            p.Rise = rise;
            p.Age = 0f;
            p.Life = life;
            p.StartScale = startScale;
            p.EndScale = endScale;
            p.StartColor = startColor;
            p.EndColor = endColor;
            p.Node.Visible = true;
            p.Node.Position = at;
            p.Node.Scale = Vector3.One * startScale;
            p.Material.AlbedoColor = startColor;
            _active.Add(p);
            return p;
        }

        public override void _Process(double delta)
        {
            float dt = (float)delta;
            _time += dt;
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var p = _active[i];
                p.Age += dt;
                if (p.Age < 0f)
                {
                    p.Node.Visible = false;
                    continue;
                }

                p.Node.Visible = true;
                float t = Mathf.Clamp(p.Age / p.Life, 0f, 1f);
                if (p.Shape == Shape.Streak)
                {
                    var pos = p.From.Lerp(p.To, t);
                    var dir = p.To - p.From;
                    p.Node.Position = pos;
                    if (dir.LengthSquared() > 0.01f)
                    {
                        p.Node.LookAt(pos + dir, Mathf.Abs(dir.Normalized().Y) > 0.99f ? Vector3.Right : Vector3.Up);
                    }

                    p.Node.Scale = p.StreakSize;
                }
                else
                {
                    p.Node.Position = p.From + p.Rise * p.Age;
                    p.Node.Scale = Vector3.One * Mathf.Lerp(p.StartScale, p.EndScale, t);
                }

                p.Material.AlbedoColor = p.StartColor.Lerp(p.EndColor, t);
                if (p.Age >= p.Life)
                {
                    p.Node.Visible = false;
                    _active.RemoveAt(i);
                    (p.Shape == Shape.Sphere ? _spheres : _streaks).Push(p);
                }
            }

            for (int i = _emitters.Count - 1; i >= 0; i--)
            {
                var em = _emitters[i];
                if (_time > em.Until)
                {
                    _emitters.RemoveAt(i);
                    continue;
                }

                em.Accumulator += dt;
                while (em.Accumulator > 0.35f)
                {
                    em.Accumulator -= 0.35f;
                    Spawn(Shape.Sphere, em.Position + Jitter(0.6f), new Vector3(0.3f, 1.6f, 0.1f), 0.6f, 2.8f, 4f, new Color(0.2f, 0.19f, 0.18f, 0.6f), new Color(0.4f, 0.4f, 0.4f, 0f));
                    if (em.Fire)
                    {
                        Spawn(Shape.Sphere, em.Position + Jitter(0.5f), new Vector3(0f, 1f, 0f), 0.4f, 0.1f, 0.5f, new Color(1f, 0.55f, 0.15f, 0.9f), new Color(1f, 0.3f, 0.1f, 0f));
                    }
                }

                _emitters[i] = em;
            }
        }
    }
}
