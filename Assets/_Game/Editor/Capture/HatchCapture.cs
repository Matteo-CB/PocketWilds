using System.IO;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Editor.Animals;
using PocketWilds.Editor.Eggs;
using PocketWilds.Presentation;
using PocketWilds.Simulation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PocketWilds.Editor.Capture
{
    /// <summary>
    /// ED-008 hatch review sheet: renders key moments of the real hatch staging (real prefabs, shaders,
    /// palettes and VFX) side by side in a throwaway scene. Deterministic: the sequence is ticked at a
    /// fixed step and particles are simulated to the exact moment.
    /// </summary>
    public static class HatchCapture
    {
        public const string DefaultPath = CaptureUtility.DefaultFolder + "/T14_hatch_sheet.png";

        [MenuItem("Pocket Wilds/Capture/Hatch Sheet", priority = 520)]
        public static void CaptureMenu() => Debug.Log("[HatchCapture] " + CaptureSheet(DefaultPath, "egg.grassland.basic", "variant.rabbit.brown", RarityTier.Common));

        public static string CaptureSheet(string path, string eggId, string variantId, RarityTier rarity)
        {
            // Never prompt (batch and MCP callers): refuse when a saved scene has unsaved changes.
            var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (!string.IsNullOrEmpty(active.path) && active.isDirty)
                return "refused: save or discard changes in " + active.path;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var database = AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>(AnimalPipeline.ContentDatabasePath);
            var registry = database.CreateBuilder().Build();
            var config = AssetDatabase.LoadAssetAtPath<HatchPresentationConfig>(EggPipeline.HatchConfigPath);
            var catalog = AssetDatabase.LoadAssetAtPath<AnimalVisualCatalog>(AnimalPipeline.VisualCatalogPath);
            var egg = registry.Eggs.Get(ContentId.FromSource(eggId));
            var variant = registry.Variants.Get(ContentId.FromSource(variantId));
            registry.Species.TryGet(variant.Species, out var species);
            catalog.TryGet(species.VisualKey, out var visual);

            var camera = BuildStage();
            float burst = config.anticipation + config.afterCrack1 + config.afterCrack2;
            float[] times = { 0.3f, config.anticipation + 0.08f, burst - 0.04f, burst + 0.1f, burst + 0.3f, burst + 0.8f };
            const int width = 360, height = 480;
            var sheet = new Texture2D(width * times.Length, height, TextureFormat.RGB24, false);
            var target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB, 4);
            using (var palettes = new PaletteTable(registry))
            {
                var resolver = new AnimalVisualResolver(registry, palettes);
                palettes.TryGetRow(egg.Id, out int row);
                uint eggVisual = EggVisualPacking.FromDefinition(egg, row);
                try
                {
                    for (int f = 0; f < times.Length; f++)
                    {
                        RenderMoment(config, visual.prefab, resolver, eggVisual, variant, rarity, times[f], burst, camera, target);
                        RenderTexture.active = target;
                        sheet.ReadPixels(new Rect(0, 0, width, height), width * f, 0);
                        RenderTexture.active = null;
                    }
                    sheet.Apply();
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    File.WriteAllBytes(path, sheet.EncodeToPNG());
                }
                finally
                {
                    RenderTexture.ReleaseTemporary(target);
                    Object.DestroyImmediate(sheet);
                }
            }
            return path;
        }

        private static void RenderMoment(HatchPresentationConfig config, AnimalView animalPrefab, AnimalVisualResolver resolver,
            uint eggVisual, VariantDefinition variant, RarityTier rarity, float moment, float burst, Camera camera, RenderTexture target)
        {
            var egg = Object.Instantiate(config.eggPrefab);
            egg.Configure(Vector3.zero, 180f, eggVisual);
            var instance = new AnimalInstance { InstanceId = 1, Seed = 7, Species = variant.Species, Variant = variant.Id };
            resolver.TryResolve(instance, Vector3.zero, 180f, out var snapshot);
            var animal = Object.Instantiate(animalPrefab);
            animal.Configure(snapshot);
            GameObject effect = null;
            try
            {
                // No pooled VFX here: effects are simulated to the exact moment below.
                var player = new HatchSequencePlayer(config, null, null, null);
                player.Present(egg);
                player.Hatch(animal, variant.Species, rarity, false, null);
                const float step = 1f / 60f;
                float t = 0f;
                while (t + step <= moment)
                {
                    t += step;
                    player.Tick(t, step);
                }

                var animator = animal.Animator;
                animator.Rebind();
                if (moment > burst + config.happyDelay)
                {
                    animator.Play(AnimalStateHashes.Happy, 0, 0f);
                    animator.Update(moment - burst - config.happyDelay);
                }
                else
                {
                    animator.Update(0f);
                }

                ParticleSystem prefab = null;
                float age = 0f;
                if (moment >= burst)
                {
                    // The rarity accent when there is one (it carries the rarity's colors), else the common burst.
                    var accent = HatchSequencePlayer.RarityVfx(rarity);
                    prefab = config.vfx.Prefab(accent != VfxId.Count ? accent : VfxId.HatchCommon);
                    age = moment - burst;
                }
                else if (moment >= config.anticipation)
                {
                    prefab = config.vfx.Prefab(VfxId.Crack);
                    age = moment - config.anticipation;
                }
                if (prefab != null)
                {
                    var system = Object.Instantiate(prefab, egg.VfxPoint.position, Quaternion.identity);
                    effect = system.gameObject;
                    system.Simulate(age, true, true);
                }

                camera.targetTexture = target;
                camera.Render();
                camera.targetTexture = null;
            }
            finally
            {
                Object.DestroyImmediate(egg.gameObject);
                Object.DestroyImmediate(animal.gameObject);
                if (effect != null)
                    Object.DestroyImmediate(effect);
            }
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
            camera.nearClipPlane = 0.05f;
            cameraGo.transform.position = new Vector3(0f, 0.85f, -1.75f);
            cameraGo.transform.LookAt(new Vector3(0f, 0.33f, 0f));
            return camera;
        }
    }
}
