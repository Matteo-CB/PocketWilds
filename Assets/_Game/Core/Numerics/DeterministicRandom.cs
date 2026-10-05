namespace PocketWilds.Core
{
    /// <summary>
    /// Small explicit RNG for simulation rolls (Codebase Blueprint: never UnityEngine.Random for persistent
    /// logic). PCG32 (O'Neill): same seed, same sequence on every platform. A value type: callers keep
    /// it on the stack, nothing allocates.
    /// </summary>
    public struct DeterministicRandom
    {
        private const ulong Multiplier = 6364136223846793005UL;
        private const ulong Increment = 1442695040888963407UL;

        private ulong _state;

        public DeterministicRandom(uint seed)
        {
            _state = 0UL;
            NextUInt();
            _state += seed;
            NextUInt();
        }

        public uint NextUInt()
        {
            ulong old = _state;
            _state = old * Multiplier + Increment;
            uint xorShifted = (uint)(((old >> 18) ^ old) >> 27);
            int rotation = (int)(old >> 59);
            return (xorShifted >> rotation) | (xorShifted << ((-rotation) & 31));
        }

        /// <summary>Uniform in [0, 1) with 32 bits of resolution.</summary>
        public double NextDouble() => NextUInt() * (1.0 / 4294967296.0);

        /// <summary>Uniform integer in [0, maxExclusive).</summary>
        public int Next(int maxExclusive) => maxExclusive <= 1 ? 0 : (int)(NextDouble() * maxExclusive);
    }
}
