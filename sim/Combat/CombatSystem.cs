using System;
using System.Collections.Generic;
using OCT7.Sim.Data;
using OCT7.Sim.Production;
using OCT7.Sim.Units;
using OCT7.Sim.World;

namespace OCT7.Sim.Combat
{
    /// <summary>
    /// Target acquisition, firing and damage (docs/02 §3):
    ///   hit chance = accuracy(range band) × target received accuracy × cover × retreat × moving × shooter suppression.
    /// Infantry lose individual models; vehicles roll penetration vs front/rear armor; structures take weapon-scaled damage.
    /// Targets must be visible to the shooter's player and in direct line of sight.
    /// </summary>
    public sealed class CombatSystem
    {
        private readonly Simulation _sim;
        private readonly List<Squad> _deadSquads = new List<Squad>();
        private readonly List<Structure> _deadStructures = new List<Structure>();

        public CombatSystem(Simulation sim)
        {
            _sim = sim;
        }

        /// <summary>Explicit attack order: the squad pursues the target until it dies or another order arrives.</summary>
        public void OrderAttack(Squad squad, int targetId)
        {
            if (!IsEnemyTarget(squad, targetId))
            {
                return;
            }

            squad.ClearOrders();
            squad.TargetId = targetId;
            squad.ForcedTarget = true;
            if (_sim.TryGetEntityPosition(targetId, out var pos) && !InFiringPosition(squad, targetId))
            {
                _sim.Movement.OrderMove(squad, ApproachPoint(squad, targetId, pos));
            }
        }

        public void Tick()
        {
            var rules = _sim.Rules;
            float dt = SimConfig.TickSeconds;
            var squads = _sim.World.Squads;

            for (int i = 0; i < squads.Count; i++)
            {
                var s = squads[i];
                bool underFire = _sim.Tick - s.LastSuppressedTick < rules.SuppressionDecayDelaySeconds * SimConfig.TicksPerSecond;
                if (!s.Def.IsVehicle && !underFire)
                {
                    s.Suppression = Math.Max(0f, s.Suppression - rules.SuppressionDecayPerSecond * dt);
                }

                s.SuppressionState = s.Suppression >= rules.PinnedThreshold ? SuppressionState.Pinned
                    : s.Suppression >= rules.SuppressedThreshold ? SuppressionState.Suppressed
                    : SuppressionState.None;
                s.StationarySeconds = s.IsMoving ? 0f : s.StationarySeconds + dt;
                for (int w = 0; w < s.Cooldowns.Length; w++)
                {
                    s.Cooldowns[w] = Math.Max(0f, s.Cooldowns[w] - dt);
                }
            }

            for (int i = 0; i < squads.Count; i++)
            {
                var s = squads[i];
                if (!s.IsAlive || s.Weapons.Count == 0)
                {
                    continue;
                }

                if (s.IsRetreating)
                {
                    s.TargetId = 0;
                    continue;
                }

                UpdateTarget(s);
                if (s.TargetId != 0)
                {
                    FireWeapons(s);
                }
            }

            RemoveDead();
        }

        public bool IsEnemyTarget(Squad shooter, int targetId)
        {
            var sq = _sim.World.GetSquad(targetId);
            if (sq != null)
            {
                return sq.IsAlive && sq.OwnerId != shooter.OwnerId;
            }

            var st = _sim.World.GetStructure(targetId);
            return st != null && st.IsAlive && st.OwnerId != shooter.OwnerId;
        }

        /// <summary>Distance from shooter to target (structures: to the nearest footprint edge).</summary>
        public float DistanceTo(Squad shooter, int targetId)
        {
            var sq = _sim.World.GetSquad(targetId);
            if (sq != null)
            {
                return Vec2.Distance(shooter.Position, sq.Position);
            }

            var st = _sim.World.GetStructure(targetId);
            return st != null ? st.DistanceTo(shooter.Position) : float.MaxValue;
        }

        private void UpdateTarget(Squad s)
        {
            if (s.ForcedTarget)
            {
                if (!IsEnemyTarget(s, s.TargetId))
                {
                    s.ForcedTarget = false;
                    s.TargetId = 0;
                }
                else
                {
                    if (InFiringPosition(s, s.TargetId))
                    {
                        if (s.IsMoving)
                        {
                            _sim.Movement.Stop(s);
                        }
                    }
                    else if ((!s.IsMoving || _sim.Tick - s.RepathTick > 20) && _sim.TryGetEntityPosition(s.TargetId, out var pos))
                    {
                        _sim.Movement.OrderMove(s, ApproachPoint(s, s.TargetId, pos));
                    }

                    return;
                }
            }

            if (s.TargetId != 0 && IsEnemyTarget(s, s.TargetId) && InFiringPosition(s, s.TargetId))
            {
                return;
            }

            s.TargetId = AcquireTarget(s);
        }

        /// <summary>In range of at least one weapon, visible to the shooter's player, and in direct line of sight.</summary>
        private bool InFiringPosition(Squad s, int targetId)
        {
            var sq = _sim.World.GetSquad(targetId);
            if (sq != null)
            {
                return Vec2.Distance(s.Position, sq.Position) <= s.MaxRange
                    && _sim.Vision.IsVisible(s.OwnerId, sq.Position)
                    && VisibilityGrid.HasLineOfSight(_sim.Map.Grid, s.Position, sq.Position);
            }

            var st = _sim.World.GetStructure(targetId);
            if (st == null)
            {
                return false;
            }

            var aim = NearestPointOn(st, s.Position);
            return st.DistanceTo(s.Position) <= s.MaxRange
                && _sim.Vision.IsVisible(s.OwnerId, aim)
                && VisibilityGrid.HasLineOfSight(_sim.Map.Grid, s.Position, aim);
        }

        private int AcquireTarget(Squad s)
        {
            bool hasAntiTank = false;
            bool hasStructureWeapon = false;
            foreach (var w in s.Weapons)
            {
                hasAntiTank |= w.PrefersVehicles;
                hasStructureWeapon |= w.VsStructure >= 0.3f;
            }

            float range = s.MaxRange;
            int best = 0;
            int bestPriority = int.MaxValue;
            float bestDist = float.MaxValue;
            foreach (var e in _sim.World.Squads)
            {
                if (e.OwnerId == s.OwnerId || !e.IsAlive)
                {
                    continue;
                }

                float d = Vec2.Distance(s.Position, e.Position);
                if (d > range || !_sim.Vision.IsVisible(s.OwnerId, e.Position) || !VisibilityGrid.HasLineOfSight(_sim.Map.Grid, s.Position, e.Position))
                {
                    continue;
                }

                int priority = e.Def.IsVehicle ? (hasAntiTank ? 0 : 2) : (hasAntiTank ? 1 : 0);
                if (priority < bestPriority || (priority == bestPriority && d < bestDist))
                {
                    best = e.Id;
                    bestPriority = priority;
                    bestDist = d;
                }
            }

            if (best != 0 || !hasStructureWeapon)
            {
                return best;
            }

            foreach (var st in _sim.World.Structures)
            {
                if (st.OwnerId == s.OwnerId || st.Def.Kind == StructureKind.Sandbags)
                {
                    continue;
                }

                float d = st.DistanceTo(s.Position);
                if (d < bestDist && InFiringPosition(s, st.Id))
                {
                    best = st.Id;
                    bestDist = d;
                }
            }

            return best;
        }

        private void FireWeapons(Squad s)
        {
            var targetSquad = _sim.World.GetSquad(s.TargetId);
            var targetStructure = targetSquad == null ? _sim.World.GetStructure(s.TargetId) : null;
            if (targetSquad == null && targetStructure == null)
            {
                s.TargetId = 0;
                return;
            }

            var targetPos = targetSquad != null ? targetSquad.Position : NearestPointOn(targetStructure, s.Position);
            float dist = targetSquad != null ? Vec2.Distance(s.Position, targetPos) : targetStructure.DistanceTo(s.Position);
            var slots = s.Def.Weapons;

            for (int i = 0; i < s.Weapons.Count; i++)
            {
                var w = s.Weapons[i];
                if (s.Cooldowns[i] > 0f || dist > w.Range)
                {
                    continue;
                }

                if (s.IsMoving && (w.NeedsSetup || w.MovingAccuracy <= 0f))
                {
                    continue;
                }

                if (w.NeedsSetup && s.StationarySeconds < w.SetupTime)
                {
                    continue;
                }

                // Weapons with < 5% penetration chance even against rear armor don't waste time on the vehicle.
                if (targetSquad != null && targetSquad.Def.IsVehicle && targetSquad.Def.ArmorRear > 0f && w.Penetration / targetSquad.Def.ArmorRear < 0.05f)
                {
                    continue;
                }

                var slot = i < slots.Count ? slots[i] : null;
                int shots = slot != null && slot.PerModel ? s.Models : (s.Models > 0 ? (slot?.Count ?? 1) : 0);
                int hits = 0;
                for (int k = 0; k < shots; k++)
                {
                    bool hit = targetSquad != null ? ResolveShot(s, w, targetSquad, dist) : ResolveShot(s, w, targetStructure);
                    if (hit)
                    {
                        hits++;
                    }

                    if ((targetSquad != null && !targetSquad.IsAlive) || (targetStructure != null && !targetStructure.IsAlive))
                    {
                        break;
                    }
                }

                s.Cooldowns[i] = w.Cooldown * _sim.Random.Range(0.9f, 1.1f);
                s.LastFiredTick = _sim.Tick;
                if (!s.IsMoving)
                {
                    var dir = (targetPos - s.Position).Normalized();
                    if (dir != Vec2.Zero)
                    {
                        s.Facing = dir;
                    }
                }

                _sim.Emit(new SimEvent
                {
                    Type = SimEventType.ShotFired,
                    PlayerId = s.OwnerId,
                    SourceId = s.Id,
                    TargetId = s.TargetId,
                    DefId = w.Id,
                    From = s.Position,
                    To = targetPos,
                    Value = hits,
                });

                if ((targetSquad != null && !targetSquad.IsAlive) || (targetStructure != null && !targetStructure.IsAlive))
                {
                    s.TargetId = 0;
                    s.ForcedTarget = false;
                    return;
                }
            }
        }

        /// <summary>Chance that one shot from <paramref name="s"/> with <paramref name="w"/> hits squad <paramref name="t"/> (docs/02 §3).</summary>
        public float HitChance(Squad s, WeaponDef w, Squad t)
        {
            var rules = _sim.Rules;
            float dist = Vec2.Distance(s.Position, t.Position);
            float acc = BandAccuracy(w, dist) * t.Def.ReceivedAccuracy;
            if (!t.Def.IsVehicle)
            {
                acc *= w.VsInfantryAccuracy;
                var cover = _sim.Cover.CoverAgainst(t.Position, s.Position, rules.CoverDirectionThreshold);
                acc *= cover == CoverType.Heavy ? rules.HeavyCoverMultiplier : cover == CoverType.Light ? rules.LightCoverMultiplier : 1f;
            }

            if (t.IsRetreating)
            {
                acc *= rules.RetreatReceivedAccuracy;
            }

            if (s.IsMoving)
            {
                acc *= w.MovingAccuracy;
            }

            acc *= s.SuppressionState == SuppressionState.Pinned ? rules.PinnedAccuracyMultiplier
                : s.SuppressionState == SuppressionState.Suppressed ? rules.SuppressedAccuracyMultiplier : 1f;
            return Math.Min(0.98f, Math.Max(0f, acc));
        }

        /// <summary>Chance a hit penetrates a vehicle: front armor if the shooter is within ~72° of its facing, else rear.</summary>
        public static float PenetrationChance(WeaponDef w, Squad target, Vec2 shooterPosition)
        {
            var toShooter = (shooterPosition - target.Position).Normalized();
            bool front = target.Facing.X * toShooter.X + target.Facing.Y * toShooter.Y >= 0.3f;
            float armor = front ? target.Def.ArmorFront : target.Def.ArmorRear;
            return armor <= 0f ? 1f : Math.Min(1f, w.Penetration / armor);
        }

        private bool ResolveShot(Squad s, WeaponDef w, Squad t, float dist)
        {
            var rules = _sim.Rules;
            bool hit = _sim.Random.NextFloat() < HitChance(s, w, t);
            if (t.Def.IsVehicle)
            {
                if (!hit)
                {
                    return false;
                }

                if (_sim.Random.NextFloat() >= PenetrationChance(w, t, s.Position))
                {
                    _sim.Emit(new SimEvent { Type = SimEventType.Deflected, PlayerId = s.OwnerId, SourceId = s.Id, TargetId = t.Id, DefId = w.Id, To = t.Position });
                    return false;
                }

                DamageModel(t, 0, w.Damage, s);
                return true;
            }

            float suppression = hit ? w.Suppression : w.Suppression * rules.MissSuppressionFactor;
            t.Suppression = Math.Min(1f, t.Suppression + suppression);
            if (suppression > 0f)
            {
                t.LastSuppressedTick = _sim.Tick;
            }
            if (!hit)
            {
                return false;
            }

            for (int m = 0; m < w.ModelsHitVsInfantry && t.IsAlive; m++)
            {
                DamageModel(t, PickAliveModel(t), w.InfantryDamage, s);
            }

            return true;
        }

        private bool ResolveShot(Squad s, WeaponDef w, Structure t)
        {
            float acc = 0.9f * (s.IsMoving ? w.MovingAccuracy : 1f);
            if (_sim.Random.NextFloat() >= acc)
            {
                return false;
            }

            t.Health -= w.Damage * w.VsStructure;
            t.LastDamagedTick = _sim.Tick;
            if (t.Health <= 0f)
            {
                t.Health = 0f;
                if (!_deadStructures.Contains(t))
                {
                    _deadStructures.Add(t);
                }
            }

            return true;
        }

        private int PickAliveModel(Squad t)
        {
            int k = _sim.Random.NextInt(0, t.Models);
            for (int i = 0; i < t.ModelHealth.Length; i++)
            {
                if (t.ModelHealth[i] > 0f)
                {
                    if (k == 0)
                    {
                        return i;
                    }

                    k--;
                }
            }

            return 0;
        }

        private void DamageModel(Squad t, int index, float damage, Squad attacker)
        {
            if (t.ModelHealth[index] <= 0f)
            {
                return;
            }

            t.LastDamagedTick = _sim.Tick;
            t.ModelHealth[index] -= damage;
            if (t.ModelHealth[index] > 0f)
            {
                return;
            }

            t.ModelHealth[index] = 0f;
            t.Models--;
            _sim.Emit(new SimEvent { Type = SimEventType.ModelKilled, PlayerId = t.OwnerId, SourceId = attacker.Id, TargetId = t.Id, Value = index, To = t.Position });
            if (t.Models <= 0 && !_deadSquads.Contains(t))
            {
                attacker.Kills++;
                _deadSquads.Add(t);
            }
        }

        private void RemoveDead()
        {
            foreach (var s in _deadSquads)
            {
                _sim.World.RemoveSquad(s.Id);
                _sim.Emit(new SimEvent { Type = SimEventType.SquadDestroyed, PlayerId = s.OwnerId, TargetId = s.Id, DefId = s.Def.Id, To = s.Position });
            }

            foreach (var st in _deadStructures)
            {
                _sim.Production.DestroyStructure(st);
            }

            _deadSquads.Clear();
            _deadStructures.Clear();
        }

        private static float BandAccuracy(WeaponDef w, float dist)
        {
            if (dist <= w.Range / 3f)
            {
                return w.AccuracyNear;
            }

            return dist <= w.Range * 2f / 3f ? w.AccuracyMid : w.AccuracyFar;
        }

        private Vec2 ApproachPoint(Squad s, int targetId, Vec2 targetPos)
        {
            // Close to ~80% of max range along the line to the target.
            var toTarget = targetPos - s.Position;
            float dist = toTarget.Length;
            float desired = Math.Max(4f, s.MaxRange * 0.8f);
            if (dist <= desired)
            {
                return targetPos;
            }

            return s.Position + toTarget / dist * (dist - desired);
        }

        public static Vec2 NearestPointOn(Structure st, Vec2 p)
        {
            float cs = st.CellSize;
            float minX = st.MinCell.X * cs, minY = st.MinCell.Y * cs;
            float maxX = (st.MinCell.X + st.SizeX) * cs - 0.01f, maxY = (st.MinCell.Y + st.SizeY) * cs - 0.01f;
            return new Vec2(Math.Min(maxX, Math.Max(minX, p.X)), Math.Min(maxY, Math.Max(minY, p.Y)));
        }
    }
}
