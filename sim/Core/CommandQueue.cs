using System.Collections.Generic;

namespace OCT7.Sim
{
    /// <summary>Holds pending commands and releases them in deterministic order (tick, player, sequence).</summary>
    public sealed class CommandQueue
    {
        private readonly List<Command> _pending = new List<Command>();
        private long _nextSequence;

        public int Count => _pending.Count;

        public void Enqueue(Command command)
        {
            command.Sequence = _nextSequence++;
            _pending.Add(command);
        }

        /// <summary>Moves every command due at or before <paramref name="tick"/> into <paramref name="output"/>, sorted.</summary>
        public void TakeDue(int tick, List<Command> output)
        {
            int start = output.Count;
            for (int i = 0; i < _pending.Count; i++)
            {
                if (_pending[i].Tick <= tick)
                {
                    output.Add(_pending[i]);
                }
            }

            if (output.Count == start)
            {
                return;
            }

            _pending.RemoveAll(c => c.Tick <= tick);
            output.Sort(start, output.Count - start, CommandOrder.Instance);
        }

        private sealed class CommandOrder : IComparer<Command>
        {
            public static readonly CommandOrder Instance = new CommandOrder();

            public int Compare(Command a, Command b)
            {
                int c = a.Tick.CompareTo(b.Tick);
                if (c != 0)
                {
                    return c;
                }

                c = a.PlayerId.CompareTo(b.PlayerId);
                return c != 0 ? c : a.Sequence.CompareTo(b.Sequence);
            }
        }
    }
}
