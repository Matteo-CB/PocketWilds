using System.IO;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Editor.Animals;
using PocketWilds.Editor.Vfx;
using PocketWilds.Presentation;
using PocketWilds.Simulation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PocketWilds.Editor.Capture
{
    /// <summary>
    /// T34A VFX review sheets: every feedback effect at four moments of its life, around a real rabbit on the
    /// meadow color, in a throwaway scene. Seen exactly as on the reference phone: the refuge camera at its hatch
    /// distance (9.5 m, pitch 40, FOV 30) renders a 1080 x 2340 frame and each cell is a 1:1 crop around the
    /// rabbit. Deterministic (fixed random seed, simulated to the exact moment). One row per effect.
    /// </summary>
    public static class VfxCapture
    {
        private static readonly float[] Moments = { 0.12f, 0.3f, 0.52f, 0.78f };
        private const int Cell = 540;
        private const int PhoneWidth = 1080, PhoneHeight = 2340;

        [MenuItem("Pocket Wilds/Capture/VFX Sheets (T34A)", priority = 521)]
        public static void CaptureMenu() => Debug.Log("[VfxCapture] " + Capture("T34A", 1f));

        /// <param name="countScale">0.5 Low, 1 Medium, 1.25 High.</param>
        public static string Capture(string prefix, float countScale)
        {
            var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (!string.IsNullOrEmpty(active.path) && active.isDirty)
                return "refused: save or discard changes in " + active.path;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var catalog = AssetDatabase.LoadAssetAtPath<VfxCatalog>(VfxPipeline.CatalogPath);
            var database = AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>(AnimalPipeline.ContentDatabasePath);
            var visuals = AssetDatabase.LoadAssetAtPath<AnimalVisualCatalog>(AnimalPipeline.VisualCatalogPath);
            var registry = database.CreateBuilder().Build();
            var camera = BuildStage();
            string folder = CaptureUtility.DefaultFolder;
            Directory.CreateDirectory(folder);

            VfxId[][] sheets =
            {
                new[] { VfxId.EggTap, VfxId.Crack, VfxId.HatchCommon, VfxId.HatchRare, VfxId.HatchEpic, VfxId.HatchLegendary },
                new[] { VfxId.HatchFabulous, VfxId.NewSpecies, VfxId.NewVariant, VfxId.MasteryLevelUp, VfxId.FavoriteHeart, VfxId.RareGlint },
            };
            string[] names = { "hatch", "feedback" };
            using (var palettes = new PaletteTable(registry))
            {
                var resolver = new AnimalVisualResolver(registry, palettes);
                var variant = registry.Variants.Get(ContentId.FromSource("variant.rabbit.brown"));
                visuals.TryGet("PF_Animal_Rabbit", out var rabbitVisual);
                var instance = new AnimalInstance { InstanceId = 1, Seed = 7, Species = variant.Species, Variant = variant.Id };
                resolver.TryResolve(instance, Vector3.zero, 180f, out var snapshot);
                var rabbit = Object.Instantiate(rabbitVisual.prefab);
                rabbit.Configure(snapshot);
                rabbit.Animator.Rebind();
                rabbit.Animator.Update(0f);
                var target = RenderTexture.GetTemporary(PhoneWidth, PhoneHeight, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB, 4);
                // The cell is centered on the rabbit's screen point.
                camera.targetTexture = target;
                var center = camera.WorldToScreenPoint(new Vector3(0f, 0.3f, 0f));
                camera.Render();
                camera.targetTexture = null;
                int x0 = Mathf.Clamp(Mathf.RoundToInt(center.x) - Cell / 2, 0, PhoneWidth - Cell);
                int y0 = Mathf.Clamp(Mathf.RoundToInt(center.y) - Cell / 2, 0, PhoneHeight - Cell);
                try
                {
                    for (int s = 0; s < sheets.Length; s++)
                    {
                        var ids = sheets[s];
                        var sheet = new Texture2D(Cell * Moments.Length, Cell * ids.Length, TextureFormat.RGB24, false);
                        for (int row = 0; row < ids.Length; row++)
                        {
                            var prefab = catalog.Prefab(ids[row]);
                            if (prefab == null)
                                continue;
                            float duration = Duration(prefab);
                            for (int col = 0; col < Moments.Length; col++)
                            {
                                var system = Object.Instantiate(prefab, Origin(ids[row]), Quaternion.identity);
                                try
                                {
                                    Prepare(system, countScale);
                                    system.Simulate(Moments[col] * duration, true, true);
                                    camera.targetTexture = target;
                                    camera.Render();
                                    camera.targetTexture = null;
                                    RenderTexture.active = target;
                                    // Rows from the top: texture y grows upward.
                                    sheet.ReadPixels(new Rect(x0, y0, Cell, Cell), col * Cell, (ids.Length - 1 - row) * Cell);
                                    RenderTexture.active = null;
                                }
                                finally
                                {
                                    Object.DestroyImmediate(system.gameObject);
                                }
                            }
                        }
                        sheet.Apply();
                        File.WriteAllBytes($"{folder}/{prefix}_vfx_{names[s]}.png", sheet.EncodeToPNG());
                        Object.DestroyImmediate(sheet);
                    }
                }
                finally
                {
                    RenderTexture.ReleaseTemporary(target);
                    Object.DestroyImmediate(rabbit.gameObject);
                }
            }
            string glow = CaptureRareGlow(prefix, registry, visuals);
            return $"{folder}/{prefix}_vfx_hatch.png, {folder}/{prefix}_vfx_feedback.png, {glow}";
        }

        /// <summary>
        /// SH_RareGlow review: the same rabbit four times at the hatch distance, Common, Legendary (gold 3), Golden trait
        /// (gold 5) and Fabulous (sky 12), side by side in one phone-scale frame.
        /// </summary>
        private static string CaptureRareGlow(string prefix, ContentRegistry registry, AnimalVisualCatalog visuals)
        {
            var camera = Camera.main != null ? Camera.main : Object.FindAnyObjectByType<Camera>();
            visuals.TryGet("PF_Animal_Rabbit", out var rabbitVisual);
            string[] variants = { "variant.rabbit.white", "variant.rabbit.brown" };
            var rabbits = new System.Collections.Generic.List<GameObject>();
            string path = $"{CaptureUtility.DefaultFolder}/{prefix}_rare_glow.png";
            using (var palettes = new PaletteTable(registry))
            {
                var resolver = new AnimalVisualResolver(registry, palettes);
                (RarityTier rarity, uint traits)[] looks =
                {
                    (RarityTier.Common, 0u), (RarityTier.Legendary, 0u), (RarityTier.Rare, (uint)AnimalTrait.Golden), (RarityTier.Fabulous, 0u),
                };
                try
                {
                    for (int k = 0; k < looks.Length * variants.Length; k++)
                    {
                        int i = k % looks.Length;
                        var variant = registry.Variants.Get(ContentId.FromSource(variants[k / looks.Length]));
                        var instance = new AnimalInstance
                        {
                            InstanceId = (uint)(10 + k), Seed = 7, Species = variant.Species, Variant = variant.Id,
                            Rarity = looks[i].rarity, TraitBits = looks[i].traits,
                        };
                        var position = new Vector3((i - 1.5f) * 0.75f, 0f, (k / looks.Length) * -0.9f);
                        resolver.TryResolve(instance, position, 180f, out var snapshot);
                        var view = Object.Instantiate(rabbitVisual.prefab);
                        view.Configure(snapshot);
                        view.Animator.Rebind();
                        view.Animator.Update(0f);
                        rabbits.Add(view.gameObject);
                    }
                    // Closer than the hatch framing: four animals in one look.
                    var previous = camera.transform.position;
                    camera.transform.position = new Vector3(0f, 0.3f, 0f) - camera.transform.rotation * Vector3.forward * 5.5f;
                    CaptureUtility.Render(camera, 1080, 720, path, 4, null);
                    camera.transform.position = previous;
                }
                finally
                {
                    foreach (var go in rabbits)
                        Object.DestroyImmediate(go);
                }
            }
            return path;
        }

        private static Vector3 Origin(VfxId id)
        {
            switch (id)
            {
                case VfxId.NewSpecies:
                case VfxId.NewVariant:
                    return new Vector3(0f, 0.02f, 0f);
                case VfxId.RareGlint:
                    return new Vector3(0.05f, 0.32f, 0f);
                default:
                    return new Vector3(0f, 0.22f, 0f);
            }
        }

        private static void Prepare(ParticleSystem system, float countScale)
        {
            foreach (var ps in system.GetComponentsInChildren<ParticleSystem>(true))
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ps.useAutoRandomSeed = false;
                ps.randomSeed = 1234u;
                var emission = ps.emission;
                for (int b = 0; b < emission.burstCount; b++)
                {
                    var burst = emission.GetBurst(b);
                    var count = burst.count;
                    count.constant = Mathf.Max(1, Mathf.RoundToInt(count.constant * countScale));
                    burst.count = count;
                    emission.SetBurst(b, burst);
                }
            }
        }

        private static float Duration(ParticleSystem prefab)
        {
            float longest = 0f;
            foreach (var ps in prefab.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main;
                float life = main.startLifetime.mode == ParticleSystemCurveMode.TwoConstants ? main.startLifetime.constantMax : main.startLifetime.constant;
                longest = Mathf.Max(longest, main.startDelay.constant + main.duration + life);
            }
            return longest;
        }

        private static Camera BuildStage()
        {
            var lightGo = new GameObject("CaptureLight");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.15f;
            light.color = new Color(1f, 0.96f, 0.9f);
            light.shadows = LightShadows.Soft;
            lightGo.transform.rotation = Quaternion.Euler(48f, -35f, 0f);

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", new Color(0.55f, 0.72f, 0.45f));
            ground.GetComponent<Renderer>().sharedMaterial = material;

            var cameraGo = new GameObject("CaptureCamera");
            var camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.62f, 0.76f, 0.86f);
            camera.fieldOfView = 30f;
            camera.nearClipPlane = 0.3f;
            // The refuge rig at its hatch reveal framing (RefugeCameraRig: pitch 40, distance 9.5).
            var focus = new Vector3(0f, 0.3f, 0f);
            var rotation = Quaternion.Euler(40f, 0f, 0f);
            cameraGo.transform.SetPositionAndRotation(focus - rotation * Vector3.forward * 9.5f, rotation);
            return camera;
        }
    }
}
