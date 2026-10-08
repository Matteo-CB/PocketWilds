using System.Collections.Generic;
using System.Reflection;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Editor.Animals;
using PocketWilds.Editor.UI;
using PocketWilds.Presentation;
using PocketWilds.Services.Localization;
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
    /// T37 screen review in the real refuge, in each shipped language: the HUD's Play button with its tickets, the hub
    /// (open, new, free and locked cards with their hints, records, challenges, the ticket chip), the hub in practice, the
    /// « no ticket » card, a game's HUD (score, time ring, streak), the pause card, the result card (3 stars, a record and
    /// its rewards, then a practice result). Texts are the shipped ones; the hints come from the game's formatter. Nothing
    /// is saved.
    /// </summary>
    public static class ActivityCapture
    {
        private static readonly string[] Animals = { "rabbit.brown", "squirrel.red", "hedgehog.brown", "rabbit.white", "squirrel.autumn", "hedgehog.cream" };

        [MenuItem("Pocket Wilds/Capture/Activities (T37)", priority = 531)]
        public static void CaptureMenu() => Debug.Log("[ActivityCapture] " + Capture("T37"));

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
            var handle = typeof(CanvasScaler).GetMethod("Handle", BindingFlags.Instance | BindingFlags.NonPublic);
            using (var palettes = new PaletteTable(registry))
            {
                try
                {
                    var chunk = WorldCapture.SceneChunk();
                    var entry = Object.FindAnyObjectByType<VerticalSliceEntry>();
                    var hud = Object.FindAnyObjectByType<RefugeHud>();
                    var hub = Object.FindAnyObjectByType<ActivityHubScreen>(FindObjectsInactive.Include);
                    var game = Object.FindAnyObjectByType<ActivityHudScreen>(FindObjectsInactive.Include);
                    var result = Object.FindAnyObjectByType<ActivityResultScreen>(FindObjectsInactive.Include);
                    var empty = Object.FindAnyObjectByType<TicketsEmptyCard>(FindObjectsInactive.Include);
                    var camera = Camera.main;
                    WorldCapture.FrameLikeGame(chunk);
                    chunk.Bind(palettes);
                    entry.Lighting.Apply(Object.FindAnyObjectByType<Light>(), camera, QualityTier.Medium);
                    chunk.ApplyQuality(QualityTier.Medium);
                    WorldCapture.Populate(registry, palettes, chunk, Animals.Length, temp, Animals);

                    var canvases = new List<(Canvas canvas, CanvasScaler scaler)>();
                    void Attach(Component screen, float distance)
                    {
                        if (screen == null)
                            return;
                        var canvas = screen.GetComponentInParent<Canvas>(true);
                        canvas = canvas != null ? canvas.rootCanvas : null;
                        if (canvas == null || canvases.Exists(c => c.canvas == canvas))
                            return;
                        canvas.renderMode = RenderMode.ScreenSpaceCamera;
                        canvas.worldCamera = camera;
                        canvas.planeDistance = camera.nearClipPlane + distance;
                        canvases.Add((canvas, canvas.GetComponent<CanvasScaler>()));
                    }
                    Attach(hud, 0.12f);
                    Attach(game, 0.1f);
                    Attach(hub, 0.08f);
                    Attach(result, 0.06f);
                    Attach(empty, 0.05f);
                    System.Action refresh = () =>
                    {
                        foreach (var c in canvases)
                            if (c.scaler != null)
                                handle?.Invoke(c.scaler, null);
                        Canvas.ForceUpdateCanvases();
                    };
                    void Shot(string name)
                    {
                        string path = $"{CaptureUtility.DefaultFolder}/{prefix}_{name}.png";
                        results.Add(CaptureUtility.Render(camera, 1080, 2340, path, 4, refresh));
                    }
                    void Run(System.Action<float> tick, int frames)
                    {
                        for (int i = 0; i < frames; i++)
                            tick(0.05f);
                    }

                    hud.Bind(null);
                    hud.BindDaily(null);
                    hud.BindPlay(null);
                    hud.SetLight(BigValue.FromDouble(18400), BigValue.FromDouble(34));
                    hud.SetEgg(BigValue.FromDouble(873), false, true);
                    var actions = new NoActions();
                    foreach (string language in UiPipeline.Languages)
                    {
                        var text = new LocalizationService(database.localization, language, SystemLanguage.English);
                        var model = new SampleHub(text);

                        // The refuge: Play above Today, 4 tickets on its badge, its dot.
                        hud.SetShop(true, 365);
                        hud.SetGems(true, 365);
                        hud.SetToday(true, false);
                        hud.SetPlay(true, 4, true);
                        Run(hud.Tick, 30);
                        Shot($"refuge_play_{language}");

                        if (hub != null)
                        {
                            hub.Open(model, text, actions);
                            Run(hub.Tick, 30);
                            Shot($"hub_{language}");
                            model.Practice = true;
                            model.Selected = 1;
                            hub.Refresh();
                            Run(hub.Tick, 20);
                            Shot($"hub_practice_{language}");
                            model.Practice = false;
                            model.Selected = 0;
                            hub.Refresh();
                            Run(hub.Tick, 10);

                            if (empty != null)
                            {
                                var tickets = model.Tickets;
                                tickets.Count = 0;
                                tickets.MinutesToNext = 72;
                                empty.Open(in tickets, text, actions);
                                Run(empty.Tick, 30);
                                Shot($"tickets_empty_{language}");
                                empty.Close();
                                Run(empty.Tick, 20);
                            }
                            hub.Close();
                            Run(hub.Tick, 20);
                        }

                        // A game: the refuge HUD away, score, time ring, streak; then the pause card.
                        if (game != null)
                        {
                            hud.SetPlaying(true);
                            Run(hud.Tick, 10);
                            var hudModel = new ActivityHudModel
                            {
                                Activity = ActivityId.Grooming, Mode = ActivityMode.Free, Duration = 45f, Star1 = 600, Star2 = 1200, Star3 = 1800, Best = 1640,
                            };
                            game.Show(in hudModel, text, actions);
                            game.SetScore(1320);
                            game.SetTimeLeft(17.4f);
                            game.SetStreak(5);
                            Run(game.Tick, 30);
                            Shot($"game_hud_{language}");
                            game.SetPaused(true);
                            Run(game.Tick, 20);
                            Shot($"game_pause_{language}");
                            game.SetPaused(false);
                            Run(game.Tick, 10);
                            game.Hide();
                            Run(game.Tick, 10);
                        }

                        // The result: three stars, a new record, its rewards (doc 05 sheet 20), then a practice result.
                        if (result != null)
                        {
                            var cells = new RewardCell[RewardCells.Max];
                            cells[0] = new RewardCell(RewardKind.Gems, 2);
                            cells[1] = new RewardCell(RewardKind.Light, 0) { LightMinutes = 20, Light = BigValue.FromDouble(40800) };
                            cells[2] = new RewardCell(RewardKind.Bond, 6);
                            cells[3] = new RewardCell(RewardKind.Treat, 3, ContentId.FromSource("treat.clover_crunch"));
                            var won = new ActivityResultModel
                            {
                                Activity = ActivityId.Grooming, Mode = ActivityMode.Free, ChallengeIndex = -1, Score = 1840, Best = 1840, Stars = 3,
                                NewRecord = true, Rewarded = true, CanReplay = true,
                            };
                            result.Open(in won, cells, 4, text, actions);
                            Run(result.Tick, 70);
                            Shot($"result_{language}");
                            result.Close();
                            Run(result.Tick, 20);
                            var practice = new ActivityResultModel
                            {
                                Activity = ActivityId.Grooming, Mode = ActivityMode.Practice, ChallengeIndex = -1, Score = 940, Best = 1840, Stars = 1,
                                Rewarded = false, CanReplay = true,
                            };
                            result.Open(in practice, cells, 0, text, actions);
                            Run(result.Tick, 50);
                            Shot($"result_practice_{language}");
                            result.Close();
                            Run(result.Tick, 20);
                        }
                        hud.SetPlaying(false);
                        Run(hud.Tick, 10);
                    }
                }
                finally
                {
                    foreach (var go in temp)
                        if (go != null)
                            Object.DestroyImmediate(go);
                    EditorSceneManager.OpenScene(WorldCapture.ScenePath, OpenSceneMode.Single);
                }
            }
            return string.Join(", ", results);
        }

        /// <summary>A hub in the middle of the game: some activities played, one new, one free, the later ones locked.</summary>
        private sealed class SampleHub : IActivityHubModel
        {
            private readonly ActivityCardModel[] _cards;
            private readonly ChallengeCellModel[] _cells = new ChallengeCellModel[ActivityCatalog.ChallengesPerActivity];
            private readonly ContentId[] _themes = { ContentId.FromSource("biome.grassland"), ContentId.FromSource("biome.forest") };

            public SampleHub(ILocalization text)
            {
                var input = new ActivityUnlockInput { RefugeLevel = 9, Animals = 14, DayNumber = 4 };
                var cards = new List<ActivityCardModel>();
                for (int slot = 0; slot < ActivityCatalog.ActivityCount; slot++)
                {
                    var id = ActivityCatalog.IdOfSlot(slot);
                    var d = ActivityCatalog.AtSlot(slot);
                    bool open = d.RefugeLevel <= 5 && d.Special == ActivitySpecialUnlock.None;
                    var card = new ActivityCardModel
                    {
                        Activity = id,
                        Name = text.Get(ActivityText.NameKey(id)),
                        State = open ? ActivityCardState.Open : ActivityCardState.Locked,
                        UnlockHint = open ? null : ActivityTextFormatter.UnlockHint(text, id, input),
                    };
                    if (id == ActivityId.Grooming)
                    {
                        card.Best = 1840;
                        card.BestStars = 3;
                        card.ChallengesDone = 4;
                    }
                    else if (id == ActivityId.HideAndSeek)
                    {
                        card.State = ActivityCardState.New;
                    }
                    else if (id == ActivityId.Expeditions)
                    {
                        card.FreeGame = true;
                    }
                    cards.Add(card);
                }
                // The open ones first, as the presenter orders them.
                cards.Sort((a, b) => (a.State == ActivityCardState.Locked).CompareTo(b.State == ActivityCardState.Locked));
                _cards = cards.ToArray();
                for (int i = 0; i < _cells.Length; i++)
                {
                    _cells[i] = new ChallengeCellModel
                    {
                        Index = i,
                        Stars = (byte)(i < 4 ? 3 : i == 4 ? 2 : i == 5 ? 1 : 0),
                        Locked = i > 6,
                        Goal = text.Get(ActivityText.Challenge).Replace("{n}", (i + 1).ToString()),
                    };
                }
                Tickets = new TicketsModel { Count = 4, Max = 5, MinutesToNext = 72, PackPrice = 30, PackSize = 3, PacksLeftToday = 2, Gems = 365 };
            }

            public int Selected;
            public bool Practice { get; set; }
            public TicketsModel Tickets { get; set; }

            public int CardCount => _cards.Length;
            public ActivityCardModel Card(int index) => _cards[index];
            public int SelectedCard => Selected;
            public int ChallengeCount => _cells.Length;
            public ChallengeCellModel Challenge(int index) => _cells[index];
            public int ThemeCount => _themes.Length;
            public ContentId Theme(int index) => _themes[index];
            public int SelectedTheme => 0;
        }

        private sealed class NoActions : IActivityHubActions, ITicketsEmptyActions, IActivityHudActions, IActivityResultActions
        {
            public void SelectActivity(int card)
            {
            }

            public void Play()
            {
            }

            public void PlayChallenge(int challenge)
            {
            }

            public void SetPractice(bool practice)
            {
            }

            public void SetTheme(int theme)
            {
            }

            public void BuyTickets()
            {
            }

            public void CloseActivities()
            {
            }

            public void PlayPractice()
            {
            }

            public void TicketsEmptyClosed()
            {
            }

            public void PauseActivity()
            {
            }

            public void ResumeActivity()
            {
            }

            public void QuitActivity()
            {
            }

            public void PlayAgain()
            {
            }

            public void BackToHub()
            {
            }
        }
    }
}
