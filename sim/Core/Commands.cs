using System.Collections.Generic;

namespace OCT7.Sim
{
    /// <summary>
    /// Every change a player (human or AI) makes to the match is a command.
    /// Commands are queued with a target tick and applied at the start of that tick in a fixed order,
    /// which makes matches replayable and keeps human and AI on exactly the same code path.
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
    }

    /// <summary>Move one or more squads to a ground position. Squads spread into a loose formation around the target.</summary>
    public sealed class MoveSquadsCommand : Command
    {
        public int[] SquadIds;
        public Vec2 Target;

        public MoveSquadsCommand()
        {
        }

        public MoveSquadsCommand(int playerId, IReadOnlyList<int> squadIds, Vec2 target)
        {
            PlayerId = playerId;
            SquadIds = new int[squadIds.Count];
            for (int i = 0; i < squadIds.Count; i++)
            {
                SquadIds[i] = squadIds[i];
            }

            Target = target;
        }

        public override void Apply(Simulation sim)
        {
            if (SquadIds == null || SquadIds.Length == 0)
            {
                return;
            }

            // Sort ids so formation slots never depend on selection order.
            var ids = (int[])SquadIds.Clone();
            System.Array.Sort(ids);

            int slot = 0;
            for (int i = 0; i < ids.Length; i++)
            {
                var squad = sim.World.GetSquad(ids[i]);
                if (squad == null || squad.OwnerId != PlayerId)
                {
                    continue;
                }

                sim.Movement.OrderMove(squad, Target + FormationOffset(slot, ids.Length));
                slot++;
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
}
