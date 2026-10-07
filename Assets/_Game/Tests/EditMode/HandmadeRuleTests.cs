using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace PocketWilds.Tests.EditMode
{
    /// <summary>
    /// Owner brief of 7 October 2026 (charte « fait main », section 12.3; Docs/Design/Handmade_Rule.md): every final
    /// asset has an editable source of ours and nothing in the chain comes from a generator. Models were exported by
    /// Blender from a .blend in SourceArt/Blender, sounds have their master and recipe, sprites their master and
    /// drawing script, fonts their build; no file name, model header, image or sound metadata carries the trace of
    /// an image, 3D or sound generator or of a model library. Sources: SourceArt/Handmade_Sources.json.
    /// </summary>
    public class HandmadeRuleTests
    {
        private const string Self = "Assets/_Game/Tests/EditMode/HandmadeRuleTests.cs";
        private const string RuleDoc = "Docs/Design/Handmade_Rule.md";
        private const string SourcesPath = "SourceArt/Handmade_Sources.json";
        private const string Art = "Assets/_Game/Art/";

        private static readonly string[] ModelExtensions = { ".fbx", ".obj", ".glb", ".gltf", ".dae", ".3ds", ".blend", ".usd", ".usdz", ".ply", ".stl" };
        private static readonly string[] ImageExtensions = { ".png", ".jpg", ".jpeg", ".tga", ".psd", ".exr", ".tif", ".tiff", ".bmp", ".gif", ".webp", ".hdr" };
        private static readonly string[] AudioExtensions = { ".wav", ".ogg", ".mp3", ".aif", ".aiff", ".flac", ".m4a" };
        private static readonly string[] FontExtensions = { ".ttf", ".otf" };

        /// <summary>
        /// Image, 3D and sound generators and model libraries (charte, sections 12.1 and 12.3). Lower case, matched
        /// as substrings of lower-cased names and metadata. Short or common words are written in a form that only
        /// the tool uses ("lumalabs", not "luma"; "adobe firefly", not the insect).
        /// </summary>
        private static readonly string[] Signatures =
        {
            "hyper3d", "rodin", "hunyuan", "meshy", "tripo", "lumalabs", "luma ai", "lumaai", "kaedim", "sloyd", "csm.ai",
            "stable diffusion", "stable-diffusion", "stablediffusion", "sdxl", "midjourney", "dall-e", "dall·e", "dalle",
            "comfyui", "automatic1111", "invokeai", "novelai", "adobe firefly", "leonardo.ai", "leonardo ai", "ideogram",
            "krea.ai", "scenario.gg", "elevenlabs", "suno.ai", "suno ai", "udio.com", "polyhaven", "poly haven",
            "sketchfab", "poly.pizza", "poly pizza",
        };

        /// <summary>PNG text keys written by image generators (Stable Diffusion web UIs, ComfyUI, InvokeAI).</summary>
        private static readonly string[] GeneratorPngKeys = { "parameters", "prompt", "workflow", "negative_prompt", "sd-metadata", "invokeai_metadata", "dream" };

        /// <summary>Generative tools of the MCP servers (charte, section 12.1). Never called by the project's code.</summary>
        private static readonly string[] GenerativeTools =
        {
            "generate_3d", "generate_3d_model_from_text", "generate_3d_model_from_image", "generate_3d_model_texture",
            "auto_rig_3d_model", "generate_or_edit_images", "generate_sfx", "generate_music", "generate_tts",
            "generate_image", "import_asset", "search_assets",
        };

        [Serializable]
        private class BlendSource
        {
            public string blend;
            public string[] builders;
        }

        [Serializable]
        private class GeneratedSource
        {
            public string final;
            public string from;
            public string[] builders;
        }

        [Serializable]
        private class LargeTexture
        {
            public string final;
            public string why;
        }

        [Serializable]
        private class Sources
        {
            public BlendSource[] blends;
            public GeneratedSource[] generated;
            public LargeTexture[] largeTextures;
        }

        private static string Root => Directory.GetParent(Application.dataPath).FullName;

        private static string Relative(string path) => path.Substring(Root.Length + 1).Replace('\\', '/');

        private static bool Exists(string relative) => File.Exists(Path.Combine(Root, relative));

        private static bool Covers(string final, string path) => final.EndsWith("/") ? path.StartsWith(final, StringComparison.Ordinal) : path == final;

        private static Sources LoadSources()
        {
            string path = Path.Combine(Root, SourcesPath);
            Assert.IsTrue(File.Exists(path), SourcesPath + " is missing.");
            var sources = JsonUtility.FromJson<Sources>(File.ReadAllText(path));
            Assert.IsNotNull(sources?.blends, SourcesPath + ": no blends.");
            Assert.IsNotNull(sources.generated, SourcesPath + ": no generated entries.");
            sources.largeTextures ??= new LargeTexture[0];
            return sources;
        }

        private static IEnumerable<string> Files(string folder, string[] extensions = null)
        {
            string full = Path.Combine(Root, folder);
            if (!Directory.Exists(full))
                yield break;
            foreach (var path in Directory.EnumerateFiles(full, "*", SearchOption.AllDirectories))
            {
                if (extensions != null && !extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
                    continue;
                yield return Relative(path);
            }
        }

        /// <summary>A builder is one of the project's own tools: a script under SourceArt/*/Tools or an editor pipeline.</summary>
        private static string CheckBuilder(string owner, string builder)
        {
            if (string.IsNullOrEmpty(builder))
                return owner + ": empty builder";
            bool own = Regex.IsMatch(builder, @"^SourceArt/[^/]+/Tools/[^/]+\.py$") || builder.StartsWith("Assets/_Game/Editor/", StringComparison.Ordinal);
            if (!own)
                return $"{owner}: builder {builder} is not one of the project's tools (SourceArt/*/Tools or Assets/_Game/Editor)";
            return Exists(builder) ? null : $"{owner}: builder {builder} is missing";
        }

        // ------------------------------------------------------------------------------------------- models

        /// <summary>The .blend recorded in an FBX header by Blender's exporter, relative to the project.</summary>
        private static string RecordedBlend(byte[] header)
        {
            string text = Encoding.ASCII.GetString(header);
            int end = text.IndexOf(".blend", StringComparison.Ordinal);
            if (end < 0)
                return null;
            int start = Math.Max(text.LastIndexOf("SourceArt\\", end, StringComparison.Ordinal), text.LastIndexOf("SourceArt/", end, StringComparison.Ordinal));
            return start < 0 ? null : text.Substring(start, end + 6 - start).Replace('\\', '/');
        }

        private static byte[] Head(string relative, int bytes)
        {
            using var stream = File.OpenRead(Path.Combine(Root, relative));
            var buffer = new byte[Math.Min(bytes, (int)stream.Length)];
            int read = 0;
            while (read < buffer.Length)
            {
                int n = stream.Read(buffer, read, buffer.Length - read);
                if (n <= 0)
                    break;
                read += n;
            }
            return buffer;
        }

        [Test]
        public void EveryModel_WasExportedByBlender_FromABlendInSourceArt()
        {
            var sources = LoadSources();
            var listed = new HashSet<string>(sources.blends.Select(b => b.blend), StringComparer.Ordinal);
            var problems = new List<string>();
            int models = 0;
            foreach (var path in Files("Assets", ModelExtensions))
            {
                models++;
                if (!path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                {
                    problems.Add(path + ": final models are FBX files exported by SourceArt/Blender/Tools/pw_export.py");
                    continue;
                }
                var header = Head(path, 64 * 1024);
                if (Encoding.ASCII.GetString(header).IndexOf("Blender (stable FBX IO)", StringComparison.Ordinal) < 0)
                    problems.Add(path + ": not written by Blender's FBX exporter");
                string blend = RecordedBlend(header);
                if (blend == null)
                    problems.Add(path + ": the header names no .blend under SourceArt");
                else if (!blend.StartsWith("SourceArt/Blender/", StringComparison.Ordinal) || !Exists(blend))
                    problems.Add($"{path}: source {blend} is missing");
                else if (!listed.Contains(blend))
                    problems.Add($"{path}: source {blend} is not listed in {SourcesPath} with its build scripts");
            }
            Assert.Greater(models, 0, "No model found: the scan is broken.");
            Assert.IsEmpty(problems, "Every model needs its editable source (charte « fait main », 12.3):\n" + string.Join("\n", problems));
        }

        [Test]
        public void EveryBlend_IsListedWithItsBuildScripts()
        {
            var sources = LoadSources();
            var problems = new List<string>();
            foreach (var entry in sources.blends)
            {
                if (string.IsNullOrEmpty(entry.blend) || !entry.blend.StartsWith("SourceArt/Blender/", StringComparison.Ordinal) || !Exists(entry.blend))
                    problems.Add($"{entry.blend}: missing or outside SourceArt/Blender");
                if (entry.builders == null || entry.builders.Length == 0)
                    problems.Add($"{entry.blend}: no build script");
                else
                    problems.AddRange(entry.builders.Select(b => CheckBuilder(entry.blend, b)).Where(p => p != null));
            }
            var listed = new HashSet<string>(sources.blends.Select(b => b.blend), StringComparer.Ordinal);
            foreach (var blend in Files("SourceArt", new[] { ".blend" }))
                if (!listed.Contains(blend))
                    problems.Add($"{blend}: not listed in {SourcesPath}");
            Assert.IsEmpty(problems, "Blender sources:\n" + string.Join("\n", problems));
        }

        // ------------------------------------------------------------------------------------------- sounds

        private static string ToolsText(string folder) =>
            string.Join("\n", Files(folder, new[] { ".py" }).Select(p => File.ReadAllText(Path.Combine(Root, p))));

        [Test]
        public void EverySound_HasItsMasterAndItsRecipe()
        {
            string recipes = ToolsText("SourceArt/Audio/Tools");
            var variant = new Regex(@"_\d{2}$");
            var problems = new List<string>();
            int sounds = 0;
            foreach (var path in Files("Assets", AudioExtensions))
            {
                sounds++;
                var match = Regex.Match(path, @"^Assets/_Game/Art/Audio/([^/]+)/([^/]+)$");
                if (!match.Success)
                {
                    problems.Add(path + ": sounds live in Assets/_Game/Art/Audio/<category>/");
                    continue;
                }
                string master = $"SourceArt/Audio/Masters/{match.Groups[1].Value}/{match.Groups[2].Value}";
                if (!Exists(master))
                    problems.Add($"{path}: master {master} is missing");
                string recipe = variant.Replace(Path.GetFileNameWithoutExtension(path), "");
                if (recipes.IndexOf(recipe, StringComparison.Ordinal) < 0)
                    problems.Add($"{path}: no recipe named {recipe} in SourceArt/Audio/Tools");
            }
            Assert.Greater(sounds, 0, "No sound found: the scan is broken.");
            Assert.IsEmpty(problems, "Every sound is synthesized by the project (charte « fait main », 12.1):\n" + string.Join("\n", problems));
        }

        // ------------------------------------------------------------------------------------------- images

        /// <summary>
        /// A drawing script names a sprite whole ("ICO_Paw") or as a prefix and a part ("ICO_Rarity_" with "Common",
        /// "ICO_GemPack_%02d").
        /// </summary>
        private static bool Draws(string scripts, string name)
        {
            if (scripts.IndexOf(name, StringComparison.Ordinal) >= 0)
                return true;
            for (int i = name.IndexOf('_'); i > 0 && i < name.Length - 1; i = name.IndexOf('_', i + 1))
            {
                string prefix = name.Substring(0, i + 1), part = name.Substring(i + 1);
                if (scripts.IndexOf(prefix, StringComparison.Ordinal) < 0)
                    continue;
                if (scripts.Contains("\"" + part + "\"") || scripts.Contains("'" + part + "'"))
                    return true;
                if (part.All(char.IsDigit) && scripts.Contains(prefix + "%"))
                    return true;
            }
            return false;
        }

        [Test]
        public void EveryImage_IsDrawnOrRenderedByTheProject()
        {
            var sources = LoadSources();
            string drawings = ToolsText("SourceArt/UI/Tools");
            var problems = new List<string>();
            foreach (var entry in sources.generated)
            {
                if (entry.builders == null || entry.builders.Length == 0)
                    problems.Add($"{entry.final}: no builder");
                else
                    problems.AddRange(entry.builders.Select(b => CheckBuilder(entry.final, b)).Where(p => p != null));
            }
            int images = 0;
            foreach (var path in Files("Assets", ImageExtensions))
            {
                images++;
                if (sources.generated.Any(g => Covers(g.final, path)))
                    continue;
                string name = Path.GetFileNameWithoutExtension(path);
                if (!path.StartsWith(Art + "UI/", StringComparison.Ordinal) || !path.EndsWith(".png", StringComparison.Ordinal))
                {
                    problems.Add($"{path}: not a UI sprite and not listed under generated in {SourcesPath}");
                    continue;
                }
                if (!Exists($"SourceArt/UI/Masters/{name}@4x.png"))
                    problems.Add($"{path}: master SourceArt/UI/Masters/{name}@4x.png is missing");
                if (!Draws(drawings, name))
                    problems.Add($"{path}: no script in SourceArt/UI/Tools draws {name}");
            }
            Assert.Greater(images, 0, "No image found: the scan is broken.");
            Assert.IsEmpty(problems, "Every image is drawn by our scripts or rendered from our models (charte « fait main », 12.1):\n" + string.Join("\n", problems));
        }

        [Test]
        public void EveryOtherFinalMesh_HasItsBuilder()
        {
            var sources = LoadSources();
            var problems = new List<string>();
            foreach (var path in Files(Art.TrimEnd('/'), new[] { ".asset", ".mesh" }))
            {
                var head = Encoding.UTF8.GetString(Head(path, 512));
                if (!Regex.IsMatch(head, @"^Mesh:", RegexOptions.Multiline))
                    continue;
                if (!sources.generated.Any(g => Covers(g.final, path)))
                    problems.Add($"{path}: a mesh asset with no builder listed under generated in {SourcesPath}");
            }
            Assert.IsEmpty(problems, "Baked meshes:\n" + string.Join("\n", problems));
        }

        /// <summary>PNG size from the IHDR chunk; other formats never pass the image test.</summary>
        private static (int Width, int Height) PngSize(string relative)
        {
            var head = Head(relative, 24);
            if (head.Length < 24)
                return (0, 0);
            int Read(int at) => (head[at] << 24) | (head[at + 1] << 16) | (head[at + 2] << 8) | head[at + 3];
            return (Read(16), Read(20));
        }

        [Test]
        public void TexturesOver512_AreListedWithTheirReason()
        {
            var sources = LoadSources();
            var problems = new List<string>();
            foreach (var path in Files("Assets", new[] { ".png" }))
            {
                var (width, height) = PngSize(path);
                if (Math.Max(width, height) <= 512)
                    continue;
                var listed = sources.largeTextures.FirstOrDefault(t => Covers(t.final, path));
                if (listed == null || string.IsNullOrWhiteSpace(listed.why))
                    problems.Add($"{path}: {width} x {height}, more than 512 px without a reason in {SourcesPath} (largeTextures)");
            }
            Assert.IsEmpty(problems, "Large textures:\n" + string.Join("\n", problems));
        }

        // ------------------------------------------------------------------------------------------- fonts

        [Test]
        public void EveryFont_IsTheProjectsOwnBuild()
        {
            var problems = new List<string>();
            if (!Exists("SourceArt/Fonts/Tools/pw_font_build.py"))
                problems.Add("SourceArt/Fonts/Tools/pw_font_build.py is missing");
            int fonts = 0;
            foreach (var path in Files("Assets", FontExtensions))
            {
                fonts++;
                string build = "SourceArt/Fonts/Build/" + Path.GetFileName(path);
                if (!path.StartsWith(Art + "UI/Fonts/", StringComparison.Ordinal))
                    problems.Add(path + ": fonts live in Assets/_Game/Art/UI/Fonts (Pocket Round and Pocket Plush only)");
                else if (!Exists(build))
                    problems.Add($"{path}: build {build} is missing");
                else if (!File.ReadAllBytes(Path.Combine(Root, path)).SequenceEqual(File.ReadAllBytes(Path.Combine(Root, build))))
                    problems.Add($"{path}: differs from {build}; rebuild with pw_font_build.py");
            }
            Assert.Greater(fonts, 0, "No font found: the scan is broken.");
            Assert.IsEmpty(problems, "Fonts:\n" + string.Join("\n", problems));
        }

        // ------------------------------------------------------------------------------------------- traces

        private static string Signature(string text)
        {
            string lower = text.ToLowerInvariant();
            return Signatures.FirstOrDefault(s => lower.Contains(s));
        }

        private static string Printable(byte[] bytes)
        {
            var text = new StringBuilder(bytes.Length);
            foreach (var b in bytes)
                text.Append(b >= 0x20 && b < 0x7F ? (char)b : ' ');
            return text.ToString();
        }

        /// <summary>
        /// The string properties of a binary FBX: type 'S', a 32-bit length, then the text (names keep their
        /// "\0\x01" class separator). Header, scene info, object and material names all live there.
        /// </summary>
        private static string FbxStrings(byte[] fbx)
        {
            var text = new StringBuilder();
            for (int i = 0; i + 5 <= fbx.Length; i++)
            {
                if (fbx[i] != (byte)'S')
                    continue;
                int length = BitConverter.ToInt32(fbx, i + 1);
                if (length < 1 || length > 4096 || i + 5 + length > fbx.Length)
                    continue;
                bool readable = true;
                for (int k = i + 5; k < i + 5 + length && readable; k++)
                    readable = (fbx[k] >= 0x20 && fbx[k] < 0x7F) || fbx[k] == 0 || fbx[k] == 1;
                if (!readable)
                    continue;
                text.Append(Encoding.ASCII.GetString(fbx, i + 5, length).Replace('\0', ' ').Replace('\x01', ' ')).Append('\n');
                i += 4 + length;
            }
            return text.ToString();
        }

        /// <summary>The strings of a TrueType or OpenType font's name table (family, designer, vendor, license...).</summary>
        private static string FontNames(byte[] font)
        {
            int U16(int at) => (font[at] << 8) | font[at + 1];
            int U32(int at) => (font[at] << 24) | (font[at + 1] << 16) | (font[at + 2] << 8) | font[at + 3];
            var text = new StringBuilder();
            if (font.Length < 12)
                return "";
            int tables = U16(4);
            for (int t = 0; t < tables && 12 + 16 * t + 16 <= font.Length; t++)
            {
                int record = 12 + 16 * t;
                if (Encoding.ASCII.GetString(font, record, 4) != "name")
                    continue;
                int table = U32(record + 8);
                int count = U16(table + 2), strings = table + U16(table + 4);
                for (int n = 0; n < count; n++)
                {
                    int at = table + 6 + 12 * n;
                    int platform = U16(at), length = U16(at + 8), offset = strings + U16(at + 10);
                    if (offset + length > font.Length)
                        continue;
                    text.Append(platform == 1 ? Encoding.ASCII.GetString(font, offset, length) : Encoding.BigEndianUnicode.GetString(font, offset, length)).Append('\n');
                }
            }
            return text.ToString();
        }

        /// <summary>Text chunks of a PNG (tEXt, zTXt, iTXt, eXIf) as key and value.</summary>
        private static IEnumerable<(string Key, string Value)> PngText(byte[] png)
        {
            for (int i = 8; i + 8 <= png.Length;)
            {
                int length = (png[i] << 24) | (png[i + 1] << 16) | (png[i + 2] << 8) | png[i + 3];
                string type = Encoding.ASCII.GetString(png, i + 4, 4);
                if (length < 0 || i + 12 + length > png.Length)
                    yield break;
                if (type == "tEXt" || type == "zTXt" || type == "iTXt" || type == "eXIf")
                {
                    var data = new byte[length];
                    Array.Copy(png, i + 8, data, 0, length);
                    int zero = Array.IndexOf(data, (byte)0);
                    string key = zero > 0 ? Encoding.ASCII.GetString(data, 0, zero) : "";
                    string value = Printable(data);
                    if (type == "zTXt" && zero > 0 && zero + 4 < data.Length)
                        value = Inflate(data, zero + 2);
                    yield return (key, value);
                }
                if (type == "IEND")
                    yield break;
                i += 12 + length;
            }
        }

        private static string Inflate(byte[] data, int zlibStart)
        {
            try
            {
                using var input = new MemoryStream(data, zlibStart + 2, data.Length - zlibStart - 2);
                using var inflate = new DeflateStream(input, CompressionMode.Decompress);
                using var output = new MemoryStream();
                inflate.CopyTo(output);
                return Printable(output.ToArray());
            }
            catch (InvalidDataException)
            {
                return Printable(data);
            }
        }

        /// <summary>Every RIFF chunk of a WAV except the samples (LIST/INFO, bext, iXML...).</summary>
        private static string WavText(byte[] wav)
        {
            var text = new StringBuilder();
            for (int i = 12; i + 8 <= wav.Length;)
            {
                string id = Encoding.ASCII.GetString(wav, i, 4);
                int length = BitConverter.ToInt32(wav, i + 4);
                if (length < 0 || i + 8 + length > wav.Length)
                    break;
                if (id != "data")
                    text.Append(id).Append(' ').Append(Printable(wav.Skip(i + 8).Take(length).ToArray())).Append('\n');
                i += 8 + length + (length & 1);
            }
            return text.ToString();
        }

        [Test]
        public void NoGeneratorTrace_InNamesOrMetadata()
        {
            var problems = new List<string>();
            foreach (var folder in new[] { "Assets", "SourceArt", "Docs", "Tools" })
            {
                string full = Path.Combine(Root, folder);
                if (!Directory.Exists(full))
                    continue;
                foreach (var entry in Directory.EnumerateFileSystemEntries(full, "*", SearchOption.AllDirectories))
                {
                    string relative = Relative(entry);
                    if (relative == Self || relative == RuleDoc)
                        continue;
                    string hit = Signature(Path.GetFileName(entry));
                    if (hit != null)
                        problems.Add($"{relative}: name contains \"{hit}\"");
                }
            }

            var media = ModelExtensions.Concat(ImageExtensions).Concat(AudioExtensions).Concat(FontExtensions).Where(e => e != ".blend").ToArray();
            foreach (var path in Files("Assets", media).Concat(Files("SourceArt", media)))
            {
                string extension = Path.GetExtension(path).ToLowerInvariant();
                string text;
                if (extension == ".png")
                {
                    var png = File.ReadAllBytes(Path.Combine(Root, path));
                    var chunks = PngText(png).ToList();
                    foreach (var (key, _) in chunks)
                        if (GeneratorPngKeys.Contains(key.ToLowerInvariant()))
                            problems.Add($"{path}: PNG text key \"{key}\" (written by image generators)");
                    text = string.Join("\n", chunks.Select(c => c.Key + " " + c.Value));
                }
                else if (extension == ".wav")
                {
                    text = WavText(File.ReadAllBytes(Path.Combine(Root, path)));
                }
                else if (extension == ".ttf" || extension == ".otf")
                {
                    text = FontNames(File.ReadAllBytes(Path.Combine(Root, path)));
                }
                else if (extension == ".fbx")
                {
                    text = FbxStrings(File.ReadAllBytes(Path.Combine(Root, path)));
                }
                else
                {
                    text = Printable(Head(path, 64 * 1024));
                }
                string meta = Path.Combine(Root, path + ".meta");
                if (File.Exists(meta))
                    text += "\n" + File.ReadAllText(meta);
                string hit = Signature(text);
                if (hit != null)
                    problems.Add($"{path}: metadata contains \"{hit}\"");
            }
            Assert.IsEmpty(problems, "No generator or model library in the chain (charte « fait main », 12.1):\n" + string.Join("\n", problems));
        }

        [Test]
        public void ProjectCode_NeverCallsAGenerativeTool()
        {
            var call = new Regex(@"\b(" + string.Join("|", GenerativeTools.Select(Regex.Escape)) + @")\b", RegexOptions.Compiled);
            var problems = new List<string>();
            var code = Files("Assets/_Game", new[] { ".cs" })
                .Concat(Files("SourceArt", new[] { ".py" }))
                .Concat(Files("Tools", new[] { ".py" }));
            foreach (var path in code)
            {
                if (path == Self)
                    continue;
                var lines = File.ReadAllLines(Path.Combine(Root, path));
                for (int i = 0; i < lines.Length; i++)
                {
                    var match = call.Match(lines[i]);
                    if (match.Success)
                        problems.Add($"{path}:{i + 1}: {match.Value}");
                }
            }
            Assert.IsEmpty(problems, "Generative tools are never part of the chain (charte « fait main », 12.1):\n" + string.Join("\n", problems));
        }
    }
}
