using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Editor.Animals;
using PocketWilds.Editor.Profile;
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
    /// T44B « Succès, titres, cadres, poses » captures (doc « 01 Succès, titres, cadres, poses et journal », sections 1, 3, 4,
    /// 5) at the reference 1080 × 2340, in French and English: the card with the Succès tab (badges in their four shapes, a
    /// claim waiting), each tab (Titres with « Aucun titre » and locked sources, Cadres around the portrait, Poses with « Mode
    /// photo bientôt »), and in French a category filter, the badges scrolled down (recycled cells) and a claim with its
    /// « +35 Gemmes ». Over scripted achievements and items in the contract's shape (the real frame keys of agent BA's
    /// FrameArt, the real title and pose keys of agent BC's table when it exists). Files in Docs/Captures.
    /// </summary>
    public static class ProfileCapture
    {
        private const int Width = 1080;
        private const int Height = 2340;
        private const float Step = 1f / 30f;

        [MenuItem("Pocket Wilds/Capture/Profile (T44B)", priority = 538)]
        public static void CaptureMenu() => Debug.Log("[ProfileCapture] " + Capture("T44B_profile"));

        public static string Capture(string prefix)
        {
            var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (active.path != WorldCapture.ScenePath && !string.IsNullOrEmpty(active.path) && active.isDirty)
                return "refused: save or discard changes in " + active.path;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ProfileScreenPipeline.PrefabPath);
            if (prefab == null)
                return "no screen prefab: run Pocket Wilds/Profile/Build Profile Screen (T44B) first";
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
            var portrait = FindPortrait();
            var shots = new List<string>();
            var problems = new List<string>();

            GameObject cameraObject = null;
            ProfileScreenPresenter presenter = null;
            try
            {
                cameraObject = new GameObject("ProfileCaptureCamera") { hideFlags = HideFlags.HideAndDontSave };
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
                    var context = new GameContext(null, registry, null, null, null, null, null, null, null, null, null, null, localization: text);
                    var achievements = new CaptureAchievements();
                    var items = new CaptureItems();
                    presenter?.Dispose();
                    presenter = new ProfileScreenPresenter(context, achievements, items, null, null, prefab)
                    {
                        Portrait = () => portrait,
                        ReduceMotionOverride = false,
                    };
                    presenter.OpenProfile();
                    var view = presenter.View as ProfileScreenView;
                    if (view == null)
                        return "the screen prefab has no ProfileScreenView (rebuild it)";
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
                        for (int pass = 0; pass < 2; pass++)
                        {
                            string written = CaptureUtility.Render(camera, Width, Height, path, 4, () =>
                            {
                                Refresh();
                                presenter.Tick(0f);
                                Canvas.ForceUpdateCanvases();
                            });
                            if (pass == 1)
                                shots.Add(written);
                        }
                    }

                    // 1. The card and the Succès tab: the four shapes, a claim waiting, « Plus que … ».
                    Refresh();
                    Run(0.3f);
                    if (presenter.Tab != ProfileTab.Achievements || presenter.FilteredCount != CaptureAchievements.Total)
                        problems.Add(language + ": the Succès tab did not open with the 200 achievements");
                    Shot("achievements_" + language);

                    if (language == "fr")
                    {
                        // 2. A claim: the badge pops, « +35 Gemmes ».
                        presenter.Claim(2);
                        Run(0.2f);
                        if (!view.NoteShown)
                            problems.Add("no Gems note after the claim");
                        Shot("claim");
                        Run(2.6f);

                        // 3. The badges scrolled down: the same 12 cells show other achievements.
                        var content = view.Parts.badgeContent;
                        content.anchoredPosition = new Vector2(content.anchoredPosition.x, 30f * ProfileScreenView.BadgePitchY);
                        Run(0.1f);
                        if (view.CellOf(ProfileList.Badges, 60) < 0)
                            problems.Add("the scrolled badges were not recycled");
                        Shot("achievements_scrolled");

                        // 4. A category: Biomes.
                        presenter.SelectCategory(1 + (int)AchievementCategory.Biomes);
                        Run(0.2f);
                        Shot("achievements_biomes");
                        presenter.SelectCategory(0);
                    }

                    // 5. Titres: « Aucun titre », owned ones (one chosen), locked ones with their source.
                    presenter.SelectTab(ProfileTab.Titles);
                    Run(0.2f);
                    Shot("titles_" + language);

                    // 6. Cadres around the portrait (one chosen).
                    presenter.SelectTab(ProfileTab.Frames);
                    Run(0.2f);
                    Shot("frames_" + language);

                    // 7. Poses with « Mode photo bientôt ».
                    presenter.SelectTab(ProfileTab.Poses);
                    Run(0.2f);
                    Shot("poses_" + language);
                    presenter.CloseScreen();
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

        /// <summary>A codex portrait to stand for the favorite animal (the first animal thumbnail found; none: the paw).</summary>
        private static Sprite FindPortrait()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Sprite", new[] { "Assets/_Game" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains("/Thumbnails/"))
                    return AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }
            return null;
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

        /// <summary>The keys of agent BC's table with <paramref name="prefix"/> (their « .name » rows), in table order.</summary>
        private static List<string> KeysOf(string prefix)
        {
            var table = ProfileScreenTexts.ContentNames();
            var keys = new List<string>();
            for (int i = 0; i < table.GetLength(0); i++)
            {
                string key = table[i, 0];
                if (key != null && key.StartsWith(prefix, StringComparison.Ordinal) && key.EndsWith(".name", StringComparison.Ordinal))
                    keys.Add(key.Substring(0, key.Length - ".name".Length));
            }
            return keys;
        }

        /// <summary>200 achievements in the doc's categories (section 1 « Répartition ») at every stage: locked, Bronze to Star, claims waiting.</summary>
        private sealed class CaptureAchievements : IAchievements
        {
            public const int Total = 200;
            private static readonly int[] PerCategory = { 20, 52, 10, 12, 26, 10, 12, 10, 12, 12, 10, 8, 6 };
            private readonly AchievementInfo[] _infos = new AchievementInfo[Total];
            private readonly long[] _progress = new long[Total];
            private readonly AchievementTier[] _claimed = new AchievementTier[Total];

            public CaptureAchievements()
            {
                int index = 0;
                for (int c = 0; c < PerCategory.Length; c++)
                    for (int k = 0; k < PerCategory[c] && index < Total; k++, index++)
                    {
                        long scale = 1 + (index % 4) * 4;
                        _infos[index] = new AchievementInfo(index, "achievement." + (index + 1).ToString("000"), (AchievementCategory)c, 1 * scale, 10 * scale,
                            50 * scale, 200 * scale);
                        // A stage per achievement: locked, partway, Bronze, Silver, Gold, Star.
                        long[] stages = { 0, 7 * scale, 12 * scale, 60 * scale, 210 * scale, 3 * scale };
                        _progress[index] = stages[(index * 7) % stages.Length];
                    }
                for (int i = 0; i < Total; i++)
                    _claimed[i] = Reached(i);
                // Claims waiting: the first one's Silver, the third's Bronze to Gold (+35), a Star elsewhere.
                _claimed[0] = AchievementTier.Bronze;
                _progress[0] = _infos[0].Silver;
                _progress[2] = _infos[2].Gold;
                _claimed[2] = AchievementTier.None;
                _progress[5] = _infos[5].Star;
                _claimed[5] = AchievementTier.Gold;
            }

            public int Count => Total;

            public AchievementInfo At(int index) => _infos[index];

            public long Progress(int index) => _progress[index];

            public AchievementTier Reached(int index)
            {
                var info = _infos[index];
                long p = _progress[index];
                return p >= info.Star ? AchievementTier.Star : p >= info.Gold ? AchievementTier.Gold : p >= info.Silver ? AchievementTier.Silver
                    : p >= info.Bronze ? AchievementTier.Bronze : AchievementTier.None;
            }

            public AchievementTier Claimed(int index) => _claimed[index];

            public int Claimable
            {
                get
                {
                    int n = 0;
                    for (int i = 0; i < Total; i++)
                        n += Math.Max(0, (int)Reached(i) - (int)_claimed[i]);
                    return n;
                }
            }

            public bool TryClaim(int index)
            {
                if (_claimed[index] >= Reached(index))
                    return false;
                _claimed[index] = Reached(index);
                Changed?.Invoke();
                return true;
            }

            public event Action Changed;
        }

        /// <summary>322 titles, 42 frames (agent BA's real keys) and 47 poses; some owned, one title and one frame chosen.</summary>
        private sealed class CaptureItems : IProfileItems
        {
            private readonly ProfileItemInfo[][] _infos = new ProfileItemInfo[3][];
            private readonly bool[][] _owned = new bool[3][];
            private readonly bool[][] _unseen = new bool[3][];
            private readonly int[] _selected = { 2, 1, -1 };

            public CaptureItems()
            {
                Fill(ProfileItemKind.Title, 322, KeysOf("title."), "title.capture_");
                Fill(ProfileItemKind.Frame, 42, FrameArt.All.Select(f => f.Key).ToList(), "frame.capture_");
                Fill(ProfileItemKind.Pose, 47, KeysOf("pose."), "pose.capture_");
                for (int i = 0; i < 9; i++)
                    _owned[0][i * 3] = true;
                _owned[0][2] = true;
                _unseen[0][6] = true;
                for (int i = 0; i < 6; i++)
                    _owned[1][i] = true;
                _unseen[1][4] = true;
                for (int i = 0; i < 8; i++)
                    _owned[2][i] = true;
                _unseen[2][1] = true;
            }

            private void Fill(ProfileItemKind kind, int count, List<string> keys, string fallback)
            {
                int k = (int)kind;
                _infos[k] = new ProfileItemInfo[count];
                _owned[k] = new bool[count];
                _unseen[k] = new bool[count];
                for (int i = 0; i < count; i++)
                    _infos[k][i] = new ProfileItemInfo(i, i < keys.Count ? keys[i] : fallback + (i + 1), i);
            }

            public int Count(ProfileItemKind kind) => _infos[(int)kind].Length;

            public ProfileItemInfo At(ProfileItemKind kind, int index) => _infos[(int)kind][index];

            public bool Owned(ProfileItemKind kind, int index) => _owned[(int)kind][index];

            public int Selected(ProfileItemKind kind) => kind == ProfileItemKind.Pose ? -1 : _selected[(int)kind];

            public bool TrySelect(ProfileItemKind kind, int index)
            {
                if (kind == ProfileItemKind.Pose || (index >= 0 && !_owned[(int)kind][index]) || (index < 0 && kind != ProfileItemKind.Title))
                    return false;
                _selected[(int)kind] = index;
                Changed?.Invoke();
                return true;
            }

            public bool Unseen(ProfileItemKind kind, int index) => _unseen[(int)kind][index];

            public void MarkSeen(ProfileItemKind kind)
            {
                Array.Clear(_unseen[(int)kind], 0, _unseen[(int)kind].Length);
                Changed?.Invoke();
            }

            public event Action Changed;
        }
    }
}
