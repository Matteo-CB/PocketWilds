using System;
using System.Collections.Generic;
using PocketWilds.Content;
using PocketWilds.Core;
using UnityEngine;

namespace PocketWilds.Presentation
{
    /// <summary>
    /// Every palette (animal variants, eggs, then world palettes) in one tiny global texture, 8 colors per row.
    /// Built once at boot. A new palette is data only: no material, no texture asset, no code.
    /// Shaders read the row from the per-renderer Shader User Value. T48B: the texture keeps its CPU copy (8 x rows x 4 bytes,
    /// 16 KB at 512 rows) so a season rewrites a few world rows in place and uploads them in one Apply.
    /// </summary>
    public sealed class PaletteTable : IDisposable
    {
        public const int MaxRows = 1024;
        public const int Columns = 8;
        public static readonly int PaletteTextureId = Shader.PropertyToID("_PW_Palettes");
        public static readonly int RowCountId = Shader.PropertyToID("_PW_PaletteRowCount");

        private readonly Dictionary<ContentId, int> _rowById;
        private Texture2D _texture;
        private int _pendingRows;

        public PaletteTable(ContentRegistry registry)
        {
            var variants = registry.Variants;
            var eggs = registry.Eggs;
            var world = registry.Palettes;
            int count = variants.Count + eggs.Count + world.Count;
            if (count > MaxRows)
                throw new InvalidOperationException($"Too many palettes ({count}). Raise the RSUV row bits first.");

            int rows = Mathf.NextPowerOfTwo(Mathf.Max(1, count));
            _rowById = new Dictionary<ContentId, int>(count);
            var pixels = new Color32[Columns * rows];
            int row = 0;
            for (int i = 0; i < variants.Count; i++)
                Write(pixels, row++, variants[i].Id, variants[i].Palette);
            for (int i = 0; i < eggs.Count; i++)
                Write(pixels, row++, eggs[i].Id, eggs[i].Palette);
            for (int i = 0; i < world.Count; i++)
                Write(pixels, row++, world[i].Id, world[i].Colors);

            _texture = new Texture2D(Columns, rows, TextureFormat.RGBA32, mipChain: false, linear: false)
            {
                name = "PW_Palettes",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave,
            };
            _texture.SetPixels32(pixels);
            _texture.Apply(updateMipmaps: false, makeNoLongerReadable: false);

            Shader.SetGlobalTexture(PaletteTextureId, _texture);
            Shader.SetGlobalFloat(RowCountId, rows);
            RowCount = rows;
            PaletteCount = count;
        }

        public int RowCount { get; }
        public int PaletteCount { get; }

        /// <summary>Row of a variant or egg palette.</summary>
        public bool TryGetRow(ContentId id, out int row) => _rowById.TryGetValue(id, out row);

        /// <summary>T48B the 8 colors a row holds now (CPU copy of the texture). No allocation.</summary>
        public void ReadRow(int row, Color32[] into)
        {
            var pixels = _texture.GetPixelData<Color32>(0);
            for (int c = 0; c < Columns; c++)
                into[c] = pixels[row * Columns + c];
        }

        /// <summary>
        /// T48B rewrites one row in the CPU copy from colors[offset..offset+8). False when it already holds them (nothing to upload).
        /// The upload waits for <see cref="ApplyRows"/>. No allocation.
        /// </summary>
        public bool WriteRow(int row, Color32[] colors, int offset = 0)
        {
            if (_texture == null || row < 0 || row >= RowCount)
                return false;
            var pixels = _texture.GetPixelData<Color32>(0);
            bool changed = false;
            for (int c = 0; c < Columns; c++)
            {
                int at = row * Columns + c;
                var color = colors[offset + c];
                var old = pixels[at];
                if (old.r == color.r && old.g == color.g && old.b == color.b && old.a == color.a)
                    continue;
                pixels[at] = color;
                changed = true;
            }
            if (changed)
                _pendingRows++;
            return changed;
        }

        /// <summary>T48B uploads the rows written since the last call in one Apply; returns how many rows changed (0: no upload).</summary>
        public int ApplyRows()
        {
            int rows = _pendingRows;
            if (rows == 0 || _texture == null)
                return 0;
            _texture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
            _pendingRows = 0;
            return rows;
        }

        private void Write(Color32[] pixels, int row, ContentId id, Color32[] palette)
        {
            _rowById.Add(id, row);
            for (int c = 0; c < Columns; c++)
                pixels[row * Columns + c] = palette[c];
        }

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

    /// <summary>
    /// Packs the per-renderer Shader User Value read by SH_AnimalMaster. Bits 21-22 (T49) carry the variant module slot:
    /// the shader never reads them, AnimalView shows the matching module mesh when it applies the value.
    /// </summary>
    public static class AnimalVisualPacking
    {
        public static uint Pack(int paletteRow, PatternChannel pattern, int patternStrength, int rareIntensity, int moduleSlot = 0)
        {
            uint row = (uint)Mathf.Clamp(paletteRow, 0, PaletteTable.MaxRows - 1);
            uint channel = (uint)pattern & 7u;
            uint strength = (uint)Mathf.Clamp(patternStrength, 0, 15);
            uint rare = (uint)Mathf.Clamp(rareIntensity, 0, 15);
            uint module = (uint)Mathf.Clamp(moduleSlot, 0, 3);
            return row | (channel << 10) | (strength << 13) | (rare << 17) | (module << 21);
        }

        /// <summary>T49: variant module slot (0 none, 1-3).</summary>
        public static int ModuleSlot(uint packed) => (int)((packed >> 21) & 3u);

        public static int PaletteRow(uint packed) => (int)(packed & 1023u);
        public static PatternChannel Pattern(uint packed) => (PatternChannel)((packed >> 10) & 7u);
        public static int PatternStrength(uint packed) => (int)((packed >> 13) & 15u);
        public static int RareIntensity(uint packed) => (int)((packed >> 17) & 15u);
    }
}
