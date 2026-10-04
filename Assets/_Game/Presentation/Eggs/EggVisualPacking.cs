using PocketWilds.Content;
using UnityEngine;

namespace PocketWilds.Presentation
{
    /// <summary>Packs the per-renderer Shader User Value read by SH_EggMaster (layout in the shader header).</summary>
    public static class EggVisualPacking
    {
        private const int StageShift = 10;
        private const int GlowShift = 24;

        public static uint Pack(int paletteRow, int crackStage, int strengthR, int strengthG, int strengthB, int glow)
        {
            uint row = (uint)Mathf.Clamp(paletteRow, 0, PaletteTable.MaxRows - 1);
            return row
                   | ((uint)Mathf.Clamp(crackStage, 0, 3) << StageShift)
                   | ((uint)Mathf.Clamp(strengthR, 0, 15) << 12)
                   | ((uint)Mathf.Clamp(strengthG, 0, 15) << 16)
                   | ((uint)Mathf.Clamp(strengthB, 0, 15) << 20)
                   | ((uint)Mathf.Clamp(glow, 0, 15) << GlowShift);
        }

        public static uint FromDefinition(EggDefinition egg, int paletteRow) =>
            Pack(paletteRow, 0, egg.MaskStrengthR, egg.MaskStrengthG, egg.MaskStrengthB, 0);

        public static uint WithCrackStage(uint packed, int stage) =>
            (packed & ~(3u << StageShift)) | ((uint)Mathf.Clamp(stage, 0, 3) << StageShift);

        public static uint WithGlow(uint packed, int glow) =>
            (packed & ~(15u << GlowShift)) | ((uint)Mathf.Clamp(glow, 0, 15) << GlowShift);

        public static int PaletteRow(uint packed) => (int)(packed & 1023u);
        public static int CrackStage(uint packed) => (int)((packed >> StageShift) & 3u);
        public static int StrengthR(uint packed) => (int)((packed >> 12) & 15u);
        public static int StrengthG(uint packed) => (int)((packed >> 16) & 15u);
        public static int StrengthB(uint packed) => (int)((packed >> 20) & 15u);
        public static int Glow(uint packed) => (int)((packed >> GlowShift) & 15u);
    }
}
