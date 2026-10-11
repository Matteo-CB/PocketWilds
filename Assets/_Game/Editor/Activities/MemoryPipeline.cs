using System;
using System.Collections.Generic;
using System.IO;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Editor.Animals;
using PocketWilds.Editor.UI;
using PocketWilds.Presentation;
using PocketWilds.UI;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace PocketWilds.Editor.Activities
{
    /// <summary>
    /// T41 « Memory des empreintes » screen pipeline (doc 01 Contenu S-ACT-004; doc « Mini-jeux &amp; activités », sections 1
    /// and 6): PF_UI_Memory (MemoryScreenView: a screen-space canvas at sorting order 16, under the activity HUD; the cream
    /// tablecloth; the safe area's grid under the HUD's top card with 20 pooled card views, each a shadow, the Prairie
    /// clover back, the cream face with its portrait slot, the pawprint and its soft stamp shadow, the matched mark, the « ×2 »
    /// badge and the gold edge; 8 round glints and 4 hearts), the coat-colour table (each variant's palette primary, from
    /// DEF_ContentDatabase), Atlas_Memory (agent MA's sprites, packed apart from every other atlas) and the Addressables
    /// group Activity_Memory (the screen and its atlas). No raycaster (the stage hit tests the cards itself). The portraits
    /// are never referenced: the screen borrows the codex thumbnails the scene already loaded (CommonContentTests: nothing
    /// duplicated), and nothing of the scene's UI kit, fonts or tokens is referenced either. Missing sprites are listed and
    /// left empty (the view falls back to plain colours), never invented. Idempotent; rerun when agent MA's sprites change.
    /// </summary>
    public static class MemoryPipeline
    {
        public const string PrefabPath = MemoryLayout.PrefabPath;
        public const string AtlasPath = "Assets/_Game/Art/UI/Atlases/Atlas_Memory.spriteatlasv2";
        public const string AtlasAddress = "activity.memory.atlas";

        // Agent MA's sprites (Art/UI/Activities/Memory).
        public const string CardBack = "UI_Memory_CardBack_Prairie";
        public const string CardFace = "UI_Memory_CardFace";
        public const string GlowEdge = "UI_Memory_GlowEdge";
        public const string DoubleBadge = "UI_Memory_DoubleBadge";
        public const string Tablecloth = "UI_Memory_Tablecloth";
        public const string PawPrefix = "UI_Paw_";

        /// <summary>Optional: the match's round glint and heart and the matched mark, when drawn (else the HUD lends a heart and a check).</summary>
        public const string Glint = "UI_Memory_Glint";

        public const string Heart = "UI_Memory_Heart";
        public const string Mark = "UI_Memory_Mark";

        // The screen's colours (doc 05 palette, the HUD's gold).
        public static readonly Color Gold = UiColorTokens.Hex(0xF2B544);
        public static readonly Color GlintColor = new Color(0.949f, 0.710f, 0.267f, 1f);
        public static readonly Color HeartColor = UiColorTokens.Hex(0xF28DA8);
        public static readonly Color MarkColor = UiColorTokens.Hex(0x86C27A);
        public static readonly Color Cream = UiColorTokens.Hex(0xFDF5E6);
        public static readonly Color ClothBase = UiColorTokens.Hex(0xFBF0DC);
        public static readonly Color BackFallback = UiColorTokens.Hex(0xCFE5B4);
        public static readonly Color Shadow = new Color(0.42f, 0.30f, 0.20f, 0.12f);

        [MenuItem("Pocket Wilds/Activities/Build Memory (T41)", priority = 243)]
        public static void BuildMenu() => Debug.Log("[MemoryPipeline] " + BuildAll());

        public static string BuildAll()
        {
            var report = new List<string>();
            var missing = new List<string>();
            var art = LoadArt(missing);
            report.Add(missing.Count > 0
                ? "WARNING: agent MA's sprites missing (left empty, plain colours shown): " + string.Join(", ", missing)
                : "agent MA's sprites: all found");
            var coats = BuildCoats(report);
            var atlas = EnsureAtlas(art, report);
            var view = BuildPrefab(art, coats, report);
            if (view != null)
                report.Add(EnsureAddressables(view.gameObject, atlas));
            AssetDatabase.SaveAssets();
            return string.Join("; ", report);
        }

        // ---------------------------------------------------------------- sprites

        /// <summary>Agent MA's sprites by role (null when not drawn yet).</summary>
        public sealed class Art
        {
            public Sprite Back, Face, Glow, Badge, Cloth, Glint, Heart, Mark;
            public readonly List<(string species, Sprite paw)> Paws = new List<(string, Sprite)>();

            public IEnumerable<Sprite> All()
            {
                foreach (var sprite in new[] { Back, Face, Glow, Badge, Cloth, Glint, Heart, Mark })
                    if (sprite != null)
                        yield return sprite;
                foreach (var (_, paw) in Paws)
                    if (paw != null)
                        yield return paw;
            }
        }

        public static string SpritePath(string name) => MemoryLayout.SpriteFolder + "/" + name + ".png";

        public static Art LoadArt(List<string> missing)
        {
            Sprite Load(string name, bool required)
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath(name));
                if (sprite == null && required)
                    missing.Add(name);
                return sprite;
            }

            var art = new Art
            {
                Back = Load(CardBack, true), Face = Load(CardFace, true), Glow = Load(GlowEdge, true), Badge = Load(DoubleBadge, true),
                Cloth = Load(Tablecloth, true), Glint = Load(Glint, false), Heart = Load(Heart, false), Mark = Load(Mark, false),
            };
            var database = AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>(AnimalPipeline.ContentDatabasePath);
            if (database != null)
            {
                foreach (var species in database.species)
                {
                    if (species == null || species.secret)
                        continue;
                    art.Paws.Add((species.sourceId, Load(PawName(species.sourceId), true)));
                }
            }
            return art;
        }

        /// <summary>« species.rabbit » → « UI_Paw_Rabbit ».</summary>
        public static string PawName(string speciesSourceId)
        {
            string name = speciesSourceId.Substring(speciesSourceId.LastIndexOf('.') + 1);
            var parts = name.Split('_');
            for (int i = 0; i < parts.Length; i++)
                if (parts[i].Length > 0)
                    parts[i] = char.ToUpperInvariant(parts[i][0]) + parts[i].Substring(1);
            return PawPrefix + string.Join("", parts);
        }

        // ---------------------------------------------------------------- coat colours

        /// <summary>Each variant's coat colour: its palette primary (the main coat), from the authored species definitions.</summary>
        public static List<(string source, uint id, Color coat)> BuildCoats(List<string> report)
        {
            var coats = new List<(string, uint, Color)>();
            var database = AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>(AnimalPipeline.ContentDatabasePath);
            if (database == null)
            {
                report.Add("WARNING: no content database: no coat table (the stage reads the palettes at run time)");
                return coats;
            }
            foreach (var species in database.species)
            {
                if (species == null)
                    continue;
                foreach (var variant in species.variants)
                {
                    if (variant == null || string.IsNullOrEmpty(variant.sourceId))
                        continue;
                    var runtime = species.ToRuntime(variant);
                    coats.Add((variant.sourceId, ContentId.FromSource(variant.sourceId).Value, MemoryLayout.Coat(runtime.Palette[0])));
                }
            }
            report.Add($"{coats.Count} coat colours");
            return coats;
        }

        // ---------------------------------------------------------------- atlas

        /// <summary>Atlas_Memory: agent MA's sprites, packed apart (Atlas_CommonHUD and Atlas_Activities never take them).</summary>
        private static SpriteAtlasAsset EnsureAtlas(Art art, List<string> report)
        {
            var textures = new List<Object>();
            foreach (var sprite in art.All())
            {
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GetAssetPath(sprite));
                if (texture != null && !textures.Contains(texture))
                    textures.Add(texture);
            }
            if (textures.Count == 0)
            {
                report.Add("no atlas (no sprite yet)");
                return null;
            }
            if (EditorSettings.spritePackerMode == SpritePackerMode.Disabled)
                EditorSettings.spritePackerMode = SpritePackerMode.SpriteAtlasV2;
            Directory.CreateDirectory(Path.GetDirectoryName(AtlasPath));
            var atlas = new SpriteAtlasAsset();
            atlas.Add(textures.ToArray());
            SpriteAtlasAsset.Save(atlas, AtlasPath);
            AssetDatabase.ImportAsset(AtlasPath, ImportAssetOptions.ForceUpdate);
            var importer = (SpriteAtlasImporter)AssetImporter.GetAtPath(AtlasPath);
            importer.includeInBuild = true;
            var packing = importer.packingSettings;
            packing.enableRotation = false;
            packing.enableTightPacking = false;
            packing.padding = 4;
            importer.packingSettings = packing;
            var textureSettings = importer.textureSettings;
            textureSettings.generateMipMaps = false;
            textureSettings.filterMode = FilterMode.Bilinear;
            textureSettings.sRGB = true;
            importer.textureSettings = textureSettings;
            foreach (var platform in new[] { "Android", "iPhone" })
            {
                var settings = importer.GetPlatformSettings(platform);
                settings.overridden = true;
                settings.maxTextureSize = 1024;
                settings.format = TextureImporterFormat.ASTC_4x4;
                importer.SetPlatformSettings(settings);
            }
            importer.SaveAndReimport();
            report.Add($"Atlas_Memory: {textures.Count} sprites");
            return atlas;
        }

        // ---------------------------------------------------------------- prefab

        public static MemoryScreenView BuildPrefab(Art art, List<(string source, uint id, Color coat)> coats, List<string> report)
        {
            var center = new Vector2(0.5f, 0.5f);
            var root = new GameObject(MemoryLayout.ScreenPrefabName, typeof(RectTransform));
            root.layer = 5;
            try
            {
                var canvas = root.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = MemoryLayout.SortingOrder;
                canvas.additionalShaderChannels = AdditionalCanvasShaderChannels.None;
                UiPipeline.AddScaler(root);
                // No GraphicRaycaster: the stage hit tests the cards' rects (raycast free), the HUD keeps its own touches.
                var view = root.AddComponent<MemoryScreenView>();
                var content = UiPipeline.Rect("Content", root.transform);
                UiPipeline.Stretch(content);

                // The cream tablecloth over the whole screen (the refuge does not render under it).
                var baseImage = UiPipeline.AddImage("Base", content, null, ClothBase, Image.Type.Simple);
                baseImage.preserveAspect = false;
                UiPipeline.Stretch(baseImage.rectTransform);
                var cloth = UiPipeline.AddImage("Cloth", content, art.Cloth, Color.white, Image.Type.Tiled);
                cloth.preserveAspect = false;
                UiPipeline.Stretch(cloth.rectTransform);
                cloth.enabled = art.Cloth != null;
                baseImage.enabled = art.Cloth == null;

                var safe = UiPipeline.Rect("SafeArea", content);
                UiPipeline.Stretch(safe);

                // The grid under the HUD's top card; its own canvas (the cards move, the cloth never rebuilds).
                var grid = UiPipeline.Rect("Grid", safe);
                grid.anchorMin = Vector2.zero;
                grid.anchorMax = Vector2.one;
                grid.pivot = center;
                grid.offsetMin = new Vector2(MemoryLayout.GridSide, MemoryLayout.GridBottom);
                grid.offsetMax = new Vector2(-MemoryLayout.GridSide, -MemoryLayout.GridTop);
                var gridCanvas = grid.gameObject.AddComponent<Canvas>();
                gridCanvas.additionalShaderChannels = AdditionalCanvasShaderChannels.None;

                var size = MemoryLayout.Fit(4, 3, 1000f, 1938f);
                var cards = new MemoryScreenView.CardParts[MemoryLayout.CardCapacity];
                for (int i = 0; i < cards.Length; i++)
                    cards[i] = BuildCard(grid, i, art, size);

                var effects = UiPipeline.Rect("Effects", grid);
                UiPipeline.Stretch(effects);
                var glints = new Image[MemoryLayout.GlintCapacity];
                for (int i = 0; i < glints.Length; i++)
                {
                    glints[i] = UiPipeline.AddImage("Glint" + i, effects, art.Glint, GlintColor, Image.Type.Simple);
                    UiPipeline.Place(glints[i].rectTransform, center, center, Vector2.zero, new Vector2(size.x * 0.55f, size.x * 0.55f));
                    glints[i].enabled = false;
                }
                var hearts = new Image[MemoryLayout.HeartCapacity];
                for (int i = 0; i < hearts.Length; i++)
                {
                    hearts[i] = UiPipeline.AddImage("Heart" + i, effects, art.Heart, HeartColor, Image.Type.Simple);
                    UiPipeline.Place(hearts[i].rectTransform, center, center, Vector2.zero, new Vector2(size.x * 0.36f, size.x * 0.36f));
                    hearts[i].enabled = false;
                }

                view.SetReferences(canvas, content.gameObject, safe, grid, effects, baseImage, cloth, cards, glints, hearts);
                var pawSpecies = new uint[art.Paws.Count];
                var paws = new Sprite[art.Paws.Count];
                for (int i = 0; i < art.Paws.Count; i++)
                {
                    pawSpecies[i] = ContentId.FromSource(art.Paws[i].species).Value;
                    paws[i] = art.Paws[i].paw;
                }
                view.SetArt(pawSpecies, paws, art.Glint, art.Heart, art.Mark);
                var sources = new string[coats.Count];
                var ids = new uint[coats.Count];
                var colors = new Color[coats.Count];
                for (int i = 0; i < coats.Count; i++)
                    (sources[i], ids[i], colors[i]) = coats[i];
                view.SetCoats(sources, ids, colors);
                // The gold edge drawn white takes the gold; drawn gold already, it keeps its own colours.
                var glowTint = art.Glow != null && IsColored(art.Glow) ? Color.white : Gold;
                view.SetColors(Gold, glowTint, GlintColor, HeartColor, MarkColor, Cream, BackFallback);
                content.gameObject.SetActive(false);
                canvas.enabled = false;

                Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
                UiPipeline.MaskScrollContent(root);
                var saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath).GetComponent<MemoryScreenView>();
                report.Add($"{MemoryLayout.ScreenPrefabName}: {cards.Length} cards, {glints.Length} glints, {hearts.Length} hearts, {paws.Length} pawprints"
                           + (saved.Valid ? "" : " (INCOMPLETE)"));
                return saved;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>The sprite is drawn in colour (its opaque pixels' mean saturation above 0.25), not white for tinting.</summary>
        public static bool IsColored(Sprite sprite)
        {
            string path = AssetDatabase.GetAssetPath(sprite);
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return false;
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!texture.LoadImage(File.ReadAllBytes(path)))
                    return false;
                var pixels = texture.GetPixels32();
                double saturation = 0.0;
                int count = 0;
                for (int i = 0; i < pixels.Length; i += 7)
                {
                    var p = pixels[i];
                    if (p.a < 128)
                        continue;
                    int max = Math.Max(p.r, Math.Max(p.g, p.b)), min = Math.Min(p.r, Math.Min(p.g, p.b));
                    saturation += max > 0 ? (max - min) / (double)max : 0.0;
                    count++;
                }
                return count > 0 && saturation / count > 0.25;
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        private static MemoryScreenView.CardParts BuildCard(RectTransform grid, int index, Art art, Vector2 size)
        {
            var center = new Vector2(0.5f, 0.5f);
            var parts = new MemoryScreenView.CardParts();
            var root = UiPipeline.Rect("Card" + index.ToString("00"), grid);
            UiPipeline.Place(root, center, center, MemoryLayout.SlotCenter(index % 12, 4, 3, size), size);
            parts.root = root;
            var body = UiPipeline.Rect("Body", root);
            UiPipeline.Stretch(body);
            parts.body = body;

            // A soft warm shadow under the card (the face's rounded shape, a little lower).
            parts.shadow = UiPipeline.AddImage("Shadow", body, art.Face, Shadow, Image.Type.Simple);
            parts.shadow.preserveAspect = false;
            UiPipeline.Stretch(parts.shadow.rectTransform);
            parts.shadow.rectTransform.offsetMin = new Vector2(2f, -9f);
            parts.shadow.rectTransform.offsetMax = new Vector2(-2f, -3f);

            parts.back = UiPipeline.AddImage("Back", body, art.Back, art.Back != null ? Color.white : BackFallback, Image.Type.Simple);
            parts.back.preserveAspect = false;
            UiPipeline.Stretch(parts.back.rectTransform);

            var face = UiPipeline.Rect("Face", body);
            UiPipeline.Stretch(face);
            parts.face = face.gameObject;
            parts.faceCard = UiPipeline.AddImage("Card", face, art.Face, art.Face != null ? Color.white : Cream, Image.Type.Simple);
            parts.faceCard.preserveAspect = false;
            UiPipeline.Stretch(parts.faceCard.rectTransform);
            parts.portrait = UiPipeline.AddImage("Portrait", face, null, Color.white, Image.Type.Simple);
            parts.portrait.preserveAspect = true;
            UiPipeline.Stretch(parts.portrait.rectTransform);
            float inset = size.x * MemoryLayout.PortraitInset;
            parts.portrait.rectTransform.offsetMin = new Vector2(inset, inset);
            parts.portrait.rectTransform.offsetMax = new Vector2(-inset, -inset);
            parts.portrait.enabled = false;
            float paw = size.x * MemoryLayout.PawSize;
            parts.pawOutline = UiPipeline.AddImage("PawShadow", face, null, Color.clear, Image.Type.Simple);
            UiPipeline.Place(parts.pawOutline.rectTransform, center, center, new Vector2(0f, -paw * MemoryLayout.PawShadowDrop), new Vector2(paw, paw));
            parts.pawOutline.enabled = false;
            parts.paw = UiPipeline.AddImage("Paw", face, null, Color.white, Image.Type.Simple);
            UiPipeline.Place(parts.paw.rectTransform, center, center, Vector2.zero, new Vector2(paw, paw));
            parts.paw.enabled = false;
            float mark = size.x * MemoryLayout.MarkSize;
            parts.mark = UiPipeline.AddImage("Mark", face, art.Mark, MarkColor, Image.Type.Simple);
            UiPipeline.Place(parts.mark.rectTransform, new Vector2(1f, 0f), center, MemoryLayout.MarkAt(mark), new Vector2(mark, mark));
            parts.mark.enabled = false;
            face.gameObject.SetActive(false);

            float badge = size.x * MemoryLayout.BadgeSize;
            parts.badge = UiPipeline.AddImage("Badge", body, art.Badge, Color.white, Image.Type.Simple);
            UiPipeline.Place(parts.badge.rectTransform, new Vector2(1f, 1f), center, MemoryLayout.BadgeAt(badge), new Vector2(badge, badge));
            parts.badge.enabled = false;

            parts.glow = UiPipeline.AddImage("Glow", body, art.Glow, Gold, Image.Type.Simple);
            parts.glow.preserveAspect = false;
            UiPipeline.Stretch(parts.glow.rectTransform);
            parts.glow.rectTransform.offsetMin = new Vector2(-MemoryLayout.GlowOutset, -MemoryLayout.GlowOutset);
            parts.glow.rectTransform.offsetMax = new Vector2(MemoryLayout.GlowOutset, MemoryLayout.GlowOutset);
            parts.glow.enabled = false;
            return parts;
        }

        // ---------------------------------------------------------------- Addressables

        /// <summary>
        /// Group Activity_Memory (doc Mini-jeux, section 1): the screen and its atlas, loaded at the first game. The portraits
        /// stay out (borrowed from the codex catalog the scene holds): CommonContentTests, nothing duplicated.
        /// </summary>
        public static string EnsureAddressables(GameObject prefab, SpriteAtlasAsset atlas)
        {
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            var group = EnsureGroup(settings, MemoryLayout.Group);
            var entry = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(prefab)), group, false, false);
            entry.address = MemoryLayout.ScreenAddress;
            string atlasGuid = AssetDatabase.AssetPathToGUID(AtlasPath);
            string result = $"Addressables {MemoryLayout.Group}: {MemoryLayout.ScreenAddress}";
            if (atlas != null && !string.IsNullOrEmpty(atlasGuid))
            {
                var atlasEntry = settings.CreateOrMoveEntry(atlasGuid, group, false, false);
                atlasEntry.address = AtlasAddress;
                result += ", " + AtlasAddress;
            }
            EditorUtility.SetDirty(group);
            EditorUtility.SetDirty(settings);
            return result;
        }

        private static AddressableAssetGroup EnsureGroup(AddressableAssetSettings settings, string name)
        {
            var group = settings.FindGroup(name) ?? settings.CreateGroup(name, false, false, true, null,
                typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
            var bundled = group.GetSchema<BundledAssetGroupSchema>();
            bundled.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackTogether;
            bundled.Compression = BundledAssetGroupSchema.BundleCompressionMode.LZ4;
            bundled.IncludeInBuild = true;
            bundled.BuildPath.SetVariableByName(settings, AddressableAssetSettings.kLocalBuildPath);
            bundled.LoadPath.SetVariableByName(settings, AddressableAssetSettings.kLocalLoadPath);
            group.GetSchema<ContentUpdateGroupSchema>().StaticContent = true;
            EditorUtility.SetDirty(group);
            return group;
        }
    }
}
