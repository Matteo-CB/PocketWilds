using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Editor.Albums;
using PocketWilds.Editor.Animals;
using PocketWilds.Editor.UI;
using PocketWilds.Presentation;
using PocketWilds.Services.Localization;
using PocketWilds.Simulation;
using PocketWilds.UI;
using TMPro;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.U2D;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace PocketWilds.Editor.Capture
{
    /// <summary>
    /// T44A « Albums » captures (doc « 01 Albums et autocollants », sections 1.10 and 1.12) at the reference 1080 × 2340, in
    /// French and English: the shelf, a full page (Common, Rare and Shiny with their signs, « Nouveau »), a partial page
    /// (silhouettes and their sources' icons, « 6/12 »), a page with « bientôt » slots, the choose list and its confirmation,
    /// and the reveal (a Rare turning over, a Shiny with its pearl sheen, a duplicate « Déjà collé »). Over scripted albums
    /// (the 16 albums, 60 pages, 720 stickers in the contract's shape); the sticker art comes from the page atlases of
    /// DEF_StickerArt when they are captured, else the silhouettes show. Files in Docs/Captures.
    /// </summary>
    public static class AlbumCapture
    {
        private const int Width = 1080;
        private const int Height = 2340;
        private const float Step = 1f / 30f;

        [MenuItem("Pocket Wilds/Capture/Albums (T44A)", priority = 537)]
        public static void CaptureMenu() => Debug.Log("[AlbumCapture] " + Capture("T44A_albums"));

        public static string Capture(string prefix)
        {
            var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (active.path != WorldCapture.ScenePath && !string.IsNullOrEmpty(active.path) && active.isDirty)
                return "refused: save or discard changes in " + active.path;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AlbumScreenPipeline.PrefabPath);
            if (prefab == null)
                return "no screen prefab: run Pocket Wilds/Albums/Build Albums Screen (T44A) first";
            EditorSceneManager.OpenScene(WorldCapture.ScenePath, OpenSceneMode.Single);
            var database = AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>(AnimalPipeline.ContentDatabasePath);
            var registry = database.CreateBuilder().Build();
            var handle = typeof(CanvasScaler).GetMethod("Handle", BindingFlags.Instance | BindingFlags.NonPublic);
            var texts = new List<(string language, LocalizationService text)>();
            foreach (string language in UiPipeline.Languages)
                texts.Add((language, new LocalizationService(database.localization, language, SystemLanguage.English)));
            var sprites = LoadAll<Sprite>("t:Sprite", "Assets/_Game/Art/UI/Kit", "Assets/_Game/Art/UI/Icons");
            var fonts = LoadAll<TMP_FontAsset>("t:TMP_FontAsset", "Assets/_Game/Art/UI/Fonts");
            var materials = LoadAll<Material>("t:Material", "Assets/_Game/Art/UI/Fonts");
            var catalog = AssetDatabase.LoadAssetAtPath<StickerArtCatalog>(StickerArtCatalog.AssetPath);
            var shots = new List<string>();
            var problems = new List<string>();

            GameObject cameraObject = null;
            AlbumScreenPresenter presenter = null;
            try
            {
                cameraObject = new GameObject("AlbumCaptureCamera") { hideFlags = HideFlags.HideAndDontSave };
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
                    var albums = new CaptureAlbums();
                    presenter?.Dispose();
                    presenter = new AlbumScreenPresenter(context, albums, null, null, prefab)
                    {
                        CatalogOverride = catalog,
                        AtlasOverride = LoadAtlas,
                        ReduceMotionOverride = false,
                    };
                    presenter.OpenAlbums();
                    var view = presenter.View as AlbumScreenView;
                    if (view == null)
                        return "the screen prefab has no AlbumScreenView (rebuild it)";
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

                    // 1. The shelf: 16 covers, the stars, « Choisir un autocollant » from 10 stars.
                    Refresh();
                    Run(0.3f);
                    if (presenter.Mode != AlbumScreenMode.Shelf)
                        problems.Add(language + ": the shelf did not open");
                    Shot("shelf_" + language);

                    // 2. A full page (the Prairie's first): every rarity with its sign, two « Nouveau ».
                    presenter.OpenAlbum(0);
                    Run(0.4f);
                    if (presenter.PageShown != 0)
                        problems.Add(language + ": page 0 not shown");
                    if (language == "fr")
                        Shot("page_full");

                    // 3. A partial page: silhouettes with their sources' icons, « 6/12 ».
                    presenter.NextPage();
                    Run(0.4f);
                    if (view.RatioText != "6/12")
                        problems.Add($"{language}: ratio « {view.RatioText} »");
                    Shot("page_partial_" + language);

                    // 4. A page with « bientôt » slots (subjects not in this build yet).
                    presenter.NextPage();
                    Run(0.4f);
                    if (language == "fr")
                        Shot("page_soon");

                    if (language == "fr")
                    {
                        // 5. The choose list, then a missing sticker's confirmation.
                        presenter.Back();
                        Run(0.2f);
                        presenter.OpenChoose();
                        Run(0.3f);
                        if (presenter.Mode != AlbumScreenMode.ChooseList || presenter.ChoiceRowCount == 0)
                            problems.Add("the choose list is empty");
                        Shot("choose_list");
                        presenter.PickChoicePage(0);
                        Run(0.3f);
                        int slot = albums.FirstMissingSlot(presenter.PageShown);
                        presenter.TapSlot(slot);
                        Run(0.2f);
                        if (!view.ConfirmShown)
                            problems.Add("no confirmation");
                        Shot("choose_confirm");
                        presenter.CancelChoice();
                        presenter.Back();
                        presenter.Back();
                        Run(0.2f);
                    }

                    // 6. The reveal over the shelf: a Rare turning over, a Shiny with its sheen, a duplicate.
                    if (presenter.Mode != AlbumScreenMode.Shelf)
                        presenter.Back();
                    albums.Gain(CaptureAlbums.StickerOf(5, 9), true, 0);
                    Run(StickerRevealTiming.PouchIn + 0.3f);
                    if (language == "fr")
                        Shot("reveal_pouch");
                    presenter.TapReveal();
                    Run(0.2f);
                    if (!presenter.Revealing)
                        problems.Add(language + ": the reveal did not start");
                    Shot("reveal_rare_" + language);
                    Run(2f);
                    if (language == "fr")
                    {
                        albums.Gain(CaptureAlbums.StickerOf(5, 11), true, 0);
                        Run(StickerRevealTiming.PouchIn + 0.1f);
                        presenter.TapReveal();
                        Run(0.62f);
                        Shot("reveal_shiny");
                        Run(2f);
                        albums.Gain(CaptureAlbums.StickerOf(0, 2), false, 1);
                        Run(StickerRevealTiming.PouchIn + 0.1f);
                        presenter.TapReveal();
                        Run(0.3f);
                        if (string.IsNullOrEmpty(view.DuplicateText))
                            problems.Add("no « Déjà collé »");
                        Shot("reveal_duplicate");
                        Run(2f);
                    }
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

        /// <summary>A page atlas by its Addressables address (edit mode: straight from the asset).</summary>
        private static SpriteAtlas LoadAtlas(string address)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null || string.IsNullOrEmpty(address))
                return null;
            foreach (var group in settings.groups)
            {
                if (group == null)
                    continue;
                foreach (var entry in group.entries)
                    if (entry.address == address)
                        return AssetDatabase.LoadAssetAtPath<SpriteAtlas>(entry.AssetPath);
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

        /// <summary>
        /// Scripted albums in the contract's shape: 13 biome albums of 4 pages, then event, season and activity albums (60
        /// pages, 720 stickers). Page 0 is full, page 1 half, page 2 has « bientôt » slots; slots 8 to 10 are Rare, 11 Shiny.
        /// </summary>
        private sealed class CaptureAlbums : IAlbums
        {
            private static readonly string[] Biomes =
            {
                "grassland", "forest", "wetland", "mountain", "snow", "tropical", "desert", "blossom", "mushroom", "cloud_islands", "starry_sky",
                "fern", "nebula",
            };

            private readonly StickerInfo[] _stickers = new StickerInfo[720];
            private readonly AlbumPageInfo[] _pages = new AlbumPageInfo[60];
            private readonly string[] _albumKeys = new string[16];
            private readonly AlbumKind[] _kinds = new AlbumKind[16];
            private readonly bool[] _owned = new bool[720];
            private readonly bool[] _unseen = new bool[720];
            private readonly bool[] _available = new bool[720];
            private readonly List<StickerGain> _pending = new List<StickerGain>();

            public CaptureAlbums()
            {
                // The real data (AlbumData, the 720 stickers of the doc) through the real service, and the captured art.
                var real = new AlbumService(new AlbumState());
                for (int a = 0; a < _albumKeys.Length && a < real.AlbumCount; a++)
                {
                    _albumKeys[a] = real.AlbumKey(a);
                    _kinds[a] = real.KindOfAlbum(a);
                }
                for (int p = 0; p < _pages.Length && p < real.PageCount; p++)
                    _pages[p] = real.PageAt(p);
                var captured = new HashSet<string>(StickerArtKeys.Captured);
                for (int i = 0; i < _stickers.Length && i < real.StickerCount; i++)
                {
                    _stickers[i] = real.StickerAt(i);
                    _available[i] = captured.Contains(_stickers[i].Key);
                }
                // A partly filled Prairie: every captured sticker of its two pages owned, two of them new; a few others.
                for (int i = 0; i < 24; i++)
                    _owned[i] = _available[i];
                _unseen[0] = _unseen[13] = true;
                for (int i = 24; i < _stickers.Length; i++)
                    if (_available[i] && (i % 3 == 0))
                        _owned[i] = true;
                AlbumStars = 14;
            }

            public static int StickerOf(int page, int slot) => page * 12 + slot;

            public int FirstMissingSlot(int page)
            {
                for (int s = 0; s < 12; s++)
                    if (!_owned[page * 12 + s] && _available[page * 12 + s])
                        return s;
                return 0;
            }

            public void Gain(int sticker, bool isNew, int stars)
            {
                if (isNew)
                {
                    _owned[sticker] = true;
                    _unseen[sticker] = true;
                }
                else
                {
                    AlbumStars += stars;
                }
                _pending.Add(new StickerGain(sticker, isNew, stars, false, false, false));
                Gained?.Invoke();
            }

            public bool IsOpen => true;
            public int AlbumCount => 16;
            public AlbumKind KindOfAlbum(int album) => _kinds[album];
            public string AlbumKey(int album) => _albumKeys[album];
            public int PageCount => _pages.Length;
            public AlbumPageInfo PageAt(int page) => _pages[page];
            public bool IsPageOpen(int page) => _pages[page].Album == 0 || _pages[page].Album == 5 || page == 4 * 1;
            public int StickerCount => _stickers.Length;
            public StickerInfo StickerAt(int sticker) => _stickers[sticker];
            public bool Owned(int sticker) => _owned[sticker];
            public bool Unseen(int sticker) => _unseen[sticker];

            public void MarkPageSeen(int page)
            {
                for (int s = 0; s < 12; s++)
                    _unseen[page * 12 + s] = false;
            }

            public bool Available(int sticker) => _available[sticker] || _pages[_stickers[sticker].Page].Album == 5;

            public int OwnedOnPage(int page)
            {
                int n = 0;
                for (int s = 0; s < 12; s++)
                    if (_owned[page * 12 + s])
                        n++;
                return n;
            }

            public int AlbumStars { get; private set; }
            public int ChoicePrice => 10;

            public bool TryChoose(int sticker)
            {
                if (AlbumStars < ChoicePrice || _owned[sticker])
                    return false;
                AlbumStars -= ChoicePrice;
                Gain(sticker, true, 0);
                return true;
            }

            public int PendingCount => _pending.Count;
            public StickerGain PendingAt(int index) => _pending[index];
            public void ClearPending() => _pending.Clear();
            public event Action Gained;
        }
    }
}
