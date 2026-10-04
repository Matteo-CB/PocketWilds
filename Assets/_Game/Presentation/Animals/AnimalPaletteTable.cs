using System;
using System.Collections.Generic;
using PocketWilds.Content;
using PocketWilds.Core;
using UnityEngine;

namespace PocketWilds.Presentation
{
    /// <summary>
    /// Every variant palette in one tiny global texture (8 colors per row). Built once at boot.
    /// A new palette is data only: no material, no texture asset, no code.
    /// </summary>
    public sealed class AnimalPaletteTable : IDisposable
    {
        public const int MaxRows = 1024;
        public static readonly int PaletteTextureId = Shader.PropertyToID("_PW_AnimalPalettes");
        public static readonly int RowCountId = Shader.PropertyToID("_PW_PaletteRowCount");

        private readonly Dictionary<ContentId, int> _rowByVariant;
        private Texture2D _texture;

        public AnimalPaletteTable(ContentTable<VariantDefinition> variants)
        {
            int count = variants.Count;
            if (count > MaxRows)
                throw new InvalidOperationException($"Too many palettes ({count}). Raise the RSUV row bits first.");

            int rows = Mathf.NextPowerOfTwo(Mathf.Max(1, count));
            _rowByVariant = new Dictionary<ContentId, int>(count);
            var pixels = new Color32[VariantDefinition.PaletteSize * rows];
            for (int row = 0; row < count; row++)
            {
                var variant = variants[row];
                _rowByVariant.Add(variant.Id, row);
                for (int c = 0; c < VariantDefinition.PaletteSize; c++)
                    pixels[row * VariantDefinition.PaletteSize + c] = variant.Palette[c];
            }

            _texture = new Texture2D(VariantDefinition.PaletteSize, rows, TextureFormat.RGBA32, mipChain: false, linear: false)
            {
                name = "PW_AnimalPalettes",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave,
            };
            _texture.SetPixels32(pixels);
            _texture.Apply(updateMipmaps: false, makeNoLongerReadable: true);

            Shader.SetGlobalTexture(PaletteTextureId, _texture);
            Shader.SetGlobalFloat(RowCountId, rows);
            RowCount = rows;
            PaletteCount = count;
        }

        public int RowCount { get; }
        public int PaletteCount { get; }

        public bool TryGetRow(ContentId variant, out int row) => _rowByVariant.TryGetValue(variant, out row);

        public void Dispose()
        {
            if (_texture == null)
                return;
            Shader.SetGlobalFloat(RowCountId, 0f);
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(_texture);
            else
                UnityEngine.Object.DestroyImmediate(_texture);
            _texture = null;
        }
    }

    /// <summary>Packs the per-renderer Shader User Value read by SH_AnimalMaster.</summary>
    public static class AnimalVisualPacking
    {
        public static uint Pack(int paletteRow, PatternChannel pattern, int patternStrength, int rareIntensity)
        {
            uint row = (uint)Mathf.Clamp(paletteRow, 0, AnimalPaletteTable.MaxRows - 1);
            uint channel = (uint)pattern & 7u;
            uint strength = (uint)Mathf.Clamp(patternStrength, 0, 15);
            uint rare = (uint)Mathf.Clamp(rareIntensity, 0, 15);
            return row | (channel << 10) | (strength << 13) | (rare << 17);
        }

        public static int PaletteRow(uint packed) => (int)(packed & 1023u);
        public static PatternChannel Pattern(uint packed) => (PatternChannel)((packed >> 10) & 7u);
        public static int PatternStrength(uint packed) => (int)((packed >> 13) & 15u);
        public static int RareIntensity(uint packed) => (int)((packed >> 17) & 15u);
    }
}
