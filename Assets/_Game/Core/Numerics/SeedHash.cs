namespace PocketWilds.Core
{
    /// <summary>
    /// Stateless deterministic hashing for per-animal cosmetic values (idle phase, small offsets).
    /// Same seed and salt give the same result on every platform. Not for gameplay rolls:
    /// hatch and genetics use their own versioned RNG streams.
    /// </summary>
    public static class SeedHash
    {
        /// <summary>lowbias32 finalizer (Wellons). Good avalanche, no allocation.</summary>
        public static uint Mix(uint x)
        {
            x ^= x >> 16;
            x *= 0x7feb352du;
            x ^= x >> 15;
            x *= 0x846ca68bu;
            x ^= x >> 16;
            return x;
        }

        public static uint Mix(uint seed, uint salt) => Mix(seed ^ Mix(salt + 0x9e3779b9u));

        /// <summary>Uniform value in [0, 1) from the top 24 bits.</summary>
        public static float Unit(uint seed, uint salt) => (Mix(seed, salt) >> 8) * (1f / 16777216f);
    }
}
