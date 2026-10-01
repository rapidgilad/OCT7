using System;

namespace OCT7.Sim
{
    /// <summary>
    /// Seeded xorshift128+ generator. The only source of randomness allowed in simulation logic
    /// (never System.Random, never time-based seeds), so a match replays identically from its seed.
    /// </summary>
    public sealed class DeterministicRandom
    {
        private ulong _s0;
        private ulong _s1;

        public DeterministicRandom(ulong seed)
        {
            ulong x = seed;
            _s0 = SplitMix64(ref x);
            _s1 = SplitMix64(ref x);
            if (_s0 == 0 && _s1 == 0)
            {
                _s1 = 1;
            }
        }

        public ulong State0 => _s0;
        public ulong State1 => _s1;

        public ulong NextULong()
        {
            ulong s1 = _s0;
            ulong s0 = _s1;
            ulong result = s0 + s1;
            _s0 = s0;
            s1 ^= s1 << 23;
            _s1 = s1 ^ s0 ^ (s1 >> 17) ^ (s0 >> 26);
            return result;
        }

        /// <summary>Uniform float in [0, 1).</summary>
        public float NextFloat() => (NextULong() >> 40) * (1f / (1UL << 24));

        /// <summary>Uniform float in [min, max).</summary>
        public float Range(float min, float max) => min + (max - min) * NextFloat();

        /// <summary>Uniform int in [minInclusive, maxExclusive).</summary>
        public int NextInt(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive)
            {
                throw new ArgumentOutOfRangeException(nameof(maxExclusive), "maxExclusive must be greater than minInclusive.");
            }

            ulong range = (ulong)((long)maxExclusive - minInclusive);
            return (int)((long)minInclusive + (long)(NextULong() % range));
        }

        /// <summary>Returns true with the given probability (0..1).</summary>
        public bool Chance(float probability) => NextFloat() < probability;

        private static ulong SplitMix64(ref ulong x)
        {
            x += 0x9E3779B97F4A7C15UL;
            ulong z = x;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
    }
}
