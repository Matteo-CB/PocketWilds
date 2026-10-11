using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using PocketWilds.Content;
using PocketWilds.Editor.Animals;
using PocketWilds.Editor.Audio;
using PocketWilds.Editor.Cosmetics;
using PocketWilds.Editor.Lighting;
using PocketWilds.Editor.UI;
using PocketWilds.Editor.Validation;
using PocketWilds.Presentation;
using PocketWilds.Services.Feedback;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace PocketWilds.Editor.Production
{
    /// <summary>
    /// T49B one check of a wizard (biome or decoration): passed or what is missing, and whether a failure blocks (an error
    /// for the AssetValidator) or is only work still « à faire » (a chunk prefab whose art is not built yet).
    /// </summary>
    public readonly struct WizardFinding
    {
        public WizardFinding(string name, bool passed, string detail, bool blocking = true)
        {
            Name = name;
            Passed = passed;
            Detail = detail;
            Blocking = blocking;
        }

        public string Name { get; }
        public bool Passed { get; }
        public string Detail { get; }
        public bool Blocking { get; }

        public ContentWizard.Check ToCheck() => new ContentWizard.Check(Name, Passed, Detail);
    }

    /// <summary>
    /// ED-004 Biome Wizard (T49B, doc 06 Content IDs: the wizards write the data assets by the contracts, nothing ad hoc in a
    /// script). From a <see cref="BiomeSpec"/> it creates or updates, idempotently: DEF_Biome_{Name} with its zones (opening
    /// order: the save bit is the position, so existing zones can only be followed by new ones), DEF_Palettes_{Name} (seed rows,
    /// registered), Art/Lighting/{Name}/LP_{Name}_{Morning,Day,Evening,Night} (copied from the Grassland's, then tinted by the
    /// spec's mood), DEF_ChunkSet_{Name} (one entry per zone, chunk.{key}.{chunk}, every 14 m along +x, the arrival pinned,
    /// prefab references filled once the chunk prefabs exist), DEF_BiomeAudio_{Name} (group Audio_{Name}, audio.{key}), the
    /// Addressables groups Biome_{Name}_Chunks (pack separately), _Kit and _Zones (pack together) and the label biome_{key},
    /// DEF_World_{Name} (world.{key} in the kit group), the fr and en texts biome.{key}.name and zone.{key}.{chunk}.name, and the
    /// registration in DEF_ContentDatabase. An existing art asset (palette row, lighting profile, sky, audio clip, chunk entry)
    /// is adopted and never overwritten: <see cref="MigrateGrassland"/> runs the Prairie through the same path. The same
    /// <see cref="Inspect"/> feeds the wizard report and <see cref="AssetValidator.ValidateBiome"/>.
    /// </summary>
    public static class BiomeWizard
    {
        public const string DefaultRoot = "Assets/_Game";
        public const float ChunkSpacing = 14f;
        public const float ChunkHalfSize = 7f;
        public static readonly string[] Moods = { "Morning", "Day", "Evening", "Night" };

        // ---------------------------------------------------------------- spec

        /// <summary>One zone of the spec: chunk name (lower case), art name of its prefab (PF_Chunk_{Name}_{Art}), role, names.</summary>
        public sealed class ZoneSpec
        {
            public string Chunk = string.Empty;
            public string Art = string.Empty;
            public BiomeZoneRole Role = BiomeZoneRole.Expansion;
            public string English = string.Empty;
            public string French = string.Empty;

            public ZoneSpec(string chunk, string art, BiomeZoneRole role, string english, string french)
            {
                Chunk = chunk;
                Art = art;
                Role = role;
                English = english;
                French = french;
            }
        }

        /// <summary>The biome's light mood: the colors the Grassland's four profiles are pulled toward, and how far (0 to 1).</summary>
        public sealed class MoodSpec
        {
            public Color Sun = Color.white;
            public Color Ambient = Color.white;
            public Color Sky = Color.white;
            public Color Fog = Color.white;
            public float Strength;
        }

        /// <summary>Everything a biome needs (doc 06): identity, zones in opening order, seed palettes, light mood, basic egg.</summary>
        public sealed class BiomeSpec
        {
            /// <summary>File name part, Pascal case: Grassland, Forest (DEF_Biome_Forest, Biome_Forest_Chunks).</summary>
            public string Name = string.Empty;

            /// <summary>Short key, lower case letters and digits: grassland, forest.</summary>
            public string Key = string.Empty;

            /// <summary>Two capitals: GL, FO.</summary>
            public string Prefix = string.Empty;

            public int Order;
            public string English = string.Empty;
            public string French = string.Empty;
            public readonly List<ZoneSpec> Zones = new List<ZoneSpec>();

            /// <summary>Seed palette rows (palette.{key}.{name}; base, R, G, B, A, occlusion, accent), added when missing only.</summary>
            public readonly List<(string row, string[] hex)> Palettes = new List<(string row, string[] hex)>();

            public MoodSpec Mood = new MoodSpec();
            public string BasicEggId = string.Empty;

            public string SourceId => "biome." + Key;
            public string DisplayKey => SourceId + ".name";
        }

        /// <summary>Where a biome's assets live, from its name and the root folder (tests use a temporary root).</summary>
        public readonly struct BiomePaths
        {
            public BiomePaths(string name, string root)
            {
                Name = name;
                Root = root;
            }

            public string Name { get; }
            public string Root { get; }
            public string BiomesFolder => Root + "/Content/Biomes";
            public string Definition => $"{BiomesFolder}/DEF_Biome_{Name}.asset";
            public string ChunkSet => $"{BiomesFolder}/DEF_ChunkSet_{Name}.asset";
            public string World => $"{BiomesFolder}/DEF_World_{Name}.asset";
            public string Palettes => $"{Root}/Content/Palettes/DEF_Palettes_{Name}.asset";
            public string LightingFolder => $"{Root}/Art/Lighting/{Name}";
            public string Lighting(string mood) => $"{LightingFolder}/LP_{Name}_{mood}.asset";
            public string Sky => $"{LightingFolder}/MAT_Sky_{Name}.mat";
            public string Audio => $"{Root}/Content/Audio/DEF_BiomeAudio_{Name}.asset";
            public string ZoneKit => $"{Root}/Content/World/DEF_ZoneKit_{Name}.asset";
            public string Weather => $"{Root}/Content/World/DEF_Weather_{Name}.asset";
            public string AmbientLife => $"{Root}/Content/World/DEF_AmbientLife_{Name}.asset";
            public string ChunkFolder => $"{Root}/Prefabs/World/Chunks/{Name}";
            public string ChunkPrefab(string art) => $"{ChunkFolder}/PF_Chunk_{Name}_{art}.prefab";
            public string ChunksGroup => $"Biome_{Name}_Chunks";
            public string KitGroup => $"Biome_{Name}_Kit";
            public string ZonesGroup => $"Biome_{Name}_Zones";
            public string AudioGroup => $"Audio_{Name}";
        }

        public static string Label(string key) => "biome_" + key;
        public static string ChunkAddress(string key, string chunk) => $"chunk.{key}.{chunk}";
        public static string ZoneTextKey(string key, string chunk) => $"zone.{key}.{chunk}.name";
        public static string AudioAddress(string key) => "audio." + key;
        public static string ZonesAddress(string key) => "zones." + key;

        /// <summary>DEF_Biome_Forest → Forest.</summary>
        public static string NameOf(BiomeDefinitionAsset biome) =>
            biome.name.StartsWith("DEF_Biome_", StringComparison.Ordinal) ? biome.name.Substring("DEF_Biome_".Length) : biome.name;

        /// <summary>The root folder a biome asset was made under (Assets/_Game for the shipped ones).</summary>
        public static string RootOf(BiomeDefinitionAsset biome)
        {
            string path = AssetDatabase.GetAssetPath(biome);
            int at = path.IndexOf("/Content/Biomes/", StringComparison.Ordinal);
            return at > 0 ? path.Substring(0, at) : DefaultRoot;
        }

        // ---------------------------------------------------------------- presets

        /// <summary>The Prairie as it ships: names from UiPipeline.Texts, the 8 zones of the legacy table (bits unchanged).</summary>
        public static BiomeSpec GrasslandSpec()
        {
            var spec = new BiomeSpec
            {
                Name = "Grassland", Key = "grassland", Prefix = "GL", Order = 0, BasicEggId = "egg.grassland.basic",
                English = UiText("biome.grassland.name", 1), French = UiText("biome.grassland.name", 2),
            };
            foreach (var (chunk, art, role) in new[]
                     {
                         ("start", "Start", BiomeZoneRole.Arrival), ("meadow", "Meadow", BiomeZoneRole.Expansion),
                         ("streambend", "StreamBend", BiomeZoneRole.Expansion), ("orchard", "Orchard", BiomeZoneRole.Expansion),
                         ("hill", "Hill", BiomeZoneRole.Expansion), ("windmillrise", "WindmillRise", BiomeZoneRole.Expansion),
                         ("pondedge", "PondEdge", BiomeZoneRole.Expansion), ("forestedge", "ForestEdge", BiomeZoneRole.Passage),
                     })
            {
                string key = ZoneTextKey("grassland", chunk);
                spec.Zones.Add(new ZoneSpec(chunk, art, role, UiText(key, 1), UiText(key, 2)));
            }
            return spec;
        }

        /// <summary>
        /// T50's Forest (written now, run by T50): key forest, prefix FO, order 1, seven zones from the woodland edge to the
        /// way toward the lake; deeper greens, warm brown, amber touches, moss and dark wood; cozy, never dark.
        /// </summary>
        public static BiomeSpec ForestSpec()
        {
            var spec = new BiomeSpec
            {
                Name = "Forest", Key = "forest", Prefix = "FO", Order = 1, BasicEggId = "egg.forest.basic",
                English = "Forest", French = "Forêt",
                Mood = new MoodSpec
                {
                    Sun = Hex("FFD08A"), Ambient = Hex("7F9C6A"), Sky = Hex("B9D6C4"), Fog = Hex("DCCFAE"), Strength = 0.32f,
                },
            };
            // Zone names are used inside sentences (« Ouvrir {zone} »), with their article like the Prairie's.
            spec.Zones.Add(new ZoneSpec("entry", "Entry", BiomeZoneRole.Arrival, "the Woodland Edge", "l'Orée"));
            spec.Zones.Add(new ZoneSpec("deepforest", "DeepForest", BiomeZoneRole.Expansion, "the Undergrowth", "le Sous-bois"));
            spec.Zones.Add(new ZoneSpec("clearing", "Clearing", BiomeZoneRole.Expansion, "the Clearing", "la Clairière"));
            spec.Zones.Add(new ZoneSpec("hollow", "Hollow", BiomeZoneRole.Expansion, "the Hollow", "le Creux"));
            spec.Zones.Add(new ZoneSpec("brook", "Brook", BiomeZoneRole.Expansion, "the Brook", "le Ruisselet"));
            spec.Zones.Add(new ZoneSpec("mushroomcorner", "MushroomCorner", BiomeZoneRole.Expansion, "the Mushroom Corner", "le Coin des champignons"));
            spec.Zones.Add(new ZoneSpec("laketransition", "LakeTransition", BiomeZoneRole.Passage, "the Way Toward the Lake", "le Chemin vers le lac"));
            // Seed rows (base, R, G, B, A, occlusion, accent): deep leaf greens, warm bark, moss, amber touches.
            spec.Palettes.Add(("palette.forest.tree", new[] { "5E4636", "3F7A4A", "5E9A52", "D9A441", "8FB86A", "2A2620", "FFE2A8" }));
            spec.Palettes.Add(("palette.forest.bush", new[] { "4F8A4C", "3F7A4A", "7DAE5C", "E3B45A", "A8CC7A", "26301F", "FFE2A8" }));
            spec.Palettes.Add(("palette.forest.ground", new[] { "6E5A3E", "5C8A45", "8A6A48", "A9C77A", "C9B48A", "2E2A20", "F2D9A0" }));
            spec.Palettes.Add(("palette.forest.rock", new[] { "8C8778", "6E9A52", "A39E8E", "C9C2A8", "B8C79A", "3A382E", "F2E6C8" }));
            spec.Palettes.Add(("palette.forest.log", new[] { "5A4030", "6E9A52", "7A5A40", "D9A441", "A88A64", "2A2018", "FFE2A8" }));
            spec.Palettes.Add(("palette.forest.mushroom", new[] { "E8DCC4", "C8643E", "F2E6D0", "D9A441", "F7F0E0", "5A4034", "FFFFFF" }));
            spec.Palettes.Add(("palette.forest.fern", new[] { "4C8248", "3A6E40", "6FA35A", "9CC46E", "B8D88A", "22301E", "E8F2C8" }));
            spec.Palettes.Add(("palette.forest.water", new[] { "6FA8A0", "8CC4B8", "C8E6DC", "4E8A80", "E8F4EE", "2E4A46", "FFFFFF" }));
            return spec;
        }

        // ---------------------------------------------------------------- menus

        [MenuItem("Pocket Wilds/Content/Migrate Grassland Biome (T49B)", priority = 151)]
        public static void MigrateGrasslandMenu() => Debug.Log("[BiomeWizard] " + MigrateGrassland());

        /// <summary>The Prairie through the wizard: every existing asset adopted (nothing rebuilt), DEF_World_Grassland made.</summary>
        /// <summary>
        /// The biome the vertical slice scene wires directly (chunk set, moods, kits on VerticalSliceEntry): its world profile stays
        /// out of Addressables, which would duplicate those assets, until the biome switch (T50) loads every biome by its profile.
        /// </summary>
        public const string SceneBiomeKey = "grassland";

        public static string MigrateGrassland() => CreateOrUpdate(GrasslandSpec());

        // ---------------------------------------------------------------- create or update

        /// <summary>Spec problems (key, prefix, name, zones); empty when the spec can be written.</summary>
        public static List<string> SpecProblems(BiomeSpec spec)
        {
            var problems = new List<string>();
            if (!Regex.IsMatch(spec.Name ?? string.Empty, "^[A-Z][A-Za-z0-9]*$"))
                problems.Add($"nom « {spec.Name} » : PascalCase attendu (Forest)");
            if (!Regex.IsMatch(spec.Key ?? string.Empty, "^[a-z0-9]+$"))
                problems.Add($"clé « {spec.Key} » : lettres minuscules et chiffres");
            if (!Regex.IsMatch(spec.Prefix ?? string.Empty, "^[A-Z]{2}$"))
                problems.Add($"préfixe « {spec.Prefix} » : deux capitales");
            if (string.IsNullOrWhiteSpace(spec.English) || string.IsNullOrWhiteSpace(spec.French))
                problems.Add("nom du biome en et fr");
            problems.AddRange(ZoneProblems(spec.Zones.Select(z => (z.Chunk, z.Role)).ToList()));
            foreach (var zone in spec.Zones)
            {
                if (!Regex.IsMatch(zone.Art ?? string.Empty, "^[A-Z][A-Za-z0-9]*$"))
                    problems.Add($"zone {zone.Chunk} : nom d'art « {zone.Art} » en PascalCase");
                if (string.IsNullOrWhiteSpace(zone.English) || string.IsNullOrWhiteSpace(zone.French))
                    problems.Add($"zone {zone.Chunk} : nom en et fr");
            }
            foreach (var (row, hex) in spec.Palettes)
            {
                if (!row.StartsWith("palette." + spec.Key + ".", StringComparison.Ordinal))
                    problems.Add($"palette {row} : palette.{spec.Key}.* attendu");
                if (hex == null || hex.Length < 7)
                    problems.Add($"palette {row} : 7 couleurs");
            }
            return problems;
        }

        /// <summary>The zone rules (doc 06): one arrival, first; at most one passage, last; unique chunk names of [a-z0-9].</summary>
        public static List<string> ZoneProblems(IReadOnlyList<(string chunk, BiomeZoneRole role)> zones)
        {
            var problems = new List<string>();
            if (zones.Count == 0)
            {
                problems.Add("aucune zone");
                return problems;
            }
            if (zones[0].role != BiomeZoneRole.Arrival || zones.Count(z => z.role == BiomeZoneRole.Arrival) != 1)
                problems.Add("une seule arrivée, en premier");
            int passages = zones.Count(z => z.role == BiomeZoneRole.Passage);
            if (passages > 1 || (passages == 1 && zones[zones.Count - 1].role != BiomeZoneRole.Passage))
                problems.Add("au plus un passage, en dernier");
            if (zones.Count > 32)
                problems.Add($"{zones.Count} zones : 32 au plus (bits de sauvegarde)");
            foreach (var zone in zones)
            {
                if (!Regex.IsMatch(zone.chunk ?? string.Empty, "^[a-z0-9]+$"))
                    problems.Add($"chunk « {zone.chunk} » : lettres minuscules et chiffres");
            }
            foreach (var twice in zones.GroupBy(z => z.chunk).Where(g => g.Count() > 1))
                problems.Add($"chunk {twice.Key} en double");
            return problems;
        }

        /// <summary>Creates or updates every asset of the biome under <paramref name="root"/>. Idempotent; never overwrites art.</summary>
        public static string CreateOrUpdate(BiomeSpec spec, string root = DefaultRoot)
        {
            var problems = SpecProblems(spec);
            if (problems.Count > 0)
                throw new InvalidOperationException($"Biome {spec.Name} refusé : " + string.Join(" ; ", problems));
            var paths = new BiomePaths(spec.Name, root);
            var database = ContentWizard.Database();
            var made = new List<string>();

            // Definition: the zones may only grow at the end (a zone's save bit is its position).
            EnsureFolder(paths.BiomesFolder);
            var biome = LoadOrCreate<BiomeDefinitionAsset>(paths.Definition, made);
            for (int i = 0; i < biome.zones.Count; i++)
            {
                if (i >= spec.Zones.Count || biome.zones[i] == null || biome.zones[i].chunk != spec.Zones[i].Chunk || biome.zones[i].role != spec.Zones[i].Role)
                    throw new InvalidOperationException($"{paths.Definition} : l'ordre des zones est figé (bit de sauvegarde = position) ; " +
                                                        "on ajoute des zones à la fin seulement.");
            }
            biome.sourceId = spec.SourceId;
            biome.displayKey = spec.DisplayKey;
            biome.order = spec.Order;
            biome.key = spec.Key;
            biome.prefix = spec.Prefix;
            for (int i = biome.zones.Count; i < spec.Zones.Count; i++)
                biome.zones.Add(new BiomeDefinitionAsset.ZoneRow { chunk = spec.Zones[i].Chunk, role = spec.Zones[i].Role });
            EditorUtility.SetDirty(biome);

            // Palettes: missing seed rows only.
            EnsureFolder(Path.GetDirectoryName(paths.Palettes)?.Replace('\\', '/'));
            var palettes = LoadOrCreate<PaletteSetAsset>(paths.Palettes, made);
            int rows = 0;
            foreach (var (row, hex) in spec.Palettes)
            {
                if (palettes.palettes.Any(p => p != null && p.sourceId == row))
                    continue;
                AccessoryVisualPipeline.Add(palettes, row, hex);
                rows++;
            }
            if (rows > 0)
                EditorUtility.SetDirty(palettes);

            var moods = EnsureLighting(spec, paths, made);
            var chunkSet = EnsureChunkSet(spec, paths, made);
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            string label = Label(spec.Key);
            settings.AddLabel(label);
            var chunks = EnsureGroup(settings, paths.ChunksGroup, BundledAssetGroupSchema.BundlePackingMode.PackSeparately);
            var kit = EnsureGroup(settings, paths.KitGroup, BundledAssetGroupSchema.BundlePackingMode.PackTogether);
            var zones = EnsureGroup(settings, paths.ZonesGroup, BundledAssetGroupSchema.BundlePackingMode.PackTogether);
            foreach (var zone in spec.Zones)
            {
                string prefab = paths.ChunkPrefab(zone.Art);
                if (File.Exists(prefab))
                    EnsureEntry(settings, chunks, prefab, ChunkAddress(spec.Key, zone.Chunk), label);
            }
            if (File.Exists(paths.ZoneKit))
                EnsureEntry(settings, zones, paths.ZoneKit, ZonesAddress(spec.Key), label);

            var audio = EnsureAudio(spec, paths, settings, made);
            var world = LoadOrCreate<BiomeWorldProfile>(paths.World, made);
            world.biomeId = spec.SourceId;
            world.key = spec.Key;
            world.chunkSet = chunkSet;
            world.morning = moods[0];
            world.day = moods[1];
            world.evening = moods[2];
            world.night = moods[3];
            world.weatherKit = AssetDatabase.LoadAssetAtPath<WeatherKit>(paths.Weather) ?? world.weatherKit;
            world.ambientLife = AssetDatabase.LoadAssetAtPath<AmbientLifeKit>(paths.AmbientLife) ?? world.ambientLife;
            world.zoneKitKey = ZonesAddress(spec.Key);
            world.audioKey = AudioAddress(spec.Key);
            world.basicEggId = spec.BasicEggId;
            EditorUtility.SetDirty(world);
            AssetDatabase.SaveAssets();
            if (spec.Key == SceneBiomeKey)
            {
                // The vertical slice wires this biome's chunk set, moods and kits directly: an Addressable profile would copy them
                // into the kit bundle (CommonContentTests duplicates). It joins Addressables with the biome switch (T50).
                var stale = settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(paths.World));
                if (stale != null)
                    settings.RemoveAssetEntry(stale.guid);
            }
            else
                EnsureEntry(settings, kit, paths.World, BiomeWorldProfile.AddressOf(spec.Key), label);

            // Texts, then the content database.
            foreach (var table in database.localization.Where(t => t != null))
            {
                bool french = table.languageCode == "fr";
                if (!french && table.languageCode != "en")
                    continue;
                table.Set(spec.DisplayKey, french ? spec.French : spec.English);
                foreach (var zone in spec.Zones)
                    table.Set(ZoneTextKey(spec.Key, zone.Chunk), french ? zone.French : zone.English);
                EditorUtility.SetDirty(table);
            }
            database.biomes.RemoveAll(b => b == null);
            if (!database.biomes.Contains(biome))
                database.biomes.Add(biome);
            database.paletteSets.RemoveAll(p => p == null);
            if (!database.paletteSets.Contains(palettes))
                database.paletteSets.Add(palettes);
            EditorUtility.SetDirty(database);
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();

            int prefabs = spec.Zones.Count(z => File.Exists(paths.ChunkPrefab(z.Art)));
            return $"{spec.Name} : {spec.Zones.Count} zones, {prefabs}/{spec.Zones.Count} prefabs de chunks, {rows} lignes de palette ajoutées, " +
                   $"audio {(audio.ambience != null ? audio.ambience.name : "à faire")}, " +
                   (made.Count == 0 ? "rien de créé (tout adopté)." : "créés : " + string.Join(", ", made.Select(Path.GetFileNameWithoutExtension)) + ".");
        }

        private static LightingProfile[] EnsureLighting(BiomeSpec spec, BiomePaths paths, List<string> made)
        {
            EnsureFolder(paths.LightingFolder);
            var result = new LightingProfile[Moods.Length];
            Material sky = AssetDatabase.LoadAssetAtPath<Material>(paths.Sky);
            bool skyMade = false;
            for (int i = 0; i < Moods.Length; i++)
            {
                string path = paths.Lighting(Moods[i]);
                var profile = AssetDatabase.LoadAssetAtPath<LightingProfile>(path);
                if (profile != null)
                {
                    result[i] = profile;
                    continue;
                }
                string seedPath = $"{LightingPipeline.LightingFolder}/LP_Grassland_{Moods[i]}.asset";
                var seed = AssetDatabase.LoadAssetAtPath<LightingProfile>(seedPath);
                if (seed == null || seedPath == path)
                    throw new InvalidOperationException($"Profil {Moods[i]} de la Prairie absent ({seedPath}) : lancer les pipelines de lumière d'abord.");
                if (sky == null)
                {
                    if (!AssetDatabase.CopyAsset(LightingPipeline.SkyMaterialPath, paths.Sky))
                        throw new InvalidOperationException("Copie du ciel impossible vers " + paths.Sky);
                    sky = AssetDatabase.LoadAssetAtPath<Material>(paths.Sky);
                    skyMade = true;
                    made.Add(paths.Sky);
                }
                profile = ScriptableObject.CreateInstance<LightingProfile>();
                EditorUtility.CopySerialized(seed, profile);
                Tint(profile, spec.Mood, Moods[i] == "Night" ? spec.Mood.Strength * 0.5f : spec.Mood.Strength);
                profile.sky = sky;
                AssetDatabase.CreateAsset(profile, path);
                made.Add(path);
                result[i] = profile;
            }
            if (skyMade && result[1] != null)
                LightingPipeline.WriteSky(result[1]);
            return result;
        }

        /// <summary>Pulls a seeded profile toward the biome's mood (sun and glow, ambient, sky and clouds, fog); intensities kept.</summary>
        private static void Tint(LightingProfile p, MoodSpec mood, float k)
        {
            if (k <= 0f)
                return;
            Color Pull(Color c, Color to) => Color.Lerp(c, to, k);
            p.sunColor = Pull(p.sunColor, mood.Sun);
            p.sunGlowTint = Pull(p.sunGlowTint, mood.Sun);
            p.cloudLight = Pull(p.cloudLight, mood.Sun);
            p.ambientTop = Pull(p.ambientTop, mood.Ambient);
            p.ambientEquator = Pull(p.ambientEquator, mood.Ambient);
            p.ambientBottom = Pull(p.ambientBottom, mood.Ambient);
            p.skyTop = Pull(p.skyTop, mood.Sky);
            p.skyHorizon = Pull(p.skyHorizon, mood.Sky);
            p.skyBottom = Pull(p.skyBottom, mood.Sky);
            p.cloudShade = Pull(p.cloudShade, mood.Sky);
            p.fogColor = Pull(p.fogColor, mood.Fog);
        }

        private static WorldChunkSet EnsureChunkSet(BiomeSpec spec, BiomePaths paths, List<string> made)
        {
            var set = LoadOrCreate<WorldChunkSet>(paths.ChunkSet, made);
            var entries = new List<WorldChunkSet.Entry>();
            for (int i = 0; i < set.Count; i++)
                entries.Add(set.Get(i));
            for (int i = 0; i < spec.Zones.Count; i++)
            {
                var zone = spec.Zones[i];
                string id = ChunkAddress(spec.Key, zone.Chunk);
                int at = entries.FindIndex(e => e.id == id);
                if (at < 0)
                {
                    entries.Add(new WorldChunkSet.Entry
                    {
                        id = id, origin = new Vector3(i * ChunkSpacing, 0f, 0f), pinned = zone.Role == BiomeZoneRole.Arrival,
                    });
                    at = entries.Count - 1;
                }
                string prefab = paths.ChunkPrefab(zone.Art);
                var entry = entries[at];
                if (string.IsNullOrEmpty(entry.prefab?.AssetGUID) && File.Exists(prefab))
                {
                    entry.prefab = new AssetReferenceGameObject(AssetDatabase.AssetPathToGUID(prefab));
                    entries[at] = entry;
                }
            }
            bool fresh = set.Count == 0;
            set.SetAuthoring(entries.ToArray(), fresh ? ChunkHalfSize : set.HalfSize, fresh ? StreamingWindow.Default : set.Window);
            EditorUtility.SetDirty(set);
            return set;
        }

        private static BiomeAudioSet EnsureAudio(BiomeSpec spec, BiomePaths paths, AddressableAssetSettings settings, List<string> made)
        {
            EnsureFolder(Path.GetDirectoryName(paths.Audio)?.Replace('\\', '/'));
            var beds = LoadOrCreate<BiomeAudioSet>(paths.Audio, made);
            var grassland = AssetDatabase.LoadAssetAtPath<BiomeAudioSet>(AudioPipeline.GrasslandBedsPath);
            // The biome's own ambience once its master exists (AMB_{Name}_Day/Evening), the Prairie's beds until then.
            AudioClip Own(string mood) => AssetDatabase.LoadAssetAtPath<AudioClip>($"{AudioPipeline.AmbienceFolder}/AMB_{spec.Name}_{mood}.wav");
            bool changed = false;
            if (beds.music == null) { beds.music = grassland != null ? grassland.music : null; changed = true; }
            if (beds.ambience == null) { beds.ambience = Own("Day") ?? (grassland != null ? grassland.ambience : null); changed = true; }
            if (beds.ambienceEvening == null) { beds.ambienceEvening = Own("Evening") ?? (grassland != null ? grassland.ambienceEvening : null); changed = true; }
            if (beds.rain == null) { beds.rain = grassland != null ? grassland.rain : null; changed = true; }
            if (changed)
                EditorUtility.SetDirty(beds);
            AssetDatabase.SaveAssets();
            var group = EnsureGroup(settings, paths.AudioGroup, BundledAssetGroupSchema.BundlePackingMode.PackTogether);
            EnsureEntry(settings, group, paths.Audio, AudioAddress(spec.Key), null);
            return beds;
        }

        // ---------------------------------------------------------------- inspection (report and validator)

        public static List<ContentWizard.Row> BiomeReport()
        {
            var rows = new List<ContentWizard.Row>();
            foreach (var biome in ContentWizard.Database().biomes.Where(b => b != null))
                rows.Add(BiomeRow(biome));
            return rows;
        }

        public static ContentWizard.Row BiomeRow(BiomeDefinitionAsset biome)
        {
            var row = new ContentWizard.Row { Id = biome.sourceId, Label = NameOf(biome) };
            row.Checks.AddRange(Inspect(biome).Select(f => f.ToCheck()));
            return row;
        }

        /// <summary>Every check of a biome, on the disk and in the data (never from a status file).</summary>
        public static List<WizardFinding> Inspect(BiomeDefinitionAsset biome)
        {
            var findings = new List<WizardFinding>();
            string name = NameOf(biome);
            var paths = new BiomePaths(name, RootOf(biome));
            string key = biome.key ?? string.Empty;
            var database = ContentWizard.Database();
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(false);
            string label = Label(key);

            var zoneProblems = ZoneProblems(biome.zones.Select(z => (z?.chunk ?? string.Empty, z?.role ?? BiomeZoneRole.Expansion)).ToList());
            if (biome.sourceId != "biome." + key)
                zoneProblems.Add($"sourceId {biome.sourceId} : biome.{key} attendu");
            if (!Regex.IsMatch(biome.prefix ?? string.Empty, "^[A-Z]{2}$"))
                zoneProblems.Add($"préfixe « {biome.prefix} » : deux capitales");
            findings.Add(new WizardFinding("Définition et zones", zoneProblems.Count == 0, zoneProblems.Count == 0
                ? $"{biome.zones.Count} zones : {string.Join(", ", biome.zones.Select(z => z.chunk + (z.role == BiomeZoneRole.Expansion ? string.Empty : $" ({Role(z.role)})")))}"
                : string.Join(" ; ", zoneProblems)));

            var set = AssetDatabase.LoadAssetAtPath<WorldChunkSet>(paths.ChunkSet);
            var setProblems = new List<string>();
            var missingPrefabs = new List<string>();
            var addressProblems = new List<string>();
            if (set == null)
                setProblems.Add(paths.ChunkSet + " absent");
            else
            {
                for (int i = 0; i < biome.zones.Count; i++)
                {
                    var zone = biome.zones[i];
                    if (zone == null)
                        continue;
                    string id = ChunkAddress(key, zone.chunk);
                    int at = -1;
                    for (int e = 0; e < set.Count && at < 0; e++)
                        if (set.Get(e).id == id)
                            at = e;
                    if (at < 0)
                    {
                        setProblems.Add(id + " sans entrée");
                        continue;
                    }
                    var entry = set.Get(at);
                    if (entry.pinned != (zone.role == BiomeZoneRole.Arrival))
                        setProblems.Add(id + (entry.pinned ? " épinglé (seule l'arrivée l'est)" : " : l'arrivée reste épinglée"));
                    string guid = entry.prefab?.AssetGUID;
                    string prefabPath = string.IsNullOrEmpty(guid) ? null : AssetDatabase.GUIDToAssetPath(guid);
                    if (string.IsNullOrEmpty(prefabPath) || !File.Exists(prefabPath))
                    {
                        missingPrefabs.Add(id);
                        continue;
                    }
                    var owned = settings?.FindAssetEntry(guid);
                    if (owned == null || owned.address != id || owned.parentGroup == null || owned.parentGroup.Name != paths.ChunksGroup || !owned.labels.Contains(label))
                        addressProblems.Add($"{Path.GetFileNameWithoutExtension(prefabPath)} : {id} dans {paths.ChunksGroup} avec {label}");
                }
                if (set.Count != biome.zones.Count)
                    setProblems.Add($"{set.Count} entrées pour {biome.zones.Count} zones");
            }
            findings.Add(new WizardFinding("Ensemble de chunks", setProblems.Count == 0,
                setProblems.Count == 0 ? $"{paths.ChunkSet}, demi-taille {set.HalfSize:0.#} m" : string.Join(" ; ", setProblems)));
            findings.Add(new WizardFinding("Prefabs de chunks", set != null && missingPrefabs.Count == 0,
                missingPrefabs.Count == 0 ? $"{biome.zones.Count}/{biome.zones.Count}" : $"à faire : {string.Join(", ", missingPrefabs)}", blocking: false));

            if (settings == null)
                addressProblems.Add("pas de réglages Addressables");
            else
            {
                foreach (var group in new[] { paths.ChunksGroup, paths.KitGroup, paths.ZonesGroup })
                    if (settings.FindGroup(group) == null)
                        addressProblems.Add(group + " absent");
                if (!settings.GetLabels().Contains(label))
                    addressProblems.Add("label " + label + " absent");
            }
            findings.Add(new WizardFinding("Groupes Addressables et label", addressProblems.Count == 0,
                addressProblems.Count == 0 ? $"{paths.ChunksGroup}, {paths.KitGroup}, {paths.ZonesGroup}, {label}" : string.Join(" ; ", addressProblems)));

            var lights = Moods.Select(m => (m, AssetDatabase.LoadAssetAtPath<LightingProfile>(paths.Lighting(m)))).ToList();
            var noLight = lights.Where(l => l.Item2 == null || l.Item2.sky == null).Select(l => l.m).ToList();
            findings.Add(new WizardFinding("Lumière (4 ambiances)", noLight.Count == 0,
                noLight.Count == 0 ? paths.LightingFolder : "manque (ou sans ciel) : " + string.Join(", ", noLight)));

            var audio = AssetDatabase.LoadAssetAtPath<BiomeAudioSet>(paths.Audio);
            var audioEntry = settings != null && audio != null ? settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(paths.Audio)) : null;
            bool audioOk = audio != null && audio.music != null && audio.ambience != null && audioEntry != null
                           && audioEntry.address == AudioAddress(key) && audioEntry.parentGroup != null && audioEntry.parentGroup.Name == paths.AudioGroup;
            string ownAmbience = audio != null && audio.ambience != null && !audio.ambience.name.Contains(name) ? " (ambiance de la Prairie en attendant la sienne)" : string.Empty;
            findings.Add(new WizardFinding("Audio", audioOk, audioOk ? $"{AudioAddress(key)} dans {paths.AudioGroup}{ownAmbience}"
                : audio == null ? paths.Audio + " absent" : $"musique et ambiance, adresse {AudioAddress(key)} dans {paths.AudioGroup}"));

            var palettes = AssetDatabase.LoadAssetAtPath<PaletteSetAsset>(paths.Palettes);
            int ownRows = palettes == null ? 0 : palettes.palettes.Count(p => p != null && p.sourceId.StartsWith("palette." + key + ".", StringComparison.Ordinal));
            bool palettesOk = palettes != null && ownRows > 0 && database.paletteSets.Contains(palettes);
            findings.Add(new WizardFinding("Palettes", palettesOk, palettes == null ? paths.Palettes + " absent"
                : $"{ownRows} lignes palette.{key}.*" + (database.paletteSets.Contains(palettes) ? string.Empty : ", non enregistrées dans la base")));

            var world = AssetDatabase.LoadAssetAtPath<BiomeWorldProfile>(paths.World);
            var worldProblems = new List<string>();
            if (world == null)
                worldProblems.Add(paths.World + " absent");
            else
            {
                if (world.biomeId != biome.sourceId || world.key != key)
                    worldProblems.Add("identité");
                if (world.chunkSet != set)
                    worldProblems.Add("ensemble de chunks");
                if (world.morning == null || world.day == null || world.evening == null || world.night == null)
                    worldProblems.Add("4 ambiances");
                if (world.zoneKitKey != ZonesAddress(key) || world.audioKey != AudioAddress(key) || string.IsNullOrEmpty(world.basicEggId))
                    worldProblems.Add("clés (zones, audio, œuf)");
                var worldEntry = settings?.FindAssetEntry(AssetDatabase.AssetPathToGUID(paths.World));
                if (key == SceneBiomeKey)
                {
                    if (worldEntry != null)
                        worldProblems.Add("hors Addressables tant que la scène le référence (doublons)");
                }
                else if (worldEntry == null || worldEntry.address != BiomeWorldProfile.AddressOf(key) || worldEntry.parentGroup == null || worldEntry.parentGroup.Name != paths.KitGroup)
                    worldProblems.Add($"adresse {BiomeWorldProfile.AddressOf(key)} dans {paths.KitGroup}");
            }
            findings.Add(new WizardFinding("Profil du monde", worldProblems.Count == 0,
                worldProblems.Count == 0 ? BiomeWorldProfile.AddressOf(key) : string.Join(" ; ", worldProblems)));
            bool kits = world != null && world.weatherKit != null && world.ambientLife != null;
            findings.Add(new WizardFinding("Météo et vie ambiante", kits, kits ? $"{world.weatherKit.name}, {world.ambientLife.name}"
                : $"à faire : {Path.GetFileName(paths.Weather)}, {Path.GetFileName(paths.AmbientLife)}", blocking: false));
            var zoneKit = settings?.FindAssetEntry(AssetDatabase.AssetPathToGUID(paths.ZoneKit));
            bool zoneKitOk = File.Exists(paths.ZoneKit) && zoneKit != null && zoneKit.address == ZonesAddress(key);
            findings.Add(new WizardFinding("Kit de zones", zoneKitOk, zoneKitOk ? ZonesAddress(key) : $"à faire : {Path.GetFileName(paths.ZoneKit)} ({ZonesAddress(key)})", blocking: false));

            var keys = new List<string> { biome.displayKey };
            keys.AddRange(biome.zones.Where(z => z != null).Select(z => ZoneTextKey(key, z.chunk)));
            var missing = MissingTexts(database, keys);
            findings.Add(new WizardFinding("Textes en et fr", missing.Count == 0,
                missing.Count == 0 ? $"{keys.Count} clés" : "manque : " + string.Join(", ", missing.Take(4))));
            findings.Add(new WizardFinding("Base de contenu", database.biomes.Contains(biome),
                database.biomes.Contains(biome) ? "enregistré" : "absent de DEF_ContentDatabase.biomes"));
            return findings;
        }

        private static string Role(BiomeZoneRole role) => role == BiomeZoneRole.Arrival ? "arrivée" : role == BiomeZoneRole.Passage ? "passage" : "extension";

        public static List<string> MissingTexts(ContentDatabaseAsset database, IEnumerable<string> keys)
        {
            var missing = new List<string>();
            foreach (var key in keys)
            {
                foreach (var language in new[] { "en", "fr" })
                {
                    var table = database.localization.FirstOrDefault(t => t != null && t.languageCode == language);
                    if (table == null || !table.TryGet(key, out var value) || string.IsNullOrWhiteSpace(value))
                        missing.Add($"{key} ({language})");
                }
            }
            return missing;
        }

        // ---------------------------------------------------------------- removal (temporary roots only)

        /// <summary>
        /// Removes a biome made under a temporary root (tests): its folder, Addressables groups and label, database entries
        /// and texts. Refuses the project root: a shipped biome is never removed by a tool.
        /// </summary>
        public static string Remove(string name, string key, string root)
        {
            if (string.IsNullOrEmpty(root) || root == DefaultRoot || root == "Assets" || !root.StartsWith("Assets/", StringComparison.Ordinal))
                throw new InvalidOperationException("Suppression refusée hors d'une racine temporaire : " + root);
            var paths = new BiomePaths(name, root);
            var database = ContentWizard.Database();
            database.biomes.RemoveAll(b => b == null || AssetDatabase.GetAssetPath(b).StartsWith(root + "/", StringComparison.Ordinal));
            database.paletteSets.RemoveAll(p => p == null || AssetDatabase.GetAssetPath(p).StartsWith(root + "/", StringComparison.Ordinal));
            foreach (var table in database.localization.Where(t => t != null))
            {
                int removed = table.entries.RemoveAll(e => e.key == $"biome.{key}.name" || (e.key != null && e.key.StartsWith($"zone.{key}.", StringComparison.Ordinal)));
                if (removed > 0)
                    EditorUtility.SetDirty(table);
            }
            EditorUtility.SetDirty(database);
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(false);
            if (settings != null)
            {
                foreach (var groupName in new[] { paths.ChunksGroup, paths.KitGroup, paths.ZonesGroup, paths.AudioGroup })
                {
                    var group = settings.FindGroup(groupName);
                    if (group != null)
                        settings.RemoveGroup(group);
                }
                settings.RemoveLabel(Label(key));
                EditorUtility.SetDirty(settings);
            }
            AssetDatabase.SaveAssets();
            if (AssetDatabase.IsValidFolder(root))
                AssetDatabase.DeleteAsset(root);
            return $"{name} retiré de {root}.";
        }

        // ---------------------------------------------------------------- helpers

        /// <summary>Creates the folder chain in the AssetDatabase (so assets can be created and the folder deleted cleanly).</summary>
        public static void EnsureFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder) || AssetDatabase.IsValidFolder(folder))
                return;
            string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }

        private static T LoadOrCreate<T>(string path, List<string> made) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
                return asset;
            EnsureFolder(Path.GetDirectoryName(path)?.Replace('\\', '/'));
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            made.Add(path);
            return asset;
        }

        /// <summary>The group, created local and LZ4 with the packing mode when missing; an existing group is adopted as it is.</summary>
        public static AddressableAssetGroup EnsureGroup(AddressableAssetSettings settings, string name, BundledAssetGroupSchema.BundlePackingMode mode)
        {
            var group = settings.FindGroup(name);
            if (group != null)
                return group;
            group = settings.CreateGroup(name, false, false, true, null, typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
            var bundled = group.GetSchema<BundledAssetGroupSchema>();
            bundled.BundleMode = mode;
            bundled.Compression = BundledAssetGroupSchema.BundleCompressionMode.LZ4;
            bundled.IncludeInBuild = true;
            bundled.BuildPath.SetVariableByName(settings, AddressableAssetSettings.kLocalBuildPath);
            bundled.LoadPath.SetVariableByName(settings, AddressableAssetSettings.kLocalLoadPath);
            group.GetSchema<ContentUpdateGroupSchema>().StaticContent = true;
            EditorUtility.SetDirty(group);
            return group;
        }

        /// <summary>The asset's entry in the group with the address (and label); moved only when it is elsewhere or misnamed.</summary>
        public static AddressableAssetEntry EnsureEntry(AddressableAssetSettings settings, AddressableAssetGroup group, string path, string address, string label)
        {
            string guid = AssetDatabase.AssetPathToGUID(path);
            if (string.IsNullOrEmpty(guid))
                throw new InvalidOperationException("Asset absent : " + path);
            var entry = settings.FindAssetEntry(guid);
            if (entry == null || entry.parentGroup != group)
                entry = settings.CreateOrMoveEntry(guid, group, false, false);
            if (entry.address != address)
                entry.address = address;
            if (!string.IsNullOrEmpty(label) && !entry.labels.Contains(label))
                entry.SetLabel(label, true, true, false);
            EditorUtility.SetDirty(group);
            return entry;
        }

        private static string UiText(string key, int column)
        {
            for (int i = 0; i < UiPipeline.Texts.GetLength(0); i++)
                if (UiPipeline.Texts[i, 0] == key)
                    return UiPipeline.Texts[i, column];
            return string.Empty;
        }

        private static Color Hex(string hex) => ColorUtility.TryParseHtmlString("#" + hex, out var color) ? color : Color.magenta;
    }
}
