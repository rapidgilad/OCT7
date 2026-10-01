using System.Collections.Generic;
using Godot;
using OCT7.Game.Visual;
using OCT7.Sim.Data;
using OCT7.Sim.Units;

namespace OCT7.Game.Views
{
    /// <summary>
    /// Visuals for one squad: procedural soldiers in a loose formation (or a vehicle), team disc, selection ring,
    /// and procedural animation — walk cycle, crouch when suppressed, prone when pinned, recoil when firing,
    /// falling when killed (corpses stay where they fell), scale-in when reinforced. Local +Z is forward.
    /// </summary>
    public partial class SquadView : Node3D
    {
        private const float CorpseSeconds = 25f;

        private readonly List<SoldierRig> _soldiers = new List<SoldierRig>();
        private readonly List<Vector3> _slots = new List<Vector3>();
        private readonly List<float> _reviveTimers = new List<float>();
        private UnitDef _def;
        private Color _team;
        private WeaponKind _primaryKind;
        private bool _hasSetupAtgm;
        private Node3D _vehicle;
        private Node3D _turret;
        private Node3D _launcher;
        private MeshInstance3D _ring;
        private float _walkPhase;
        private Vector3 _lastPosition;
        private float _speed;
        private float _turretYaw;
        private Node3D _corpses;

        public int SquadId { get; private set; }
        public bool IsVehicle => _def != null && _def.IsVehicle;
        public Vector3 VehicleMuzzle { get; private set; }

        public void Build(Squad squad, Color team, Node3D corpseContainer)
        {
            SquadId = squad.Id;
            _def = squad.Def;
            _team = team;
            _corpses = corpseContainer;
            Name = $"Squad{squad.Id}_{squad.Def.Id}";
            _primaryKind = squad.Weapons.Count > 0 ? squad.Weapons[0].Kind : WeaponKind.SmallArms;
            _hasSetupAtgm = squad.Weapons.Count > 0 && squad.Weapons[0].Kind == WeaponKind.AntiTank && squad.Weapons[0].NeedsSetup;

            float footprint;
            if (_def.IsVehicle)
            {
                _vehicle = ModelFactory.BuildVehicle(_def, team, out var muzzle, out _turret);
                VehicleMuzzle = muzzle;
                AddChild(_vehicle);
                footprint = 4.2f;
            }
            else
            {
                BuildSlots(_def.SquadSize);
                for (int i = 0; i < _def.SquadSize; i++)
                {
                    var rig = NewSoldier(i);
                    rig.Root.Visible = squad.GetModelHealth(i) > 0f;
                    _soldiers.Add(rig);
                    _reviveTimers.Add(0f);
                }

                if (_hasSetupAtgm)
                {
                    _launcher = ModelFactory.BuildAtgmLauncher(team);
                    _launcher.Position = new Vector3(0.6f, 0f, 1.4f);
                    AddChild(_launcher);
                }

                footprint = 2.2f + 0.45f * _def.SquadSize;
            }

            // Ground decals: a faint team-colored ring always, a bright ring when selected (CoH-style outlines, no filled discs).
            AddChild(new MeshInstance3D
            {
                Mesh = MeshKit.RingMesh(footprint, 0.15f, 40),
                MaterialOverride = MeshKit.Flat(new Color(team.Lightened(0.2f), 0.45f)),
                Position = new Vector3(0f, 0.05f, 0f),
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            });

            _ring = new MeshInstance3D
            {
                Mesh = MeshKit.RingMesh(footprint + 0.45f, 0.32f, 48),
                MaterialOverride = MeshKit.Flat(new Color(0.7f, 1f, 0.6f, 0.95f)),
                Position = new Vector3(0f, 0.06f, 0f),
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                Visible = false,
            };
            AddChild(_ring);
        }

        private SoldierRig NewSoldier(int index)
        {
            bool special = index == 0 && _primaryKind != WeaponKind.SmallArms;
            var rig = ModelFactory.BuildSoldier(_def, _primaryKind, _team, index, special && !_hasSetupAtgm);
            rig.Root.Position = _slots[index];
            AddChild(rig.Root);
            return rig;
        }

        private void BuildSlots(int count)
        {
            int columns = count <= 3 ? count : 3;
            for (int i = 0; i < count; i++)
            {
                int row = i / columns;
                int col = i % columns;
                float jitterX = (MeshKit.Hash01(i, 11) - 0.5f) * 0.6f;
                float jitterZ = (MeshKit.Hash01(i, 12) - 0.5f) * 0.6f;
                float x = (col - (columns - 1) * 0.5f) * 1.9f + jitterX + (row % 2) * 0.6f;
                float z = -row * 1.9f + jitterZ + (columns - 1) * 0.3f;
                _slots.Add(new Vector3(x, 0f, z));
            }
        }

        public void SetSelected(bool selected) => _ring.Visible = selected;

        /// <summary>Per-frame visual update.</summary>
        public void UpdateVisual(Squad squad, Vector3 position, float yaw, float delta, int tick, Vector3? aimAt)
        {
            var moved = position - _lastPosition;
            _lastPosition = position;
            float instSpeed = delta > 0f ? new Vector2(moved.X, moved.Z).Length() / delta : 0f;
            _speed = Mathf.Lerp(_speed, Mathf.Min(instSpeed, 12f), 0.2f);
            Position = position;
            Rotation = new Vector3(0f, Mathf.LerpAngle(Rotation.Y, yaw, 0.15f), 0f);

            if (_vehicle != null)
            {
                UpdateTurret(aimAt, delta);
                return;
            }

            bool moving = _speed > 0.4f;
            _walkPhase += delta * _speed * 2.4f;
            bool firing = tick - squad.LastFiredTick < 6;
            var state = squad.SuppressionState;
            for (int i = 0; i < _soldiers.Count; i++)
            {
                var rig = _soldiers[i];
                if (!rig.Root.Visible)
                {
                    continue;
                }

                if (_reviveTimers[i] > 0f)
                {
                    _reviveTimers[i] = Mathf.Max(0f, _reviveTimers[i] - delta);
                    rig.Root.Scale = Vector3.One * 1.12f * (1f - _reviveTimers[i] / 0.5f);
                }

                float phase = _walkPhase + i * 1.3f;
                float legSwing = moving ? Mathf.Sin(phase) * 32f : 0f;
                if (state == SuppressionState.Pinned && !squad.IsRetreating)
                {
                    rig.Pose.RotationDegrees = new Vector3(84f, 0f, 0f);
                    rig.Pose.Position = new Vector3(0f, 0.16f, -0.6f);
                    rig.LegL.RotationDegrees = Vector3.Zero;
                    rig.LegR.RotationDegrees = Vector3.Zero;
                    continue;
                }

                rig.Pose.RotationDegrees = Vector3.Zero;
                rig.Pose.Position = Vector3.Zero;
                bool crouch = (state == SuppressionState.Suppressed && !squad.IsRetreating) || (!moving && firing && i % 2 == 1);
                if (crouch)
                {
                    rig.UpperBody.Position = new Vector3(0f, 0.55f, 0.05f);
                    rig.LegL.RotationDegrees = new Vector3(-70f, 0f, 0f);
                    rig.LegR.RotationDegrees = new Vector3(10f, 0f, 0f);
                }
                else
                {
                    float bob = moving ? Mathf.Abs(Mathf.Sin(phase)) * 0.05f : 0f;
                    rig.UpperBody.Position = new Vector3(0f, 0.84f + bob, 0f);
                    rig.LegL.RotationDegrees = new Vector3(legSwing, 0f, 0f);
                    rig.LegR.RotationDegrees = new Vector3(-legSwing, 0f, 0f);
                }

                float recoil = firing ? Mathf.Sin(tick * 2.1f + i) * 4f : 0f;
                rig.ArmR.RotationDegrees = new Vector3(-65f - recoil, 0f, 8f);
                if (aimAt.HasValue && !moving)
                {
                    var toTarget = aimAt.Value - (position + Basis.FromEuler(Rotation) * rig.Root.Position);
                    float worldYaw = Mathf.Atan2(toTarget.X, toTarget.Z);
                    rig.Root.Rotation = new Vector3(0f, Mathf.LerpAngle(rig.Root.Rotation.Y, worldYaw - Rotation.Y, 0.2f), 0f);
                }
                else
                {
                    rig.Root.Rotation = new Vector3(0f, Mathf.LerpAngle(rig.Root.Rotation.Y, 0f, 0.1f), 0f);
                }
            }

            if (_launcher != null)
            {
                _launcher.Visible = !moving;
            }
        }

        private void UpdateTurret(Vector3? aimAt, float delta)
        {
            if (_turret == null)
            {
                return;
            }

            float target = 0f;
            if (aimAt.HasValue)
            {
                var local = ToLocal(aimAt.Value);
                target = Mathf.Atan2(local.X, local.Z);
            }

            _turretYaw = Mathf.LerpAngle(_turretYaw, target, Mathf.Min(1f, delta * 2.5f));
            _turret.Rotation = new Vector3(0f, _turretYaw, 0f);
        }

        /// <summary>World position of a soldier's muzzle (or the vehicle gun) for muzzle flashes.</summary>
        public Vector3 MuzzleWorld(int soldierIndex)
        {
            if (_vehicle != null)
            {
                var local = VehicleMuzzle;
                if (_turret != null)
                {
                    local = _turret.Position + Basis.FromEuler(new Vector3(0f, _turretYaw, 0f)) * (VehicleMuzzle - _turret.Position);
                }

                return ToGlobal(local);
            }

            if (_soldiers.Count == 0)
            {
                return GlobalPosition + Vector3.Up;
            }

            int i = Mathf.Clamp(soldierIndex, 0, _soldiers.Count - 1);
            for (int k = 0; k < _soldiers.Count && !_soldiers[i].Root.Visible; k++)
            {
                i = (i + 1) % _soldiers.Count;
            }

            var rig = _soldiers[i];
            return rig.Root.ToGlobal(rig.Muzzle / 1.12f);
        }

        public int SoldierCount => _soldiers.Count;

        /// <summary>A soldier died: it falls where it stood and stays as a corpse for a while.</summary>
        public void OnModelKilled(int index)
        {
            if (index < 0 || index >= _soldiers.Count || !_soldiers[index].Root.Visible)
            {
                return;
            }

            var rig = _soldiers[index];
            var transform = rig.Root.GlobalTransform;
            RemoveChild(rig.Root);
            _corpses.AddChild(rig.Root);
            rig.Root.GlobalTransform = transform;
            var corpse = new CorpseAnimator(rig, CorpseSeconds, MeshKit.Hash01(SquadId, index) > 0.5f);
            rig.Root.AddChild(corpse);

            var replacement = NewSoldier(index);
            replacement.Root.Visible = false;
            _soldiers[index] = replacement;
        }

        public void OnModelReinforced(int index)
        {
            if (index < 0 || index >= _soldiers.Count)
            {
                return;
            }

            _soldiers[index].Root.Visible = true;
            _reviveTimers[index] = 0.5f;
        }

        /// <summary>Vehicle destroyed: leave a burnt wreck in the world.</summary>
        public Node3D DetachWreck()
        {
            if (_vehicle == null)
            {
                return null;
            }

            var transform = _vehicle.GlobalTransform;
            RemoveChild(_vehicle);
            _corpses.AddChild(_vehicle);
            _vehicle.GlobalTransform = transform;
            Burn(_vehicle);
            var wreck = _vehicle;
            _vehicle = null;
            return wreck;
        }

        private static void Burn(Node node)
        {
            foreach (Node child in node.GetChildren())
            {
                if (child is MeshInstance3D mi)
                {
                    mi.MaterialOverride = MeshKit.Mat(new Color(0.12f, 0.11f, 0.1f), 1f);
                }

                Burn(child);
            }
        }
    }
}
