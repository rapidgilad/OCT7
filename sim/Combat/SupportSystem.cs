using System;
using OCT7.Sim.Data;
using OCT7.Sim.Units;

namespace OCT7.Sim.Combat
{
    /// <summary>Retreat, reinforcement and healing near base (docs/02 §3 "Retreat &amp; reinforce").</summary>
    public sealed class SupportSystem
    {
        private readonly Simulation _sim;

        public SupportSystem(Simulation sim)
        {
            _sim = sim;
        }

        /// <summary>Where a player's squads retreat to: the HQ structure, or the start position without one.</summary>
        public Vec2 RetreatPoint(int playerId)
        {
            var hq = _sim.World.FindHq(playerId);
            if (hq != null)
            {
                return hq.Center;
            }

            var p = _sim.GetPlayer(playerId);
            return p != null ? p.HqPosition : Vec2.Zero;
        }

        public void OrderRetreat(Squad squad)
        {
            squad.ClearOrders();
            squad.ReinforcePending = 0;
            squad.IsRetreating = true;
            if (!_sim.Movement.OrderMove(squad, RetreatPoint(squad.OwnerId)))
            {
                squad.IsRetreating = false;
            }
        }

        /// <summary>True near the player's HQ or a completed production building.</summary>
        public bool NearReinforcePoint(Squad s)
        {
            float radius = _sim.Rules.ReinforceRadius;
            foreach (var st in _sim.World.Structures)
            {
                if (st.OwnerId == s.OwnerId && st.IsComplete && st.Def.Kind != StructureKind.Sandbags && st.DistanceTo(s.Position) <= radius)
                {
                    return true;
                }
            }

            return false;
        }

        public void Tick()
        {
            var rules = _sim.Rules;
            float dt = SimConfig.TickSeconds;
            foreach (var s in _sim.World.Squads)
            {
                if (s.IsRetreating)
                {
                    var point = RetreatPoint(s.OwnerId);
                    if (!s.IsMoving || Vec2.Distance(s.Position, point) <= rules.RetreatArriveDistance)
                    {
                        s.IsRetreating = false;
                        _sim.Movement.Stop(s);
                    }
                }

                if (s.ReinforcePending > 0)
                {
                    TickReinforce(s, dt);
                }

                TickHeal(s, dt);
            }
        }

        private void TickReinforce(Squad s, float dt)
        {
            if (s.Def.IsVehicle || s.Models >= s.Def.SquadSize || s.Models == 0)
            {
                s.ReinforcePending = 0;
                return;
            }

            if (s.IsRetreating || !NearReinforcePoint(s))
            {
                return;
            }

            s.ReinforceTimer += dt;
            if (s.ReinforceTimer < _sim.Rules.ReinforceSecondsPerModel)
            {
                return;
            }

            var player = _sim.GetPlayer(s.OwnerId);
            float cost = s.Def.ReinforceCostPerModel;
            if (player == null || player.Manpower < cost)
            {
                return;
            }

            player.Manpower -= cost;
            for (int i = 0; i < s.ModelHealth.Length; i++)
            {
                if (s.ModelHealth[i] <= 0f)
                {
                    s.ModelHealth[i] = s.Def.HealthPerModel;
                    s.Models++;
                    _sim.Emit(new SimEvent { Type = SimEventType.ModelReinforced, PlayerId = s.OwnerId, TargetId = s.Id, Value = i, To = s.Position });
                    break;
                }
            }

            s.ReinforcePending--;
            s.ReinforceTimer = 0f;
        }

        private void TickHeal(Squad s, float dt)
        {
            var rules = _sim.Rules;
            if (_sim.Tick - s.LastDamagedTick < rules.HealDelaySeconds * SimConfig.TicksPerSecond)
            {
                return;
            }

            var hq = _sim.World.FindHq(s.OwnerId);
            if (hq == null || hq.DistanceTo(s.Position) > rules.HealRadius)
            {
                return;
            }

            float amount = s.Def.HealthPerModel * rules.HealFractionPerSecond * dt;
            for (int i = 0; i < s.ModelHealth.Length; i++)
            {
                if (s.ModelHealth[i] > 0f)
                {
                    s.ModelHealth[i] = Math.Min(s.Def.HealthPerModel, s.ModelHealth[i] + amount);
                }
            }
        }
    }
}
