using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using PocketWilds.Content;
using PocketWilds.Core;
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
    /// spec's mood), DEF_ChunkSet_{Name} (one entry per zone, chunk.{key}.{chunk}, every 14 m along +x from the spec's OriginX, T50: only the
    /// world's first arrival pinned,
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

            /// <summary>
            /// T51 open to players in this version (BiomeDefinition.OpenToPlayers): false keeps the gate « Bientôt » while the biome
            /// is being made, whatever species it already has. Tests, captures and benchmarks open it through saves.
            /// </summary>
            public bool OpenToPlayers = true;

            /// <summary>
            /// T50: world x of the biome's first chunk on the strip (Grassland 0, Forest 112); its chunks follow every 14 m.
            /// Only the biome at the world's origin pins its arrival (the refuge); a later arrival streams with the camera.
            /// </summary>
            public float OriginX;

            /// <summary>T50: lowest tier with the sun rays in the biome's weather kit (High: Magnifique, as DEF_WorldQuality).</summary>
            public QualityTier SunRaysFrom = QualityTier.High;

            /// <summary>T50: zones (chunk names) where the sun rays stand; empty: everywhere.</summary>
            public readonly List<string> SunRayZones = new List<string>();

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
                Name = "Grassland", Key = "grassland", Prefix = "GL", Order = 0, BasicEggId = "egg.grassland.basic", OriginX = 0f,
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
                // T51: « Bientôt » for players until the Forest is complete (the Owl and the Boar Piglet in T52, the bear cub in T53).
                OpenToPlayers = false,
                // T50: the strip goes on east of the Grassland's ForestEdge (98): Entry at 112, the region from x = 105.
                OriginX = 112f, SunRaysFrom = QualityTier.Medium,
                Mood = new MoodSpec
                {
                    Sun = Hex("FFD08A"), Ambient = Hex("8FBCA4"), Sky = Hex("B8DCCF"), Fog = Hex("E6E2C8"), Strength = 0.32f,
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
            // Rows (base, R, G, B, A, occlusion, accent): deeper leaf greens, warm bark, light moss, amber touches; cozy and luminous,
            // never dark (T50 art pass: a light moss floor with warm litter and a light tan path, soft occlusion). The wizard adds
            // missing rows only; ForestPipeline.TunePalettes writes these values over DEF_Palettes_Forest on every build.
            spec.Palettes.Add(("palette.forest.tree", new[] { "7A5A42", "2F7A58", "52A070", "E2AE4E", "8CCB8E", "2E3A30", "FFE6B4" }));
            spec.Palettes.Add(("palette.forest.bush", new[] { "3F8C60", "357E56", "6BB47E", "EBC066", "A6D6A0", "283C30", "FFE6B4" }));
            spec.Palettes.Add(("palette.forest.ground", new[] { "74A464", "3F9A68", "C08A50", "8FB878", "E0C99A", "2F4A3C", "9CC48C" }));
            spec.Palettes.Add(("palette.forest.rock", new[] { "9A9586", "5EA478", "B0AB9A", "D2CCB2", "B6D6AC", "4A473C", "F2E6C8" }));
            spec.Palettes.Add(("palette.forest.log", new[] { "6E4E38", "5E9E6C", "8E6C4C", "E0AC48", "B4966C", "3A2C22", "FFE2A8" }));
            spec.Palettes.Add(("palette.forest.mushroom", new[] { "E8DCC4", "C8643E", "F2E6D0", "D9A441", "F7F0E0", "5A4034", "FFFFFF" }));
            spec.Palettes.Add(("palette.forest.fern", new[] { "3E9464", "347F56", "66B47A", "98CE8C", "BDE4B0", "243C2E", "E6F6DC" }));
            // T51 owner: the Forest has its own green (deep emerald, hue about 145), never the Grassland's yellow-green grass.
            spec.Palettes.Add(("palette.forest.grass", new[] { "3F9466", "7CC492", "C8C79A", "3F9466", "3F9466", "2C5A40", "9CCB86" }));
            spec.Palettes.Add(("palette.forest.water", new[] { "6FA8A0", "8CC4B8", "C8E6DC", "4E8A80", "E8F4EE", "2E4A46", "FFFFFF" }));
            // T50 rows the Forest's pieces need (pw_forest.py, ForestPipeline): FO_Tree_D's light bark with dark marks, the canopy
            // wren (column 6: a yellow-breasted variant), the Clearing's two butterfly colorways (column 6: the second variant).
            spec.Palettes.Add(("palette.forest.birch", new[] { "E6DDC8", "4C9466", "82C08A", "E6C05A", "4A423A", "3A3428", "FFF2CC" }));
            spec.Palettes.Add(("palette.forest.life_bird", new[] { "7A5E48", "EBD3A8", "5E4636", "E8B64E", "7A5E48", "4A3A2E", "F2D27A" }));
            spec.Palettes.Add(("palette.forest.life_butterfly_amber", new[] { "4A3A2A", "F2B45A", "B86A3E", "4A3A2A", "4A3A2A", "35291E", "F7DDA8" }));
            spec.Palettes.Add(("palette.forest.life_butterfly_cream", new[] { "3E3540", "F4F0D8", "B8D88A", "3E3540", "3E3540", "2E2830", "E6F0B8" }));
            // T50: sun shafts through the canopy at the woodland edge and in the Clearing (Équilibrée and up).
            spec.SunRayZones.Add("entry");
            spec.SunRayZones.Add("clearing");
            return spec;
        }

        // ---------------------------------------------------------------- menus

        [MenuItem("Pocket Wilds/Content/Migrate Grassland Biome (T49B)", priority = 151)]
        public static void MigrateGrasslandMenu() => Debug.Log("[BiomeWizard] " + MigrateGrassland());

        /// <summary>
        /// The Prairie through the wizard: every existing asset adopted (nothing rebuilt), DEF_World_Grassland made. T50: like every
        /// biome, its profile is Addressable (world.grassland in Biome_Grassland_Kit): the scene loads it by key.
        /// </summary>
        public static string MigrateGrassland() => CreateOrUpdate(GrasslandSpec());

        /// <summary>
        /// T50 (decision 8): every shipped biome's world profile Addressable as world.{key} in its kit group with its label, the
        /// Grassland included, so the scene entry loads each biome by key and holds no biome asset. Idempotent; profiles not made yet
        /// are listed.
        /// </summary>
        public static string EnsureWorldEntries()
        {
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            var done = new List<string>();
            var missing = new List<string>();
            foreach (var biome in ContentWizard.Database().biomes.Where(b => b != null))
            {
                if (RootOf(biome) != DefaultRoot || string.IsNullOrEmpty(biome.key))
                    continue;
                var paths = new BiomePaths(NameOf(biome), DefaultRoot);
                if (!File.Exists(paths.World))
                {
                    missing.Add(Path.GetFileNameWithoutExtension(paths.World));
                    continue;
                }
                string label = Label(biome.key);
                settings.AddLabel(label);
                var kit = EnsureGroup(settings, paths.KitGroup, BundledAssetGroupSchema.BundlePackingMode.PackTogether);
                EnsureEntry(settings, kit, paths.World, BiomeWorldProfile.AddressOf(biome.key), label);
                done.Add(BiomeWorldProfile.AddressOf(biome.key));
            }
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            return $"profils : {string.Join(", ", done)}" + (missing.Count == 0 ? "." : $" ; à faire : {string.Join(", ", missing)}.");
        }

        [MenuItem("Pocket Wilds/Content/Create Forest Biome (T50)", priority = 152)]
        public static void CreateForestMenu() => Debug.Log("[BiomeWizard] " + CreateOrUpdate(ForestSpec()));

        [MenuItem("Pocket Wilds/Content/Update Biome Open Flags (T51)", priority = 153)]
        public static void ApplyOpenFlagsMenu() => Debug.Log("[BiomeWizard] " + ApplyOpenFlags());

        /// <summary>
        /// T51 writes only the open-to-players flag of the shipped presets (Grassland open, Forest « Bientôt ») onto their
        /// existing definitions, without rebuilding the rest of the biome. Idempotent.
        /// </summary>
        public static string ApplyOpenFlags()
        {
            var lines = new List<string>();
            foreach (var spec in new[] { GrasslandSpec(), ForestSpec() })
            {
                var paths = new BiomePaths(spec.Name, DefaultRoot);
                var biome = AssetDatabase.LoadAssetAtPath<BiomeDefinitionAsset>(paths.Definition);
                if (biome == null)
                {
                    lines.Add($"{spec.Name} : définition absente");
                    continue;
                }
                biome.openToPlayers = spec.OpenToPlayers;
                EditorUtility.SetDirty(biome);
                lines.Add($"{spec.Name} : {(spec.OpenToPlayers ? "ouvert" : "Bientôt")}");
            }
            AssetDatabase.SaveAssets();
            return string.Join(" ; ", lines) + ".";
        }

        /// <summary>T50: where a zone's chunk stands on the strip: the spec's origin plus 14 m per zone along +x.</summary>
        public static Vector3 ChunkOrigin(BiomeSpec spec, int zone) => new Vector3(spec.OriginX + zone * ChunkSpacing, 0f, 0f);

        /// <summary>T50: only the biome at the world's origin pins its arrival (the world's first arrival, the refuge).</summary>
        public static bool PinsArrival(BiomeSpec spec) => Mathf.Abs(spec.OriginX) < 0.01f;

        /// <summary>T50: the world x ranges of the spec's sun ray zones (each zone's chunk square, west to east edge).</summary>
        public static Vector2[] SunRayAreas(BiomeSpec spec)
        {
            var areas = new List<Vector2>();
            foreach (var chunk in spec.SunRayZones)
            {
                int at = spec.Zones.FindIndex(z => z.Chunk == chunk);
                if (at < 0)
                    continue;
                float x = ChunkOrigin(spec, at).x;
                areas.Add(new Vector2(x - ChunkHalfSize, x + ChunkHalfSize));
            }
            return areas.ToArray();
        }

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
            biome.openToPlayers = spec.OpenToPlayers;
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
            EnsureKits(spec, paths, root, made);
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
            // T50 (decision 8): every biome, the Grassland included, is loaded by its profile; World_Shared (WorldContentPipeline)
            // owns what several biomes' bundles use, so the kit bundle copies nothing.
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
        internal static void Tint(LightingProfile p, MoodSpec mood, float k)
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
                    entries.Add(new WorldChunkSet.Entry { id = id });
                    at = entries.Count - 1;
                }
                string prefab = paths.ChunkPrefab(zone.Art);
                var entry = entries[at];
                // T50: the place on the strip and the pin are the spec's (a set made before the strip is moved into place).
                entry.origin = ChunkOrigin(spec, i);
                entry.pinned = zone.Role == BiomeZoneRole.Arrival && PinsArrival(spec);
                entries[at] = entry;
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

        /// <summary>
        /// T50: the biome's weather and ambient life kits, when missing, as copies of the Grassland's (shared meshes and materials,
        /// no new texture): the weather kit takes the spec's sun ray tier and zones; the life kit is the Grassland's species list,
        /// spawning at the biome chunks' anchors of the same names. Existing kits are adopted and never overwritten.
        /// </summary>
        private static void EnsureKits(BiomeSpec spec, BiomePaths paths, string root, List<string> made)
        {
            var grassland = new BiomePaths("Grassland", root);
            var fallback = new BiomePaths("Grassland", DefaultRoot);
            // The seed biome's own kits are its pipeline's (GrasslandPipeline): never copied from themselves.
            if (paths.Weather == grassland.Weather || paths.Weather == fallback.Weather)
                return;
            if (!File.Exists(paths.Weather))
            {
                var source = AssetDatabase.LoadAssetAtPath<WeatherKit>(grassland.Weather) ?? AssetDatabase.LoadAssetAtPath<WeatherKit>(fallback.Weather);
                var kit = source != null ? UnityEngine.Object.Instantiate(source) : ScriptableObject.CreateInstance<WeatherKit>();
                kit.sunRaysFrom = spec.SunRaysFrom;
                kit.sunRayAreas = SunRayAreas(spec);
                EnsureFolder(Path.GetDirectoryName(paths.Weather)?.Replace('\\', '/'));
                AssetDatabase.CreateAsset(kit, paths.Weather);
                made.Add(paths.Weather);
            }
            if (!File.Exists(paths.AmbientLife))
            {
                var source = AssetDatabase.LoadAssetAtPath<AmbientLifeKit>(grassland.AmbientLife) ?? AssetDatabase.LoadAssetAtPath<AmbientLifeKit>(fallback.AmbientLife);
                var kit = source != null ? UnityEngine.Object.Instantiate(source) : ScriptableObject.CreateInstance<AmbientLifeKit>();
                EnsureFolder(Path.GetDirectoryName(paths.AmbientLife)?.Replace('\\', '/'));
                AssetDatabase.CreateAsset(kit, paths.AmbientLife);
                made.Add(paths.AmbientLife);
            }
            AssetDatabase.SaveAssets();
        }

        private static BiomeAudioSet EnsureAudio(BiomeSpec spec, BiomePaths paths, AddressableAssetSettings settings, List<string> made)
        {
            EnsureFolder(Path.GetDirectoryName(paths.Audio)?.Replace('\\', '/'));
            var beds = LoadOrCreate<BiomeAudioSet>(paths.Audio, made);
            var grassland = AssetDatabase.LoadAssetAtPath<BiomeAudioSet>(AudioPipeline.GrasslandBedsPath);
            // The biome's own music and ambience once their masters exist (MUS_{Name}Variation, AMB_{Name}_Day/Evening), the
            // Prairie's beds until then; a borrowed Prairie clip gives way to the biome's own when it comes (never the biome's own art).
            AudioClip Own(string mood) => AssetDatabase.LoadAssetAtPath<AudioClip>($"{AudioPipeline.AmbienceFolder}/AMB_{spec.Name}_{mood}.wav");
            var ownMusic = AssetDatabase.LoadAssetAtPath<AudioClip>($"{AudioPipeline.MusicFolder}/MUS_{spec.Name}Variation.wav");
            bool changed = false;
            AudioClip Pick(AudioClip current, AudioClip own, AudioClip borrowed)
            {
                var clip = current == null ? own ?? borrowed : own != null && current == borrowed ? own : current;
                changed |= clip != current;
                return clip;
            }
            beds.music = Pick(beds.music, ownMusic, grassland != null ? grassland.music : null);
            beds.ambience = Pick(beds.ambience, Own("Day"), grassland != null ? grassland.ambience : null);
            beds.ambienceEvening = Pick(beds.ambienceEvening, Own("Evening"), grassland != null ? grassland.ambienceEvening : null);
            beds.rain = Pick(beds.rain, null, grassland != null ? grassland.rain : null);
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
                    // T50: only the arrival at the world's origin is pinned (a later biome's arrival streams with the camera).
                    bool pin = zone.role == BiomeZoneRole.Arrival && Mathf.Abs(entry.origin.x) < 0.01f;
                    if (entry.pinned != pin)
                        setProblems.Add(id + (entry.pinned ? " épinglé (seule l'arrivée à l'origine du monde l'est)" : " : l'arrivée reste épinglée"));
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
                // T50: every biome, the Grassland included, loads by its profile (the scene holds no biome asset).
                if (worldEntry == null || worldEntry.address != BiomeWorldProfile.AddressOf(key) || worldEntry.parentGroup == null || worldEntry.parentGroup.Name != paths.KitGroup)
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
