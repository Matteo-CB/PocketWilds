using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace PocketWilds.Tests.EditMode
{
    /// <summary>
    /// Owner rule of 6 October 2026: nothing religious and no divine vocabulary anywhere in the project
    /// (Docs/Design/Vocabulary_Rule.md). Scans asset paths, localized text, content, prefabs and scenes, code,
    /// Blender tools and repository docs. Words are matched as whole tokens (camelCase and snake_case split).
    /// </summary>
    public class VocabularyRuleTests
    {
        // The rule document and this test are the only places allowed to list the words.
        private static readonly string[] Allowed = { "Docs/Design/Vocabulary_Rule.md", "Assets/_Game/Tests/EditMode/VocabularyRuleTests.cs" };

        private static readonly HashSet<string> French = new HashSet<string>(StringComparer.Ordinal)
        {
            "bible", "bibles", "sanctuaire", "sanctuaires", "sacré", "sacrée", "sacrés", "sacrées", "divin", "divine", "divins",
            "divines", "divinité", "divinités", "céleste", "célestes", "saint", "sainte", "saints", "saintes", "ange", "anges",
            "dieu", "dieux", "déesse", "déesses", "paradis", "miracle", "miracles", "bénédiction", "bénédictions", "béni", "bénie",
            "prière", "prières", "temple", "temples", "autel", "autels", "oracle", "oracles", "moine", "moines", "prêtre",
            "prêtres", "église", "églises", "chapelle", "chapelles", "relique", "reliques", "pèlerin", "pèlerins", "totem",
            "totems", "rituel", "rituels", "astral", "astrale", "astraux", "mythique", "mythiques", "karma", "zen", "nirvana",
            "mantra", "mantras", "résurrection", "réincarnation", "démon", "démons", "enfer", "enfers",
        };

        private static readonly HashSet<string> English = new HashSet<string>(StringComparer.Ordinal)
        {
            "bible", "bibles", "sanctuary", "sanctuaries", "sacred", "holy", "divine", "deity", "deities", "celestial", "heaven",
            "heavens", "heavenly", "angel", "angels", "angelic", "god", "gods", "goddess", "goddesses", "paradise", "miracle",
            "miracles", "blessing", "blessings", "blessed", "prayer", "prayers", "pray", "temple", "temples", "shrine", "shrines",
            "altar", "altars", "oracle", "oracles", "monk", "monks", "priest", "priests", "church", "churches", "chapel",
            "chapels", "relic", "relics", "pilgrim", "pilgrims", "totem", "totems", "ritual", "rituals", "astral", "mythic",
            "mythical", "karma", "zen", "nirvana", "mantra", "mantras", "resurrection", "reincarnation", "demon", "demons",
            "hell", "halo", "halos", "lotus",
        };

        // "aura" is also the French verb "avoir" (il aura): checked in names, code and English text only.
        private static readonly HashSet<string> EnglishOnly = new HashSet<string>(StringComparer.Ordinal) { "aura", "auras" };

        private static readonly Regex EngineKeys = new Regex(@"^\s*m_\w+:", RegexOptions.Compiled | RegexOptions.Multiline);
        private static readonly Regex Word = new Regex(@"[^\W\d_]+", RegexOptions.Compiled);
        private static readonly Regex CamelSplit = new Regex(@"(?<=[a-zà-ÿ])(?=[A-ZÀ-Þ])|(?<=[A-ZÀ-Þ])(?=[A-ZÀ-Þ][a-zà-ÿ])", RegexOptions.Compiled);

        private static string Root => Directory.GetParent(Application.dataPath).FullName;

        private static string Relative(string path) => path.Substring(Root.Length + 1).Replace('\\', '/');

        private static IEnumerable<string> Tokens(string text)
        {
            foreach (Match m in Word.Matches(text))
            {
                yield return m.Value.ToLowerInvariant();
                foreach (var part in CamelSplit.Split(m.Value))
                {
                    if (part.Length != m.Value.Length)
                        yield return part.ToLowerInvariant();
                }
            }
        }

        private static List<string> Violations(string text, bool englishOnlyWords)
        {
            var found = new List<string>();
            foreach (var token in Tokens(text))
            {
                if (French.Contains(token) || English.Contains(token) || (englishOnlyWords && EnglishOnly.Contains(token)))
                    found.Add(token);
            }
            return found;
        }

        private static IEnumerable<string> Files(string folder, params string[] extensions)
        {
            string full = Path.Combine(Root, folder);
            if (!Directory.Exists(full))
                yield break;
            foreach (var path in Directory.EnumerateFiles(full, "*", SearchOption.AllDirectories))
            {
                if (!extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
                    continue;
                string relative = Relative(path);
                if (Allowed.Contains(relative) || relative.Contains("/Library/") || relative.Contains("/Temp/"))
                    continue;
                yield return path;
            }
        }

        private static void AssertClean(IEnumerable<string> files, Func<string, bool> englishOnlyWords, long maxBytes = long.MaxValue)
        {
            var problems = new List<string>();
            foreach (var path in files)
            {
                if (new FileInfo(path).Length > maxBytes)
                    continue;
                string text = File.ReadAllText(path);
                // Unity's own serialized field names (m_HaloStrength in RenderSettings...) are engine API, not ours.
                text = EngineKeys.Replace(text, string.Empty);
                var words = Violations(text, englishOnlyWords(path));
                if (words.Count > 0)
                    problems.Add($"{Relative(path)}: {string.Join(", ", words.Distinct())}");
            }
            Assert.IsEmpty(problems, "Vocabulary rule (Docs/Design/Vocabulary_Rule.md):\n" + string.Join("\n", problems));
        }

        [Test]
        public void TokenizerSplitsNamesAndKeepsWholeWords()
        {
            CollectionAssert.Contains(Violations("PF_CameraRig_Sanctuary", true), "sanctuary");
            CollectionAssert.Contains(Violations("RarityTier.Mythic", true), "mythic");
            CollectionAssert.Contains(Violations("SH_RareAura", true), "aura");
            CollectionAssert.IsEmpty(Violations("Il aura un nouvel œuf.", false), "The French verb avoir is not the English aura.");
            CollectionAssert.IsEmpty(Violations("shell, change, orange, frozen, dozen, monkey, Refuge, Fabulous", true));
        }

        [Test]
        public void AssetPaths_HaveNoReligiousWords()
        {
            var problems = new List<string>();
            foreach (var path in Directory.EnumerateFileSystemEntries(Path.Combine(Root, "Assets", "_Game"), "*", SearchOption.AllDirectories)
                         .Concat(Directory.EnumerateFileSystemEntries(Path.Combine(Root, "SourceArt"), "*", SearchOption.AllDirectories)))
            {
                string relative = Relative(path);
                if (Allowed.Contains(relative))
                    continue;
                var words = Violations(Path.GetFileNameWithoutExtension(relative), true);
                if (words.Count > 0)
                    problems.Add($"{relative}: {string.Join(", ", words.Distinct())}");
            }
            Assert.IsEmpty(problems, string.Join("\n", problems));
        }

        [Test]
        public void LocalizedTextContentPrefabsAndScenes_HaveNoReligiousWords()
        {
            // Mesh data (Baked) is skipped by size: hex digits cannot spell a word anyway.
            AssertClean(Files("Assets/_Game/Content", ".asset"), p => !Path.GetFileName(p).StartsWith("LOC_fr", StringComparison.Ordinal), 4_000_000);
            AssertClean(Files("Assets/_Game/Prefabs", ".prefab"), _ => true, 4_000_000);
            AssertClean(Files("Assets/_Game/Scenes", ".unity"), _ => true, 8_000_000);
        }

        [Test]
        public void CodeAndTools_HaveNoReligiousWords()
        {
            AssertClean(Files("Assets/_Game", ".cs", ".shader", ".hlsl", ".asmdef"), _ => true);
            AssertClean(Files("SourceArt", ".py"), _ => true);
            AssertClean(Files("Tools", ".py"), _ => true);
        }

        [Test]
        public void RepositoryDocsAndStatus_HaveNoReligiousWords()
        {
            AssertClean(Files("Docs", ".md", ".csv", ".json"), _ => false);
            // Mixed French and English notes: "aura" stays unchecked there (ids are covered by the asset path test).
            AssertClean(Files("Assets/_Game/Editor/Production", ".json"), _ => false);
        }
    }
}
