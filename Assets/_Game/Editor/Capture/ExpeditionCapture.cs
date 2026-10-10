using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Editor.Activities;
using PocketWilds.Editor.Animals;
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
    /// T42 « Expéditions » review (doc 01 Contenu section 7, A18 and A19; doc 05 sheet 23): the real screen prefab driven by
    /// the real presenter in edit mode, over a scripted IExpeditions (slots, timers and loot set by hand), rendered through a
    /// camera at 1080 × 2340. Frames: the first opening with the A18 paw on Prés fleuris (each language); the planning sheet
    /// with three animals picked, the live stars and their reasons, the durations and the loot preview (each language); the
    /// first stroll's single chip with the paw on « Partir »; the slots empty, away (« De retour dans 3 h 12 », « Finir
    /// maintenant »), back (glowing) and locked (800 Gems, the Wild Club) (each language); the welcome's postcard mid-pivot
    /// with its ring and petals, then settled over the dim; « Tout réclamer » with two postcards fanned. The fonts and the UI
    /// kit are lent from the project the way the scene lends them at run time; the postcard picture is EP's destination art
    /// when rendered, else a soft stand-in (the real one is rendered at run time). The refuge is not drawn behind the welcome
    /// (the camera's cream stands in for it). Nothing is saved.
    /// </summary>
    public static class ExpeditionCapture
    {
        private const int Width = 1080;
        private const int Height = 2340;
        private const float Step = 1f / 30f;

        private static readonly (string species, string variant, bool favorite)[] Animals =
        {
            ("species.rabbit", "variant.rabbit.brown", false), ("species.hedgehog", "variant.hedgehog.brown", false),
            ("species.squirrel", "variant.squirrel.red", true), ("species.rabbit", "variant.rabbit.white", false),
            ("species.squirrel", "variant.squirrel.grey", false), ("species.hedgehog", "variant.hedgehog.brown", false),
        };

        [MenuItem("Pocket Wilds/Capture/Expeditions (T42)", priority = 536)]
        public static void CaptureMenu() => Debug.Log("[ExpeditionCapture] " + Capture("T42_expeditions"));

        public static string Capture(string prefix)
        {
            var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (active.path != WorldCapture.ScenePath && !string.IsNullOrEmpty(active.path) && active.isDirty)
                return "refused: save or discard changes in " + active.path;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ExpeditionPipeline.PrefabPath);
            if (prefab == null)
                return "no screen prefab: run Pocket Wilds/Activities/Build Expeditions (T42) first";
            EditorSceneManager.OpenScene(WorldCapture.ScenePath, OpenSceneMode.Single);

            var database = AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>(AnimalPipeline.ContentDatabasePath);
            var registry = database.CreateBuilder().Build();
            var codex = AssetDatabase.LoadAssetAtPath<CodexVisualCatalog>(AnimalThumbnailPipeline.CatalogPath);
            var handle = typeof(CanvasScaler).GetMethod("Handle", BindingFlags.Instance | BindingFlags.NonPublic);
            var texts = new List<(string language, LocalizationService text)>();
            foreach (string language in UiPipeline.Languages)
                texts.Add((language, new LocalizationService(database.localization, language, SystemLanguage.English)));
            var sprites = LoadAll<Sprite>("t:Sprite", "Assets/_Game/Art/UI/Kit", "Assets/_Game/Art/UI/Icons");
            var fonts = LoadAll<TMP_FontAsset>("t:TMP_FontAsset", "Assets/_Game/Art/UI/Fonts");
            var materials = LoadAll<Material>("t:Material", "Assets/_Game/Art/UI/Fonts");
            var shots = new List<string>();
            var problems = new List<string>();
            var notes = new List<string>();

            var collection = new CollectionState();
            for (int i = 0; i < Animals.Length; i++)
                collection.Add(new AnimalInstance
                {
                    Species = ContentId.FromSource(Animals[i].species), Variant = ContentId.FromSource(Animals[i].variant), Rarity = RarityTier.Common,
                    Seed = (uint)(i + 1) * 2654435761u, Flags = Animals[i].favorite ? AnimalFlags.Favorite : AnimalFlags.None,
                });
            var picture = LoadPicture(notes);

            GameObject cameraObject = null;
            ExpeditionScreenPresenter presenter = null;
            try
            {
                cameraObject = new GameObject("ExpeditionCaptureCamera") { hideFlags = HideFlags.HideAndDontSave };
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
                    var fake = new ScriptedExpeditions(registry, collection);
                    var renderer = new StandInRenderer(picture);
                    presenter?.Dispose();
                    presenter = new ExpeditionScreenPresenter(context, fake, null, renderer, null, prefab) { CollectionOverride = collection };
                    presenter.OpenFromHub();
                    var view = presenter.View as ExpeditionScreenView;
                    if (view == null)
                        return "the screen prefab has no ExpeditionScreenView (rebuild it)";
                    view.Lend(sprites, fonts, materials);
                    view.SetPortraits(codex);
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
                        {
                            fake.Advance(Step);
                            presenter.Tick(Step);
                        }
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

                    // 1. The first opening (A18): two empty slots, the Gems and Club slots locked, the paw on Prés fleuris.
                    Refresh();
                    Run(0.3f);
                    if (presenter.TutorialStep != 1)
                        problems.Add(language + ": no A18 paw on the destination");
                    Shot("first_" + language);

                    // 2. The first stroll's sheet: one animal, the single « Première balade : 3 min » chip, the paw on « Partir ».
                    presenter.TapDestination(0);
                    Run(0.3f);
                    presenter.TapAnimal(0);
                    Run(0.3f);
                    if (presenter.TutorialStep != 3)
                        problems.Add(language + ": no A18 paw on Partir");
                    if (language == "fr")
                        Shot("first_stroll");

                    // 3. The planning sheet once the first stroll is past: three animals, the stars and reasons, 4 h, the loot.
                    presenter.CloseSheet();
                    fake.FirstStroll = false;
                    fake.Raise(0);
                    Run(0.3f);
                    presenter.TapSlot(0);
                    Run(0.3f);
                    presenter.TapAnimal(0);
                    presenter.TapAnimal(1);
                    presenter.TapAnimal(2);
                    presenter.PickDuration((int)ExpeditionDuration.FourHours);
                    Run(0.35f);
                    if (!view.SheetShown || presenter.TeamCount != 3)
                        problems.Add($"{language}: sheet {view.SheetShown}, team {presenter.TeamCount}");
                    Shot("sheet_" + language);

                    // 4. The slots: away (3 h 12 left), back (glowing), locked for 800 Gems and with the Wild Club.
                    presenter.CloseSheet();
                    fake.SetAway(0, 3 * 3600 + 12 * 60, 4 * 3600, 2);
                    fake.SetBack(1, 3);
                    Run(0.6f);
                    if (view.SlotStateShown(0) != ExpeditionSlotState.Away || view.SlotStateShown(1) != ExpeditionSlotState.Back)
                        problems.Add(language + ": slots not shown away and back");
                    if (string.IsNullOrEmpty(view.SlotTimerText(0)))
                        problems.Add(language + ": no timer text");
                    Shot("slots_" + language);

                    if (language != "fr")
                        continue;

                    // 5. The welcome: the postcard pivots in (mid-pivot with its ring and petals), then settles over the dim.
                    presenter.TapSlot(1);
                    Run(ExpeditionLayout.FadeSeconds + 0.05f);
                    int guard = 0;
                    while (!view.PostcardShown(0) && guard++ < 120)
                        Run(Step);
                    Run(0.28f);
                    if (!view.PostcardShown(0))
                        problems.Add("the postcard did not show");
                    if (renderer.Begun != 1)
                        problems.Add($"postcards asked: {renderer.Begun}");
                    Shot("welcome_pivot");
                    Run(0.25f);
                    presenter.Tick(0f);
                    Shot("welcome_postcard");
                    presenter.BackToHub();
                    Run(0.4f);

                    // 6. « Tout réclamer »: two teams back, one postcard each, 300 ms apart.
                    fake.SetBack(0, 2);
                    fake.SetBack(1, 3);
                    Run(0.6f);
                    Shot("claim_all_button");
                    presenter.ClaimAll();
                    guard = 0;
                    while (!view.PostcardSettled(1) && guard++ < 150)
                        Run(Step);
                    if (presenter.WelcomeCount != 2)
                        problems.Add($"claim all welcomed {presenter.WelcomeCount}");
                    Shot("claim_all");
                    presenter.BackToHub();
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
                if (picture != null && !AssetDatabase.Contains(picture))
                    Object.DestroyImmediate(picture);
                EditorSceneManager.OpenScene(WorldCapture.ScenePath, OpenSceneMode.Single);
            }
            return string.Join(", ", shots) + (notes.Count > 0 ? " [" + string.Join("; ", notes) + "]" : "")
                   + (problems.Count > 0 ? " PROBLEMS: " + string.Join("; ", problems) : "");
        }

        private static List<T> LoadAll<T>(string filter, params string[] folders) where T : Object
        {
            var found = new List<T>();
            foreach (var guid in AssetDatabase.FindAssets(filter, folders))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                    if (asset is T typed)
                        found.Add(typed);
            }
            return found;
        }

        /// <summary>EP's Prés fleuris card art when it exists (read only), else a soft meadow gradient (capture only).</summary>
        private static Texture2D LoadPicture(List<string> notes)
        {
            string path = PostcardLayout.CardArtFolder + "/" + PostcardLayout.CardArtName("expedition.flower_meadows") + ".png";
            var art = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (art != null)
            {
                notes.Add("postcard picture: EP's card art (the real postcard is rendered at run time with the team)");
                return art;
            }
            notes.Add("postcard picture: a stand-in gradient (no EP card art yet)");
            var texture = new Texture2D(64, 96, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            var sky = new Color(0.78f, 0.89f, 0.96f, 1f);
            var grass = new Color(0.62f, 0.80f, 0.48f, 1f);
            for (int y = 0; y < texture.height; y++)
            for (int x = 0; x < texture.width; x++)
            {
                float hill = 0.42f + 0.08f * Mathf.Sin(x * 0.09f);
                texture.SetPixel(x, y, y / (float)texture.height < hill ? grass : sky);
            }
            texture.Apply();
            return texture;
        }

        /// <summary>The capture's renderer: the stand-in picture at once (the real one renders the team in the set).</summary>
        private sealed class StandInRenderer : IPostcardRenderer
        {
            private readonly Texture2D _picture;
            private Texture2D _shown;

            public StandInRenderer(Texture2D picture) => _picture = picture;

            public int Begun { get; private set; }

            public void Begin(in ExpeditionReturn welcome)
            {
                Begun++;
                _shown = _picture;
            }

            public bool Busy => false;

            public Texture2D Picture => _shown;

            public void Tick()
            {
            }

            public void Release() => _shown = null;
        }

        /// <summary>
        /// A scripted IExpeditions for the review: slots set by hand, a clock advanced by the capture, the doc's rating rule
        /// (section 7.2) on the collection's species, the 1 h to 12 h loot of section 7.3 for the preview, Prés fleuris open.
        /// </summary>
        private sealed class ScriptedExpeditions : IExpeditions
        {
            private static readonly (string key, string biome, RigFamilyMask family)[] Table =
            {
                ("expedition.flower_meadows", "biome.grassland", RigFamilyMask.SmallQuadruped), ("expedition.stump_wood", "biome.forest", RigFamilyMask.MediumQuadruped),
                ("expedition.reed_shores", "biome.lake", RigFamilyMask.SwimmerLow), ("expedition.torrent_pasture", "biome.mountain", RigFamilyMask.Bird),
                ("expedition.frosted_pines", "biome.snow", RigFamilyMask.HeavyQuadruped), ("expedition.turquoise_falls", "biome.jungle", RigFamilyMask.ClimberPrimate),
                ("expedition.arch_oasis", "biome.desert", RigFamilyMask.SmallQuadruped), ("expedition.petal_bridge", "biome.sakura", RigFamilyMask.Bird),
            };

            private readonly ContentRegistry _content;
            private readonly CollectionState _collection;
            private readonly ExpeditionDestination[] _destinations;
            private readonly ExpeditionSlotState[] _states = { ExpeditionSlotState.Empty, ExpeditionSlotState.Empty, ExpeditionSlotState.Locked, ExpeditionSlotState.Locked };
            private readonly ExpeditionSlotUnlock[] _unlocks = { ExpeditionSlotUnlock.None, ExpeditionSlotUnlock.None, ExpeditionSlotUnlock.Gems, ExpeditionSlotUnlock.Club };
            private readonly double[] _left = new double[4];
            private readonly double[] _total = new double[4];
            private readonly byte[] _stars = new byte[4];
            private readonly int[] _sizes = new int[4];
            private readonly uint[,] _teams = new uint[4, 3];

            public ScriptedExpeditions(ContentRegistry content, CollectionState collection)
            {
                _content = content;
                _collection = collection;
                _destinations = Table.Select((t, i) => new ExpeditionDestination(ContentId.FromSource(t.key), t.key, "EXP-" + i,
                    ContentId.FromSource(t.biome), t.family, false)).ToArray();
            }

            public bool FirstStroll { get; set; } = true;

            public void Raise(int slot) => SlotChanged?.Invoke(slot);

            public void Advance(float seconds)
            {
                for (int s = 0; s < 4; s++)
                    if (_states[s] == ExpeditionSlotState.Away)
                        _left[s] = Math.Max(0.0, _left[s] - seconds);
            }

            public void SetAway(int slot, double left, double total, int size)
            {
                _states[slot] = ExpeditionSlotState.Away;
                _left[slot] = left;
                _total[slot] = total;
                SetTeam(slot, size);
                Raise(slot);
            }

            public void SetBack(int slot, int size)
            {
                _states[slot] = ExpeditionSlotState.Back;
                _left[slot] = 0.0;
                _total[slot] = 3600.0;
                SetTeam(slot, size);
                Raise(slot);
            }

            private void SetTeam(int slot, int size)
            {
                _sizes[slot] = size;
                _stars[slot] = (byte)Math.Min(3, size);
                for (int k = 0; k < size; k++)
                    _teams[slot, k] = _collection[(slot * 3 + k) % _collection.Count].InstanceId;
            }

            public bool IsOpen => true;
            public int SlotCount => 4;
            public ExpeditionSlotState StateOf(int slot) => _states[slot];
            public ExpeditionSlotUnlock UnlockOf(int slot) => _states[slot] == ExpeditionSlotState.Locked ? _unlocks[slot] : ExpeditionSlotUnlock.None;
            public long SlotPrice => 800;
            public bool TryBuySlot() => false;
            public ContentId DestinationOf(int slot) => _destinations[0].Id;
            public ExpeditionDuration DurationOf(int slot) => ExpeditionDuration.FourHours;
            public int TeamSizeOf(int slot) => _sizes[slot];
            public uint TeamMember(int slot, int index) => _teams[slot, index];
            public byte StarsOf(int slot) => _stars[slot];
            public double SecondsLeft(int slot) => _states[slot] == ExpeditionSlotState.Away ? _left[slot] : 0.0;
            public double TotalSecondsOf(int slot) => _total[slot];

            public int BackCount
            {
                get
                {
                    int n = 0;
                    for (int s = 0; s < 4; s++)
                        if (_states[s] == ExpeditionSlotState.Back)
                            n++;
                    return n;
                }
            }

            public double SoonestReturn
            {
                get
                {
                    double best = -1.0;
                    for (int s = 0; s < 4; s++)
                        if (_states[s] == ExpeditionSlotState.Away && (best < 0.0 || _left[s] < best))
                            best = _left[s];
                    return best;
                }
            }

            public int SpeedUpCost(int slot) => Math.Max(5, (int)Math.Ceiling(_left[slot] / 360.0));
            public int SpeedUpsLeftToday => 3;
            public bool TrySpeedUp(int slot) => false;
            public int DestinationCount => _destinations.Length;
            public ExpeditionDestination DestinationAt(int index) => _destinations[index];

            public bool TryGetDestination(ContentId id, out ExpeditionDestination destination)
            {
                foreach (var d in _destinations)
                    if (d.Id == id)
                    {
                        destination = d;
                        return true;
                    }
                destination = default;
                return false;
            }

            public bool IsDestinationOpen(ContentId destination) => destination == _destinations[0].Id;

            public bool IsAway(uint instanceId)
            {
                for (int s = 0; s < 4; s++)
                    if (_states[s] == ExpeditionSlotState.Away || _states[s] == ExpeditionSlotState.Back)
                        for (int k = 0; k < _sizes[s]; k++)
                            if (_teams[s, k] == instanceId)
                                return true;
                return false;
            }

            public ExpeditionRating Rate(ContentId destination, uint[] team, int count)
            {
                TryGetDestination(destination, out var d);
                int points = 10 * count;
                byte fromBiome = 0;
                bool family = false;
                var species = new HashSet<ContentId>();
                for (int i = 0; i < count; i++)
                {
                    if (!_collection.TryGet(team[i], out var animal) || !_content.Species.TryGet(animal.Species, out var definition))
                        continue;
                    species.Add(animal.Species);
                    if (definition.Biome == d.Biome)
                        fromBiome++;
                    if ((d.FavoriteFamilies & Mask(definition.RigFamily)) != 0)
                        family = true;
                }
                points += 10 * fromBiome + (family ? 10 : 0) + (species.Count >= 3 ? 10 : 0);
                byte stars = (byte)(points >= 60 ? 3 : points >= 40 ? 2 : 1);
                return new ExpeditionRating(points, stars, fromBiome, family, species.Count >= 3);
            }

            private static RigFamilyMask Mask(RigFamily family) => family switch
            {
                RigFamily.SmallQuadruped => RigFamilyMask.SmallQuadruped,
                RigFamily.MediumQuadruped => RigFamilyMask.MediumQuadruped,
                RigFamily.HeavyQuadruped => RigFamilyMask.HeavyQuadruped,
                RigFamily.Bird => RigFamilyMask.Bird,
                RigFamily.SwimmerLow => RigFamilyMask.SwimmerLow,
                RigFamily.ClimberPrimate => RigFamilyMask.ClimberPrimate,
                RigFamily.Hopper => RigFamilyMask.Hopper,
                _ => RigFamilyMask.None,
            };

            public int SuggestTeam(ContentId destination, uint[] into)
            {
                int n = Math.Min(3, Math.Min(into.Length, _collection.Count));
                for (int i = 0; i < n; i++)
                    into[i] = _collection[i].InstanceId;
                return n;
            }

            public double DurationSeconds(ExpeditionDuration duration) =>
                FirstStroll ? 180.0 : duration switch { ExpeditionDuration.OneHour => 3600.0, ExpeditionDuration.FourHours => 14400.0, ExpeditionDuration.EightHours => 28800.0, _ => 43200.0 };

            public bool FirstStrollNext => FirstStroll;
            public int DiscoveryGuaranteeIn => 9;

            public void Preview(ContentId destination, ExpeditionDuration duration, byte stars, out ExpeditionLoot preview)
            {
                int d = (int)duration;
                preview = new ExpeditionLoot
                {
                    LightMinutes = new[] { 15, 50, 90, 120 }[d] * (stars >= 3 ? 1.2 : stars == 2 ? 1.1 : 1.0),
                    Treats = new[] { 1, 2, 3, 4 }[d] + (stars >= 2 ? 1 : 0),
                    SeedKinds = 1,
                    Seeds0 = new[] { 1, 2, 3, 4 }[d] + 1 + (stars >= 3 ? 1 : 0),
                    StickerChance = new[] { 10, 25, 45, 60 }[d] + (stars >= 3 ? 10 : 0),
                    DiscoveryChance = new[] { 0, 2, 6, 9 }[d],
                    Tickets = new[] { 0, 0, 1, 1 }[d],
                    BondPoints = 5,
                    MasteryXp = 15,
                };
            }

            public ExpeditionStartResult Start(int slot, ContentId destination, ExpeditionDuration duration, uint[] team, int count) => ExpeditionStartResult.NotOpen;
            public int ChallengeCount => 10;
            public bool IsChallengeOpen(int challenge) => challenge == 0 || challenge == 3 || challenge == 4 || challenge == 7;
            public ExpeditionDuration ChallengeDuration(int challenge) => ExpeditionDuration.OneHour;
            public bool ChallengeAllows(int challenge, ContentId destination) => true;
            public bool ChallengeAccepts(int challenge, ContentId destination, uint[] team, int count) => true;
            public int ChallengeThreshold(int challenge, int stars) => 10 + 20 * stars;
            public byte ChallengeStars(int challenge) => (byte)(challenge == 0 ? 2 : 0);
            public ExpeditionStartResult StartChallenge(int slot, int challenge, ContentId destination, uint[] team, int count) => ExpeditionStartResult.NotOpen;

            public bool TryCollect(int slot, out ExpeditionReturn welcome)
            {
                welcome = default;
                if (_states[slot] != ExpeditionSlotState.Back)
                    return false;
                Preview(_destinations[0].Id, ExpeditionDuration.FourHours, _stars[slot], out var loot);
                loot.StickerChance = 0;
                loot.DiscoveryChance = 0;
                loot.Stickers = 1;
                welcome = new ExpeditionReturn
                {
                    Slot = slot, Destination = _destinations[0].Id, Duration = ExpeditionDuration.FourHours, Stars = _stars[slot], TeamSize = _sizes[slot],
                    Animal0 = _teams[slot, 0], Animal1 = _teams[slot, 1], Animal2 = _teams[slot, 2], Loot = loot, PostcardId = slot + 1,
                };
                _states[slot] = ExpeditionSlotState.Empty;
                _sizes[slot] = 0;
                Raise(slot);
                return true;
            }

            public void PostcardRendered(int postcardId)
            {
            }

            public int PostcardCount => 1;
            public int PostcardAt(int index) => 1;
            public event Action<int> SlotChanged;
        }
    }
}
