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
    /// T36 screen review in the real refuge, in each shipped language: the HUD with the Today button and its dot, wish
    /// bubbles above animals and the wish card, Today (welcome, closed gift, wishes, calendar, journal row) and Today with
    /// the gift opened, the weekly journal, the return gift on the offline card, the animal sheet's treat button. Texts are
    /// the shipped ones, built by the game's formatter from real wish and journal records. Nothing is saved.
    /// </summary>
    public static class DailyVisitCapture
    {
        private static readonly string[] Animals = { "rabbit.brown", "squirrel.red", "hedgehog.brown", "rabbit.white", "squirrel.autumn", "hedgehog.cream" };

        [MenuItem("Pocket Wilds/Capture/Daily Visit (T36)", priority = 530)]
        public static void CaptureMenu() => Debug.Log("[DailyVisitCapture] " + Capture("T36"));

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
                    var today = Object.FindAnyObjectByType<TodayScreen>(FindObjectsInactive.Include);
                    var journal = Object.FindAnyObjectByType<WeeklyJournalScreen>(FindObjectsInactive.Include);
                    var card = Object.FindAnyObjectByType<WishCardView>(FindObjectsInactive.Include);
                    var offline = Object.FindAnyObjectByType<OfflineReturnScreen>(FindObjectsInactive.Include);
                    var sheet = Object.FindAnyObjectByType<AnimalSheetScreen>(FindObjectsInactive.Include);
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
                    Attach(card, 0.1f);
                    Attach(today, 0.06f);
                    Attach(journal, 0.05f);
                    Attach(offline, 0.05f);
                    Attach(sheet, 0.05f);
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

                    // A profile holding the six animals (Mochi first), with three wishes and a first journal.
                    var state = PlayerState.CreateNew(System.DateTimeOffset.UtcNow);
                    var animals = new List<GameObject>();
                    foreach (var go in temp)
                        if (go.GetComponent<AnimalView>() != null)
                            animals.Add(go);
                    for (int i = 0; i < Animals.Length; i++)
                    {
                        string species = Animals[i].Substring(0, Animals[i].IndexOf('.'));
                        state.Collection.Add(new AnimalInstance
                        {
                            Species = ContentId.FromSource("species." + species), Variant = ContentId.FromSource("variant." + Animals[i]),
                            Seed = (uint)(i * 31 + 3),
                        });
                    }
                    state.Collection.CustomNames[1] = "Mochi";
                    var wishes = SampleWishes(state);
                    var goals = SampleGoals();
                    var bubblePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/World/PF_World_WishBubble.prefab");

                    hud.Bind(null);
                    hud.BindDaily(null);
                    hud.SetLight(BigValue.FromDouble(18400), BigValue.FromDouble(34));
                    hud.SetEgg(BigValue.FromDouble(873), false, true);

                    foreach (string language in UiPipeline.Languages)
                    {
                        var text = new LocalizationService(database.localization, language, SystemLanguage.English);
                        var format = new DailyVisitTextFormatter(registry, text, state);
                        var model = new SampleToday(registry, format, wishes, state);
                        var journalModel = new SampleJournal(format, goals);

                        // A big return gift with the Light of the absence.
                        if (offline != null)
                        {
                            var bundle = DailyRewardTables.Return(true, false, new RewardContext(false, false, false, false,
                                ContentId.FromSource("treat.clover_crunch"), 15.0));
                            var cells = new RewardCell[RewardCells.Max];
                            int count = RewardCells.FromBundle(bundle, cells, BigValue.FromDouble(4.2e5));
                            var gift = new ReturnGiftCard
                            {
                                Big = true, DaysAway = 16,
                                Line0 = format.ReturnLine(DailyVisitText.ReturnLineWishes, 3),
                                Line1 = text.Get(DailyVisitText.ReturnLineGift),
                            };
                            offline.SetGift(gift, cells, count, text);
                            offline.Show(BigValue.FromDouble(2.6e6), text, null);
                            for (int i = 0; i < 30; i++) offline.Tick(0.05f);
                            var offlineCanvas = offline.GetComponent<Canvas>();
                            Debug.Log($"[DailyVisitCapture] offline open={offline.IsOpen} enabled={offlineCanvas.enabled} mode={offlineCanvas.renderMode} " +
                                $"camera={(offlineCanvas.worldCamera != null)} plane={offlineCanvas.planeDistance} content={offline.transform.Find("Content").gameObject.activeInHierarchy}");
                            Shot($"return_gift_{language}");
                            offline.ClearGift();
                            offline.transform.Find("Content").gameObject.SetActive(false);
                            offline.GetComponent<Canvas>().enabled = false;
                        }

                        // The refuge: the Today button with its dot, the Gems chip, three bubbles (one fulfilled).
                        hud.SetShop(true, 365);
                        hud.SetGems(true, 365);
                        hud.SetToday(true, true);
                        for (int i = 0; i < 20; i++) hud.Tick(0.05f);
                        var bubbles = new List<WishBubbleView>();
                        if (bubblePrefab != null)
                        {
                            for (int i = 0; i < wishes.Length && i < animals.Count; i++)
                                bubbles.Add(PlaceBubble(bubblePrefab, animals[i], camera, i, wishes[i], temp));
                        }
                        Shot($"refuge_bubbles_{language}");

                        // The wish card over Mochi's bubble.
                        if (card != null && bubbles.Count > 0)
                        {
                            var screen = camera.WorldToScreenPoint(bubbles[0].DrawnCenter);
                            var data = model.Wish(0).Card;
                            var cells = new RewardCell[RewardCells.Max];
                            cells[0] = new RewardCell(RewardKind.Gems, 3);
                            cells[1] = new RewardCell(RewardKind.Treat, 1, ContentId.FromSource("treat.clover_crunch"));
                            cells[2] = new RewardCell(RewardKind.Bond, 15);
                            card.Open(data, cells, 3, new Vector2(screen.x, screen.y), text, null);
                            for (int i = 0; i < 12; i++) card.Tick(0.05f);
                            Shot($"wish_card_{language}");
                            card.Close();
                            for (int i = 0; i < 10; i++) card.Tick(0.05f);
                        }
                        foreach (var bubble in bubbles)
                            Object.DestroyImmediate(bubble.gameObject);

                        // Today on day 1: the welcome gift, the closed gift box, three wishes, today's calendar box.
                        if (today != null)
                        {
                            today.Open(model, text, model);
                            for (int i = 0; i < 30; i++) today.Tick(0.05f);
                            Shot($"today_{language}");
                            model.OpenGiftNow(today);
                            for (int i = 0; i < 60; i++) today.Tick(0.05f);
                            Shot($"today_gift_opened_{language}");
                            model.Stamp(today);
                            for (int i = 0; i < 30; i++) today.Tick(0.05f);
                            Shot($"today_stamped_{language}");
                            today.Close();
                            for (int i = 0; i < 20; i++) today.Tick(0.05f);
                        }

                        // The first weekly journal: two goals ahead, one reached.
                        if (journal != null)
                        {
                            journal.Open(journalModel, text, journalModel);
                            for (int i = 0; i < 30; i++) journal.Tick(0.05f);
                            Shot($"journal_{language}");
                            journal.Close();
                            for (int i = 0; i < 20; i++) journal.Tick(0.05f);
                        }

                        // The animal sheet with its treat button (two Clover Crunch owned, the family's favorite).
                        if (sheet != null)
                        {
                            var data = new AnimalSheetData(1, "Mochi", text.Get("species.rabbit.name"), text.Get("variant.rabbit.brown.name"),
                                ContentId.FromSource("species.rabbit"), ContentId.FromSource("variant.rabbit.brown"), RarityTier.Common, true, 0.35f, 1, 0u);
                            sheet.Open(data, text, null);
                            sheet.SetTreat(new SheetTreatData { Available = true, Treat = ContentId.FromSource("treat.clover_crunch"), Owned = 2, Preferred = true });
                            for (int i = 0; i < 20; i++) sheet.Tick(0.05f);
                            Shot($"sheet_treat_{language}");
                            // Both buttons of the row: the treat on the left, « Brosser » with its free tag on the right.
                            sheet.SetGroom(true, true);
                            for (int i = 0; i < 20; i++) sheet.Tick(0.05f);
                            Shot($"sheet_treat_groom_{language}");
                            sheet.SetGroom(false, false);
                            sheet.Close();
                            for (int i = 0; i < 20; i++) sheet.Tick(0.05f);
                        }
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

        private static void ForceClose(OfflineReturnScreen offline)
        {
            var close = typeof(OfflineReturnScreen).GetMethod("Close", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            close?.Invoke(offline, null);
            if (offline.IsOpen)
                offline.gameObject.SetActive(false);
        }

        /// <summary>A bubble above the animal's head, facing the camera, at the game's size for this camera distance.</summary>
        private static WishBubbleView PlaceBubble(GameObject prefab, GameObject animal, Camera camera, int index, in WishRecord wish, List<GameObject> temp)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            temp.Add(go);
            var view = go.GetComponent<WishBubbleView>();
            float top = animal.transform.position.y;
            foreach (var renderer in animal.GetComponentsInChildren<Renderer>())
                top = Mathf.Max(top, renderer.bounds.max.y);
            float distance = Vector3.Distance(camera.transform.position, animal.transform.position);
            float radius = WishBubblePlacement.WorldRadius(distance, Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad));
            var center = new Vector3(animal.transform.position.x, top + radius * 2.0f, animal.transform.position.z);
            var state = new WishBubbleState
            {
                Slot = index, Animal = wish.Animal, Progress = wish.Progress, Target = wish.Target, Done = wish.IsDone, Tutorial = wish.IsTutorial,
                System = WishCatalog.TryGet((WishTemplateId)wish.Template, out var template) ? template.System : WishSystem.None,
            };
            view.Prewarm(index);
            view.Begin(state, false);
            view.Place(center, camera.transform.rotation, radius, false);
            view.SetVisible(true, false);
            for (int i = 0; i < 40; i++)
                view.Tick(0.05f);
            return view;
        }

        /// <summary>Three wishes of three shipped systems (Hatching, care, discoveries), Mochi's one fulfilled.</summary>
        private static WishRecord[] SampleWishes(PlayerState state)
        {
            var systems = new[] { WishSystem.Hatching, WishSystem.CuddlesAndTreats, WishSystem.DiscoveriesAndMastery };
            var wishes = new WishRecord[systems.Length];
            for (int s = 0; s < systems.Length; s++)
            {
                foreach (var template in WishCatalog.All)
                {
                    if (!template.ShippedInThisBuild || template.System != systems[s] || template.Timer)
                        continue;
                    var record = new WishRecord
                    {
                        Template = (ushort)template.Id,
                        Animal = (uint)(s + 1),
                        ParamKind = (byte)template.ParamKind,
                        Target = System.Math.Max(1, template.Amount(1)),
                    };
                    if (template.ParamKind == WishParamKind.Species)
                        record.Param = ContentId.FromSource("species.rabbit");
                    else if (template.ParamKind == WishParamKind.Biome || template.ParamKind == WishParamKind.Upgrade)
                        record.Param = ContentId.FromSource("biome.grassland");
                    record.Progress = s == 0 ? record.Target : record.Target / 2;
                    if (s == 0)
                        record.Flags = WishRecord.FlagDone;
                    wishes[s] = record;
                    break;
                }
            }
            return wishes;
        }

        /// <summary>The first journal's seven goals of the shipped set, one per slot when it exists.</summary>
        private static JournalGoalRecord[] SampleGoals()
        {
            var goals = new List<JournalGoalRecord>();
            foreach (var goal in WeeklyJournalCatalog.All)
            {
                if (!goal.ShippedInThisBuild || goals.Count >= 7)
                    continue;
                int target = System.Math.Max(1, goal.Target(1));
                goals.Add(new JournalGoalRecord
                {
                    Goal = (ushort)goal.Id, Slot = (byte)goal.Slot, Target = target,
                    Progress = goals.Count == 0 ? target : goals.Count == 1 ? target / 2 : goals.Count == 2 ? target / 3 : 0,
                });
            }
            return goals.ToArray();
        }

        private sealed class SampleToday : ITodayModel, ITodayActions
        {
            private readonly TodayWishRow[] _rows;
            private readonly CalendarBoxCell[] _boxes = new CalendarBoxCell[DailyRewardTables.CalendarBoxCount];
            private readonly RewardContext _rewards = new RewardContext(false, false, false, false, ContentId.FromSource("treat.clover_crunch"), 15.0);
            private readonly DailyVisitConfig _config = new DailyVisitConfig();
            private readonly RewardCell[] _scratch = new RewardCell[RewardCells.Max];
            private int _stamped = 4;
            private bool _today = true;

            public SampleToday(ContentRegistry registry, DailyVisitTextFormatter format, WishRecord[] wishes, PlayerState state)
            {
                _rows = new TodayWishRow[wishes.Length];
                for (int i = 0; i < wishes.Length; i++)
                {
                    var wish = wishes[i];
                    state.Collection.TryGet(wish.Animal, out var animal);
                    _rows[i] = new TodayWishRow
                    {
                        Card = new WishCardData
                        {
                            Text = format.Wish(wish), Progress = wish.Progress, Target = wish.Target, Done = wish.IsDone,
                            System = WishCatalog.TryGet((WishTemplateId)wish.Template, out var t) ? t.System : WishSystem.Hatching, CanSwap = !wish.IsDone,
                        },
                        Animal = wish.Animal, Variant = animal.Variant, AnimalName = format.AnimalName(wish.Animal), Gems = 3,
                    };
                }
                Rebuild();
            }

            private void Rebuild()
            {
                for (int box = 0; box < _boxes.Length; box++)
                {
                    var bundle = DailyRewardTables.CalendarBox(box, 1, _rewards, _config);
                    _boxes[box] = new CalendarBoxCell
                    {
                        Reward = RewardCells.FromBundle(bundle, _scratch) > 0 ? _scratch[0] : default,
                        Big = DailyRewardTables.IsBigBox(box),
                        Stamped = box < _stamped,
                        Today = _today ? box == _stamped : box == _stamped - 1,
                    };
                }
            }

            public int WelcomeIndex => 0;
            public bool WelcomeClaimable => true;
            public int WelcomeRewards(RewardCell[] into) => RewardCells.FromBundle(DailyRewardTables.Welcome(0, _rewards, _config), into);
            public TodayGiftState Gift { get; private set; } = TodayGiftState.Ready;
            public GiftSize GiftSize => GiftSize.Nice;

            public int GiftRewards(RewardCell[] into)
            {
                if (Gift != TodayGiftState.Opened)
                    return 0;
                var bundle = DailyRewardTables.Gift(GiftLot.Egg, _rewards);
                bundle.Gems = 5;
                bundle.LightMinutes = 30;
                return RewardCells.FromBundle(bundle, into, BigValue.FromDouble(61200));
            }

            public int WishCount => _rows.Length;
            public TodayWishRow Wish(int row) => _rows[row];
            public int SwapsLeft => 1;
            public bool AnyWishDone => true;
            public int CalendarCycle => 1;
            public int CalendarNext => _stamped;
            public bool CalendarClaimable => _today;
            public CalendarBoxCell Box(int box) => _boxes[box];

            public RewardCell Tomorrow
            {
                get
                {
                    var bundle = DailyRewardTables.CalendarBox(_today ? _stamped + 1 : _stamped, 1, _rewards, _config);
                    return RewardCells.FromBundle(bundle, _scratch) > 0 ? _scratch[0] : default;
                }
            }

            public int DaysToBigBox => 7 - _stamped % 7;
            public bool JournalVisible => true;
            public int JournalReached => 1;
            public int JournalGoals => 7;
            public bool JournalDot => true;

            public void OpenGiftNow(TodayScreen view)
            {
                Gift = TodayGiftState.Opened;
                view.PlayGiftOpened();
                view.Refresh();
            }

            public void Stamp(TodayScreen view)
            {
                _today = false;
                _stamped++;
                Rebuild();
                view.PlayStamp(_stamped - 1);
                view.Refresh();
            }

            public void ClaimWelcome() { }
            public void OpenGift() { }
            public void ClaimWish(int row) { }
            public void ClaimAllWishes() { }
            public void SwapWish(int row) { }
            public void StampCalendar() { }
            public void OpenJournal() { }
            public void OpenGiftInfo() { }
            public void CloseToday() { }
        }

        private sealed class SampleJournal : IJournalModel, IJournalActions
        {
            private readonly JournalGoalRow[] _rows;

            public SampleJournal(DailyVisitTextFormatter format, JournalGoalRecord[] goals)
            {
                _rows = new JournalGoalRow[goals.Length];
                for (int i = 0; i < goals.Length; i++)
                {
                    var goal = goals[i];
                    _rows[i] = new JournalGoalRow
                    {
                        Text = format.Goal(goal), Progress = goal.Progress, Target = goal.Target, Reached = goal.IsReached, Slot = (JournalSlot)goal.Slot,
                        HeadStart = i == 1 || i == 2,
                        Reward0 = new RewardCell(RewardKind.Gems, 5),
                        Reward1 = new RewardCell(RewardKind.Light, 0) { LightMinutes = 30 },
                    };
                }
            }

            public int GoalCount => _rows.Length;
            public JournalGoalRow Goal(int i) => _rows[i];
            public int DaysLeft => 4;
            public bool IsFirst => true;
            public bool CanClaimComplete => false;
            public bool CompleteClaimed => false;

            public int CompleteRewards(RewardCell[] into)
            {
                into[0] = new RewardCell(RewardKind.Gems, 15);
                into[1] = new RewardCell(RewardKind.DiscoveryEgg, 1);
                return 2;
            }

            public bool HasCarry => false;
            public int CarryRewards(RewardCell[] into) => 0;
            public void ClaimGoal(int i) { }
            public void ClaimComplete() { }
            public void ClaimCarry() { }
            public void CloseJournal() { }
        }
    }
}
