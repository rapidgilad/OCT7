using System.Collections.Generic;
using OCT7.Sim.Economy;

namespace OCT7.Sim.AI
{
    /// <summary>
    /// An AI player. It reads the simulation and emits commands, exactly like a human player would.
    /// Easy / Normal / Hard will be parameter sets of one implementation (docs/04-ai-and-difficulty.md).
    /// </summary>
    public interface IAiController
    {
        int PlayerId { get; }

        /// <summary>Called once per tick before <see cref="Simulation.Step"/>. Append commands to <paramref name="output"/>.</summary>
        void Think(Simulation sim, List<Command> output);
    }

    /// <summary>
    /// Placeholder AI: every few seconds, sends idle squads a step toward the enemy force
    /// (or the enemy HQ once no enemy squads remain) and holds when close.
    /// It exercises the command → movement pipeline until the real AI exists.
    /// </summary>
    public sealed class AdvanceAi : IAiController
    {
        private const int ThinkIntervalTicks = 5 * SimConfig.TicksPerSecond;
        private const float StepDistance = 45f;
        private const float HoldDistance = 30f;

        private readonly DeterministicRandom _random;
        private readonly List<int> _idle = new List<int>();

        public AdvanceAi(int playerId, ulong matchSeed)
        {
            PlayerId = playerId;
            // Own RNG stream so AI decisions never consume the simulation's random numbers.
            _random = new DeterministicRandom(matchSeed ^ (0xA1A1A1A1UL * (ulong)(playerId + 1)));
        }

        public int PlayerId { get; }

        public void Think(Simulation sim, List<Command> output)
        {
            // Stagger players so they don't all think on the same tick.
            if ((sim.Tick + PlayerId * 7) % ThinkIntervalTicks != 0)
            {
                return;
            }

            var enemy = FindEnemy(sim);
            if (enemy == null)
            {
                return;
            }

            _idle.Clear();
            Vec2 centroid = Vec2.Zero;
            Vec2 enemyCentroid = Vec2.Zero;
            int enemyCount = 0;
            foreach (var squad in sim.World.Squads)
            {
                if (squad.OwnerId == PlayerId)
                {
                    if (!squad.IsMoving)
                    {
                        _idle.Add(squad.Id);
                        centroid += squad.Position;
                    }
                }
                else if (squad.OwnerId == enemy.Id)
                {
                    enemyCentroid += squad.Position;
                    enemyCount++;
                }
            }

            if (_idle.Count == 0)
            {
                return;
            }

            centroid /= _idle.Count;
            var objective = enemyCount > 0 ? enemyCentroid / enemyCount : enemy.HqPosition;
            var toEnemy = objective - centroid;
            float distance = toEnemy.Length;
            if (distance <= HoldDistance)
            {
                return;
            }

            float step = distance < StepDistance ? distance - HoldDistance * 0.5f : StepDistance;
            var jitter = new Vec2(_random.Range(-8f, 8f), _random.Range(-8f, 8f));
            var target = centroid + toEnemy.Normalized() * step + jitter;
            output.Add(new MoveSquadsCommand(PlayerId, _idle, sim.Map.Grid.ClampToWorld(target)));
        }

        private PlayerState FindEnemy(Simulation sim)
        {
            foreach (var p in sim.Players)
            {
                if (p.Id != PlayerId)
                {
                    return p;
                }
            }

            return null;
        }
    }

    /// <summary>Shared host loop so Godot, MatchRunner and tests drive AI and the sim in the same order.</summary>
    public static class SimLoop
    {
        public static void Step(Simulation sim, IReadOnlyList<IAiController> ais, List<Command> buffer)
        {
            buffer.Clear();
            if (ais != null)
            {
                for (int i = 0; i < ais.Count; i++)
                {
                    ais[i].Think(sim, buffer);
                }
            }

            for (int i = 0; i < buffer.Count; i++)
            {
                sim.Enqueue(buffer[i]);
            }

            sim.Step();
        }
    }
}
