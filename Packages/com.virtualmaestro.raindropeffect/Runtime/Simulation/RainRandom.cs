namespace RainDropEffect
{
    /// <summary>
    /// Xorshift32. Deterministic and allocation-free, so a given seed reproduces a run exactly —
    /// which is what the visual comparison in Task 27 depends on. Not for anything security-related.
    /// </summary>
    public struct RainRandom
    {
        uint s;

        public RainRandom(int seed)
        {
            // Xorshift dies on a zero state, so the seed 0 becomes the golden-ratio constant.
            s = seed == 0 ? 0x9E3779B9u : (uint)seed;
            if (s == 0)
            {
                s = 1;
            }
        }

        public uint NextUInt()
        {
            s ^= s << 13;
            s ^= s >> 17;
            s ^= s << 5;
            return s;
        }

        /// <summary>Uniform in [0, 1). 24 bits, which is all a float can hold anyway.</summary>
        public float NextFloat() => (NextUInt() & 0xFFFFFF) / 16777216f;

        public float Range(float min, float max) => min + (max - min) * NextFloat();

        public int Range(int minInclusive, int maxExclusive)
        {
            return maxExclusive <= minInclusive
                ? minInclusive
                : minInclusive + (int)(NextUInt() % (uint)(maxExclusive - minInclusive));
        }
    }
}
