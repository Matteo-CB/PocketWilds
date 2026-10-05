#nullable enable
using System;
using PocketWilds.Content;

namespace PocketWilds.Simulation
{
    /// <summary>Extraordinary traits (doc 03). Bits 0 to 7 of <see cref="AnimalInstance.TraitBits"/>. Stable values.</summary>
    [Flags]
    public enum AnimalTrait : byte
    {
        None = 0,
        Tiny = 1 << 0,
        Giant = 1 << 1,
        Shiny = 1 << 2,
        Golden = 1 << 3,
        Albino = 1 << 4,
        Melanistic = 1 << 5,
    }

    /// <summary>
    /// The compact genome stored in <see cref="AnimalInstance.TraitBits"/> (T26). Zero means a plain animal,
    /// so every animal saved before genetics keeps its look.
    /// <list type="bullet">
    /// <item>bits 0-7: <see cref="AnimalTrait"/> flags;</item>
    /// <item>bits 8-11: size gene, signed -8..7, 2.5 % per step (a small range, doc 03);</item>
    /// <item>bits 12-14: carried pattern channel, 0 = none (a palette without its own pattern shows it tone on tone);</item>
    /// <item>bits 15-18: carried pattern strength 0..15; bits 19-31 reserved.</item>
    /// </list>
    /// </summary>
    public static class AnimalGenome
    {
        public const int TraitCount = 6;
        public const int MinSize = -6;
        public const int MaxSize = 6;
        public const float SizeStep = 0.025f;
        public const float TinyScale = 0.8f;
        public const float GiantScale = 1.2f;

        private const uint TraitMask = 0xFFu;
        private const int SizeShift = 8;
        private const uint SizeMask = 0xFu << SizeShift;
        private const int ChannelShift = 12;
        private const uint ChannelMask = 0x7u << ChannelShift;
        private const int StrengthShift = 15;
        private const uint StrengthMask = 0xFu << StrengthShift;

        public static AnimalTrait Traits(uint bits) => (AnimalTrait)(bits & TraitMask);
        public static bool Has(uint bits, AnimalTrait trait) => (bits & (uint)trait) != 0u;
        public static uint WithTraits(uint bits, AnimalTrait traits) => (bits & ~TraitMask) | (uint)traits;

        public static int Size(uint bits)
        {
            int raw = (int)((bits & SizeMask) >> SizeShift);
            return raw >= 8 ? raw - 16 : raw;
        }

        public static uint WithSize(uint bits, int size)
        {
            int clamped = Math.Max(MinSize, Math.Min(MaxSize, size));
            return (bits & ~SizeMask) | ((uint)(clamped & 0xF) << SizeShift);
        }

        public static PatternChannel CarriedPattern(uint bits) => (PatternChannel)((bits & ChannelMask) >> ChannelShift);
        public static int CarriedStrength(uint bits) => (int)((bits & StrengthMask) >> StrengthShift);

        public static uint WithPattern(uint bits, PatternChannel channel, int strength)
        {
            int s = channel == PatternChannel.None ? 0 : Math.Max(0, Math.Min(15, strength));
            return (bits & ~(ChannelMask | StrengthMask)) | ((uint)channel << ChannelShift) | ((uint)s << StrengthShift);
        }

        /// <summary>Visible scale: size gene, then Tiny or Giant.</summary>
        public static float Scale(uint bits)
        {
            float scale = 1f + Size(bits) * SizeStep;
            if (Has(bits, AnimalTrait.Tiny))
                scale *= TinyScale;
            else if (Has(bits, AnimalTrait.Giant))
                scale *= GiantScale;
            return scale;
        }

        public static AnimalTrait TraitAt(int index) => (AnimalTrait)(1 << index);
    }
}
