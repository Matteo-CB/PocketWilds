using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Editor.Animals;
using PocketWilds.Editor.Journal;
using PocketWilds.Editor.UI;
using PocketWilds.Presentation;
using PocketWilds.Services.Localization;
using PocketWilds.Simulation;
using PocketWilds.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace PocketWilds.Editor.Capture
{
    /// <summary>
    /// T44C « Journal du refuge » captures (doc « 01 Succès, titres, cadres, poses et journal », section 6) at the reference
    /// 1080 × 2340, in French and English: the timeline with its month headers and pages (a real refuge view captured from the
    /// scene's camera and stored as journal_{id}.jpg through the journal's store, the animals' codex portraits standing in for
    /// portraits not captured yet, the systems' icons for the other moments, « Nouveau » on the newest pages), the « Moments »
    /// tab with the surprises seen, and in French its empty state. Over a scripted journal in the contract's shape (agent CC's
    /// real memory and surprise keys when its JournalTexts exists). Files in Docs/Captures.
    /// </summary>
    public static class JournalCapture
    {
        private const int Width = 1080;
        private const int Height = 2340;
        private const float Step = 1f / 30f;

        [MenuItem("Pocket Wilds/Capture/Journal (T44C)", priority = 539)]
        public static void CaptureMenu() => Debug.Log("[JournalCapture] " + Capture("T44C_journal"));

        public static string Capture(string prefix)
        {
            var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (active.path != WorldCapture.ScenePath && !string.IsNullOrEmpty(active.path) && active.isDirty)
                return "refused: save or discard changes in " + active.path;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(JournalScreenPipeline.PrefabPath);
            if (prefab == null)
                return "no screen prefab: run Pocket Wilds/Journal/Build Journal Screen (T44C) first";
            EditorSceneManager.OpenScene(WorldCapture.ScenePath, OpenSceneMode.Single);
            UiPipeline.EnsureLocalization();
            var database = AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>(AnimalPipeline.ContentDatabasePath);
            var registry = database.CreateBuilder().Build();
            var handle = typeof(CanvasScaler).GetMethod("Handle", BindingFlags.Instance | BindingFlags.NonPublic);
            var texts = new List<(string language, LocalizationService text)>();
            foreach (string language in UiPipeline.Languages)
                texts.Add((language, new LocalizationService(database.localization, language, SystemLanguage.English)));
            var sprites = LoadAll<Sprite>("t:Sprite", "Assets/_Game/Art/UI/Kit", "Assets/_Game/Art/UI/Icons");
            var fonts = LoadAll<TMP_FontAsset>("t:TMP_FontAsset", "Assets/_Game/Art/UI/Fonts");
            var materials = LoadAll<Material>("t:Material", "Assets/_Game/Art/UI/Fonts");
            var variants = CodexVariants();
            var shots = new List<string>();
            var problems = new List<string>();
            if (JournalScreenTexts.ContentOf("memory.", ".text").Length == 0)
                problems.Add("agent CC's JournalTexts not found: the pages show their keys");
            if (JournalScreenTexts.ContentOf("surprise.", ".name").Length == 0)
                problems.Add("no surprise names: run Tools/Data/pw_journal_data.py");

            string folder = Path.Combine(Application.temporaryCachePath, "JournalCapture");
            var store = new JournalImageStore(folder);
            GameObject cameraObject = null;
            JournalScreenPresenter presenter = null;
            try
            {
                if (Directory.Exists(store.Folder))
                    Directory.Delete(store.Folder, true);
                bool refugeShot = StoreRefugeView(store, 1, registry, problems);

                cameraObject = new GameObject("JournalCaptureCamera") { hideFlags = HideFlags.HideAndDontSave };
                var camera = cameraObject.AddComponent<Camera>();
                camera.enabled = false;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.78f, 0.86f, 0.70f, 1f);
                camera.cullingMask = 1 << 5;
                camera.orthographic = true;
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 50f;
                camera.aspect = Width / (float)Height;
                var refugeHud = Object.FindAnyObjectByType<RefugeHud>(FindObjectsInactive.Include);
                if (refugeHud != null)
                    refugeHud.gameObject.SetActive(false);

                foreach (var (language, text) in texts)
                {
                    for (int pass = 0; pass < (language == "fr" ? 2 : 1); pass++)
                    {
                        bool empty = pass == 1;
                        var context = new GameContext(null, registry, null, null, null, null, null, null, null, null, null, null, localization: text);
                        var journal = new CaptureJournal(language, variants, refugeShot, empty);
                        presenter?.Dispose();
                        presenter = new JournalScreenPresenter(context, journal, null, prefab, store) { ReduceMotionOverride = false };
                        presenter.OpenJournal();
                        var view = presenter.View as JournalScreenView;
                        if (view == null)
                            return "the screen prefab has no JournalScreenView (rebuild it)";
                        view.Lend(sprites, fonts, materials);
                        var canvas = view.Canvas;
                        canvas.renderMode = RenderMode.ScreenSpaceCamera;
                        canvas.worldCamera = camera;
                        canvas.planeDistance = 2f;

                        void Refresh()
                        {
                            handle?.Invoke(canvas.GetComponent<CanvasScaler>(), null);
                            Canvas.ForceUpdateCanvases();
                        }

                        void Run(float seconds)
                        {
                            int frames = Mathf.Max(1, Mathf.RoundToInt(seconds / Step));
                            for (int f = 0; f < frames; f++)
                                presenter.Tick(Step);
                        }

                        void Shot(string label)
                        {
                            string path = $"{CaptureUtility.DefaultFolder}/{prefix}_{label}.png";
                            for (int k = 0; k < 2; k++)
                            {
                                string written = CaptureUtility.Render(camera, Width, Height, path, 4, () =>
                                {
                                    Refresh();
                                    presenter.Tick(0f);
                                    Canvas.ForceUpdateCanvases();
                                });
                                if (k == 1)
                                    shots.Add(written);
                            }
                        }

                        Refresh();
                        presenter.SelectTab(JournalTab.Pages);
                        Refresh();
                        Run(0.5f);
                        if (!empty)
                        {
                            // 1. The timeline: months, pages, the stored refuge view, portraits and icons, « Nouveau ».
                            if (presenter.TimelineCount < journal.PageCount + 2)
                                problems.Add(language + ": the timeline has no month headers");
                            if (refugeShot && !presenter.PictureLoaded(presenter.ItemOfPage(0)))
                                problems.Add(language + ": the stored refuge view is not on its card");
                            Shot("timeline_" + language);
                        }
                        // 2. « Moments »: the surprises seen (or, in French, the empty state).
                        presenter.SelectTab(JournalTab.Moments);
                        Refresh();
                        Run(0.3f);
                        if (empty && !view.Parts.momentsEmpty.gameObject.activeSelf)
                            problems.Add("fr: the Moments empty state does not show");
                        Shot((empty ? "moments_empty_" : "moments_") + language);
                    }
                }
            }
            catch (Exception e)
            {
                problems.Add(e.GetType().Name + ": " + e.Message);
            }
            finally
            {
                presenter?.Dispose();
                if (cameraObject != null)
                    Object.DestroyImmediate(cameraObject);
                EditorSceneManager.OpenScene(WorldCapture.ScenePath, OpenSceneMode.Single);
            }
            return string.Join(", ", shots) + (problems.Count > 0 ? " PROBLEMS: " + string.Join("; ", problems) : "");
        }

        /// <summary>The scene's camera drawn once into 512 × 512, encoded as the capture layer does and stored as journal_{id}.jpg.</summary>
        private static bool StoreRefugeView(JournalImageStore store, int pageId, ContentRegistry registry, List<string> problems)
        {
            var camera = Camera.main != null ? Camera.main : Object.FindAnyObjectByType<Camera>();
            if (camera == null)
            {
                problems.Add("no scene camera for the refuge view");
                return false;
            }
            // The start chunk is streamed at run time: instance it, framed and lit as the player sees it (WorldCapture's way;
            // the scene is reopened from disk afterwards, nothing is saved).
            using var palettes = new PaletteTable(registry);
            var chunk = WorldCapture.SceneChunk();
            chunk.Bind(palettes);
            WorldCapture.FrameLikeGame(chunk);
            var entry = Object.FindAnyObjectByType<VerticalSliceEntry>();
            if (entry != null)
                entry.Lighting.Apply(Object.FindAnyObjectByType<Light>(), camera, QualityTier.Medium);
            var target = RenderTexture.GetTemporary(JournalLayout.PictureSize, JournalLayout.PictureSize, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB, 1);
            var previous = camera.targetTexture;
            var texture = new Texture2D(JournalLayout.PictureSize, JournalLayout.PictureSize, TextureFormat.RGB24, false, false);
            try
            {
                camera.targetTexture = target;
                camera.aspect = 1f;
                camera.Render();
                var active = RenderTexture.active;
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, JournalLayout.PictureSize, JournalLayout.PictureSize), 0, 0, false);
                texture.Apply(false, false);
                RenderTexture.active = active;
                store.Write(pageId, texture.EncodeToJPG(JournalLayout.JpegQuality));
                return true;
            }
            catch (Exception e)
            {
                problems.Add("refuge view: " + e.Message);
                return false;
            }
            finally
            {
                camera.targetTexture = previous;
                camera.ResetAspect();
                RenderTexture.ReleaseTemporary(target);
                Object.DestroyImmediate(texture);
            }
        }

        /// <summary>The codex catalog's variants (loaded, so the screen finds it), as content ids.</summary>
        private static List<ContentId> CodexVariants()
        {
            var ids = new List<ContentId>();
            foreach (string guid in AssetDatabase.FindAssets("t:CodexVisualCatalog"))
            {
                var catalog = AssetDatabase.LoadAssetAtPath<CodexVisualCatalog>(AssetDatabase.GUIDToAssetPath(guid));
                if (catalog == null || catalog.variants == null)
                    continue;
                foreach (var entry in catalog.variants)
                    if (!string.IsNullOrEmpty(entry.sourceId) && entry.sprite != null)
                        ids.Add(ContentId.FromSource(entry.sourceId));
            }
            return ids;
        }

        private static List<T> LoadAll<T>(string filter, params string[] folders) where T : Object
        {
            var result = new List<T>();
            foreach (string guid in AssetDatabase.FindAssets(filter, folders))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                    if (asset is T typed)
                        result.Add(typed);
            }
            return result;
        }

        /// <summary>Agent CC's keys with <paramref name="prefix"/> (their <paramref name="suffix"/> rows), in table order.</summary>
        private static List<string> KeysOf(string prefix, string suffix)
        {
            var table = JournalScreenTexts.ContentTexts();
            var keys = new List<string>();
            for (int i = 0; i < table.GetLength(0); i++)
            {
                string key = table[i, 0];
                if (key != null && key.StartsWith(prefix, StringComparison.Ordinal) && key.EndsWith(suffix, StringComparison.Ordinal))
                    keys.Add(key.Substring(0, key.Length - suffix.Length));
            }
            return keys;
        }

        /// <summary>Twelve pages over three months (newest first): the refuge view stored, portraits, icons; nine surprises seen.</summary>
        private sealed class CaptureJournal : IJournal
        {
            private static readonly DateTime Epoch = new DateTime(2000, 1, 1);
            private readonly JournalPage[] _pages;
            private readonly List<string> _surprises;
            private readonly bool _french;
            private int _unseen = 3;

            public CaptureJournal(string language, List<ContentId> variants, bool refugeStored, bool noMoments)
            {
                _french = language == "fr";
                // A believable first month and a half, newest first: the refuge view on its level up, the animals' portraits on
                // the pages about one animal (its name stands for any of them), the moments' icons elsewhere.
                string[] keys =
                {
                    "memory.refuge_level", "memory.first_name", "memory.first_visitor", "memory.first_harvest", "memory.first_favorite",
                    "memory.postcard", "memory.first_activity", "memory.first_rare", "memory.zone_opened", "memory.first_hatch",
                    "memory.biome_unlocked", "memory.first_egg",
                };
                bool[] portrait = { false, true, false, false, true, false, false, true, false, true, false, false };
                int today = (int)(new DateTime(2026, 10, 12) - Epoch).TotalDays;
                int[] gaps = { 0, 1, 3, 6, 11, 17, 24, 30, 38, 47, 55, 63 };
                _pages = new JournalPage[gaps.Length];
                for (int i = 0; i < gaps.Length; i++)
                {
                    var image = JournalImageKind.Thumbnail;
                    uint reference = 0u;
                    bool captured = false;
                    if (i == 0 && refugeStored)
                    {
                        image = JournalImageKind.RefugeCapture;
                        captured = true;
                    }
                    else if (portrait[i] && variants.Count > 0)
                    {
                        reference = variants[(i * 7) % variants.Count].Value;
                    }
                    _pages[i] = new JournalPage(i + 1, keys[i], today - gaps[i], image, reference, captured);
                }
                _surprises = noMoments ? new List<string>() : KeysOf("surprise.", ".name");
                if (_surprises.Count > 9)
                    _surprises.RemoveRange(9, _surprises.Count - 9);
            }

            public int PageCount => _pages.Length;

            public JournalPage PageAt(int index) => _pages[index];

            public int Variables(int pageId, string[] names, string[] values)
            {
                string[,] pairs =
                {
                    { "name", "Noisette" }, { "species", _french ? "Lapin des prés" : "Meadow rabbit" }, { "variant", _french ? "Pétale" : "Petal" },
                    { "biome", _french ? "Prairie" : "Meadow" }, { "zone", _french ? "Clairière" : "Clearing" }, { "count", "12" }, { "level", "8" },
                    { "visitor", _french ? "Hérisson fleuri" : "Flower hedgehog" }, { "destination", _french ? "Prés fleuris" : "Flower meadows" },
                    { "activity", _french ? "Cache-cache" : "Hide and seek" }, { "score", "1240" }, { "plant", _french ? "Pâquerette" : "Daisy" },
                };
                int n = Math.Min(names.Length, pairs.GetLength(0));
                for (int i = 0; i < n; i++)
                {
                    names[i] = pairs[i, 0];
                    values[i] = pairs[i, 1];
                }
                return n;
            }

            public int PendingCaptureCount => 0;

            public JournalPage PendingCaptureAt(int index) => default;

            public void CaptureStored(int pageId)
            {
            }

            public int SeenSurpriseCount => _surprises.Count;

            public string SeenSurpriseAt(int index) => _surprises[index];

            public int UnseenCount => _unseen;

            public void MarkSeen() => _unseen = 0;

            public event Action PageAdded
            {
                add { }
                remove { }
            }
        }
    }
}
