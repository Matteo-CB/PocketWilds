using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using PocketWilds.Editor.Vfx;

namespace PocketWilds.Tests.EditMode
{
    /// <summary>
    /// Owner rule of 2026-10-06: no sparkle (✨, the four-point twinkle star) anywhere in the project: no character
    /// in texts, code or docs, no four-point star in the sprite sources, no four-point star tile in the VFX atlas.
    /// Rounded five-point stars, round glints, bubbles, hearts and paws are the project's shapes. See
    /// Docs/Design/No_Sparkle_Rule.md.
    /// </summary>
    public class NoSparkleRuleTests
    {
        private static readonly string Root = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, ".."));

        /// <summary>✨ ✦ ✧ ❇ ❈ ✴ ✵ ✶ and the emoji 🌟 💫.</summary>
        private static readonly string[] Characters = { "✨", "✦", "✧", "❇", "❈", "✴", "✵", "✶", "\U0001F31F", "\U0001F4AB" };

        private const string Self = "Assets/_Game/Tests/EditMode/NoSparkleRuleTests.cs";
        private const string RuleDoc = "Docs/Design/No_Sparkle_Rule.md";

        private static IEnumerable<string> Files(string folder, params string[] extensions)
        {
            string full = Path.Combine(Root, folder);
            if (!Directory.Exists(full))
                yield break;
            foreach (var path in Directory.EnumerateFiles(full, "*", SearchOption.AllDirectories))
            {
                if (!extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
                    continue;
                string relative = path.Substring(Root.Length + 1).Replace('\\', '/');
                if (relative == Self || relative == RuleDoc)
                    continue;
                yield return path;
            }
        }

        [Test]
        public void TextsCodeAndDocs_HaveNoSparkleCharacter()
        {
            var problems = new List<string>();
            var files = Files("Assets/_Game", ".cs", ".asset", ".shader", ".hlsl", ".json")
                .Concat(Files("SourceArt", ".py", ".txt"))
                .Concat(Files("Docs", ".md", ".csv", ".json"))
                .Concat(Files("Tools", ".py"));
            foreach (var path in files)
            {
                if (new FileInfo(path).Length > 4_000_000)
                    continue;
                string text = File.ReadAllText(path);
                foreach (var c in Characters)
                    if (text.Contains(c))
                        problems.Add(path.Substring(Root.Length + 1) + ": " + c);
            }
            Assert.IsEmpty(problems, "No sparkle (owner rule 2026-10-06):\n" + string.Join("\n", problems));
        }

        [Test]
        public void SpriteSources_DrawNoFourPointStar()
        {
            var fourPoint = new Regex(@"star_points\([^)]*count\s*=\s*4|(?<!def )superellipse_points\(", RegexOptions.Compiled);
            var problems = new List<string>();
            foreach (var path in Files("SourceArt", ".py"))
            {
                var lines = File.ReadAllLines(path);
                for (int i = 0; i < lines.Length; i++)
                    if (fourPoint.IsMatch(lines[i]))
                        problems.Add($"{path.Substring(Root.Length + 1)}:{i + 1}");
            }
            Assert.IsEmpty(problems, "No four-point sparkle star in the sprite sources:\n" + string.Join("\n", problems));
        }

        /// <summary>
        /// A four-point star is bright along both axes far from its center and dark on the diagonals at the same
        /// radius. No VFX atlas tile may have that signature.
        /// </summary>
        [Test]
        public void VfxAtlas_HasNoFourPointStarTile()
        {
            for (int tile = 0; tile < VfxPipeline.TileCount; tile++)
            {
                float axes = 0f, diagonals = 0f;
                for (int k = 0; k < 4; k++)
                {
                    float a = k * Math_HalfPi;
                    float d = a + Math_HalfPi * 0.5f;
                    for (float r = 0.45f; r <= 0.85f; r += 0.1f)
                    {
                        axes += VfxPipeline.TileValue(tile, UnityEngine.Mathf.Cos(a) * r, UnityEngine.Mathf.Sin(a) * r);
                        diagonals += VfxPipeline.TileValue(tile, UnityEngine.Mathf.Cos(d) * r, UnityEngine.Mathf.Sin(d) * r);
                    }
                }
                bool sparkle = axes > 0.4f && axes > diagonals * 3f;
                Assert.IsFalse(sparkle, $"VFX atlas tile {tile} looks like a four-point star (axes {axes:F2}, diagonals {diagonals:F2}).");
            }
        }

        private const float Math_HalfPi = UnityEngine.Mathf.PI * 0.5f;
    }
}
