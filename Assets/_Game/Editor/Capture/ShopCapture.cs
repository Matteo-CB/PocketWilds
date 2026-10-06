using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Editor.Animals;
using PocketWilds.Editor.UI;
using PocketWilds.Presentation;
using PocketWilds.Services.Feedback;
using PocketWilds.Services.Localization;
using PocketWilds.Services.Store;
using PocketWilds.Simulation;
using PocketWilds.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace PocketWilds.Editor.Capture
{
    /// <summary>
    /// T28 review frames, through the real presenter, offer engine and sandbox store (no fake UI data): HUD with
    /// the shop shortcut, shop top and bottom, a purchase thank-you, the Starter Pack modal over the refuge,
    /// in each shipped language. The scene is reopened from disk afterwards, nothing is saved.
    /// </summary>
    public static class ShopCapture
    {
        [MenuItem("Pocket Wilds/Capture/Shop Frames (T28)", priority = 526)]
        public static void CaptureMenu() => Debug.Log("[ShopCapture] " + Capture("T28"));

        public static string Capture(string prefix)
        {
            var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (active.path != WorldCapture.ScenePath && !string.IsNullOrEmpty(active.path) && active.isDirty)
                return "refused: save or discard changes in " + active.path;
            EditorSceneManager.OpenScene(WorldCapture.ScenePath, OpenSceneMode.Single);

            var database = AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>(AnimalPipeline.ContentDatabasePath);
            var registry = database.CreateBuilder().Build();
            var temp = new List<GameObject>();
            var results = new List<string>();
            using (var palettes = new PaletteTable(registry))
            {
                try
                {
                    var chunk = WorldCapture.SceneChunk();
                    var entry = Object.FindAnyObjectByType<VerticalSliceEntry>();
                    var hud = Object.FindAnyObjectByType<RefugeHud>();
                    var shop = Object.FindAnyObjectByType<ShopScreen>();
                    var modal = Object.FindAnyObjectByType<OfferModalView>();
                    var camera = Camera.main;
                    WorldCapture.FrameLikeGame(chunk);
                    chunk.Bind(palettes);
                    entry.Lighting.Apply(Object.FindAnyObjectByType<Light>(), camera, QualityTier.Medium);
                    chunk.ApplyQuality(QualityTier.Medium);
                    WorldCapture.Populate(registry, palettes, chunk, 6, temp);

                    var handle = typeof(CanvasScaler).GetMethod("Handle", BindingFlags.Instance | BindingFlags.NonPublic);
                    var scalers = new List<CanvasScaler>();
                    float plane = 0.12f;
                    foreach (var screen in new Component[] { hud, shop, modal })
                    {
                        var canvas = screen.GetComponent<Canvas>();
                        canvas.renderMode = RenderMode.ScreenSpaceCamera;
                        canvas.worldCamera = camera;
                        canvas.planeDistance = camera.nearClipPlane + plane;
                        plane -= 0.03f;
                        scalers.Add(screen.GetComponent<CanvasScaler>());
                    }
                    System.Action refresh = () =>
                    {
                        foreach (var scaler in scalers)
                            handle?.Invoke(scaler, null);
                        Canvas.ForceUpdateCanvases();
                    };
                    hud.Bind(null);
                    hud.SetLight(BigValue.FromDouble(1840), BigValue.FromDouble(3.4));
                    hud.SetEgg(BigValue.FromDouble(24), false, true);

                    foreach (string language in UiPipeline.Languages)
                    {
                        var text = new LocalizationService(database.localization, language, SystemLanguage.English);
                        var state = Refuge(registry);
                        var grants = new GrantService(registry, state, null);
                        var offers = new OfferService(registry, state, grants, null);
                        var store = new StoreService(registry, offers, new SandboxStoreProvider(language, 0), new RuntimeGameClock(), new NoSaves());
                        store.InitializeAsync(CancellationToken.None).GetAwaiter().GetResult();
                        var presenter = new ShopPresenter(store, text, shop, modal, NullAudioService.Instance, NullHapticService.Instance, _ => { },
                            CancellationToken.None);
                        try
                        {
                            hud.SetShop(offers.ShopUnlocked, state.Wallet.Gems);
                            hud.Tick(0.05f);
                            if (language == "fr")
                                Frame(camera, refresh, $"{prefix}_hud_shop", results);

                            // The Starter Pack after a happy hatch (before any purchase: it is for new players).
                            if (presenter.TryShowMomentOffer())
                            {
                                for (int i = 0; i < 8; i++)
                                    presenter.Tick(0.05f);
                                Frame(camera, refresh, $"{prefix}_offer_{language}", results);
                                presenter.CloseOffer();
                            }

                            presenter.OpenShop();
                            presenter.Tick(0.05f);
                            Frame(camera, refresh, $"{prefix}_shop_{language}", results);
                            shop.GetComponentInChildren<ScrollRect>(true).verticalNormalizedPosition = 0f;
                            Frame(camera, refresh, $"{prefix}_shop_bottom_{language}", results);

                            presenter.Buy(ContentId.FromSource("offer.gems.tier03"));
                            for (int i = 0; i < 6; i++)
                                presenter.Tick(0.05f);
                            Frame(camera, refresh, $"{prefix}_shop_thanks_{language}", results);
                            presenter.CloseShop();
                        }
                        finally
                        {
                            presenter.Dispose();
                            store.Dispose();
                        }
                    }
                }
                finally
                {
                    foreach (var go in temp)
                        Object.DestroyImmediate(go);
                    EditorSceneManager.OpenScene(WorldCapture.ScenePath, OpenSceneMode.Single);
                }
            }
            return string.Join(", ", results);
        }

        /// <summary>Captures never write a save.</summary>
        private sealed class NoSaves : PocketWilds.Services.Save.ISaveWrites
        {
            public long SerializedCount => 0;
            public event System.Action<long> Written { add { } remove { } }

            public void RequestSave(SaveUrgency urgency)
            {
            }
        }

        /// <summary>A player past onboarding: 6 animals of 2 species, no purchase yet (Starter Pack eligible).</summary>
        private static PlayerState Refuge(ContentRegistry registry)
        {
            var state = PlayerState.CreateNew(System.DateTimeOffset.UtcNow);
            string[] animals = { "rabbit.brown", "rabbit.white", "squirrel.red", "rabbit.spotted", "squirrel.autumn", "rabbit.grey" };
            foreach (string animal in animals)
            {
                string species = "species." + animal.Substring(0, animal.IndexOf('.'));
                var speciesId = ContentId.FromSource(species);
                var variant = ContentId.FromSource("variant." + animal);
                state.Collection.Add(new AnimalInstance { Species = speciesId, Variant = variant, Seed = 7u });
                state.Collection.DiscoveredSpecies.Add(speciesId);
                state.Collection.DiscoveredVariants.Add(variant);
            }
            return state;
        }

        private static void Frame(Camera camera, System.Action refresh, string name, List<string> results)
        {
            string path = $"{CaptureUtility.DefaultFolder}/{name}.png";
            CaptureUtility.Render(camera, 1080, 2340, path, 4, refresh);
            results.Add(CaptureUtility.Render(camera, 1080, 2340, path, 4, refresh));
        }
    }
}
