using System.Collections.Generic;
using OCT7.Sim.Units;

namespace OCT7.Sim
{
    /// <summary>
    /// Every change a player (human or AI) makes to the match is a command.
    /// Commands are queued with a target tick and applied at the start of that tick in a fixed order,
    /// which makes matches replayable and keeps human and AI on exactly the same code path.
    /// Invalid commands are ignored (and reported via <see cref="SimEventType.CommandRejected"/> where useful).
    /// </summary>
    public abstract class Command
    {
        /// <summary>Issuing player.</summary>
        public int PlayerId;

        /// <summary>Tick on which the command executes. Values in the past are clamped to the current tick.</summary>
        public int Tick;

        /// <summary>Assigned by the queue; breaks ties deterministically between commands of the same tick and player.</summary>
        public long Sequence { get; internal set; }

        public abstract void Apply(Simulation sim);

        protected static int[] Copy(IReadOnlyList<int> ids)
        {
            var result = new int[ids?.Count ?? 0];
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = ids[i];
            }

            return result;
        }

        /// <summary>Own, alive squads from the id list, sorted by id so results never depend on selection order.</summary>
        protected static List<Squad> OwnSquads(Simulation sim, int playerId, int[] ids)
        {
            var result = new List<Squad>();
            if (ids == null)
            {
                return result;
            }

            var sorted = (int[])ids.Clone();
            System.Array.Sort(sorted);
            foreach (var id in sorted)
            {
                var s = sim.World.GetSquad(id);
                if (s != null && s.OwnerId == playerId && s.IsAlive)
                {
                    result.Add(s);
                }
            }

            return result;
        }
    }

    /// <summary>
    /// Move squads to a ground position in a loose formation. Infantry snap to the best cover within
    /// <see cref="Data.RulesDef.CoverSnapRadius"/> of their slot (CoH-style). Cancels retreat, attack and build orders.
    /// </summary>
    public sealed class MoveSquadsCommand : Command
    {
        public int[] SquadIds;
        public Vec2 Target;
        public bool SnapToCover = true;

        public MoveSquadsCommand()
        {
        }

        public MoveSquadsCommand(int playerId, IReadOnlyList<int> squadIds, Vec2 target)
        {
            PlayerId = playerId;
            SquadIds = Copy(squadIds);
            Target = target;
        }

        public override void Apply(Simulation sim)
        {
            var squads = OwnSquads(sim, PlayerId, SquadIds);
            for (int slot = 0; slot < squads.Count; slot++)
            {
                var squad = squads[slot];
                squad.ClearOrders();
                var dest = sim.Map.Grid.ClampToWorld(Target + FormationOffset(slot, squads.Count));
                if (SnapToCover && !squad.Def.IsVehicle && sim.Cover.TryFindCoverNear(dest, sim.Rules.CoverSnapRadius, out var coverPos))
                {
                    dest = coverPos;
                }

                sim.Movement.OrderMove(squad, dest);
            }
        }

        /// <summary>Slot offsets in a compact grid around the target (6 m spacing).</summary>
        public static Vec2 FormationOffset(int slot, int count)
        {
            const float spacing = 6f;
            int columns = 1;
            while (columns * columns < count)
            {
                columns++;
            }

            int row = slot / columns;
            int col = slot % columns;
            int rows = (count + columns - 1) / columns;
            float x = (col - (columns - 1) * 0.5f) * spacing;
            float y = (row - (rows - 1) * 0.5f) * spacing;
            return new Vec2(x, y);
        }
    }

    /// <summary>Attack a specific enemy squad or structure; squads close to weapon range if needed.</summary>
    public sealed class AttackCommand : Command
    {
        public int[] SquadIds;
        public int TargetId;

        public AttackCommand()
        {
        }

        public AttackCommand(int playerId, IReadOnlyList<int> squadIds, int targetId)
        {
            PlayerId = playerId;
            SquadIds = Copy(squadIds);
            TargetId = targetId;
        }

        public override void Apply(Simulation sim)
        {
            foreach (var squad in OwnSquads(sim, PlayerId, SquadIds))
            {
                sim.Combat.OrderAttack(squad, TargetId);
            }
        }
    }

    /// <summary>Retreat to the HQ: faster, harder to hit, cannot fire, ignores pinning.</summary>
    public sealed class RetreatCommand : Command
    {
        public int[] SquadIds;

        public RetreatCommand()
        {
        }

        public RetreatCommand(int playerId, IReadOnlyList<int> squadIds)
        {
            PlayerId = playerId;
            SquadIds = Copy(squadIds);
        }

        public override void Apply(Simulation sim)
        {
            foreach (var squad in OwnSquads(sim, PlayerId, SquadIds))
            {
                sim.Support.OrderRetreat(squad);
            }
        }
    }

    /// <summary>Refill lost models (near the HQ or a production building; costs manpower per model).</summary>
    public sealed class ReinforceCommand : Command
    {
        public int[] SquadIds;

        public ReinforceCommand()
        {
        }

        public ReinforceCommand(int playerId, IReadOnlyList<int> squadIds)
        {
            PlayerId = playerId;
            SquadIds = Copy(squadIds);
        }

        public override void Apply(Simulation sim)
        {
            foreach (var squad in OwnSquads(sim, PlayerId, SquadIds))
            {
                if (!squad.Def.IsVehicle)
                {
                    squad.ReinforcePending = squad.Def.SquadSize - squad.Models;
                }
            }
        }
    }

    /// <summary>Stop moving and drop attack/build orders.</summary>
    public sealed class StopCommand : Command
    {
        public int[] SquadIds;

        public StopCommand()
        {
        }

        public StopCommand(int playerId, IReadOnlyList<int> squadIds)
        {
            PlayerId = playerId;
            SquadIds = Copy(squadIds);
        }

        public override void Apply(Simulation sim)
        {
            foreach (var squad in OwnSquads(sim, PlayerId, SquadIds))
            {
                if (!squad.IsRetreating)
                {
                    squad.ClearOrders();
                    sim.Movement.Stop(squad);
                }
            }
        }
    }

    /// <summary>Queue a unit at a production structure (cost and pop are reserved immediately).</summary>
    public sealed class ProduceCommand : Command
    {
        public int StructureId;
        public string UnitId;

        public ProduceCommand()
        {
        }

        public ProduceCommand(int playerId, int structureId, string unitId)
        {
            PlayerId = playerId;
            StructureId = structureId;
            UnitId = unitId;
        }

        public override void Apply(Simulation sim)
        {
            var reason = sim.Production.TryEnqueue(PlayerId, StructureId, UnitId);
            if (reason != RejectReason.None)
            {
                sim.Reject(PlayerId, reason, UnitId);
            }
        }
    }

    /// <summary>Remove a queued unit and refund its cost.</summary>
    public sealed class CancelProductionCommand : Command
    {
        public int StructureId;
        public int Index;

        public CancelProductionCommand()
        {
        }

        public CancelProductionCommand(int playerId, int structureId, int index)
        {
            PlayerId = playerId;
            StructureId = structureId;
            Index = index;
        }

        public override void Apply(Simulation sim) => sim.Production.Cancel(PlayerId, StructureId, Index);
    }

    /// <summary>
    /// Engineers place a new structure (center cell + orientation 0/1), or help build an existing one
    /// when <see cref="ExistingStructureId"/> is set.
    /// </summary>
    public sealed class ConstructCommand : Command
    {
        public int[] SquadIds;
        public string StructureId;
        public int CellX;
        public int CellY;
        public int Orientation;
        public int ExistingStructureId;

        public ConstructCommand()
        {
        }

        public ConstructCommand(int playerId, IReadOnlyList<int> squadIds, string structureId, GridPos centerCell, int orientation = 0)
        {
            PlayerId = playerId;
            SquadIds = Copy(squadIds);
            StructureId = structureId;
            CellX = centerCell.X;
            CellY = centerCell.Y;
            Orientation = orientation;
        }

        public static ConstructCommand Assist(int playerId, IReadOnlyList<int> squadIds, int existingStructureId) =>
            new ConstructCommand { PlayerId = playerId, SquadIds = Copy(squadIds), ExistingStructureId = existingStructureId };

        public override void Apply(Simulation sim)
        {
            var engineers = OwnSquads(sim, PlayerId, SquadIds).FindAll(s => s.Def.Engineer && !s.IsRetreating);
            if (engineers.Count == 0)
            {
                sim.Reject(PlayerId, RejectReason.NoEngineer, StructureId);
                return;
            }

            if (ExistingStructureId != 0)
            {
                var existing = sim.World.GetStructure(ExistingStructureId);
                if (existing != null && existing.OwnerId == PlayerId && !existing.IsComplete)
                {
                    sim.Production.AssignBuilders(engineers, existing);
                }

                return;
            }

            var reason = sim.Production.TryPlace(PlayerId, engineers, StructureId, new GridPos(CellX, CellY), Orientation, out _);
            if (reason != RejectReason.None)
            {
                sim.Reject(PlayerId, reason, StructureId);
            }
        }
    }

    /// <summary>Where newly produced units walk to.</summary>
    public sealed class SetRallyPointCommand : Command
    {
        public int StructureId;
        public Vec2 Point;

        public SetRallyPointCommand()
        {
        }

        public SetRallyPointCommand(int playerId, int structureId, Vec2 point)
        {
            PlayerId = playerId;
            StructureId = structureId;
            Point = point;
        }

        public override void Apply(Simulation sim)
        {
            var s = sim.World.GetStructure(StructureId);
            if (s != null && s.OwnerId == PlayerId)
            {
                s.RallyPoint = sim.Map.Grid.ClampToWorld(Point);
                s.HasRallyPoint = true;
            }
        }
    }
}
