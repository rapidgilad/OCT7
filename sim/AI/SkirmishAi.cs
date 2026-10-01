using System;
using System.Collections.Generic;
using OCT7.Sim.Data;
using OCT7.Sim.Production;
using OCT7.Sim.Territory;
using OCT7.Sim.Units;

namespace OCT7.Sim.AI
{
    /// <summary>
    /// Skirmish AI (docs/04-ai-and-difficulty.md). One implementation; Easy / Normal / Hard are data presets.
    /// Layers, evaluated every think interval:
    ///   1. memory of enemies it has actually seen (fog of war respected),
    ///   2. retreat / reinforce,
    ///   3. engineers: opening structures, assisting construction, sandbags at held VPs,
    ///   4. production: scripted opening, then a weighted mix (shifted toward AT when enemy armor is seen),
    ///   5. cappers grab sectors (fuel/munitions first), the army attacks when strong enough and defends otherwise.
    /// It issues ordinary commands, exactly like a human player.
    /// </summary>
    public sealed class SkirmishAi : IAiController
    {
        private const float MemorySeconds = 60f;
        private const float EngageExtraRange = 10f;

        private readonly AiDifficultyDef _difficulty;
        private readonly AiFactionPlanDef _plan;
        private readonly DeterministicRandom _rng;
        private readonly int _thinkInterval;
        private readonly int _phase;
        private readonly List<EnemyMemory> _memory = new List<EnemyMemory>();
        private readonly List<int> _cappers = new List<int>();
        private readonly List<int> _group = new List<int>();

        private int _openingStep;
        private int _openingCount;
        private int _stepStartedTick = -1;
        private int _nextProductionTick;
        private bool _attacking;
        private bool _hasAttacked;

        public SkirmishAi(int playerId, ulong matchSeed, AiDifficultyDef difficulty, AiFactionPlanDef plan)
        {
            PlayerId = playerId;
            _difficulty = difficulty ?? throw new ArgumentNullException(nameof(difficulty));
            _plan = plan ?? new AiFactionPlanDef();
            _rng = new DeterministicRandom(matchSeed * 31UL + 0x5EED0000UL + (ulong)playerId);
            _thinkInterval = Math.Max(1, (int)Math.Round(difficulty.ThinkIntervalSeconds * SimConfig.TicksPerSecond));
            _phase = (playerId * 3) % _thinkInterval;
        }

        public int PlayerId { get; }
        public AiDifficultyDef Difficulty => _difficulty;
        public bool IsAttacking => _attacking;

        public static SkirmishAi Create(Simulation sim, int playerId, string difficultyId)
        {
            var player = sim.GetPlayer(playerId);
            return new SkirmishAi(playerId, sim.Seed, sim.Data.GetDifficulty(difficultyId), sim.Data.GetAiPlan(player.FactionId));
        }

        public void Think(Simulation sim, List<Command> output)
        {
            var me = sim.GetPlayer(PlayerId);
            if (sim.IsOver || me == null || me.IsDefeated || (sim.Tick + _phase) % _thinkInterval != 0)
            {
                return;
            }

            UpdateMemory(sim);
            var handled = new HashSet<int>();
            ManageRetreatAndReinforce(sim, output, handled);
            ManageEngineers(sim, output, handled);
            ManageProduction(sim, output);
            ManageCappers(sim, output, handled);
            ManageArmy(sim, output, handled);
        }

        // ------------------------------------------------------------------ memory

        private struct EnemyMemory
        {
            public int Id;
            public Vec2 Position;
            public float Value;
            public bool IsVehicle;
            public int LastSeenTick;
        }

        private void UpdateMemory(Simulation sim)
        {
            foreach (var s in sim.World.Squads)
            {
                if (s.OwnerId == PlayerId || !sim.Vision.IsVisible(PlayerId, s.Position))
                {
                    continue;
                }

                int index = _memory.FindIndex(m => m.Id == s.Id);
                var entry = new EnemyMemory { Id = s.Id, Position = s.Position, Value = ValueOf(s.Def) * s.HealthFraction, IsVehicle = s.Def.IsVehicle, LastSeenTick = sim.Tick };
                if (index >= 0)
                {
                    _memory[index] = entry;
                }
                else
                {
                    _memory.Add(entry);
                }
            }

            int expire = (int)(MemorySeconds * SimConfig.TicksPerSecond);
            _memory.RemoveAll(m => sim.Tick - m.LastSeenTick > expire || sim.World.GetSquad(m.Id) == null);
            _memory.Sort((a, b) => a.Id.CompareTo(b.Id));
        }

        private float KnownEnemyValue(bool vehiclesOnly = false)
        {
            float v = 0f;
            foreach (var m in _memory)
            {
                if (!vehiclesOnly || m.IsVehicle)
                {
                    v += m.Value;
                }
            }

            return v;
        }

        private static float ValueOf(UnitDef def) => def.Manpower + def.Munitions + 2f * def.Fuel;

        // ------------------------------------------------------------------ retreat / reinforce

        private void ManageRetreatAndReinforce(Simulation sim, List<Command> output, HashSet<int> handled)
        {
            var retreat = new List<int>();
            var reinforce = new List<int>();
            foreach (var s in sim.World.Squads)
            {
                if (s.OwnerId != PlayerId || s.IsRetreating)
                {
                    continue;
                }

                bool hurt = s.HealthFraction < _difficulty.RetreatHealthFraction || (s.Def.SquadSize >= 3 && s.Models == 1);
                bool underFire = sim.Tick - s.LastDamagedTick < 5 * SimConfig.TicksPerSecond;
                bool atBase = sim.Support.NearReinforcePoint(s);
                if (hurt && underFire && !atBase)
                {
                    retreat.Add(s.Id);
                    handled.Add(s.Id);
                    continue;
                }

                if (_difficulty.Reinforce && atBase && !s.Def.IsVehicle && s.Models < s.Def.SquadSize && s.ReinforcePending == 0 && !s.InCombat(sim.Tick))
                {
                    reinforce.Add(s.Id);
                }

                if (atBase && (s.Models < s.Def.SquadSize || s.HealthFraction < 0.6f) && !s.InCombat(sim.Tick))
                {
                    handled.Add(s.Id); // stay home until topped up
                }
            }

            if (retreat.Count > 0)
            {
                output.Add(new RetreatCommand(PlayerId, retreat));
            }

            if (reinforce.Count > 0)
            {
                output.Add(new ReinforceCommand(PlayerId, reinforce));
            }
        }

        // ------------------------------------------------------------------ engineers

        private void ManageEngineers(Simulation sim, List<Command> output, HashSet<int> handled)
        {
            var engineers = new List<Squad>();
            foreach (var s in sim.World.Squads)
            {
                if (s.OwnerId == PlayerId && s.Def.Engineer && !handled.Contains(s.Id))
                {
                    engineers.Add(s);
                }
            }

            if (engineers.Count == 0)
            {
                return;
            }

            // Already constructing: leave them alone.
            var free = engineers.FindAll(e => e.BuildTargetId == 0);
            foreach (var e in engineers)
            {
                if (e.BuildTargetId != 0)
                {
                    handled.Add(e.Id);
                }
            }

            if (free.Count == 0)
            {
                return;
            }

            // 1) Opening structure.
            var step = CurrentStep();
            if (step != null && !string.IsNullOrEmpty(step.Build) && !OwnsAny(sim, step.Build))
            {
                var def = sim.Data.GetStructure(step.Build);
                if (TryFindSite(sim, def, 0, out var site))
                {
                    var builder = free[0];
                    output.Add(new ConstructCommand(PlayerId, new[] { builder.Id }, def.Id, site));
                    handled.Add(builder.Id);
                    free.RemoveAt(0);
                }
            }

            // 2) Help finish anything under construction.
            foreach (var st in sim.World.Structures)
            {
                if (free.Count == 0)
                {
                    return;
                }

                if (st.OwnerId == PlayerId && !st.IsComplete)
                {
                    var helper = free[0];
                    output.Add(ConstructCommand.Assist(PlayerId, new[] { helper.Id }, st.Id));
                    handled.Add(helper.Id);
                    free.RemoveAt(0);
                }
            }

            // 3) Sandbags at held victory points.
            if (_difficulty.BuildDefenses && free.Count > 0 && sim.GetPlayer(PlayerId).Manpower > 350)
            {
                foreach (var sector in sim.Territory.Sectors)
                {
                    if (sector.Type != SectorType.Victory || sector.OwnerId != PlayerId || HasSandbagsNear(sim, sector.Position, 14f))
                    {
                        continue;
                    }

                    var toEnemy = (EnemyBase(sim) - sector.Position).Normalized();
                    var spot = sector.Position + toEnemy * 7f;
                    int orientation = Math.Abs(toEnemy.X) > Math.Abs(toEnemy.Y) ? 1 : 0;
                    var cell = sim.Map.Grid.WorldToCell(spot);
                    if (sim.Production.CanPlace(PlayerId, sim.Data.GetStructure("sandbags"), cell, orientation) == RejectReason.None)
                    {
                        var builder = free[0];
                        output.Add(new ConstructCommand(PlayerId, new[] { builder.Id }, "sandbags", cell, orientation));
                        handled.Add(builder.Id);
                        free.RemoveAt(0);
                        break;
                    }
                }
            }
        }

        private static bool HasSandbagsNear(Simulation sim, Vec2 pos, float radius)
        {
            foreach (var st in sim.World.Structures)
            {
                if (st.Def.Kind == StructureKind.Sandbags && Vec2.Distance(st.Center, pos) <= radius)
                {
                    return true;
                }
            }

            return false;
        }

        private bool OwnsAny(Simulation sim, string structureId)
        {
            foreach (var st in sim.World.Structures)
            {
                if (st.OwnerId == PlayerId && st.Def.Id == structureId)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Deterministic spiral search for a valid site around the HQ, biased toward the map center.</summary>
        private bool TryFindSite(Simulation sim, StructureDef def, int orientation, out GridPos site)
        {
            var grid = sim.Map.Grid;
            var home = BaseCenter(sim);
            var toCenter = (new Vec2(grid.WorldWidth * 0.5f, grid.WorldHeight * 0.5f) - home).Normalized();
            var preferred = grid.WorldToCell(home + toCenter * 22f);
            for (int r = 0; r <= 22; r += 2)
            {
                for (int dy = -r; dy <= r; dy += 2)
                {
                    for (int dx = -r; dx <= r; dx += 2)
                    {
                        if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r)
                        {
                            continue;
                        }

                        var c = new GridPos(preferred.X + dx, preferred.Y + dy);
                        if (sim.Production.CanPlace(PlayerId, def, c, orientation) == RejectReason.None && !NearOtherStructure(sim, c, def))
                        {
                            site = c;
                            return true;
                        }
                    }
                }
            }

            site = default;
            return false;
        }

        /// <summary>Keeps a 2-cell lane between buildings so the base never walls itself in.</summary>
        private static bool NearOtherStructure(Simulation sim, GridPos center, StructureDef def)
        {
            ProductionSystem.FootprintSize(def, 0, out int sx, out int sy);
            var min = ProductionSystem.MinCellFor(center, sx, sy);
            for (int y = min.Y - 2; y < min.Y + sy + 2; y++)
            {
                for (int x = min.X - 2; x < min.X + sx + 2; x++)
                {
                    if (sim.World.StructureAt(new GridPos(x, y)) != 0)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        // ------------------------------------------------------------------ production

        private AiBuildStepDef CurrentStep() => _openingStep < _plan.Opening.Count ? _plan.Opening[_openingStep] : null;

        private void AdvanceStep(Simulation sim)
        {
            _openingStep++;
            _openingCount = 0;
            _stepStartedTick = sim.Tick;
        }

        private void ManageProduction(Simulation sim, List<Command> output)
        {
            if (_stepStartedTick < 0)
            {
                _stepStartedTick = sim.Tick;
            }

            // Opening: build steps complete once the structure is placed; produce steps once queued N times.
            var step = CurrentStep();
            if (step != null)
            {
                bool stuck = sim.Tick - _stepStartedTick > 120 * SimConfig.TicksPerSecond;
                if (!string.IsNullOrEmpty(step.Build))
                {
                    if (OwnsAny(sim, step.Build) || stuck)
                    {
                        AdvanceStep(sim);
                    }
                }
                else if (sim.Tick >= _nextProductionTick)
                {
                    var producer = FindProducer(sim, step.Produce);
                    if (producer != null && sim.Production.CanProduce(PlayerId, producer, step.Produce) == RejectReason.None)
                    {
                        output.Add(new ProduceCommand(PlayerId, producer.Id, step.Produce));
                        SetRally(sim, producer, output);
                        _nextProductionTick = sim.Tick + (int)(_difficulty.ProductionDelaySeconds * SimConfig.TicksPerSecond);
                        if (++_openingCount >= Math.Max(1, step.Count))
                        {
                            AdvanceStep(sim);
                        }
                    }
                    else if (stuck)
                    {
                        AdvanceStep(sim);
                    }
                }
            }

            if (sim.Tick < _nextProductionTick)
            {
                return;
            }

            // Always keep one engineer squad alive.
            if (CountOwn(sim, u => u.Engineer) == 0)
            {
                var hq = sim.World.FindHq(PlayerId);
                var engineerId = hq?.Def.Produces.Find(id => sim.Data.GetUnit(id).Engineer);
                if (engineerId != null && !QueueContains(hq, engineerId) && sim.Production.CanProduce(PlayerId, hq, engineerId) == RejectReason.None)
                {
                    output.Add(new ProduceCommand(PlayerId, hq.Id, engineerId));
                    return;
                }
            }

            if (CurrentStep() != null)
            {
                return; // opening still running
            }

            // Mix: one unit per think from an idle producer, weighted, shifted toward AT if enemy armor is known.
            bool armorSeen = KnownEnemyValue(vehiclesOnly: true) > 0.25f * Math.Max(1f, KnownEnemyValue());
            var candidates = new List<(Structure producer, UnitDef unit, float weight)>();
            foreach (var w in _plan.Mix)
            {
                var unit = sim.Data.GetUnit(w.Unit);
                var producer = FindProducer(sim, unit.Id);
                if (producer == null || producer.Queue.Count >= 1 || sim.Production.CanProduce(PlayerId, producer, unit.Id) != RejectReason.None)
                {
                    continue;
                }

                float weight = w.Weight;
                if (armorSeen && IsAntiTank(sim, unit))
                {
                    weight *= 2.5f;
                }

                candidates.Add((producer, unit, weight));
            }

            if (candidates.Count == 0)
            {
                return;
            }

            float total = 0f;
            foreach (var c in candidates)
            {
                total += c.weight;
            }

            float roll = _rng.NextFloat() * total;
            foreach (var c in candidates)
            {
                roll -= c.weight;
                if (roll <= 0f)
                {
                    output.Add(new ProduceCommand(PlayerId, c.producer.Id, c.unit.Id));
                    SetRally(sim, c.producer, output);
                    _nextProductionTick = sim.Tick + (int)(_difficulty.ProductionDelaySeconds * SimConfig.TicksPerSecond);
                    return;
                }
            }
        }

        private static bool IsAntiTank(Simulation sim, UnitDef unit)
        {
            foreach (var w in unit.Weapons)
            {
                if (sim.Data.HasWeapon(w.Weapon) && sim.Data.GetWeapon(w.Weapon).PrefersVehicles)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool QueueContains(Structure st, string unitId)
        {
            foreach (var item in st.Queue)
            {
                if (item.Unit.Id == unitId)
                {
                    return true;
                }
            }

            return false;
        }

        private Structure FindProducer(Simulation sim, string unitId)
        {
            foreach (var st in sim.World.Structures)
            {
                if (st.OwnerId == PlayerId && st.IsComplete && st.Def.Produces.Contains(unitId))
                {
                    return st;
                }
            }

            return null;
        }

        private void SetRally(Simulation sim, Structure producer, List<Command> output)
        {
            if (!producer.HasRallyPoint)
            {
                var home = BaseCenter(sim);
                var toCenter = (new Vec2(sim.Map.Grid.WorldWidth * 0.5f, sim.Map.Grid.WorldHeight * 0.5f) - home).Normalized();
                output.Add(new SetRallyPointCommand(PlayerId, producer.Id, home + toCenter * 35f));
            }
        }

        private int CountOwn(Simulation sim, Predicate<UnitDef> match)
        {
            int n = 0;
            foreach (var s in sim.World.Squads)
            {
                if (s.OwnerId == PlayerId && match(s.Def))
                {
                    n++;
                }
            }

            foreach (var st in sim.World.Structures)
            {
                if (st.OwnerId == PlayerId)
                {
                    foreach (var item in st.Queue)
                    {
                        if (match(item.Unit))
                        {
                            n++;
                        }
                    }
                }
            }

            return n;
        }

        // ------------------------------------------------------------------ cappers

        private void ManageCappers(Simulation sim, List<Command> output, HashSet<int> handled)
        {
            if (!sim.Territory.Enabled)
            {
                return;
            }

            _cappers.RemoveAll(id => { var s = sim.World.GetSquad(id); return s == null || s.IsRetreating || handled.Contains(id); });

            // Recruit cappers: cheapest capable infantry first (engineers count when free).
            while (_cappers.Count < _difficulty.MaxCappers)
            {
                Squad best = null;
                foreach (var s in sim.World.Squads)
                {
                    if (s.OwnerId != PlayerId || handled.Contains(s.Id) || _cappers.Contains(s.Id) || !s.Def.CanCapture || s.IsRetreating
                        || s.Def.Category == UnitCategory.Support)
                    {
                        continue;
                    }

                    if (best == null || ValueOf(s.Def) < ValueOf(best.Def))
                    {
                        best = s;
                    }
                }

                if (best == null)
                {
                    break;
                }

                _cappers.Add(best.Id);
            }

            var claimed = new List<int>();
            foreach (var id in _cappers)
            {
                var squad = sim.World.GetSquad(id);
                handled.Add(id);
                var current = sim.Territory.SectorAt(squad.Destination);
                bool working = squad.IsMoving || (current != null && current.OwnerId != PlayerId && Vec2.Distance(squad.Position, current.Position) < sim.Rules.CaptureRadius);
                if (working && current != null && current.OwnerId != PlayerId)
                {
                    claimed.Add(current.Index);
                    continue;
                }

                var target = PickCaptureTarget(sim, squad, claimed);
                if (target != null)
                {
                    claimed.Add(target.Index);
                    output.Add(new MoveSquadsCommand(PlayerId, new[] { squad.Id }, target.Position) { SnapToCover = false });
                }
            }
        }

        private Sector PickCaptureTarget(Simulation sim, Squad squad, List<int> claimed)
        {
            Sector best = null;
            float bestScore = 0f;
            foreach (var sector in sim.Territory.Sectors)
            {
                if (!sector.IsCapturable || sector.OwnerId == PlayerId || claimed.Contains(sector.Index) || EnemyNear(sim, sector.Position, 25f))
                {
                    continue;
                }

                float typeWeight = sector.Type == SectorType.Fuel ? 3f : sector.Type == SectorType.Munitions ? 2.5f : sector.Type == SectorType.Victory ? 2f : 1f;
                bool adjacent = false;
                foreach (var n in sector.Neighbors)
                {
                    var ns = sim.Territory.Sectors[n];
                    adjacent |= ns.OwnerId == PlayerId && ns.IsConnected;
                }

                float score = typeWeight * (adjacent ? 2f : 1f) * (sector.OwnerId < 0 ? 1.5f : 1f) / (Vec2.Distance(squad.Position, sector.Position) + 40f);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = sector;
                }
            }

            return best;
        }

        private bool EnemyNear(Simulation sim, Vec2 pos, float radius)
        {
            int fresh = 15 * SimConfig.TicksPerSecond;
            foreach (var m in _memory)
            {
                if (sim.Tick - m.LastSeenTick < fresh && Vec2.Distance(m.Position, pos) < radius)
                {
                    return true;
                }
            }

            return false;
        }

        // ------------------------------------------------------------------ army

        private void ManageArmy(Simulation sim, List<Command> output, HashSet<int> handled)
        {
            var army = new List<Squad>();
            float armyValue = 0f;
            var centroid = Vec2.Zero;
            foreach (var s in sim.World.Squads)
            {
                if (s.OwnerId == PlayerId && !handled.Contains(s.Id) && !s.IsRetreating && !s.Def.Engineer)
                {
                    army.Add(s);
                    armyValue += ValueOf(s.Def) * s.HealthFraction;
                    centroid += s.Position;
                }
            }

            if (army.Count == 0)
            {
                return;
            }

            centroid /= army.Count;
            float enemyValue = KnownEnemyValue();
            if (!_attacking)
            {
                float threshold = _hasAttacked ? _difficulty.FirstAttackArmyValue * 0.6f : _difficulty.FirstAttackArmyValue;
                if (armyValue >= threshold && armyValue >= _difficulty.AttackStrengthRatio * enemyValue)
                {
                    _attacking = true;
                    _hasAttacked = true;
                }
            }
            else if (armyValue < 0.6f * enemyValue || armyValue < _difficulty.FirstAttackArmyValue * 0.35f)
            {
                _attacking = false;
            }

            var objective = _attacking ? AttackObjective(sim, centroid) : DefendObjective(sim);
            var enemyHq = EnemyHq(sim);
            bool assaultHq = _attacking && enemyHq != null && Vec2.Distance(centroid, enemyHq.Center) < 70f && sim.Vision.IsVisible(PlayerId, enemyHq.Center);

            _group.Clear();
            int fresh = 2 * SimConfig.TicksPerSecond;
            foreach (var s in army)
            {
                // Engage visible enemies near this squad.
                int targetId = 0;
                float bestD = s.MaxRange + EngageExtraRange;
                foreach (var m in _memory)
                {
                    if (sim.Tick - m.LastSeenTick > fresh)
                    {
                        continue;
                    }

                    float d = Vec2.Distance(s.Position, m.Position);
                    bool preferred = m.IsVehicle == HasAntiTank(s);
                    if (d < bestD || (preferred && d < bestD + 10f && targetId == 0))
                    {
                        bestD = d;
                        targetId = m.Id;
                    }
                }

                if (targetId != 0)
                {
                    if (s.TargetId != targetId && !(s.TargetId != 0 && sim.World.GetSquad(s.TargetId) != null))
                    {
                        output.Add(new AttackCommand(PlayerId, new[] { s.Id }, targetId));
                    }

                    continue;
                }

                if (assaultHq)
                {
                    if (s.TargetId != enemyHq.Id)
                    {
                        output.Add(new AttackCommand(PlayerId, new[] { s.Id }, enemyHq.Id));
                    }

                    continue;
                }

                bool farFromObjective = Vec2.Distance(s.Position, objective) > 22f;
                bool headingElsewhere = s.IsMoving && Vec2.Distance(s.Destination, objective) > 22f;
                if ((farFromObjective && !s.IsMoving) || headingElsewhere)
                {
                    _group.Add(s.Id);
                }
            }

            if (_group.Count > 0)
            {
                output.Add(new MoveSquadsCommand(PlayerId, _group, objective) { SnapToCover = _difficulty.Micro });
            }
        }

        private static bool HasAntiTank(Squad s)
        {
            foreach (var w in s.Weapons)
            {
                if (w.PrefersVehicles)
                {
                    return true;
                }
            }

            return false;
        }

        private Vec2 AttackObjective(Simulation sim, Vec2 from)
        {
            Sector best = null;
            float bestD = float.MaxValue;
            foreach (var s in sim.Territory.Sectors)
            {
                if (s.Type == SectorType.Victory && s.OwnerId != PlayerId)
                {
                    float d = Vec2.Distance(from, s.Position);
                    if (d < bestD)
                    {
                        best = s;
                        bestD = d;
                    }
                }
            }

            if (best == null)
            {
                foreach (var s in sim.Territory.Sectors)
                {
                    if (s.IsCapturable && s.OwnerId >= 0 && s.OwnerId != PlayerId)
                    {
                        float d = Vec2.Distance(from, s.Position);
                        if (d < bestD)
                        {
                            best = s;
                            bestD = d;
                        }
                    }
                }
            }

            return best != null ? best.Position : EnemyBase(sim);
        }

        private Vec2 DefendObjective(Simulation sim)
        {
            // Hold the own victory point closest to the enemy; otherwise contest the VP closest to home.
            var enemy = EnemyBase(sim);
            var home = BaseCenter(sim);
            Sector best = null;
            float bestD = float.MaxValue;
            foreach (var s in sim.Territory.Sectors)
            {
                if (s.Type == SectorType.Victory && s.OwnerId == PlayerId)
                {
                    float d = Vec2.Distance(enemy, s.Position);
                    if (d < bestD)
                    {
                        best = s;
                        bestD = d;
                    }
                }
            }

            if (best == null)
            {
                foreach (var s in sim.Territory.Sectors)
                {
                    if (s.Type == SectorType.Victory)
                    {
                        float d = Vec2.Distance(home, s.Position);
                        if (d < bestD)
                        {
                            best = s;
                            bestD = d;
                        }
                    }
                }
            }

            if (best != null)
            {
                return best.Position;
            }

            var toCenter = (new Vec2(sim.Map.Grid.WorldWidth * 0.5f, sim.Map.Grid.WorldHeight * 0.5f) - home).Normalized();
            return home + toCenter * 50f;
        }

        private Vec2 BaseCenter(Simulation sim)
        {
            var hq = sim.World.FindHq(PlayerId);
            return hq != null ? hq.Center : sim.GetPlayer(PlayerId).HqPosition;
        }

        private Vec2 EnemyBase(Simulation sim)
        {
            var hq = EnemyHq(sim);
            if (hq != null)
            {
                return hq.Center;
            }

            foreach (var p in sim.Players)
            {
                if (p.Id != PlayerId)
                {
                    return p.HqPosition;
                }
            }

            return BaseCenter(sim);
        }

        private Structure EnemyHq(Simulation sim)
        {
            foreach (var p in sim.Players)
            {
                if (p.Id != PlayerId)
                {
                    var hq = sim.World.FindHq(p.Id);
                    if (hq != null)
                    {
                        return hq;
                    }
                }
            }

            return null;
        }
    }
}
