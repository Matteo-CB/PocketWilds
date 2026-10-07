using System;
using System.Collections.Generic;
using System.IO;
using PocketWilds.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace PocketWilds.Editor.Vfx
{
    /// <summary>
    /// T34A feedback VFX (doc 07, H-VFX-001 to 012): the shared particle atlas, the one particle material and every
    /// pooled effect prefab, listed with pool sizes and the VFX quality profile in DEF_VfxCatalog. Source of the
    /// atlas and of every effect is this file (traceable, reproducible, no hand-tuned binary). Idempotent.
    /// Particle counts are the Medium counts of the doc; Low keeps half, High a quarter more (VfxCatalog).
    /// </summary>
    public static class VfxPipeline
    {
        public const string ShaderName = "PocketWilds/AdditiveVFXLite";
        public const string MaterialPath = "Assets/_Game/Art/Materials/MAT_VFX_AdditiveLite.mat";
        public const string AtlasPath = "Assets/_Game/Art/VFX/Textures/T_VFX_FeedbackAtlas.png";
        public const string Folder = "Assets/_Game/Art/VFX";
        public const string CatalogPath = "Assets/_Game/Content/Visuals/DEF_VfxCatalog.asset";
        public const string HatchConfigPath = "Assets/_Game/Content/Visuals/DEF_HatchPresentation.asset";

        /// <summary>8 tiles of 64 px in one row: one grayscale channel, 32 KB.</summary>
        public const int TileSize = 64;
        public const int TileCount = 8;

        /// <summary>
        /// Small particles (under 0.25 m) are scaled up so they read on a phone at the hatch distance (9.5 m, about
        /// 460 px per meter on the reference screen): a 0.1 m star is 70 px, a heart 90 px (T34A captures).
        /// </summary>
        public const float SparkScale = 1.5f;

        public const int TileDot = 0, TileGlint = 1, TileHeart = 2, TilePaw = 3, TileRing = 4, TileStar5 = 5, TilePetal = 6, TileBubble = 7;

        // Rarity colors match the UI tokens (DEF_UiColorTokens): the reveal says the same thing as the frame.
        private static readonly Color RareBlue = Hex("6AA8E8");
        private static readonly Color EpicLilac = Hex("B58AE6");
        private static readonly Color LegendGold = Hex("F2B544");
        private static readonly Color FabPink = Hex("F08FB8");
        private static readonly Color Sky = new Color(0.66f, 0.86f, 1f, 1f);
        private static readonly Color WarmWhite = new Color(1f, 0.95f, 0.86f, 1f);

        [MenuItem("Pocket Wilds/VFX/Build Feedback VFX (T34A)", priority = 165)]
        public static void BuildMenu() => Debug.Log("[VfxPipeline] " + BuildAll());

        public static string BuildAll()
        {
            var material = EnsureMaterial();
            var catalog = LoadOrCreate<VfxCatalog>(CatalogPath);
            // T35B multi hatch: up to three full hatch effects (with their rarity accent and new-discovery ring) and a crack
            // puff for each other egg play together, so these pools hold that many and keep them (soft = hard, no churn).
            var effects = new List<VfxCatalog.Effect>
            {
                Effect(VfxId.Crack, Build("VFX_Crack", material, ConfigureCrack), 10, 10, 2),
                Effect(VfxId.HatchCommon, Build("VFX_HatchCommon", material, ConfigureHatchCommon), 3, 3, 1),
                Effect(VfxId.HatchRare, Build("VFX_HatchRare", material, ConfigureHatchRare), 3, 3, 1),
                Effect(VfxId.HatchEpic, Build("VFX_HatchEpic", material, ConfigureHatchEpic), 3, 3, 1),
                Effect(VfxId.HatchLegendary, Build("VFX_HatchLegendary", material, ConfigureHatchLegendary), 3, 3, 1),
                Effect(VfxId.HatchFabulous, Build("VFX_HatchFabulous", material, ConfigureHatchFabulous), 3, 3, 1),
                Effect(VfxId.EggTap, Build("VFX_EggTap", material, ConfigureEggTap), 1, 2, 1),
                Effect(VfxId.NewSpecies, Build("VFX_NewSpecies", material, ConfigureNewSpecies), 3, 3, 1),
                Effect(VfxId.NewVariant, Build("VFX_NewVariant", material, ConfigureNewVariant), 3, 3, 1),
                Effect(VfxId.MasteryLevelUp, Build("VFX_MasteryLevelUp", material, ConfigureMasteryLevelUp), 1, 2, 1),
                Effect(VfxId.FavoriteHeart, Build("VFX_FavoriteHeart", material, ConfigureFavoriteHeart), 2, 3, 2),
                Effect(VfxId.RareGlint, Build("VFX_RareGlint", material, ConfigureRareGlint), 2, 3, 2),
                Effect(VfxId.ZoneOpen, Build("VFX_ZoneOpen", material, ConfigureZoneOpen), 1, 2, 1),
            };
            catalog.effects = effects.ToArray();
            catalog.lowCountScale = 0.5f;
            catalog.highCountScale = 1.25f;
            catalog.maxPlaying = 20;
            EditorUtility.SetDirty(catalog);

            var config = AssetDatabase.LoadAssetAtPath<HatchPresentationConfig>(HatchConfigPath);
            if (config != null)
            {
                config.vfx = catalog;
                EditorUtility.SetDirty(config);
            }
            AssetDatabase.SaveAssets();
            return $"{effects.Count} effects, atlas {TileCount} tiles, catalog {CatalogPath}";
        }

        private static VfxCatalog.Effect Effect(VfxId id, ParticleSystem prefab, int soft, int hard, int warmup) =>
            new VfxCatalog.Effect { id = id, prefab = prefab, softCapacity = soft, hardCapacity = hard, warmup = warmup };

        // ---------------- Material and atlas ----------------

        public static Material EnsureMaterial()
        {
            EnsureAtlas();
            var shader = Shader.Find(ShaderName) ?? throw new InvalidOperationException("Shader not found: " + ShaderName);
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(MaterialPath));
                material = new Material(shader) { name = "MAT_VFX_AdditiveLite" };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            material.shader = shader;
            material.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath));
            material.SetFloat("_Intensity", 1.4f);
            material.enableInstancing = false;
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>
        /// Generated grayscale atlas, one row: 0 soft dot, 1 round glint, 2 heart, 3 paw, 4 soft ring,
        /// 5 plump five-point star, 6 petal, 7 soft bubble. Owner rule (2026-10-06): no four-point sparkle star
        /// anywhere. Regenerated when the layout is older (T14 had 2 tiles) or on request.
        /// </summary>
        /// <summary>Set by the menu rebuild to rewrite the atlas after a shape change.</summary>
        public static bool Regenerate;

        public static void EnsureAtlas()
        {
            if (File.Exists(AtlasPath))
            {
                var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath);
                if (existing != null && existing.width == TileSize * TileCount && !Regenerate)
                    return;
            }
            int width = TileSize * TileCount;
            var texture = new Texture2D(width, TileSize, TextureFormat.RGBA32, false, true);
            var pixels = new Color32[width * TileSize];
            for (int tile = 0; tile < TileCount; tile++)
            {
                for (int y = 0; y < TileSize; y++)
                {
                    for (int x = 0; x < TileSize; x++)
                    {
                        float u = (x + 0.5f) / TileSize * 2f - 1f;
                        float v = (y + 0.5f) / TileSize * 2f - 1f;
                        pixels[y * width + tile * TileSize + x] = Gray(TileValue(tile, u, v));
                    }
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(AtlasPath));
            File.WriteAllBytes(AtlasPath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(AtlasPath, ImportAssetOptions.ForceUpdate);
        }

        /// <summary>Value of one atlas tile at (u, v) in [-1, 1], v up. Every shape fades to 0 before the tile edge (no bleed).</summary>
        public static float TileValue(int tile, float u, float v)
        {
            float r = Mathf.Sqrt(u * u + v * v);
            float edgeFade = Mathf.Clamp01((0.98f - Mathf.Max(Mathf.Abs(u), Mathf.Abs(v))) * 12f);
            switch (tile)
            {
                case TileDot:
                    return Mathf.Pow(Mathf.Clamp01(1f - r), 2.2f);
                case TileGlint:
                {
                    // Round glint: a bright core in a soft round glow (owner rule: never a four-point sparkle).
                    float core = Mathf.Exp(-r * r * 34f);
                    float glow = Mathf.Pow(Mathf.Clamp01(1f - r), 3f) * 0.45f;
                    return Mathf.Clamp01(core + glow) * edgeFade;
                }
                case TileHeart:
                {
                    // Inigo Quilez heart, scaled to fill the tile; soft rim of light around a full body.
                    float px = Mathf.Abs(u) * 0.86f, py = (v + 0.78f) * 0.72f;
                    float d = HeartDistance(px, py);
                    return (Fill(d, 0.035f) * 0.92f + Glow(d, 10f) * 0.3f) * edgeFade;
                }
                case TilePaw:
                {
                    float pad = EllipseDistance(u, v + 0.3f, 0.4f, 0.32f);
                    float toes = Mathf.Min(
                        Mathf.Min(Circle(u + 0.47f, v - 0.14f, 0.15f), Circle(u + 0.17f, v - 0.44f, 0.16f)),
                        Mathf.Min(Circle(u - 0.17f, v - 0.44f, 0.16f), Circle(u - 0.47f, v - 0.14f, 0.15f)));
                    float d = Mathf.Min(pad, toes);
                    return (Fill(d, 0.03f) * 0.95f + Glow(d, 12f) * 0.25f) * edgeFade;
                }
                case TileRing:
                {
                    float ring = Mathf.Exp(-Mathf.Pow((r - 0.76f) / 0.075f, 2f));
                    float inner = Mathf.Exp(-Mathf.Pow((r - 0.62f) / 0.16f, 2f)) * 0.18f;
                    return Mathf.Clamp01(ring + inner) * edgeFade;
                }
                case TileStar5:
                {
                    // Plump five-point star: rounded tips, soft edge, a brighter heart.
                    float angle = Mathf.Atan2(v, u) - Mathf.PI * 0.5f;
                    float edge = 0.58f * (1f + 0.3f * Mathf.Cos(5f * angle)) - 0.04f;
                    float d = r - edge;
                    return Mathf.Clamp01(Fill(d, 0.05f) * (0.82f + 0.18f * (1f - r)) + Glow(d, 9f) * 0.25f) * edgeFade;
                }
                case TilePetal:
                {
                    // Petal: rounded body, wider near the top, a soft point at the bottom.
                    float k = Mathf.Clamp01(v * 0.5f + 0.5f);
                    float h = v / 0.66f;
                    float half = 0.5f * Mathf.Sqrt(Mathf.Max(0f, 1f - h * h)) * (0.35f + 0.65f * Mathf.Sqrt(k));
                    float d = Mathf.Max(Mathf.Abs(u) - half, Mathf.Abs(v) - 0.66f);
                    return Fill(d, 0.04f) * (0.72f + 0.28f * k) * edgeFade;
                }
                case TileBubble:
                {
                    // Soft bubble: a light disc with a brighter rim, like a bokeh of light.
                    float disc = Mathf.Clamp01((0.66f - r) * 14f) * 0.32f;
                    float rim = Mathf.Exp(-Mathf.Pow((r - 0.6f) / 0.075f, 2f)) * 0.75f;
                    float shine = Mathf.Exp(-((u + 0.24f) * (u + 0.24f) + (v - 0.26f) * (v - 0.26f)) * 60f) * 0.6f;
                    return Mathf.Clamp01(disc + rim + shine) * edgeFade;
                }
                default:
                    return 0f;
            }
        }

        private static float Fill(float distance, float softness) => Mathf.Clamp01(0.5f - distance / softness);

        private static float Glow(float distance, float falloff) => distance <= 0f ? 1f : Mathf.Exp(-distance * falloff);

        private static float Circle(float x, float y, float radius) => Mathf.Sqrt(x * x + y * y) - radius;

        private static float EllipseDistance(float x, float y, float a, float b)
        {
            // Good enough for a soft sprite: scaled circle distance.
            float k = Mathf.Sqrt(x * x / (a * a) + y * y / (b * b));
            return (k - 1f) * Mathf.Min(a, b);
        }

        private static float HeartDistance(float px, float py)
        {
            if (py + px > 1f)
                return Mathf.Sqrt((px - 0.25f) * (px - 0.25f) + (py - 0.75f) * (py - 0.75f)) - Mathf.Sqrt(2f) / 4f;
            float a = px * px + (py - 1f) * (py - 1f);
            float m = 0.5f * Mathf.Max(px + py, 0f);
            float b = (px - m) * (px - m) + (py - m) * (py - m);
            return Mathf.Sqrt(Mathf.Min(a, b)) * Mathf.Sign(px - py);
        }

        private static Color32 Gray(float value)
        {
            byte b = (byte)Mathf.RoundToInt(Mathf.Clamp01(value) * 255f);
            return new Color32(b, b, b, 255);
        }

        // ---------------- Effect builder ----------------

        private static ParticleSystem Build(string name, Material material, Action<GameObject, Material> configure)
        {
            string path = $"{Folder}/{name}.prefab";
            Directory.CreateDirectory(Folder);
            var root = new GameObject(name);
            try
            {
                configure(root, material);
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
                return prefab.GetComponent<ParticleSystem>();
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static ParticleSystem AddSystem(GameObject root, string name, Material material, bool isRoot)
        {
            var go = isRoot ? root : new GameObject(name);
            if (!isRoot)
                go.transform.SetParent(root.transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 0.2f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.stopAction = ParticleSystemStopAction.None;
            main.cullingMode = ParticleSystemCullingMode.Automatic;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.sortingFudge = 0f;
            // No particle ever covers more than a third of a phone screen (overdraw, Gate 10).
            renderer.maxParticleSize = 0.35f;
            return ps;
        }

        private static void Tile(ParticleSystem ps, int tile)
        {
            var sheet = ps.textureSheetAnimation;
            sheet.enabled = true;
            sheet.mode = ParticleSystemAnimationMode.Grid;
            sheet.numTilesX = TileCount;
            sheet.numTilesY = 1;
            sheet.animation = ParticleSystemAnimationType.WholeSheet;
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve((tile + 0.25f) / TileCount);
            sheet.startFrame = new ParticleSystem.MinMaxCurve(0f);
            sheet.cycleCount = 1;
        }

        /// <summary>Bursts as (time, count) pairs. The system lasts until its last burst.</summary>
        private static void Bursts(ParticleSystem ps, params (float time, int count)[] bursts)
        {
            var emission = ps.emission;
            emission.enabled = true;
            var list = new ParticleSystem.Burst[bursts.Length];
            float last = 0f;
            for (int i = 0; i < bursts.Length; i++)
            {
                list[i] = new ParticleSystem.Burst(bursts[i].time, (short)bursts[i].count);
                last = Mathf.Max(last, bursts[i].time);
            }
            emission.SetBursts(list);
            var main = ps.main;
            main.duration = Mathf.Max(0.05f, last + 0.02f);
            int total = 0;
            foreach (var b in bursts)
                total += b.count;
            // Room for High (x1.25) and one overlap.
            main.maxParticles = Mathf.CeilToInt(total * 1.35f) + 1;
        }

        private static void Life(ParticleSystem ps, float min, float max, float speedMin, float speedMax, float sizeMin, float sizeMax)
        {
            if (sizeMax < 0.25f)
            {
                sizeMin *= SparkScale;
                sizeMax *= SparkScale;
            }
            var main = ps.main;
            main.startLifetime = Mathf.Approximately(min, max) ? new ParticleSystem.MinMaxCurve(min) : new ParticleSystem.MinMaxCurve(min, max);
            main.startSpeed = Mathf.Approximately(speedMin, speedMax) ? new ParticleSystem.MinMaxCurve(speedMin) : new ParticleSystem.MinMaxCurve(speedMin, speedMax);
            main.startSize = Mathf.Approximately(sizeMin, sizeMax) ? new ParticleSystem.MinMaxCurve(sizeMin) : new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
        }

        private static void Colors(ParticleSystem ps, Color a, Color b)
        {
            var main = ps.main;
            main.startColor = a == b ? new ParticleSystem.MinMaxGradient(a) : new ParticleSystem.MinMaxGradient(a, b);
        }

        private static void Spin(ParticleSystem ps, float degreesPerSecond)
        {
            var main = ps.main;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            if (degreesPerSecond <= 0f)
                return;
            var rotation = ps.rotationOverLifetime;
            rotation.enabled = true;
            float r = degreesPerSecond * Mathf.Deg2Rad;
            rotation.z = new ParticleSystem.MinMaxCurve(-r, r);
        }

        /// <summary>Fade in fast, hold, fade out; size curve from growFrom to growTo.</summary>
        private static void Fade(ParticleSystem ps, Color tintEnd, float growFrom, float growTo, float holdUntil = 0.55f)
        {
            var color = ps.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(tintEnd, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.08f), new GradientAlphaKey(0.85f, holdUntil), new GradientAlphaKey(0f, 1f) });
            color.color = new ParticleSystem.MinMaxGradient(gradient);
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, growFrom, 1f, growTo));
        }

        /// <summary>Pop: grows past full size then settles (hearts, paws).</summary>
        private static void Pop(ParticleSystem ps, Color tintEnd)
        {
            Fade(ps, tintEnd, 1f, 1f, 0.65f);
            var size = ps.sizeOverLifetime;
            var curve = new AnimationCurve(new Keyframe(0f, 0.25f), new Keyframe(0.22f, 1.12f), new Keyframe(0.45f, 0.95f), new Keyframe(1f, 0.85f));
            size.size = new ParticleSystem.MinMaxCurve(1f, curve);
        }

        private static ParticleSystem.ShapeModule Shape(ParticleSystem ps, ParticleSystemShapeType type, float radius, bool up = false, float angle = 25f)
        {
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = type;
            shape.radius = radius;
            shape.angle = angle;
            if (up)
                shape.rotation = new Vector3(-90f, 0f, 0f);
            return shape;
        }

        private static void NoShape(ParticleSystem ps)
        {
            var shape = ps.shape;
            shape.enabled = false;
        }

        /// <summary>Emits from one point raised by lift (m) above the effect origin.</summary>
        private static void Lift(ParticleSystem ps, float lift)
        {
            if (lift <= 0f)
            {
                NoShape(ps);
                return;
            }
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.0001f;
            shape.radiusThickness = 0f;
            shape.position = new Vector3(0f, lift, 0f);
        }

        private static void Gravity(ParticleSystem ps, float gravity)
        {
            var main = ps.main;
            main.gravityModifier = gravity;
        }

        private static void Delay(ParticleSystem ps, float seconds)
        {
            var main = ps.main;
            main.startDelay = seconds;
        }

        /// <param name="lift">Raises the glow so its lower half never meets the ground (hard edge).</param>
        private static ParticleSystem Glow(GameObject root, Material material, string name, bool isRoot, Color color, float size, float life,
            float growFrom, float growTo, float lift = 0.3f)
        {
            var glow = AddSystem(root, name, material, isRoot);
            Life(glow, life, life, 0f, 0f, size, size);
            Colors(glow, color, color);
            Lift(glow, lift);
            Bursts(glow, (0f, 1));
            Tile(glow, TileDot);
            Fade(glow, Color.white, growFrom, growTo);
            return glow;
        }

        private static ParticleSystem Ring(GameObject root, Material material, string name, bool isRoot, Color color, float size, float life,
            float growFrom, float growTo, bool flat, float lift = 0f)
        {
            var ring = AddSystem(root, name, material, isRoot);
            Life(ring, life, life, 0f, 0f, size, size);
            Colors(ring, color, color);
            Lift(ring, flat ? 0f : lift);
            Bursts(ring, (0f, 1));
            Tile(ring, TileRing);
            Fade(ring, Color.white, growFrom, growTo, 0.45f);
            if (flat)
            {
                var renderer = ring.GetComponent<ParticleSystemRenderer>();
                renderer.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            }
            return ring;
        }

        // ---------------- Effects ----------------

        /// <summary>H-VFX-002: dust puff and glints on each crack (11 particles).</summary>
        private static void ConfigureCrack(GameObject root, Material material)
        {
            var puff = AddSystem(root, "Puff", material, true);
            Life(puff, 0.32f, 0.5f, 0.25f, 0.6f, 0.05f, 0.09f);
            Colors(puff, new Color(1f, 0.96f, 0.86f, 0.55f), new Color(1f, 0.96f, 0.86f, 0.55f));
            Gravity(puff, -0.05f);
            Shape(puff, ParticleSystemShapeType.Hemisphere, 0.08f, up: true);
            Bursts(puff, (0f, 7));
            Tile(puff, TileDot);
            Fade(puff, new Color(1f, 0.9f, 0.8f), 0.7f, 1.4f);

            var glints = AddSystem(root, "Glints", material, false);
            Life(glints, 0.22f, 0.34f, 0.4f, 0.8f, 0.035f, 0.06f);
            Colors(glints, new Color(1f, 0.93f, 0.7f, 0.9f), new Color(1f, 0.93f, 0.7f, 0.9f));
            Spin(glints, 0f);
            Shape(glints, ParticleSystemShapeType.Sphere, 0.12f);
            Bursts(glints, (0f, 4));
            Tile(glints, TileGlint);
            Fade(glints, new Color(1f, 0.85f, 0.6f), 1f, 0.3f);
        }

        /// <summary>H-VFX-003: soft burst of plump stars and motes, under 1.2 s (20 particles).</summary>
        private static void ConfigureHatchCommon(GameObject root, Material material)
        {
            Glow(root, material, "Flash", true, new Color(1f, 0.95f, 0.82f, 0.9f), 0.9f, 0.26f, 0.55f, 1.3f);

            var stars = AddSystem(root, "Stars", material, false);
            Life(stars, 0.5f, 0.85f, 0.8f, 1.6f, 0.07f, 0.12f);
            Colors(stars, new Color(1f, 0.92f, 0.62f, 1f), new Color(1f, 0.8f, 0.88f, 1f));
            Spin(stars, 120f);
            Gravity(stars, 0.18f);
            Shape(stars, ParticleSystemShapeType.Sphere, 0.1f);
            Bursts(stars, (0f, 12));
            Tile(stars, TileStar5);
            Fade(stars, new Color(1f, 0.88f, 0.9f), 1f, 0.3f);

            var motes = AddSystem(root, "Motes", material, false);
            Life(motes, 0.7f, 1.05f, 0.25f, 0.6f, 0.04f, 0.07f);
            Colors(motes, new Color(0.86f, 1f, 0.78f, 0.7f), new Color(1f, 0.97f, 0.8f, 0.7f));
            Gravity(motes, -0.08f);
            Shape(motes, ParticleSystemShapeType.Hemisphere, 0.18f, up: true);
            Bursts(motes, (0f, 7));
            Tile(motes, TileDot);
            Fade(motes, new Color(1f, 1f, 0.85f), 0.8f, 0.4f);
        }

        /// <summary>H-VFX-004: sky blue accent on top of the common burst, a star ring and rising glints (25).</summary>
        private static void ConfigureHatchRare(GameObject root, Material material)
        {
            Glow(root, material, "Glow", true, WithAlpha(Color.Lerp(RareBlue, Color.white, 0.35f), 0.7f), 1f, 0.45f, 0.4f, 1.3f);

            var ring = AddSystem(root, "StarRing", material, false);
            Life(ring, 0.6f, 0.9f, 0.9f, 1.2f, 0.07f, 0.11f);
            Colors(ring, Color.Lerp(RareBlue, Color.white, 0.45f), Color.white);
            Spin(ring, 0f);
            var shape = Shape(ring, ParticleSystemShapeType.Circle, 0.2f, up: true);
            shape.radiusThickness = 0f;
            Bursts(ring, (0f, 16));
            Tile(ring, TileGlint);
            Fade(ring, Color.Lerp(RareBlue, Color.white, 0.3f), 1f, 0.35f);

            var rising = AddSystem(root, "Rising", material, false);
            Life(rising, 0.6f, 0.9f, 0.4f, 0.8f, 0.05f, 0.08f);
            Colors(rising, Color.Lerp(RareBlue, Color.white, 0.6f), Color.white);
            Gravity(rising, -0.12f);
            Shape(rising, ParticleSystemShapeType.Cone, 0.15f, up: true, angle: 18f);
            Bursts(rising, (0.08f, 4), (0.26f, 4));
            Tile(rising, TileBubble);
            Fade(rising, Color.Lerp(RareBlue, Color.white, 0.5f), 1f, 0.25f);
        }

        /// <summary>H-VFX-005: lilac accent with one radial ring (28).</summary>
        private static void ConfigureHatchEpic(GameObject root, Material material)
        {
            Glow(root, material, "Glow", true, WithAlpha(Color.Lerp(EpicLilac, Color.white, 0.35f), 0.7f), 1.05f, 0.5f, 0.4f, 1.3f);
            Ring(root, material, "Ring", false, WithAlpha(Color.Lerp(EpicLilac, Color.white, 0.4f), 0.75f), 0.95f, 0.55f, 0.35f, 1.4f, flat: false, lift: 0.28f);

            var stars = AddSystem(root, "Stars", material, false);
            Life(stars, 0.6f, 0.95f, 1f, 1.8f, 0.07f, 0.12f);
            Colors(stars, Color.Lerp(EpicLilac, Color.white, 0.4f), new Color(1f, 0.93f, 1f, 1f));
            Spin(stars, 140f);
            Gravity(stars, 0.15f);
            Shape(stars, ParticleSystemShapeType.Sphere, 0.12f);
            Bursts(stars, (0f, 18));
            Tile(stars, TileStar5);
            Fade(stars, Color.Lerp(EpicLilac, Color.white, 0.3f), 1f, 0.3f);

            var glints = AddSystem(root, "Glints", material, false);
            Life(glints, 0.5f, 0.75f, 0.2f, 0.5f, 0.06f, 0.09f);
            Colors(glints, Color.white, Color.Lerp(EpicLilac, Color.white, 0.6f));
            Gravity(glints, -0.1f);
            Shape(glints, ParticleSystemShapeType.Sphere, 0.35f);
            Bursts(glints, (0.1f, 4), (0.3f, 4));
            Tile(glints, TileBubble);
            Fade(glints, Color.white, 0.6f, 1.1f);
        }

        /// <summary>H-VFX-006: gold with a sky accent, still short (40).</summary>
        private static void ConfigureHatchLegendary(GameObject root, Material material)
        {
            Glow(root, material, "Glow", true, WithAlpha(Color.Lerp(LegendGold, Color.white, 0.3f), 0.72f), 1.15f, 0.55f, 0.4f, 1.3f);
            Ring(root, material, "Ring", false, WithAlpha(Color.Lerp(LegendGold, Color.white, 0.55f), 0.7f), 1f, 0.6f, 0.35f, 1.4f, flat: false, lift: 0.28f);

            var stars = AddSystem(root, "Stars", material, false);
            Life(stars, 0.6f, 1f, 1f, 1.9f, 0.08f, 0.13f);
            Colors(stars, Color.Lerp(LegendGold, Color.white, 0.2f), new Color(1f, 0.96f, 0.78f, 1f));
            Spin(stars, 0f);
            Gravity(stars, 0.15f);
            Shape(stars, ParticleSystemShapeType.Sphere, 0.12f);
            Bursts(stars, (0f, 20));
            Tile(stars, TileGlint);
            Fade(stars, Color.Lerp(LegendGold, Color.white, 0.4f), 1f, 0.3f);

            var skyMotes = AddSystem(root, "SkyMotes", material, false);
            Life(skyMotes, 0.8f, 1.1f, 0.3f, 0.6f, 0.05f, 0.08f);
            Colors(skyMotes, WithAlpha(Sky, 0.85f), WithAlpha(Color.Lerp(Sky, Color.white, 0.5f), 0.85f));
            Gravity(skyMotes, -0.1f);
            Shape(skyMotes, ParticleSystemShapeType.Hemisphere, 0.22f, up: true);
            Bursts(skyMotes, (0.05f, 10));
            Tile(skyMotes, TileDot);
            Fade(skyMotes, Color.white, 0.9f, 0.4f);

            var glints = AddSystem(root, "Glints", material, false);
            Life(glints, 0.5f, 0.75f, 0.15f, 0.45f, 0.06f, 0.1f);
            Colors(glints, Color.white, new Color(1f, 0.9f, 0.6f, 1f));
            Gravity(glints, -0.1f);
            Shape(glints, ParticleSystemShapeType.Sphere, 0.38f);
            Bursts(glints, (0.12f, 3), (0.3f, 3), (0.5f, 2));
            Tile(glints, TileBubble);
            Fade(glints, Color.white, 0.6f, 1.1f);
        }

        /// <summary>H-VFX-007: soft sky gradient, carefully spaced, one glow and one ring, no screen-filling burst (44).</summary>
        private static void ConfigureHatchFabulous(GameObject root, Material material)
        {
            Glow(root, material, "Glow", true, WithAlpha(Color.Lerp(FabPink, Color.white, 0.45f), 0.72f), 1.2f, 0.6f, 0.4f, 1.3f);
            Ring(root, material, "Ring", false, WithAlpha(Color.Lerp(Sky, Color.white, 0.3f), 0.75f), 1f, 0.65f, 0.35f, 1.45f, flat: false, lift: 0.28f);

            var stars = AddSystem(root, "Stars", material, false);
            Life(stars, 0.7f, 1.05f, 0.9f, 1.7f, 0.07f, 0.12f);
            Colors(stars, Color.Lerp(FabPink, Color.white, 0.3f), Color.Lerp(Sky, Color.white, 0.2f));
            Spin(stars, 100f);
            Gravity(stars, 0.12f);
            Shape(stars, ParticleSystemShapeType.Sphere, 0.12f);
            Bursts(stars, (0f, 20));
            Tile(stars, TileStar5);
            Fade(stars, new Color(1f, 0.95f, 1f), 1f, 0.3f);

            var petals = AddSystem(root, "Petals", material, false);
            Life(petals, 1f, 1.35f, 0.5f, 0.9f, 0.07f, 0.1f);
            Colors(petals, Color.Lerp(FabPink, Color.white, 0.25f), Color.Lerp(FabPink, Color.white, 0.55f));
            Spin(petals, 160f);
            Gravity(petals, 0.06f);
            Shape(petals, ParticleSystemShapeType.Hemisphere, 0.2f, up: true);
            Bursts(petals, (0.04f, 8));
            Tile(petals, TilePetal);
            Fade(petals, Color.white, 1f, 0.7f, 0.7f);

            var glints = AddSystem(root, "Glints", material, false);
            Life(glints, 0.5f, 0.8f, 0.15f, 0.4f, 0.06f, 0.1f);
            Colors(glints, Color.white, Color.Lerp(Sky, Color.white, 0.4f));
            Gravity(glints, -0.1f);
            Shape(glints, ParticleSystemShapeType.Sphere, 0.42f);
            Bursts(glints, (0.1f, 4), (0.3f, 4), (0.5f, 3), (0.7f, 3));
            Tile(glints, TileBubble);
            Fade(glints, Color.white, 0.6f, 1.1f);
        }

        /// <summary>H-VFX-001: a ring pulse and a few small stars at the touch, under 0.4 s (8).</summary>
        private static void ConfigureEggTap(GameObject root, Material material)
        {
            Ring(root, material, "Ring", true, WithAlpha(WarmWhite, 0.9f), 0.5f, 0.3f, 0.5f, 1.6f, flat: false);

            var dots = AddSystem(root, "Dots", material, false);
            Life(dots, 0.22f, 0.32f, 0.6f, 1f, 0.035f, 0.05f);
            Colors(dots, new Color(1f, 0.93f, 0.75f, 1f), new Color(1f, 0.85f, 0.9f, 1f));
            Spin(dots, 0f);
            Shape(dots, ParticleSystemShapeType.Sphere, 0.08f);
            Bursts(dots, (0f, 7));
            Tile(dots, TileStar5);
            Fade(dots, Color.white, 1f, 0.4f);
        }

        /// <summary>H-VFX-010: a distinct ring on the ground under the newborn, a second one, rising glints (18). The banner is the label signal.</summary>
        private static void ConfigureNewSpecies(GameObject root, Material material)
        {
            var ring = Ring(root, material, "GroundRing", true, WithAlpha(new Color(1f, 0.92f, 0.62f), 0.9f), 1.1f, 0.8f, 0.3f, 1.6f, flat: true);
            Delay(ring, 0.22f);
            var second = Ring(root, material, "GroundRing2", false, WithAlpha(new Color(1f, 0.85f, 0.9f), 0.75f), 1.1f, 0.75f, 0.25f, 1.25f, flat: true);
            Delay(second, 0.42f);

            var rising = AddSystem(root, "Rising", material, false);
            Life(rising, 0.7f, 1f, 0.5f, 0.9f, 0.05f, 0.09f);
            Colors(rising, Color.white, new Color(1f, 0.9f, 0.6f, 1f));
            Gravity(rising, -0.1f);
            var risingShape = Shape(rising, ParticleSystemShapeType.Circle, 0.32f, up: true);
            risingShape.radiusThickness = 0.2f;
            Bursts(rising, (0.25f, 5), (0.45f, 5));
            Tile(rising, TileBubble);
            Fade(rising, Color.white, 0.8f, 0.5f);

            var stars = AddSystem(root, "Stars", material, false);
            Life(stars, 0.6f, 0.9f, 0.6f, 1.1f, 0.06f, 0.1f);
            Colors(stars, new Color(1f, 0.9f, 0.6f, 1f), new Color(1f, 0.8f, 0.88f, 1f));
            Spin(stars, 120f);
            Shape(stars, ParticleSystemShapeType.Hemisphere, 0.2f, up: true);
            Bursts(stars, (0.3f, 6));
            Tile(stars, TileStar5);
            Fade(stars, Color.white, 1f, 0.35f);
        }

        /// <summary>H-VFX-011: the smaller version, one ground ring and a few glints (7).</summary>
        private static void ConfigureNewVariant(GameObject root, Material material)
        {
            var ring = Ring(root, material, "GroundRing", true, WithAlpha(new Color(0.85f, 0.95f, 1f), 0.8f), 0.85f, 0.65f, 0.3f, 1.4f, flat: true);
            Delay(ring, 0.22f);

            var rising = AddSystem(root, "Rising", material, false);
            Life(rising, 0.6f, 0.85f, 0.4f, 0.7f, 0.05f, 0.08f);
            Colors(rising, Color.white, new Color(0.85f, 0.93f, 1f, 1f));
            Gravity(rising, -0.1f);
            var risingShape = Shape(rising, ParticleSystemShapeType.Circle, 0.26f, up: true);
            risingShape.radiusThickness = 0.2f;
            Bursts(rising, (0.28f, 6));
            Tile(rising, TileBubble);
            Fade(rising, Color.white, 0.8f, 0.5f);
        }

        /// <summary>H-VFX-009: a short burst of golden paws and stars around the animal (14).</summary>
        private static void ConfigureMasteryLevelUp(GameObject root, Material material)
        {
            Ring(root, material, "Ring", true, WithAlpha(new Color(1f, 0.9f, 0.62f), 0.75f), 0.75f, 0.45f, 0.35f, 1.4f, flat: false, lift: 0.22f);

            var paws = AddSystem(root, "Paws", material, false);
            Life(paws, 0.7f, 0.9f, 0.45f, 0.8f, 0.12f, 0.15f);
            Colors(paws, new Color(1f, 0.85f, 0.5f, 1f), new Color(1f, 0.92f, 0.7f, 1f));
            Spin(paws, 40f);
            Gravity(paws, -0.06f);
            Shape(paws, ParticleSystemShapeType.Hemisphere, 0.16f, up: true);
            Bursts(paws, (0.02f, 5));
            Tile(paws, TilePaw);
            Pop(paws, new Color(1f, 0.95f, 0.85f));

            var stars = AddSystem(root, "Stars", material, false);
            Life(stars, 0.5f, 0.8f, 0.6f, 1.2f, 0.06f, 0.09f);
            Colors(stars, new Color(1f, 0.9f, 0.55f, 1f), Color.white);
            Spin(stars, 120f);
            Gravity(stars, 0.1f);
            Shape(stars, ParticleSystemShapeType.Sphere, 0.12f);
            Bursts(stars, (0f, 8));
            Tile(stars, TileStar5);
            Fade(stars, Color.white, 1f, 0.3f);
        }

        /// <summary>H-VFX-012: one to three hearts popping up, under 0.7 s (3).</summary>
        private static void ConfigureFavoriteHeart(GameObject root, Material material)
        {
            var hearts = AddSystem(root, "Hearts", material, true);
            Life(hearts, 0.45f, 0.55f, 0.35f, 0.55f, 0.13f, 0.16f);
            Colors(hearts, new Color(1f, 0.5f, 0.68f, 1f), new Color(1f, 0.68f, 0.82f, 1f));
            var main = hearts.main;
            main.startRotation = new ParticleSystem.MinMaxCurve(-0.25f, 0.25f);
            Gravity(hearts, -0.05f);
            var heartShape = Shape(hearts, ParticleSystemShapeType.Cone, 0.08f, up: true, angle: 22f);
            heartShape.position = new Vector3(0f, 0.32f, 0f);
            Bursts(hearts, (0f, 1), (0.06f, 1), (0.12f, 1));
            Tile(hearts, TileHeart);
            Pop(hearts, new Color(1f, 0.85f, 0.9f));
        }

        /// <summary>Rare animal glint (G-SH-006 option 1): three soft bubbles of light, colored at play time by the glow kind.</summary>
        private static void ConfigureRareGlint(GameObject root, Material material)
        {
            var glints = AddSystem(root, "Glints", material, true);
            Life(glints, 0.5f, 0.7f, 0.02f, 0.08f, 0.08f, 0.11f);
            Colors(glints, Color.white, Color.white);
            Shape(glints, ParticleSystemShapeType.Sphere, 0.18f);
            Bursts(glints, (0f, 1), (0.15f, 1), (0.3f, 1));
            Tile(glints, TileBubble);
            Fade(glints, Color.white, 0.5f, 1.05f, 0.5f);
        }

        /// <summary>
        /// H-VFX-102 (T35A): a zone opens. As the hedge sinks, petals and pale leaves lift off along it and drift up,
        /// a few soft motes of light rise with them (24, under 1.5 s). Played at the middle of the hedge; the box runs
        /// 7 m along it (world z).
        /// </summary>
        private static void ConfigureZoneOpen(GameObject root, Material material)
        {
            var petals = AddSystem(root, "Petals", material, true);
            Life(petals, 1.0f, 1.4f, 0.45f, 0.9f, 0.1f, 0.15f);
            Colors(petals, new Color(1f, 0.84f, 0.9f, 1f), new Color(0.84f, 0.96f, 0.72f, 1f));
            Spin(petals, 160f);
            Gravity(petals, -0.04f);
            var petalShape = Shape(petals, ParticleSystemShapeType.Box, 0.1f, up: true);
            petalShape.scale = new Vector3(0.5f, 7f, 0.3f);
            Bursts(petals, (0f, 8), (0.15f, 6), (0.3f, 4));
            Tile(petals, TilePetal);
            Fade(petals, Color.white, 1f, 0.7f);

            var motes = AddSystem(root, "Motes", material, false);
            Life(motes, 0.8f, 1.2f, 0.2f, 0.45f, 0.06f, 0.09f);
            Colors(motes, new Color(1f, 0.9f, 0.6f, 1f), Color.white);
            Gravity(motes, -0.08f);
            var moteShape = Shape(motes, ParticleSystemShapeType.Box, 0.1f, up: true);
            moteShape.scale = new Vector3(0.4f, 6f, 0.2f);
            Bursts(motes, (0.05f, 6));
            Tile(motes, TileDot);
            Fade(motes, Color.white, 1f, 0.6f);
        }

        // ---------------- Helpers ----------------

        private static Color WithAlpha(Color color, float alpha) => new Color(color.r, color.g, color.b, alpha);

        private static Color Hex(string hex) => ColorUtility.TryParseHtmlString("#" + hex, out var color) ? color : Color.magenta;

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
                return asset;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }
    }
}
