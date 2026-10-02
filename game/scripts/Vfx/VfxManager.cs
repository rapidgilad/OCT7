using System.Collections.Generic;
using Godot;
using OCT7.Game.Views;
using OCT7.Game.Visual;
using OCT7.Sim;
using OCT7.Sim.Data;

namespace OCT7.Game.Vfx
{
    /// <summary>
    /// Event-driven combat effects with pooled nodes:
    /// - soft billboard puffs (smoke, dust, fireballs; additive for fire and flashes),
    /// - tracers, ballistic debris chunks, ground shockwave rings, lingering scorch marks,
    /// - short light flashes for big guns and explosions, smoke and fire columns from wrecks.
    /// Only events the local player can see produce effects.
    /// </summary>
    public partial class VfxManager : Node3D
    {
        private const int LightCount = 6;

        private enum Kind
        {
            Puff,
            Streak,
            Chunk,
            Ring,
            Scorch,
        }

        private sealed class Particle
        {
            public MeshInstance3D Node;
            public StandardMaterial3D Material;
            public Kind Kind;
            public Vector3 Position;
            public Vector3 Velocity;
            public Vector3 To;
            public Vector3 Spin;
            public float Gravity;
            public float Drag;
            public float Age;
            public float Life;
            public float StartScale;
            public float EndScale;
            public Color StartColor;
            public Color EndColor;
            public Vector3 StreakSize;
        }

        private struct Flash
        {
            public OmniLight3D Light;
            public float Age;
            public float Life;
            public float Energy;
        }

        private struct SmokeEmitter
        {
            public Vector3 Position;
            public float Until;
            public float Accumulator;
            public bool Fire;
            public float Size;
        }

        private readonly List<Particle> _active = new List<Particle>();
        private readonly Dictionary<Kind, Stack<Particle>> _pools = new Dictionary<Kind, Stack<Particle>>();
        private readonly List<SmokeEmitter> _emitters = new List<SmokeEmitter>();
        private readonly Flash[] _lights = new Flash[LightCount];
        private readonly RandomNumberGenerator _rng = new RandomNumberGenerator { Seed = 1234 };
        private Simulation _sim;
        private ViewRegistry _views;
        private float _time;
        private int _nextLight;

        public int LocalPlayerId { get; set; }
        public bool RevealAll { get; set; }

        public void Initialize(Simulation sim, ViewRegistry views)
        {
            _sim = sim;
            _views = views;
            for (int i = 0; i < LightCount; i++)
            {
                var light = new OmniLight3D { Visible = false, LightColor = new Color(1f, 0.7f, 0.35f), OmniRange = 10f, ShadowEnabled = false };
                AddChild(light);
                _lights[i] = new Flash { Light = light };
            }
        }

        private bool Seen(Vec2 p) => RevealAll || _sim.Vision.IsVisible(LocalPlayerId, p);

        private static Vector3 V3(Vec2 p, float y) => new Vector3(p.X, y, p.Y);

        // ------------------------------------------------------------------ events

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
                        Sparks(V3(e.To, 1.6f) + Jitter(1.0f), 8);
                    }

                    break;
                case SimEventType.SquadDestroyed:
                    if (_sim.Data.HasUnit(e.DefId) && _sim.Data.GetUnit(e.DefId).IsVehicle && Seen(e.To))
                    {
                        Explosion(V3(e.To, 1.2f), 2.2f);
                        Explosion(V3(e.To, 1.8f) + Jitter(1f), 1.2f, 0.35f);
                        _emitters.Add(new SmokeEmitter { Position = V3(e.To, 1.8f), Until = _time + 30f, Fire = true, Size = 1f });
                    }

                    break;
                case SimEventType.StructureDestroyed:
                    if (Seen(e.To))
                    {
                        Explosion(V3(e.To, 2f), 3.2f);
                        Explosion(V3(e.To, 3f) + Jitter(3f), 1.6f, 0.3f);
                        Explosion(V3(e.To, 2f) + Jitter(3f), 1.4f, 0.65f);
                        _emitters.Add(new SmokeEmitter { Position = V3(e.To, 2f), Until = _time + 18f, Fire = true, Size = 1.6f });
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
            var direction = (target - muzzle).Normalized();

            switch (kind)
            {
                case WeaponKind.SmallArms:
                    MuzzleFlash(muzzle, direction, 0.45f);
                    if (_rng.Randf() < 0.7f)
                    {
                        Tracer(muzzle, target, new Color(1f, 0.82f, 0.45f), 0.1f, 0.035f);
                    }

                    if (!hit)
                    {
                        Impact(new Vector3(target.X, 0.15f, target.Z), 0.5f);
                    }

                    break;
                case WeaponKind.MachineGun:
                    MuzzleFlash(muzzle, direction, 0.6f);
                    Tracer(muzzle, target, new Color(1f, 0.6f, 0.25f), 0.08f, 0.05f);
                    Tracer(muzzle, target + Jitter(0.8f), new Color(1f, 0.6f, 0.25f), 0.11f, 0.05f, 0.04f);
                    Impact(new Vector3(target.X, 0.15f, target.Z) + Jitter(1f), 0.45f);
                    break;
                case WeaponKind.Sniper:
                    MuzzleFlash(muzzle, direction, 0.8f);
                    Puff(muzzle, direction * 2f + new Vector3(0f, 0.4f, 0f), 0.3f, 1.2f, 1.2f, new Color(0.8f, 0.78f, 0.72f, 0.4f), new Color(0.8f, 0.78f, 0.72f, 0f));
                    Tracer(muzzle, target, new Color(1f, 1f, 0.85f), 0.07f, 0.05f);
                    Impact(new Vector3(target.X, 0.15f, target.Z), 0.7f);
                    break;
                case WeaponKind.Autocannon:
                    MuzzleFlash(muzzle, direction, 0.9f);
                    Tracer(muzzle, target, new Color(1f, 0.55f, 0.2f), 0.12f, 0.08f);
                    Explosion(target, 0.5f, 0.1f);
                    break;
                case WeaponKind.AntiTank:
                    MuzzleFlash(muzzle, direction, 1.0f);
                    for (int i = 0; i < 4; i++)
                    {
                        Puff(muzzle - direction * (0.8f + i * 0.5f), -direction * 2.5f + Jitter(0.6f) + new Vector3(0f, 0.6f, 0f), 0.5f, 2.2f, 1.8f + i * 0.2f, new Color(0.82f, 0.8f, 0.74f, 0.55f), new Color(0.8f, 0.78f, 0.72f, 0f), drag: 1.5f); // backblast
                    }

                    Rocket(muzzle, target, hit);
                    break;
                case WeaponKind.TankGun:
                    MuzzleFlash(muzzle, direction, 2.2f);
                    LightFlash(muzzle, 7f, 14f, 0.15f);
                    for (int i = 0; i < 5; i++)
                    {
                        Puff(muzzle + direction * (i * 0.6f), direction * (3f - i * 0.4f) + Jitter(0.8f) + new Vector3(0f, 0.5f, 0f), 0.8f, 3.2f, 2.4f, new Color(0.78f, 0.75f, 0.68f, 0.6f), new Color(0.75f, 0.72f, 0.66f, 0f), drag: 1.2f);
                    }

                    Tracer(muzzle, target, new Color(1f, 0.9f, 0.6f), 0.12f, 0.14f);
                    Explosion(target, vehicleTarget ? 1.1f : 1.6f, 0.12f);
                    break;
            }
        }

        /// <summary>Art review (--vfx-test): plays sample effects without any sim events.</summary>
        public void PlaySample(int step, Vector3 origin)
        {
            switch (step % 4)
            {
                case 0:
                    Explosion(origin, 1.3f);
                    break;
                case 1:
                    Explosion(origin + new Vector3(8f, 1f, 0f), 2.2f);
                    _emitters.Add(new SmokeEmitter { Position = origin + new Vector3(8f, 1.8f, 0f), Until = _time + 6f, Fire = true, Size = 1f });
                    break;
                case 2:
                    for (int i = 0; i < 6; i++)
                    {
                        var from = origin + new Vector3(-10f, 1.3f, i * 0.8f);
                        MuzzleFlash(from, Vector3.Right, 0.6f);
                        Tracer(from, origin + new Vector3(6f, 1f, i * 0.8f), new Color(1f, 0.6f, 0.25f), 0.1f, 0.05f, i * 0.03f);
                    }

                    Sparks(origin + new Vector3(6f, 1.4f, 2f), 8);
                    break;
                default:
                    Rocket(origin + new Vector3(-12f, 1.4f, -4f), origin + new Vector3(2f, 1f, -4f), true);
                    break;
            }
        }

        // ------------------------------------------------------------------ effect recipes

        private Vector3 Jitter(float r) => new Vector3(_rng.RandfRange(-r, r), _rng.RandfRange(-r * 0.3f, r * 0.3f), _rng.RandfRange(-r, r));

        private void MuzzleFlash(Vector3 at, Vector3 direction, float size)
        {
            var p = Spawn(Kind.Puff, at + direction * size * 0.25f, Vector3.Zero, size * 0.6f, size, 0.06f, new Color(1f, 0.9f, 0.6f, 1f), new Color(1f, 0.6f, 0.2f, 0f), additive: true, sprite: Sprite.Flash);
            p.Node.RotationDegrees = new Vector3(0f, 0f, _rng.RandfRange(0f, 72f));
        }

        private void Tracer(Vector3 from, Vector3 to, Color color, float life, float thickness, float delay = 0f)
        {
            var p = Spawn(Kind.Streak, from, Vector3.Zero, 1f, 1f, life, color, new Color(color, 0.4f), additive: true, delay: delay);
            p.To = to;
            p.StreakSize = new Vector3(thickness, thickness, 2.4f);
        }

        private void Puff(Vector3 at, Vector3 velocity, float startScale, float endScale, float life, Color start, Color end, bool additive = false, float delay = 0f, float drag = 0.6f, float gravity = 0f) =>
            Spawn(Kind.Puff, at, velocity, startScale, endScale, life, start, end, additive, delay, drag, gravity);

        private void Impact(Vector3 at, float size)
        {
            Puff(at, new Vector3(0f, 1.2f, 0f) + Jitter(0.4f), size * 0.4f, size * 1.6f, 0.9f, new Color(0.74f, 0.66f, 0.52f, 0.7f), new Color(0.72f, 0.65f, 0.52f, 0f));
            if (_rng.Randf() < 0.5f)
            {
                Chunk(at, new Vector3(_rng.RandfRange(-1.5f, 1.5f), _rng.RandfRange(2f, 4f), _rng.RandfRange(-1.5f, 1.5f)), 0.06f, new Color(0.5f, 0.44f, 0.34f), 0.7f);
            }
        }

        private void Sparks(Vector3 at, int count)
        {
            for (int i = 0; i < count; i++)
            {
                var v = new Vector3(_rng.RandfRange(-5f, 5f), _rng.RandfRange(1f, 5f), _rng.RandfRange(-5f, 5f));
                var p = Spawn(Kind.Puff, at, v, 0.18f, 0.05f, 0.35f, new Color(1f, 0.85f, 0.45f, 1f), new Color(1f, 0.5f, 0.15f, 0f), additive: true, drag: 1.5f, gravity: -9f);
                p.Node.RotationDegrees = new Vector3(0f, 0f, _rng.RandfRange(0f, 90f));
            }

            Puff(at, Vector3.Up * 0.5f, 0.6f, 1.2f, 0.12f, new Color(1f, 0.9f, 0.7f, 1f), new Color(1f, 0.6f, 0.2f, 0f), additive: true);
        }

        private void Chunk(Vector3 at, Vector3 velocity, float size, Color color, float life)
        {
            var p = Spawn(Kind.Chunk, at, velocity, size, size * 0.6f, life, color, color);
            p.Gravity = -9.8f;
            p.Spin = new Vector3(_rng.RandfRange(-12f, 12f), _rng.RandfRange(-12f, 12f), _rng.RandfRange(-12f, 12f));
        }

        private void Rocket(Vector3 from, Vector3 to, bool hit)
        {
            const float flight = 0.3f;
            var p = Spawn(Kind.Streak, from, Vector3.Zero, 1f, 1f, flight, new Color(1f, 0.7f, 0.3f), new Color(1f, 0.55f, 0.2f, 1f), additive: true);
            p.To = to;
            p.StreakSize = new Vector3(0.14f, 0.14f, 1.0f);
            int puffs = Mathf.Clamp((int)(from.DistanceTo(to) / 1.2f), 6, 30);
            for (int i = 1; i <= puffs; i++)
            {
                float t = i / (float)(puffs + 1);
                Puff(from.Lerp(to, t), new Vector3(0f, 0.3f, 0f) + Jitter(0.15f), 0.5f, 1.8f, 1.8f + t * 0.6f, new Color(0.88f, 0.86f, 0.82f, 0.5f), new Color(0.84f, 0.82f, 0.8f, 0f), delay: flight * t);
            }

            Explosion(to, hit ? 1.3f : 0.9f, flight);
        }

        private void Explosion(Vector3 at, float size, float delay = 0f)
        {
            var ground = new Vector3(at.X, 0.12f, at.Z);
            LightFlash(at, 5f * size, 7f * size, 0.22f, delay);
            Puff(at, Vector3.Zero, size * 0.8f, size * 2.6f, 0.2f, new Color(1f, 0.95f, 0.75f, 1f), new Color(1f, 0.6f, 0.2f, 0f), additive: true, delay: delay);
            for (int i = 0; i < 5; i++)
            {
                Puff(at + Jitter(size * 0.5f), new Vector3(0f, 1.5f, 0f) + Jitter(size), size * 0.6f, size * 1.9f, _rng.RandfRange(0.45f, 0.75f), new Color(1f, 0.62f, 0.2f, 1f), new Color(0.45f, 0.12f, 0.04f, 0f), delay: delay, drag: 2f);
            }

            for (int i = 0; i < 6; i++)
            {
                Puff(at + Jitter(size * 0.6f), new Vector3(0f, _rng.RandfRange(1.6f, 3.2f), 0f) + Jitter(size * 0.8f), size * 1.1f, size * 4.4f, _rng.RandfRange(3f, 4.6f), new Color(0.2f, 0.18f, 0.16f, 0.92f), new Color(0.42f, 0.4f, 0.37f, 0f), delay: delay + 0.08f, drag: 0.9f);
            }

            for (int i = 0; i < 7; i++)
            {
                float a = i / 7f * Mathf.Tau;
                var outward = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Puff(ground + outward * size * 0.4f, outward * _rng.RandfRange(3f, 5f) * size * 0.6f, size * 0.6f, size * 2.4f, _rng.RandfRange(1.4f, 2.2f), new Color(0.74f, 0.66f, 0.52f, 0.65f), new Color(0.72f, 0.65f, 0.52f, 0f), delay: delay, drag: 2.5f);
            }

            var ring = Spawn(Kind.Ring, ground + new Vector3(0f, 0.05f, 0f), Vector3.Zero, 0.4f, size * 5f, 0.3f, new Color(1f, 0.95f, 0.85f, 0.22f), new Color(1f, 0.9f, 0.7f, 0f), delay: delay);
            ring.Node.Rotation = Vector3.Zero;
            int chunks = Mathf.Clamp((int)(size * 3.5f), 3, 10);
            for (int i = 0; i < chunks; i++)
            {
                var v = new Vector3(_rng.RandfRange(-4f, 4f), _rng.RandfRange(4f, 9f), _rng.RandfRange(-4f, 4f)) * Mathf.Sqrt(size);
                var dirt = MeshKit.Vary(new Color(0.36f, 0.31f, 0.25f), 0.1f, i);
                var p = Spawn(Kind.Chunk, at, v, _rng.RandfRange(0.08f, 0.2f) * Mathf.Sqrt(size), 0.02f, _rng.RandfRange(1.2f, 2.0f), dirt, dirt, delay: delay);
                p.Gravity = -9.8f;
                p.Spin = new Vector3(_rng.RandfRange(-10f, 10f), _rng.RandfRange(-10f, 10f), _rng.RandfRange(-10f, 10f));
            }

            var scorch = Spawn(Kind.Scorch, ground + new Vector3(0f, -0.02f, 0f), Vector3.Zero, size * 2.4f, size * 2.6f, 45f, new Color(1f, 1f, 1f, 0.9f), new Color(1f, 1f, 1f, 0f), delay: delay);
            scorch.Node.RotationDegrees = new Vector3(0f, _rng.RandfRange(0f, 360f), 0f);
        }

        private void LightFlash(Vector3 at, float energy, float range, float life, float delay = 0f)
        {
            ref var f = ref _lights[_nextLight];
            _nextLight = (_nextLight + 1) % LightCount;
            f.Light.Position = at + new Vector3(0f, 1f, 0f);
            f.Light.OmniRange = range;
            f.Energy = energy;
            f.Life = life;
            f.Age = -delay;
            f.Light.Visible = false;
        }

        // ------------------------------------------------------------------ pool

        private Particle Spawn(Kind kind, Vector3 at, Vector3 velocity, float startScale, float endScale, float life, Color startColor, Color endColor,
            bool additive = false, float delay = 0f, float drag = 0.6f, float gravity = 0f, Sprite sprite = Sprite.Soft)
        {
            if (!_pools.TryGetValue(kind, out var pool))
            {
                pool = new Stack<Particle>();
                _pools[kind] = pool;
            }

            var p = pool.Count > 0 ? pool.Pop() : Create(kind);
            p.Position = at;
            p.Velocity = velocity;
            p.To = at;
            p.Spin = Vector3.Zero;
            p.Gravity = gravity;
            p.Drag = drag;
            p.Age = -delay;
            p.Life = life;
            p.StartScale = startScale;
            p.EndScale = endScale;
            p.StartColor = startColor;
            p.EndColor = endColor;
            p.Node.Visible = delay <= 0f;
            p.Node.Position = at;
            p.Node.Scale = Vector3.One * startScale;
            p.Node.Rotation = Vector3.Zero;
            p.Material.AlbedoColor = startColor;
            p.Material.BlendMode = additive ? BaseMaterial3D.BlendModeEnum.Add : BaseMaterial3D.BlendModeEnum.Mix;
            if (kind == Kind.Puff)
            {
                p.Material.AlbedoTexture = Textures.GetSprite(sprite);
            }

            _active.Add(p);
            return p;
        }

        private Particle Create(Kind kind)
        {
            var p = new Particle { Kind = kind };
            p.Material = new StandardMaterial3D
            {
                ShadingMode = kind == Kind.Chunk ? BaseMaterial3D.ShadingModeEnum.PerPixel : BaseMaterial3D.ShadingModeEnum.Unshaded,
                Transparency = kind == Kind.Chunk ? BaseMaterial3D.TransparencyEnum.Disabled : BaseMaterial3D.TransparencyEnum.Alpha,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            };
            Mesh mesh;
            switch (kind)
            {
                case Kind.Puff:
                    mesh = Cached(ref _quad, () => new QuadMesh { Size = Vector2.One });
                    p.Material.BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled;
                    p.Material.BillboardKeepScale = true;
                    p.Material.ProximityFadeEnabled = true;
                    p.Material.ProximityFadeDistance = 0.6f;
                    break;
                case Kind.Ring:
                    mesh = MeshKit.RingMesh(1f, 0.08f, 40);
                    break;
                case Kind.Scorch:
                    mesh = Cached(ref _plane, () => new PlaneMesh { Size = Vector2.One });
                    p.Material.AlbedoTexture = Textures.GetSprite(Sprite.Scorch);
                    p.Material.ShadingMode = BaseMaterial3D.ShadingModeEnum.PerPixel;
                    p.Material.RenderPriority = -1;
                    break;
                default:
                    mesh = MeshKit.BoxMesh(Vector3.One);
                    break;
            }

            p.Node = new MeshInstance3D { Mesh = mesh, MaterialOverride = p.Material, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            AddChild(p.Node);
            return p;
        }

        private Mesh _quad;
        private Mesh _plane;

        private static Mesh Cached(ref Mesh field, System.Func<Mesh> create) => field ??= create();

        // ------------------------------------------------------------------ update

        public override void _Process(double delta)
        {
            // Clamp the step so short effects (flashes, tracers) are drawn for at least a couple of frames even when
            // the frame rate drops (software rendering, hitches); they then simply last a little longer in real time.
            float dt = Mathf.Min((float)delta, 1f / 30f);
            _time += dt;
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var p = _active[i];
                p.Age += dt;
                if (p.Age < 0f)
                {
                    continue;
                }

                p.Node.Visible = true;
                float t = Mathf.Clamp(p.Age / p.Life, 0f, 1f);
                switch (p.Kind)
                {
                    case Kind.Streak:
                        {
                            var pos = p.Position.Lerp(p.To, t);
                            var dir = p.To - p.Position;
                            p.Node.Position = pos;
                            if (dir.LengthSquared() > 0.01f)
                            {
                                p.Node.LookAt(pos + dir, Mathf.Abs(dir.Normalized().Y) > 0.99f ? Vector3.Right : Vector3.Up);
                            }

                            p.Node.Scale = p.StreakSize;
                            break;
                        }

                    case Kind.Chunk:
                        {
                            p.Velocity += new Vector3(0f, p.Gravity * dt, 0f);
                            var pos = p.Node.Position + p.Velocity * dt;
                            if (pos.Y < 0.05f)
                            {
                                pos.Y = 0.05f;
                                p.Velocity = new Vector3(p.Velocity.X * 0.4f, -p.Velocity.Y * 0.25f, p.Velocity.Z * 0.4f);
                                p.Spin *= 0.5f;
                            }

                            p.Node.Position = pos;
                            p.Node.Rotation += p.Spin * dt;
                            p.Node.Scale = Vector3.One * Mathf.Lerp(p.StartScale, p.EndScale, Mathf.Max(0f, t - 0.7f) / 0.3f);
                            break;
                        }

                    case Kind.Scorch:
                        p.Node.Scale = new Vector3(p.StartScale, 1f, p.StartScale);
                        p.Material.AlbedoColor = p.StartColor.Lerp(p.EndColor, Mathf.Max(0f, t - 0.75f) / 0.25f);
                        break;
                    default:
                        {
                            // Puffs and rings: drift with drag, ease-out growth.
                            p.Velocity *= Mathf.Max(0f, 1f - p.Drag * dt);
                            p.Velocity += new Vector3(0f, p.Gravity * dt, 0f);
                            p.Node.Position += p.Velocity * dt;
                            float grow = 1f - (1f - t) * (1f - t);
                            float scale = Mathf.Lerp(p.StartScale, p.EndScale, grow);
                            p.Node.Scale = p.Kind == Kind.Ring ? new Vector3(scale, 1f, scale) : Vector3.One * scale;
                            break;
                        }
                }

                if (p.Kind != Kind.Scorch && p.Kind != Kind.Chunk)
                {
                    p.Material.AlbedoColor = p.StartColor.Lerp(p.EndColor, t);
                }

                if (p.Age >= p.Life)
                {
                    p.Node.Visible = false;
                    _active.RemoveAt(i);
                    _pools[p.Kind].Push(p);
                }
            }

            for (int i = 0; i < LightCount; i++)
            {
                ref var f = ref _lights[i];
                if (f.Life <= 0f)
                {
                    continue;
                }

                f.Age += dt;
                if (f.Age < 0f)
                {
                    continue;
                }

                float k = 1f - f.Age / f.Life;
                f.Light.Visible = k > 0f;
                f.Light.LightEnergy = f.Energy * Mathf.Max(0f, k) * Mathf.Max(0f, k);
                if (k <= 0f)
                {
                    f.Life = 0f;
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
                float fade = Mathf.Clamp((em.Until - _time) / 6f, 0f, 1f);
                while (em.Accumulator > 0.25f)
                {
                    em.Accumulator -= 0.25f;
                    float s = em.Size;
                    Puff(em.Position + Jitter(0.5f * s), new Vector3(0.4f, _rng.RandfRange(1.4f, 2.2f), 0.15f), 0.7f * s, 3.4f * s, 4.5f, new Color(0.13f, 0.12f, 0.11f, 0.7f * fade), new Color(0.4f, 0.39f, 0.37f, 0f), drag: 0.2f);
                    if (em.Fire && fade > 0.3f)
                    {
                        Puff(em.Position + Jitter(0.4f * s), new Vector3(0f, _rng.RandfRange(1.5f, 2.5f), 0f), 0.6f * s, 0.15f * s, 0.6f, new Color(1f, 0.55f, 0.15f, 0.9f), new Color(1f, 0.25f, 0.05f, 0f), additive: true, drag: 0.5f);
                    }
                }

                _emitters[i] = em;
            }
        }
    }
}
