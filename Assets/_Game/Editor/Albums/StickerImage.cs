using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PocketWilds.Editor.Albums
{
    /// <summary>
    /// T44A image steps of the sticker capture (doc « 01 Albums et autocollants », section 1.11): alpha from two renders
    /// (black and white backgrounds, as the Codex thumbnails), crop, area-averaged downscale in premultiplied linear
    /// light, the shape (cutout inset, rounded card, round medallion) and the 10 px white cut border computed from the
    /// alpha. Pixels are row-major from the bottom (ReadPixels order); colors are premultiplied linear floats.
    /// </summary>
    public sealed class StickerImage
    {
        public readonly int Size;
        public readonly float[] Rgb;
        public readonly float[] Alpha;

        public StickerImage(int size)
        {
            Size = size;
            Rgb = new float[size * size * 3];
            Alpha = new float[size * size];
        }

        /// <summary>On black C x a, on white C x a + (1 - a): solved in linear light (the target is sRGB).</summary>
        public static StickerImage FromPair(Color32[] black, Color32[] white, int size)
        {
            var image = new StickerImage(size);
            int count = size * size;
            for (int i = 0; i < count; i++)
            {
                float br = Linear(black[i].r), bg = Linear(black[i].g), bb = Linear(black[i].b);
                float wr = Linear(white[i].r), wg = Linear(white[i].g), wb = Linear(white[i].b);
                float a = Mathf.Clamp01(1f - ((wr - br) + (wg - bg) + (wb - bb)) / 3f);
                image.Alpha[i] = a;
                image.Rgb[i * 3] = Mathf.Min(br, a);
                image.Rgb[i * 3 + 1] = Mathf.Min(bg, a);
                image.Rgb[i * 3 + 2] = Mathf.Min(bb, a);
            }
            return image;
        }

        public static StickerImage FromOpaque(Color32[] pixels, int size)
        {
            var image = new StickerImage(size);
            for (int i = 0; i < size * size; i++)
            {
                image.Alpha[i] = 1f;
                image.Rgb[i * 3] = Linear(pixels[i].r);
                image.Rgb[i * 3 + 1] = Linear(pixels[i].g);
                image.Rgb[i * 3 + 2] = Linear(pixels[i].b);
            }
            return image;
        }

        /// <summary>Square around the visible pixels, <paramref name="margin"/> of its side on each edge, inside the image.</summary>
        public RectInt CropAround(float margin)
        {
            int minX = Size, minY = Size, maxX = -1, maxY = -1;
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    if (Alpha[y * Size + x] < 0.02f)
                        continue;
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }
            if (maxX < 0)
                return new RectInt(0, 0, Size, Size);
            int content = Mathf.Max(maxX - minX + 1, maxY - minY + 1);
            int side = Mathf.Min(Size, Mathf.CeilToInt(content / Mathf.Max(0.2f, 1f - 2f * margin)));
            int x0 = Mathf.Clamp((minX + maxX + 1) / 2 - side / 2, 0, Size - side);
            int y0 = Mathf.Clamp((minY + maxY + 1) / 2 - side / 2, 0, Size - side);
            return new RectInt(x0, y0, side, side);
        }

        /// <summary>True when visible pixels touch the image's edge (the subject was cut by the frame).</summary>
        public bool TouchesEdge()
        {
            for (int i = 0; i < Size; i++)
            {
                if (Alpha[i] > 0.1f || Alpha[(Size - 1) * Size + i] > 0.1f || Alpha[i * Size] > 0.1f || Alpha[i * Size + Size - 1] > 0.1f)
                    return true;
            }
            return false;
        }

        /// <summary>Area-averaged downscale of a square crop.</summary>
        public StickerImage Resample(RectInt crop, int size)
        {
            var result = new StickerImage(size);
            float scale = crop.width / (float)size;
            for (int y = 0; y < size; y++)
            {
                int sy0 = crop.y + Mathf.FloorToInt(y * scale);
                int sy1 = Mathf.Max(sy0 + 1, crop.y + Mathf.FloorToInt((y + 1) * scale));
                for (int x = 0; x < size; x++)
                {
                    int sx0 = crop.x + Mathf.FloorToInt(x * scale);
                    int sx1 = Mathf.Max(sx0 + 1, crop.x + Mathf.FloorToInt((x + 1) * scale));
                    float r = 0f, g = 0f, b = 0f, a = 0f;
                    int n = 0;
                    for (int sy = sy0; sy < sy1 && sy < Size; sy++)
                    {
                        for (int sx = sx0; sx < sx1 && sx < Size; sx++)
                        {
                            int i = sy * Size + sx;
                            a += Alpha[i];
                            r += Rgb[i * 3];
                            g += Rgb[i * 3 + 1];
                            b += Rgb[i * 3 + 2];
                            n++;
                        }
                    }
                    int o = y * size + x;
                    n = Mathf.Max(1, n);
                    result.Alpha[o] = a / n;
                    result.Rgb[o * 3] = r / n;
                    result.Rgb[o * 3 + 1] = g / n;
                    result.Rgb[o * 3 + 2] = b / n;
                }
            }
            return result;
        }

        public StickerImage Resample(int size) => Resample(new RectInt(0, 0, Size, Size), size);

        /// <summary>
        /// Multiplies the content by its shape, antialiased: cutouts keep a rounded square inset by the border (a portrait's
        /// body is cut there and the border follows the cut), cards a rounded square inset by the border (the border then
        /// rounds it to <paramref name="cardCorner"/>), medallions a disc inset by the border.
        /// </summary>
        public void ApplyShape(StickerShape shape, int border, int cardCorner)
        {
            float half = Size * 0.5f;
            float inset = border + (shape == StickerShape.Cutout ? 2f : 0f);
            float radius = shape == StickerShape.Card ? Mathf.Max(1f, cardCorner - border) : shape == StickerShape.Cutout ? Size * 0.16f : 0f;
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float px = x + 0.5f - half, py = y + 0.5f - half;
                    float distance;
                    if (shape == StickerShape.Medallion)
                    {
                        distance = Mathf.Sqrt(px * px + py * py) - (half - inset);
                    }
                    else
                    {
                        float bx = half - inset - radius, by = half - inset - radius;
                        float qx = Mathf.Abs(px) - bx, qy = Mathf.Abs(py) - by;
                        float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
                        distance = outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
                    }
                    float coverage = Mathf.Clamp01(0.5f - distance);
                    if (coverage >= 1f)
                        continue;
                    int i = y * Size + x;
                    Alpha[i] *= coverage;
                    Rgb[i * 3] *= coverage;
                    Rgb[i * 3 + 1] *= coverage;
                    Rgb[i * 3 + 2] *= coverage;
                }
            }
        }

        /// <summary>
        /// The white cut border: every pixel within <paramref name="border"/> px of the content (alpha at least one half)
        /// becomes white under the content, antialiased on its outer edge. No baked shadow (the UI's shared shadow sprite).
        /// </summary>
        public void AddBorder(int border)
        {
            var offsets = new List<(int dx, int dy, float d)>();
            int reach = border + 1;
            for (int dy = -reach; dy <= reach; dy++)
                for (int dx = -reach; dx <= reach; dx++)
                {
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d <= border + 0.75f)
                        offsets.Add((dx, dy, d));
                }
            offsets.Sort((a, b) => a.d.CompareTo(b.d));
            var inside = new bool[Size * Size];
            for (int i = 0; i < inside.Length; i++)
                inside[i] = Alpha[i] >= 0.5f;
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    int i = y * Size + x;
                    if (inside[i])
                    {
                        Composite(i, 1f);
                        continue;
                    }
                    float nearest = float.MaxValue;
                    foreach (var (dx, dy, d) in offsets)
                    {
                        int sx = x + dx, sy = y + dy;
                        if (sx < 0 || sy < 0 || sx >= Size || sy >= Size || !inside[sy * Size + sx])
                            continue;
                        nearest = d;
                        break;
                    }
                    float white = Mathf.Clamp01(border + 0.5f - nearest);
                    if (white > 0f)
                        Composite(i, white);
                }
            }
        }

        private void Composite(int i, float white)
        {
            float a = Alpha[i];
            float under = (1f - a) * white;
            Rgb[i * 3] += under;
            Rgb[i * 3 + 1] += under;
            Rgb[i * 3 + 2] += under;
            Alpha[i] = a + under;
        }

        /// <summary>Straight-alpha sRGB pixels (rows from the bottom, as Texture2D expects).</summary>
        public Color32[] ToPixels()
        {
            var pixels = new Color32[Size * Size];
            for (int i = 0; i < pixels.Length; i++)
            {
                float a = Alpha[i];
                if (a < 0.002f)
                {
                    pixels[i] = new Color32(255, 255, 255, 0);
                    continue;
                }
                pixels[i] = new Color32(ToGamma(Rgb[i * 3] / a), ToGamma(Rgb[i * 3 + 1] / a), ToGamma(Rgb[i * 3 + 2] / a), ToByte(a));
            }
            return pixels;
        }

        public void WritePng(string path)
        {
            string directory = System.IO.Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                System.IO.Directory.CreateDirectory(directory);
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            try
            {
                texture.SetPixels32(ToPixels());
                texture.Apply(false, false);
                System.IO.File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        /// <summary>
        /// The review contact sheet: the game images in order, <paramref name="columns"/> per row, on a soft grey so the
        /// white borders show, written as one PNG. Missing files leave an empty cell.
        /// </summary>
        public static int WriteContactSheet(IReadOnlyList<string> images, int cell, int columns, string path)
        {
            const int Gap = 16;
            int rows = Mathf.Max(1, (images.Count + columns - 1) / columns);
            int width = columns * cell + (columns + 1) * Gap, height = rows * cell + (rows + 1) * Gap;
            var sheet = new Color32[width * height];
            var background = new Color32(216, 222, 227, 255);
            for (int i = 0; i < sheet.Length; i++)
                sheet[i] = background;
            int drawn = 0;
            var reader = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                for (int n = 0; n < images.Count; n++)
                {
                    if (string.IsNullOrEmpty(images[n]) || !System.IO.File.Exists(images[n]))
                        continue;
                    if (!reader.LoadImage(System.IO.File.ReadAllBytes(images[n])) || reader.width != cell || reader.height != cell)
                        continue;
                    var pixels = reader.GetPixels32();
                    int column = n % columns, row = n / columns;
                    int ox = Gap + column * (cell + Gap);
                    int oy = height - (row + 1) * (cell + Gap);
                    for (int y = 0; y < cell; y++)
                        for (int x = 0; x < cell; x++)
                        {
                            var p = pixels[y * cell + x];
                            int o = (oy + y) * width + ox + x;
                            float a = p.a / 255f;
                            var b = sheet[o];
                            sheet[o] = new Color32((byte)(p.r * a + b.r * (1f - a)), (byte)(p.g * a + b.g * (1f - a)), (byte)(p.b * a + b.b * (1f - a)), 255);
                        }
                    drawn++;
                }
                var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                try
                {
                    texture.SetPixels32(sheet);
                    texture.Apply(false, false);
                    System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
                    System.IO.File.WriteAllBytes(path, texture.EncodeToPNG());
                }
                finally
                {
                    Object.DestroyImmediate(texture);
                }
            }
            finally
            {
                Object.DestroyImmediate(reader);
            }
            return drawn;
        }

        private static float Linear(byte value) => Mathf.GammaToLinearSpace(value / 255f);

        private static byte ToByte(float value) => (byte)(Mathf.Clamp01(value) * 255f + 0.5f);

        private static byte ToGamma(float linear) => ToByte(Mathf.LinearToGammaSpace(Mathf.Clamp01(linear)));
    }
}
