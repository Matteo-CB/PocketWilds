#nullable enable
namespace PocketWilds.Simulation
{
    /// <summary>
    /// Inheritance table of the genetics prototype (T26). Doc 03 asks for a weighted table under a simple
    /// surface; these are the starting values, data rather than code so balancing never touches logic.
    /// </summary>
    public sealed class GeneticsConfig
    {
        /// <summary>Two parents of the same variant: the child keeps it this often.</summary>
        public double SameVariantKeep = 0.90;

        /// <summary>Two different variants: each parent passes its own this often (the rest is a mutation).</summary>
        public double ParentVariantShare = 0.42;

        /// <summary>
        /// Mutation odds by rarity offset from the rarer parent, offsets -2..+3. A mutation is never a parent's
        /// variant and never a secret one.
        /// </summary>
        public double[] MutationTierWeights = { 0.05, 0.25, 0.40, 0.25, 0.04, 0.01 };
        public const int MutationLowestOffset = -2;

        /// <summary>Carried pattern (shown on a palette without its own pattern): both parents carry, one, none.</summary>
        public double PatternBoth = 0.75;
        public double PatternOne = 0.40;
        public double PatternNew = 0.03;

        /// <summary>Extraordinary traits: both parents, one parent, none (per trait, same order as <see cref="AnimalTrait"/>).</summary>
        public double TraitBoth = 0.60;
        public double TraitOne = 0.25;
        public double[] TraitNew = { 0.010, 0.010, 0.005, 0.002, 0.003, 0.003 };

        /// <summary>Doc 03: an egg after a light delay. Used when breeding reaches the refuge.</summary>
        public double BreedingSeconds = 300.0;

        public static GeneticsConfig Default => new GeneticsConfig();
    }
}
