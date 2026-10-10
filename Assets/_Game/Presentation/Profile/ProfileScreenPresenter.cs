using System;
using PocketWilds.Core;
using PocketWilds.Services.Feedback;
using PocketWilds.Simulation;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Object = UnityEngine.Object;

namespace PocketWilds.Presentation
{
    /// <summary>
    /// T44B « Succès, titres, cadres, poses » (doc « 01 Succès, titres, cadres, poses et journal », sections 1, 3, 4, 5;
    /// screen I-SCR-020). The refuge HUD's Succès button (a dot while a tier waits) opens PF_UI_Achievements (Addressables
    /// group Profile, address profile.screen: loaded when opened, released when closed) over a hidden refuge: the profile
    /// card (the favorite animal's codex portrait inside the selected frame, the selected title under it) and four tabs.
    /// Succès: category chips over a grid of badges in the 4 shared shapes with the category's icon, the goal line with the
    /// next tier's threshold, a progress bar, « Plus que 3 » and the claim button with its Gems. Titres: « Aucun titre » then
    /// the 322 titles in gallery order, owned ones selectable, locked ones with their source. Cadres: the 42 frames around
    /// the portrait. Poses: the 47 poses with « Mode photo bientôt ». The lists are recycled (12 badge cells for 200
    /// achievements); the models are rebuilt from <see cref="IAchievements.Changed"/> and <see cref="IProfileItems.Changed"/>,
    /// never by polling; nothing allocates per frame (name keys are built once at the first opening, labels from char buffers).
    /// </summary>
    public sealed class ProfileScreenPresenter : IProfileScreenActions, IProfileHudActions, IDisposable
    {
        private readonly GameContext _context;
        private readonly IAchievements _achievements;
        private readonly IProfileItems _items;
        private readonly ILocalization _text;
        private readonly Transform _parent;
        private readonly GameObject _prefab;
        private readonly Action _onChanged;
        private IProfileHud _hud;

        private AsyncOperationHandle<GameObject> _handle;
        private bool _ownsHandle;
        private GameObject _screenObject;
        private IProfileScreenView _view;
        private bool _failed;

        private bool _screenOpen;
        private bool _screenShown;
        private ProfileTab _tab;
        private int _chip;
        private bool _dirty;
        private bool _hudDirty = true;

        // Built once at the first opening.
        private bool _indexed;
        private string[] _achievementName = Array.Empty<string>();
        private string[] _achievementGoal = Array.Empty<string>();
        private int[] _filtered = Array.Empty<int>();
        private int _filteredCount;
        private readonly int[][] _order = new int[3][];
        private readonly string[][] _itemName = new string[3][];
        private readonly string[][] _itemSource = new string[3][];
        private readonly bool[] _chipDot = new bool[ProfileLayout.Chips];

        private const AudioEventId _openSound = AudioEventId.PanelOpen;
        private const AudioEventId _claimSound = AudioEventId.StarEarned;

        /// <summary>
        /// The shipped wiring: the screen under the activity host's root, the refuge hidden and its HUD faded with the Play
        /// HUD's « playing » state while the screen shows. Null without achievements.
        /// </summary>
        public static ProfileScreenPresenter Create(GameContext context, IAchievements achievements, IProfileItems items, ActivityPresenter activities,
            IProfileHud hud)
        {
            if (achievements == null || items == null)
                return null;
            var host = activities?.Host;
            var presenter = new ProfileScreenPresenter(context, achievements, items, hud, host?.Root, null);
            if (activities != null)
            {
                presenter.SetWorldVisible = visible => activities.Host?.SetWorldVisible?.Invoke(visible);
                presenter.SetHudHidden = hidden => activities.PlayHud?.SetPlaying(hidden);
            }
            return presenter;
        }

        /// <summary>Tests and captures: a given parent and screen prefab (else the Addressables one).</summary>
        public ProfileScreenPresenter(GameContext context, IAchievements achievements, IProfileItems items, IProfileHud hud, Transform parent,
            GameObject screenPrefab)
        {
            _context = context;
            _achievements = achievements ?? throw new ArgumentNullException(nameof(achievements));
            _items = items ?? throw new ArgumentNullException(nameof(items));
            _text = context?.Localization ?? KeysOnly.Instance;
            _parent = parent;
            _prefab = screenPrefab;
            _onChanged = OnChanged;
            _achievements.Changed += _onChanged;
            _items.Changed += _onChanged;
            BindHud(hud);
        }

        /// <summary>The refuge shows or hides under the screen (the activity host's world).</summary>
        public Action<bool> SetWorldVisible { get; set; }

        /// <summary>The refuge HUD fades while the screen shows.</summary>
        public Action<bool> SetHudHidden { get; set; }

        /// <summary>The favorite animal's codex portrait (read when the screen shows; null: the paw).</summary>
        public Func<Sprite> Portrait { get; set; }

        /// <summary>The first favorite animal's variant (else the newest animal's), for the card's portrait; called on a change only.</summary>
        private ContentId FavoriteVariant()
        {
            var collection = _context != null && _context.Save != null ? _context.State.Collection : null;
            if (collection == null || collection.Count == 0)
                return ContentId.None;
            for (int i = 0; i < collection.Count; i++)
                if (collection[i].IsFavorite)
                    return collection[i].Variant;
            return collection[collection.Count - 1].Variant;
        }

        /// <summary>Whether the HUD's Succès button shows (default: always; every achievement is visible from the start).</summary>
        public Func<bool> HudVisible { get; set; }

        /// <summary>Reduce Motion for captures and tests without a save.</summary>
        public bool? ReduceMotionOverride { get; set; }

        public bool IsOpen => _screenOpen;

        public bool ScreenShown => _screenShown;

        public bool ScreenLoaded => _handle.IsValid();

        public ProfileTab Tab => _tab;

        public int Chip => _chip;

        public IProfileScreenView View => _view;

        public GameObject ScreenObject => _screenObject;

        public IAchievements Achievements => _achievements;

        public IProfileItems Items => _items;

        /// <summary>The badges shown with the current filter, and the achievement at a position.</summary>
        public int FilteredCount => _filteredCount;

        public int AchievementAt(int item) => (uint)item < (uint)_filteredCount ? _filtered[item] : -1;

        /// <summary>The gallery position of a title, frame or pose (titles start at 1: row 0 is « Aucun titre »).</summary>
        public int ItemOfIndex(ProfileItemKind kind, int index)
        {
            var order = _order[(int)kind];
            if (order == null)
                return -1;
            for (int i = 0; i < order.Length; i++)
                if (order[i] == index)
                    return kind == ProfileItemKind.Title ? i + 1 : i;
            return -1;
        }

        public void BindHud(IProfileHud hud)
        {
            _hud = hud;
            _hud?.BindProfile(this);
            _hudDirty = true;
        }

        private void OnChanged()
        {
            _dirty = true;
            _hudDirty = true;
        }

        // ---------------- opening and closing ----------------

        public void OpenProfile()
        {
            if (_screenOpen)
                return;
            _screenOpen = true;
            _tab = ProfileTab.Achievements;
            _chip = 0;
            Sound(_openSound);
            Haptic(HapticEventId.TapLight);
            EnsureView();
            Advance();
        }

        public void CloseScreen()
        {
            if (!_screenOpen)
                return;
            Sound(AudioEventId.UiBack);
            Close();
        }

        private void Close()
        {
            MarkSeen(_tab);
            _screenOpen = false;
            if (_screenShown)
            {
                _screenShown = false;
                SetWorldVisible?.Invoke(true);
                SetHudHidden?.Invoke(false);
            }
            Release();
            _hudDirty = true;
        }

        private void EnsureView()
        {
            if (_view != null || _failed)
                return;
            if (_prefab != null)
                BuildScreen(_prefab);
            else
                StartLoading();
        }

        private void StartLoading()
        {
            if (_handle.IsValid())
                return;
            if (!AddressExists(ProfileLayout.ScreenAddress, typeof(GameObject)))
            {
                _failed = true;
                GameLog.Error(LogCategory.Presentation,
                    $"Profile: no Addressables entry {ProfileLayout.ScreenAddress} (run Pocket Wilds/Profile/Build Profile Screen (T44B)).");
                return;
            }
            _handle = Addressables.LoadAssetAsync<GameObject>(ProfileLayout.ScreenAddress);
            _ownsHandle = true;
        }

        private static bool AddressExists(string address, Type type)
        {
            if (string.IsNullOrEmpty(address))
                return false;
            foreach (var locator in Addressables.ResourceLocators)
                if (locator.Locate(address, type, out _))
                    return true;
            return false;
        }

        private void Advance()
        {
            if (_view == null && !_failed && _handle.IsValid() && _handle.IsDone)
            {
                if (_handle.Status != AsyncOperationStatus.Succeeded || _handle.Result == null)
                {
                    _failed = true;
                    GameLog.Error(LogCategory.Presentation, "Profile: the screen did not load (" + ProfileLayout.ScreenAddress + ").");
                }
                else
                {
                    BuildScreen(_handle.Result);
                }
            }
            if (_view == null)
            {
                if (_failed)
                {
                    _screenOpen = false;
                    Release();
                }
                return;
            }
            if (_screenOpen && !_screenShown)
                ShowScreen();
        }

        private void BuildScreen(GameObject prefab)
        {
            _screenObject = Object.Instantiate(prefab, _parent);
            _screenObject.name = ProfileLayout.ScreenPrefabName;
            _view = _screenObject.GetComponent<IProfileScreenView>();
            if (_view == null || !_view.Valid)
            {
                GameLog.Error(LogCategory.Presentation, "Profile: the screen prefab has no complete ProfileScreenView (rebuild it with ProfileScreenPipeline).");
                DestroyObject(_screenObject);
                _screenObject = null;
                _view = null;
                _failed = true;
                return;
            }
            Index();
            _view.Bind(_text, this);
            _view.Hide();
        }

        private void ShowScreen()
        {
            _screenShown = true;
            _view.ReduceMotion = ReduceMotion;
            _view.Show();
            SetWorldVisible?.Invoke(false);
            SetHudHidden?.Invoke(true);
            BindCard();
            BindChips();
            BindTabDots();
            ShowTab(_tab, true);
            _dirty = false;
            _view.Tick(0f);
        }

        private void Release()
        {
            _view = null;
            _screenShown = false;
            if (_screenObject != null)
                DestroyObject(_screenObject);
            _screenObject = null;
            if (_ownsHandle && _handle.IsValid())
                Addressables.Release(_handle);
            _handle = default;
            _ownsHandle = false;
            _failed = false;
        }

        // ---------------- indexing ----------------

        /// <summary>The name keys and the galleries' order (allocates once, at the first opening).</summary>
        private void Index()
        {
            if (_indexed)
                return;
            _indexed = true;
            int count = Mathf.Max(0, _achievements.Count);
            _achievementName = new string[count];
            _achievementGoal = new string[count];
            _filtered = new int[count];
            for (int i = 0; i < count; i++)
            {
                var info = _achievements.At(i);
                _achievementName[i] = ProfileText.Name(info.Key);
                _achievementGoal[i] = ProfileText.Goal(info.Key);
            }
            for (int k = 0; k < 3; k++)
            {
                var kind = (ProfileItemKind)k;
                int n = Mathf.Max(0, _items.Count(kind));
                var order = new int[n];
                var keys = new int[n];
                _itemName[k] = new string[n];
                _itemSource[k] = new string[n];
                for (int i = 0; i < n; i++)
                {
                    var info = _items.At(kind, i);
                    order[i] = i;
                    keys[i] = info.Order;
                    _itemName[k][i] = ProfileText.Name(info.Key);
                    _itemSource[k][i] = ProfileText.Source(info.Key);
                }
                Array.Sort(keys, order);
                _order[k] = order;
            }
            Filter();
        }

        private void Filter()
        {
            _filteredCount = 0;
            int count = _filtered.Length;
            for (int i = 0; i < count; i++)
                if (_chip == 0 || (int)_achievements.At(i).Category == _chip - 1)
                    _filtered[_filteredCount++] = i;
        }

        // ---------------- Tick ----------------

        /// <summary>Per frame from the refuge (unscaled time). No allocation while the screen shows.</summary>
        public void Tick(float deltaTime)
        {
            if (_view == null && _screenOpen)
                Advance();
            if (_screenShown && _view != null)
            {
                if (_dirty)
                {
                    _dirty = false;
                    BindCard();
                    BindChips();
                    BindTabDots();
                    _view.RefreshList(ListOf(_tab));
                }
                _view.Tick(deltaTime);
            }
            if (_hudDirty)
            {
                _hudDirty = false;
                UpdateHud();
            }
        }

        private void UpdateHud()
        {
            if (_hud == null)
                return;
            bool visible = HudVisible == null || HudVisible();
            _hud.SetProfile(visible, visible && _achievements.Claimable > 0);
        }

        // ---------------- the card, the chips, the tabs ----------------

        private void BindCard()
        {
            int title = _items.Selected(ProfileItemKind.Title);
            int frame = _items.Selected(ProfileItemKind.Frame);
            bool hasFrame = (uint)frame < (uint)_items.Count(ProfileItemKind.Frame);
            _view.SetCard(new ProfileCardModel
            {
                Portrait = Portrait?.Invoke(),
                PortraitVariant = FavoriteVariant(),
                FrameKey = hasFrame ? _items.At(ProfileItemKind.Frame, frame).Key : null,
                Title = (uint)title < (uint)_items.Count(ProfileItemKind.Title) ? Text(_itemName[0][title]) : Text(ProfileText.NoTitle),
                FrameName = hasFrame ? Text(_itemName[1][frame]) : Text(ProfileText.PlainFrame),
            });
        }

        private void BindChips()
        {
            for (int c = 0; c < ProfileLayout.Chips; c++)
                _chipDot[c] = false;
            int count = _achievementName.Length;
            for (int i = 0; i < count; i++)
            {
                if (_achievements.Claimed(i) >= _achievements.Reached(i))
                    continue;
                _chipDot[0] = true;
                int chip = (int)_achievements.At(i).Category + 1;
                if ((uint)chip < ProfileLayout.Chips)
                    _chipDot[chip] = true;
            }
            for (int c = 0; c < ProfileLayout.Chips; c++)
            {
                var category = (AchievementCategory)Mathf.Max(0, c - 1);
                _view.SetChip(c, new ProfileChipModel
                {
                    Shown = true,
                    All = c == 0,
                    Category = category,
                    Label = Text(c == 0 ? ProfileText.All : ProfileText.Category(category)),
                    Selected = c == _chip,
                    Dot = _chipDot[c],
                });
            }
        }

        private void BindTabDots()
        {
            _view.SetTabDots(_achievements.Claimable > 0, AnyUnseen(ProfileItemKind.Title), AnyUnseen(ProfileItemKind.Frame),
                AnyUnseen(ProfileItemKind.Pose));
        }

        private bool AnyUnseen(ProfileItemKind kind)
        {
            int n = _items.Count(kind);
            for (int i = 0; i < n; i++)
                if (_items.Unseen(kind, i))
                    return true;
            return false;
        }

        public void SelectTab(ProfileTab tab)
        {
            if (!_screenShown || tab == _tab)
                return;
            Sound(AudioEventId.UiTap);
            // « Nouveau » shows during the visit of the tab, and counts as seen when leaving it.
            MarkSeen(_tab);
            ShowTab(tab, true);
            BindTabDots();
        }

        private void ShowTab(ProfileTab tab, bool toTop)
        {
            _tab = tab;
            _view.SetTab(tab);
            var list = ListOf(tab);
            _view.SetListCount(list, ItemCount(tab), toTop);
        }

        private void MarkSeen(ProfileTab tab)
        {
            if (tab == ProfileTab.Achievements)
                return;
            var kind = KindOf(tab);
            if (AnyUnseen(kind))
                _items.MarkSeen(kind);
        }

        private int ItemCount(ProfileTab tab) => tab switch
        {
            ProfileTab.Achievements => _filteredCount,
            ProfileTab.Titles => _items.Count(ProfileItemKind.Title) + 1,
            ProfileTab.Frames => _items.Count(ProfileItemKind.Frame),
            _ => _items.Count(ProfileItemKind.Pose),
        };

        private static ProfileList ListOf(ProfileTab tab) =>
            tab == ProfileTab.Achievements ? ProfileList.Badges : tab == ProfileTab.Frames ? ProfileList.Frames : ProfileList.Rows;

        private static ProfileItemKind KindOf(ProfileTab tab) =>
            tab == ProfileTab.Frames ? ProfileItemKind.Frame : tab == ProfileTab.Poses ? ProfileItemKind.Pose : ProfileItemKind.Title;

        public void SelectCategory(int chip)
        {
            if (!_screenShown || (uint)chip >= ProfileLayout.Chips)
                return;
            if (chip == _chip && _tab == ProfileTab.Achievements)
                return;
            Sound(AudioEventId.UiTap);
            _chip = chip;
            Filter();
            BindChips();
            if (_tab != ProfileTab.Achievements)
            {
                MarkSeen(_tab);
                BindTabDots();
            }
            ShowTab(ProfileTab.Achievements, true);
        }

        // ---------------- the cells ----------------

        public void FillCell(ProfileList list, int cell, int item)
        {
            if (_view == null)
                return;
            switch (list)
            {
                case ProfileList.Badges:
                    _view.SetBadge(cell, BadgeModel(item));
                    break;
                case ProfileList.Frames:
                    _view.SetFrameCell(cell, FrameModel(item));
                    break;
                default:
                    _view.SetRow(cell, RowModel(item));
                    break;
            }
        }

        private AchievementCellModel BadgeModel(int item)
        {
            if ((uint)item >= (uint)_filteredCount)
                return default;
            int index = _filtered[item];
            var info = _achievements.At(index);
            var reached = _achievements.Reached(index);
            var claimed = _achievements.Claimed(index);
            long progress = _achievements.Progress(index);
            bool top = reached >= AchievementTier.Star;
            var next = top ? AchievementTier.Star : reached + 1;
            long from = reached == AchievementTier.None || top ? 0 : info.Threshold(reached);
            long to = info.Threshold(next);
            float fill = top ? 1f : to > from ? Mathf.Clamp01((progress - from) / (float)(to - from)) : 0f;
            return new AchievementCellModel
            {
                Shown = true,
                Name = Text(_achievementName[index]),
                Goal = Text(_achievementGoal[index]),
                Next = to,
                Left = top ? 0 : Math.Max(0L, to - progress),
                Fill = fill,
                Category = info.Category,
                Reached = reached,
                Claimable = claimed < reached,
                Gems = ProfileLayout.GemsBetween(claimed, reached),
                Complete = top && claimed >= AchievementTier.Star,
            };
        }

        /// <summary>The title, frame or pose index at a gallery position (titles: -1 for the « Aucun titre » row).</summary>
        private int IndexAt(ProfileItemKind kind, int item)
        {
            var order = _order[(int)kind];
            if (order == null)
                return -2;
            int at = kind == ProfileItemKind.Title ? item - 1 : item;
            if (kind == ProfileItemKind.Title && at == -1)
                return -1;
            return (uint)at < (uint)order.Length ? order[at] : -2;
        }

        private ProfileRowModel RowModel(int item)
        {
            var kind = KindOf(_tab);
            int index = IndexAt(kind, item);
            if (index == -2)
                return default;
            if (index == -1)
                return new ProfileRowModel
                {
                    Shown = true, Kind = kind, None = true, Name = Text(ProfileText.NoTitle), Source = Text(ProfileText.NoTitleSource), Owned = true,
                    Selected = _items.Selected(ProfileItemKind.Title) < 0,
                };
            int k = (int)kind;
            bool owned = _items.Owned(kind, index);
            return new ProfileRowModel
            {
                Shown = true,
                Kind = kind,
                Name = Text(_itemName[k][index]),
                Source = Text(_itemSource[k][index]),
                Owned = owned,
                Selected = kind == ProfileItemKind.Title && owned && _items.Selected(kind) == index,
                New = owned && _items.Unseen(kind, index),
            };
        }

        private ProfileFrameCellModel FrameModel(int item)
        {
            int index = IndexAt(ProfileItemKind.Frame, item);
            if (index < 0)
                return default;
            bool owned = _items.Owned(ProfileItemKind.Frame, index);
            return new ProfileFrameCellModel
            {
                Shown = true,
                FrameKey = _items.At(ProfileItemKind.Frame, index).Key,
                Name = Text(_itemName[1][index]),
                Owned = owned,
                Selected = owned && _items.Selected(ProfileItemKind.Frame) == index,
                New = owned && _items.Unseen(ProfileItemKind.Frame, index),
            };
        }

        public void TapCell(ProfileList list, int item)
        {
            if (!_screenShown)
                return;
            if (list == ProfileList.Badges)
            {
                if ((uint)item >= (uint)_filteredCount)
                    return;
                int index = _filtered[item];
                if (_achievements.Claimed(index) < _achievements.Reached(index))
                {
                    Claim(item);
                    return;
                }
                return;
            }
            var kind = list == ProfileList.Frames ? ProfileItemKind.Frame : KindOf(_tab);
            int at = IndexAt(kind, item);
            if (at == -2)
                return;
            if (kind == ProfileItemKind.Pose)
            {
                bool owned = _items.Owned(kind, at);
                _view.ShowNote(owned ? Text(ProfileText.PhotoSoon) : Text(_itemSource[2][at]));
                Sound(owned ? AudioEventId.UiTap : AudioEventId.ErrorSoft);
                return;
            }
            if (at >= 0 && !_items.Owned(kind, at))
            {
                _view.ShowNote(Text(_itemSource[(int)kind][at]));
                Sound(AudioEventId.ErrorSoft);
                return;
            }
            if (_items.Selected(kind) == at)
                return;
            if (!_items.TrySelect(kind, at))
            {
                Sound(AudioEventId.ErrorSoft);
                return;
            }
            Sound(AudioEventId.UiConfirm);
            Haptic(HapticEventId.TapLight);
            // Changed comes from the items too; the card and the visible cells follow now.
            BindCard();
            _view.RefreshList(list);
            _dirty = false;
        }

        public void Claim(int item)
        {
            if (!_screenShown || (uint)item >= (uint)_filteredCount)
                return;
            int index = _filtered[item];
            int gems = ProfileLayout.GemsBetween(_achievements.Claimed(index), _achievements.Reached(index));
            if (gems <= 0 || !_achievements.TryClaim(index))
            {
                Sound(AudioEventId.ErrorSoft);
                return;
            }
            Sound(_claimSound);
            Haptic(HapticEventId.Confirm);
            _view.PlayClaim(item);
            _view.ShowNote(ClaimedText(gems));
            BindChips();
            BindTabDots();
            BindCard();
            _view.RefreshList(ProfileList.Badges);
            _dirty = false;
            _hudDirty = true;
        }

        private string _claimedOne;
        private string _claimedOther;
        private readonly char[] _noteBuffer = new char[64];
        private readonly string[] _claimedCache = new string[16];
        private readonly int[] _claimedCacheGems = new int[16];

        /// <summary>« +35 Gemmes »: the few sums there are (5 to 85) are kept once written.</summary>
        private string ClaimedText(int gems)
        {
            for (int i = 0; i < _claimedCache.Length; i++)
                if (_claimedCache[i] != null && _claimedCacheGems[i] == gems)
                    return _claimedCache[i];
            _claimedOne ??= _text.GetPlural(ProfileText.Claimed, 1);
            _claimedOther ??= _text.GetPlural(ProfileText.Claimed, 2);
            string template = PluralForms.IsOne(_text.Language, gems) ? _claimedOne : _claimedOther;
            int length = 0;
            for (int c = 0; c < template.Length && length < _noteBuffer.Length; c++)
            {
                if (template[c] == '{' && c + 2 < template.Length && template[c + 1] == 'n' && template[c + 2] == '}')
                {
                    string digits = gems.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    for (int d = 0; d < digits.Length && length < _noteBuffer.Length; d++)
                        _noteBuffer[length++] = digits[d];
                    c += 2;
                    continue;
                }
                _noteBuffer[length++] = template[c];
            }
            string text = new string(_noteBuffer, 0, length);
            for (int i = 0; i < _claimedCache.Length; i++)
                if (_claimedCache[i] == null)
                {
                    _claimedCache[i] = text;
                    _claimedCacheGems[i] = gems;
                    break;
                }
            return text;
        }

        // ---------------- helpers ----------------

        private bool ReduceMotion => ReduceMotionOverride ?? (_context != null && _context.Save != null && _context.State.Settings.ReduceMotion);

        private string Text(string key) => key != null ? _text.Get(key) : string.Empty;

        private void Sound(AudioEventId id)
        {
            if (_context != null && _context.Audio != null)
                _context.Audio.Play(id, Vector3.zero, default);
        }

        private void Haptic(HapticEventId id)
        {
            if (_context != null && _context.Haptics != null)
                _context.Haptics.Play(id);
        }

        private static void DestroyObject(Object target)
        {
            if (target == null)
                return;
            if (Application.isPlaying)
                Object.Destroy(target);
            else
                Object.DestroyImmediate(target);
        }

        public void Dispose()
        {
            _achievements.Changed -= _onChanged;
            _items.Changed -= _onChanged;
            if (_screenOpen)
                Close();
            Release();
        }

        /// <summary>Headless scenes without localization: keys stand for texts.</summary>
        private sealed class KeysOnly : ILocalization
        {
            public static readonly KeysOnly Instance = new KeysOnly();

            public string Language => "en";

            public string Get(string key) => key;
        }
    }
}
