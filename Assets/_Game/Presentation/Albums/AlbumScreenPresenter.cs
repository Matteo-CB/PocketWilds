using System;
using System.Collections.Generic;
using PocketWilds.Core;
using PocketWilds.Services.Feedback;
using PocketWilds.Simulation;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.U2D;
using Object = UnityEngine.Object;

namespace PocketWilds.Presentation
{
    /// <summary>
    /// T44A « Albums », the screen and the reveal (doc « 01 Albums et autocollants », sections 1.2, 1.7, 1.10, 1.12, 1.13).
    /// The refuge HUD's Albums button opens PF_UI_Album (Addressables group Albums, address albums.screen: loaded when opened,
    /// released when closed) over a hidden refuge: the shelf of 16 covers with their progress and « Choisir un autocollant »
    /// from 10 album stars; a page of 12 recycled slots (owned with its rarity sign, empty as a silhouette with its source's
    /// icon, « bientôt » when its subject is not in this build) with « 6/12 », the half and full rewards; a horizontal swipe
    /// between the album's open pages, whose atlases (<see cref="StickerArtCatalog"/>, one Addressables entry per page) load
    /// with the page and its neighbours and are released after. The choose flow lists the open pages with stickers to find,
    /// then « 10 étoiles pour cet autocollant ? ». The reveal layer drains <see cref="IAlbums"/>'s pending gains over the
    /// refuge (never while an activity, the hub or a meta screen is open) or over the screen: the pouch, the rarity's exit,
    /// the paste (moment 21) flying to the albums icon or pasted straight onto its open page, « Déjà collé : +{n} étoiles »
    /// for duplicates; Common first, then Rare, then Shiny, 250 ms apart. Ticked by the refuge; nothing allocates per frame
    /// (models are rebuilt when something changes, labels from char buffers).
    /// </summary>
    public sealed class AlbumScreenPresenter : IAlbumScreenActions, IAlbumHudActions, IDisposable
    {
        /// <summary>The HUD button's state is checked twice a second (never per frame).</summary>
        private const float CheckInterval = 0.5f;

        /// <summary>A reveal waits this long at most for its page atlas (then its art shows as a silhouette).</summary>
        private const float AtlasWait = 1.5f;

        private readonly GameContext _context;
        private readonly IAlbums _albums;
        private readonly ILocalization _text;
        private readonly Transform _parent;
        private readonly GameObject _prefab;
        private readonly Action _onGained;
        private IAlbumHud _hud;

        private float _clock;
        private float _nextCheck;

        // Loading (the screen, or the reveal layer alone over the refuge).
        private AsyncOperationHandle<GameObject> _handle;
        private bool _ownsHandle;
        private GameObject _screenObject;
        private IAlbumScreenView _view;
        private bool _failed;

        // The screen.
        private bool _screenOpen;
        private bool _screenShown;
        private AlbumScreenMode _mode;
        private int _album = -1;
        private int _page = -1;
        private int _confirmSticker = -1;
        private bool _slotsDirty;
        private bool _coversDirty;

        // Albums and pages (built once at the first opening).
        private bool _indexed;
        private int[] _albumPages = Array.Empty<int>();
        private int[] _albumFirst = Array.Empty<int>();
        private int[] _albumPageCount = Array.Empty<int>();
        private readonly int[] _coverAlbum = new int[AlbumLayout.AlbumCapacity];
        private int _coverCount;
        private readonly int[] _rowPage = new int[AlbumLayout.PageCapacity];
        private int _rowCount;
        private string[] _albumNameKey = Array.Empty<string>();
        private string[] _pageNameKey = Array.Empty<string>();
        private string[] _stickerNameKey = Array.Empty<string>();

        // Page atlases.
        private StickerArtCatalog _catalog;
        private bool _catalogRead;
        private string[] _pageAtlas = Array.Empty<string>();
        private string[] _spriteName = Array.Empty<string>();
        private readonly AtlasSlot[] _atlases = new AtlasSlot[AlbumLayout.AtlasCapacity];

        // The reveal.
        private readonly StickerGain[] _queue = new StickerGain[AlbumLayout.RevealQueueCapacity];
        private int _queueCount;
        private int _queueAt;
        private bool _revealing;
        private bool _revealShown;
        private float _gapLeft;
        private float _waited;
        private float _pouchAge;
        private bool _opened;
        private float _sinceOpen;
        private int _revealPage = -1;

        private const AudioEventId _openSound = AudioEventId.PanelOpen;
        private const AudioEventId _pasteSound = AudioEventId.UiConfirm;
        private const AudioEventId _starSound = AudioEventId.StarEarned;

        private sealed class AtlasSlot
        {
            public int Page = -1;
            public string Address;
            public AsyncOperationHandle<SpriteAtlas> Handle;
            public bool Loading;
            public bool Ready;
            public readonly Sprite[] Sprites = new Sprite[AlbumLayout.SlotsPerPage];
        }

        /// <summary>
        /// The shipped wiring: the screen under the activity host's root, the refuge shown again and its HUD faded with the
        /// Play HUD's « playing » state while the screen shows; reveals wait while the activities (hub, a game, a meta screen)
        /// are open. Null without albums.
        /// </summary>
        public static AlbumScreenPresenter Create(GameContext context, IAlbums albums, ActivityPresenter activities, IAlbumHud hud)
        {
            if (albums == null)
                return null;
            var host = activities?.Host;
            var presenter = new AlbumScreenPresenter(context, albums, hud, host?.Root, null);
            if (activities != null)
            {
                presenter.SetWorldVisible = visible => activities.Host?.SetWorldVisible?.Invoke(visible);
                presenter.SetHudHidden = hidden => activities.PlayHud?.SetPlaying(hidden);
                presenter.RevealBlocked = () => activities.IsOpen;
            }
            return presenter;
        }

        /// <summary>Tests and captures: a given parent and screen prefab (else the Addressables one).</summary>
        public AlbumScreenPresenter(GameContext context, IAlbums albums, IAlbumHud hud, Transform parent, GameObject screenPrefab)
        {
            _context = context;
            _albums = albums ?? throw new ArgumentNullException(nameof(albums));
            _text = context?.Localization ?? KeysOnly.Instance;
            _parent = parent;
            _prefab = screenPrefab;
            for (int i = 0; i < _atlases.Length; i++)
                _atlases[i] = new AtlasSlot();
            _onGained = OnGained;
            _albums.Gained += _onGained;
            BindHud(hud);
        }

        /// <summary>The refuge shows or hides under the screen (the activity host's world).</summary>
        public Action<bool> SetWorldVisible { get; set; }

        /// <summary>The refuge HUD fades while the screen shows.</summary>
        public Action<bool> SetHudHidden { get; set; }

        /// <summary>True while reveals must wait (an activity, the hub, a meta screen, a modal of the refuge).</summary>
        public Func<bool> RevealBlocked { get; set; }

        /// <summary>Captures: the page atlases by address (else Addressables).</summary>
        public Func<string, SpriteAtlas> AtlasOverride { get; set; }

        /// <summary>Captures and tests: the catalog (else the one the prefab carries).</summary>
        public StickerArtCatalog CatalogOverride { get; set; }

        /// <summary>Reduce Motion for captures and tests without a save.</summary>
        public bool? ReduceMotionOverride { get; set; }

        /// <summary>The screen is loading or shown (world touches and hatching wait).</summary>
        public bool IsOpen => _screenOpen;

        public bool ScreenShown => _screenShown;

        public AlbumScreenMode Mode => _mode;

        public int AlbumShown => _album;

        public int PageShown => _page;

        public IAlbumScreenView View => _view;

        public GameObject ScreenObject => _screenObject;

        /// <summary>The screen's Addressables handle is held (released when the screen and the reveals are done).</summary>
        public bool ScreenLoaded => _handle.IsValid();

        public IAlbums Albums => _albums;

        public int ConfirmSticker => _confirmSticker;

        public bool Revealing => _revealing;

        /// <summary>The reveal batch: its size and the one playing.</summary>
        public int RevealCount => _queueCount;

        public int RevealIndex => _queueAt;

        public StickerGain RevealAt(int index) => _queue[Mathf.Clamp(index, 0, _queue.Length - 1)];

        public bool RevealOpened => _opened;

        public int CoverCount => _coverCount;

        public int ChoiceRowCount => _rowCount;

        public int ChoiceRowPage(int row) => (uint)row < (uint)_rowCount ? _rowPage[row] : -1;

        /// <summary>The page's atlas is loaded (tests: the neighbours preloaded).</summary>
        public bool AtlasReady(int page)
        {
            var slot = SlotOf(page);
            return slot != null && slot.Ready;
        }

        /// <summary>The page's atlas is held (loading or ready).</summary>
        public bool AtlasHeld(int page) => SlotOf(page) != null;

        public void BindHud(IAlbumHud hud)
        {
            _hud = hud;
            _hud?.BindAlbums(this);
            _nextCheck = 0f;
        }

        // ---------------- opening and closing ----------------

        /// <summary>The HUD's Albums button: the screen opens over a hidden refuge.</summary>
        public void OpenAlbums()
        {
            if (_screenOpen || !_albums.IsOpen)
                return;
            _screenOpen = true;
            _mode = AlbumScreenMode.Shelf;
            _album = -1;
            _page = -1;
            _confirmSticker = -1;
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
            _screenOpen = false;
            _confirmSticker = -1;
            if (_screenShown)
            {
                _screenShown = false;
                _view?.ShowConfirm(false, default, _albums.ChoicePrice);
                _view?.SetScreenShown(false);
                SetWorldVisible?.Invoke(true);
                SetHudHidden?.Invoke(false);
            }
            _page = -1;
            _album = -1;
            if (!_revealing)
                Release();
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
            if (!AddressExists(AlbumLayout.ScreenAddress, typeof(GameObject)))
            {
                _failed = true;
                GameLog.Error(LogCategory.Presentation,
                    $"Albums: no Addressables entry {AlbumLayout.ScreenAddress} (run Pocket Wilds/Albums/Build Albums Screen (T44A)).");
                return;
            }
            _handle = Addressables.LoadAssetAsync<GameObject>(AlbumLayout.ScreenAddress);
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

        /// <summary>Moves the loading on; once the view is there the screen (or the reveal) shows.</summary>
        private void Advance()
        {
            if (_view == null && !_failed && _handle.IsValid() && _handle.IsDone)
            {
                if (_handle.Status != AsyncOperationStatus.Succeeded || _handle.Result == null)
                {
                    _failed = true;
                    GameLog.Error(LogCategory.Presentation, "Albums: the screen did not load (" + AlbumLayout.ScreenAddress + ").");
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
                    // A screen that cannot load closes again at once; the gains stay granted, unrevealed.
                    _screenOpen = false;
                    if (_albums.PendingCount > 0)
                        _albums.ClearPending();
                    _revealing = false;
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
            _screenObject.name = AlbumLayout.ScreenPrefabName;
            _view = _screenObject.GetComponent<IAlbumScreenView>();
            if (_view == null || !_view.Valid)
            {
                GameLog.Error(LogCategory.Presentation, "Albums: the screen prefab has no complete AlbumScreenView (rebuild it with AlbumScreenPipeline).");
                DestroyObject(_screenObject);
                _screenObject = null;
                _view = null;
                _failed = true;
                return;
            }
            Index();
            ReadCatalog();
            _view.Bind(_text, this);
            _view.Hide();
        }

        private void ShowScreen()
        {
            _screenShown = true;
            _view.ReduceMotion = ReduceMotion;
            _view.Show();
            _view.SetScreenShown(true);
            _view.ShowConfirm(false, default, _albums.ChoicePrice);
            SetWorldVisible?.Invoke(false);
            SetHudHidden?.Invoke(true);
            ShowShelf();
            _view.Tick(0f);
        }

        /// <summary>Lets go of the screen, its bundle and the page atlases.</summary>
        private void Release()
        {
            for (int i = 0; i < _atlases.Length; i++)
                ReleaseAtlas(_atlases[i]);
            _view = null;
            _screenShown = false;
            _revealShown = false;
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

        /// <summary>The pages of each album in order, and the name keys (allocates once, at the first opening).</summary>
        private void Index()
        {
            if (_indexed)
                return;
            _indexed = true;
            int albums = Mathf.Max(0, _albums.AlbumCount);
            int pages = Mathf.Max(0, _albums.PageCount);
            _albumFirst = new int[albums];
            _albumPageCount = new int[albums];
            _albumPages = new int[pages];
            _albumNameKey = new string[albums];
            _pageNameKey = new string[pages];
            _stickerNameKey = new string[Mathf.Max(0, _albums.StickerCount)];
            for (int p = 0; p < pages; p++)
            {
                int album = _albums.PageAt(p).Album;
                if ((uint)album < (uint)albums)
                    _albumPageCount[album]++;
            }
            int at = 0;
            for (int a = 0; a < albums; a++)
            {
                _albumFirst[a] = at;
                at += _albumPageCount[a];
                _albumNameKey[a] = AlbumText.Name(_albums.AlbumKey(a));
            }
            var fill = new int[albums];
            for (int p = 0; p < pages; p++)
            {
                var info = _albums.PageAt(p);
                _pageNameKey[p] = AlbumText.Name(info.Key);
                if ((uint)info.Album < (uint)albums)
                    _albumPages[_albumFirst[info.Album] + fill[info.Album]++] = p;
            }
            _coverCount = Mathf.Min(albums, AlbumLayout.AlbumCapacity);
            for (int c = 0; c < _coverCount; c++)
                _coverAlbum[c] = c;
        }

        /// <summary>Each page's atlas address and each sticker's sprite name from the catalog (once).</summary>
        private void ReadCatalog()
        {
            if (_catalogRead)
                return;
            _catalogRead = true;
            _catalog = CatalogOverride != null ? CatalogOverride : _view?.ArtCatalog;
            int pages = Mathf.Max(0, _albums.PageCount);
            int stickers = Mathf.Max(0, _albums.StickerCount);
            _pageAtlas = new string[pages];
            _spriteName = new string[stickers];
            if (_catalog == null || _catalog.entries == null)
                return;
            var byKey = new Dictionary<string, int>(_catalog.entries.Length, StringComparer.Ordinal);
            for (int i = 0; i < _catalog.entries.Length; i++)
                if (!string.IsNullOrEmpty(_catalog.entries[i].stickerKey) && !byKey.ContainsKey(_catalog.entries[i].stickerKey))
                    byKey.Add(_catalog.entries[i].stickerKey, i);
            for (int s = 0; s < stickers; s++)
            {
                var info = _albums.StickerAt(s);
                if (info.Key == null || !byKey.TryGetValue(info.Key, out int e))
                    continue;
                var entry = _catalog.entries[e];
                _spriteName[s] = entry.spriteName;
                if ((uint)info.Page < (uint)pages && string.IsNullOrEmpty(_pageAtlas[info.Page]))
                    _pageAtlas[info.Page] = entry.pageAtlasAddress;
            }
        }

        // ---------------- Tick ----------------

        /// <summary>Per frame from the refuge (unscaled time). No allocation while the screen or a reveal shows.</summary>
        public void Tick(float deltaTime)
        {
            _clock += deltaTime;
            if (_view == null && (_screenOpen || _revealing))
                Advance();
            AdvanceAtlases();
            TickReveal(deltaTime);
            if (_screenShown && _view != null)
            {
                if (_slotsDirty && (_mode == AlbumScreenMode.Page || _mode == AlbumScreenMode.ChoosePage))
                    BindPage();
                if (_coversDirty && _mode == AlbumScreenMode.Shelf)
                    BindShelf();
            }
            if (_view != null && (_screenShown || _revealShown))
                _view.Tick(deltaTime);
            if (_clock >= _nextCheck)
            {
                _nextCheck = _clock + CheckInterval;
                UpdateHud();
            }
        }

        private void UpdateHud()
        {
            if (_hud == null)
                return;
            bool open = _albums.IsOpen;
            _hud.SetAlbums(open, open && AnyUnseen());
        }

        private bool AnyUnseen()
        {
            int count = _albums.StickerCount;
            for (int s = 0; s < count; s++)
                if (_albums.Unseen(s))
                    return true;
            return false;
        }

        // ---------------- the shelf ----------------

        private void ShowShelf()
        {
            _mode = AlbumScreenMode.Shelf;
            _album = -1;
            _page = -1;
            _view.SetMode(AlbumScreenMode.Shelf);
            BindShelf();
            KeepAtlases();
        }

        private void BindShelf()
        {
            _coversDirty = false;
            for (int c = 0; c < AlbumLayout.AlbumCapacity; c++)
            {
                if (c >= _coverCount)
                {
                    _view.SetCover(c, default);
                    continue;
                }
                int album = _coverAlbum[c];
                int owned = 0, total = 0;
                bool open = false, isNew = false;
                int first = _albumFirst[album], count = _albumPageCount[album];
                for (int i = 0; i < count; i++)
                {
                    int page = _albumPages[first + i];
                    owned += _albums.OwnedOnPage(page);
                    total += AlbumLayout.SlotsPerPage;
                    if (_albums.IsPageOpen(page))
                        open = true;
                    if (!isNew)
                        isNew = PageHasUnseen(page);
                }
                _view.SetCover(c, new AlbumCoverModel
                {
                    Shown = true, Title = Text(_albumNameKey[album]), Kind = _albums.KindOfAlbum(album), Owned = owned, Total = total,
                    Open = open, Complete = total > 0 && owned >= total, New = isNew,
                });
            }
            BindStars();
        }

        private void BindStars()
        {
            int stars = _albums.AlbumStars, price = _albums.ChoicePrice;
            _view.SetStars(stars, price, stars >= price);
        }

        private bool PageHasUnseen(int page)
        {
            int first = _albums.PageAt(page).FirstSticker;
            for (int k = 0; k < AlbumLayout.SlotsPerPage; k++)
                if (_albums.Unseen(first + k))
                    return true;
            return false;
        }

        public void OpenAlbum(int cover)
        {
            if (!_screenShown || (uint)cover >= (uint)_coverCount)
                return;
            int album = _coverAlbum[cover];
            int page = NextOpenPage(album, -1, +1);
            if (page < 0)
            {
                _view.ShowNote(Text(AlbumText.Closed));
                Sound(AudioEventId.ErrorSoft);
                return;
            }
            Sound(AudioEventId.UiTap);
            _album = album;
            ShowPage(AlbumScreenMode.Page, page, 0);
        }

        // ---------------- a page ----------------

        private void ShowPage(AlbumScreenMode mode, int page, int direction)
        {
            _mode = mode;
            _page = page;
            _album = _albums.PageAt(page).Album;
            _view.SetMode(mode);
            KeepAtlases();
            BindPage();
            if (direction != 0)
                _view.SlidePage(direction);
        }

        private void BindPage()
        {
            _slotsDirty = false;
            if (_page < 0)
                return;
            bool choosing = _mode == AlbumScreenMode.ChoosePage;
            int price = _albums.ChoicePrice;
            bool afford = _albums.AlbumStars >= price;
            _view.SetPage(new AlbumPageModel
            {
                Album = (uint)_album < (uint)_albumNameKey.Length ? Text(_albumNameKey[_album]) : string.Empty,
                Title = Text(_pageNameKey[_page]),
                Owned = _albums.OwnedOnPage(_page),
                Total = AlbumLayout.SlotsPerPage,
                HasPrevious = NextOpenPage(_album, _page, -1) >= 0,
                HasNext = NextOpenPage(_album, _page, +1) >= 0,
                Choosing = choosing,
            });
            int first = _albums.PageAt(_page).FirstSticker;
            for (int k = 0; k < AlbumLayout.SlotsPerPage; k++)
            {
                var model = SlotModel(first + k);
                model.Choosable = choosing && afford && model.State == StickerSlotState.Empty;
                _view.SetSlot(k, model);
            }
            BindStars();
            // The « Nouveau » badges show this once; the page counts as seen.
            if (PageHasUnseen(_page))
                _albums.MarkPageSeen(_page);
        }

        private AlbumSlotModel SlotModel(int sticker)
        {
            if ((uint)sticker >= (uint)_albums.StickerCount)
                return new AlbumSlotModel { State = StickerSlotState.Soon };
            var info = _albums.StickerAt(sticker);
            bool owned = _albums.Owned(sticker);
            var state = owned ? StickerSlotState.Owned : _albums.Available(sticker) ? StickerSlotState.Empty : StickerSlotState.Soon;
            return new AlbumSlotModel
            {
                State = state,
                Rarity = info.Rarity,
                Hint = info.Hint,
                Art = state == StickerSlotState.Soon ? null : SpriteOf(sticker),
                Name = owned ? StickerName(sticker) : null,
                New = owned && _albums.Unseen(sticker),
            };
        }

        /// <summary>The next open page of the album after <paramref name="from"/> in <paramref name="step"/>'s direction (-1: none).</summary>
        private int NextOpenPage(int album, int from, int step)
        {
            if ((uint)album >= (uint)_albumFirst.Length)
                return -1;
            int first = _albumFirst[album], count = _albumPageCount[album];
            int at = -1;
            if (from >= 0)
            {
                for (int i = 0; i < count; i++)
                    if (_albumPages[first + i] == from)
                    {
                        at = i;
                        break;
                    }
                if (at < 0)
                    return -1;
            }
            else
            {
                at = step > 0 ? -1 : count;
            }
            for (int i = at + step; i >= 0 && i < count; i += step)
            {
                int page = _albumPages[first + i];
                if (_albums.IsPageOpen(page))
                    return page;
            }
            return -1;
        }

        public void NextPage() => Turn(+1);

        public void PreviousPage() => Turn(-1);

        private void Turn(int step)
        {
            if (!_screenShown || _page < 0 || _view.ConfirmShown)
                return;
            int page = NextOpenPage(_album, _page, step);
            if (page < 0)
                return;
            Sound(AudioEventId.UiTap);
            ShowPage(_mode, page, step);
        }

        public void TapSlot(int slot)
        {
            if (!_screenShown || _page < 0 || (uint)slot >= AlbumLayout.SlotsPerPage || _view.ConfirmShown)
                return;
            int sticker = _albums.PageAt(_page).FirstSticker + slot;
            var model = SlotModel(sticker);
            if (model.State == StickerSlotState.Owned)
                return;
            if (model.State == StickerSlotState.Soon)
            {
                _view.ShowNote(Text(AlbumText.Soon));
                return;
            }
            if (_mode == AlbumScreenMode.ChoosePage)
            {
                if (_albums.AlbumStars < _albums.ChoicePrice)
                {
                    _view.ShowNote(Text(AlbumText.NotEnoughStars));
                    Sound(AudioEventId.ErrorSoft);
                    return;
                }
                _confirmSticker = sticker;
                Sound(AudioEventId.UiTap);
                _view.ShowConfirm(true, model, _albums.ChoicePrice);
                return;
            }
            // An empty slot: the help of its source.
            _view.ShowNote(Text(AlbumText.Hint(model.Hint)));
        }

        public void Back()
        {
            if (!_screenShown)
                return;
            if (_view.ConfirmShown)
            {
                CancelChoice();
                return;
            }
            Sound(AudioEventId.UiBack);
            if (_mode == AlbumScreenMode.ChoosePage)
                ShowChooseList();
            else if (_mode == AlbumScreenMode.Shelf)
                CloseScreen();
            else
                ShowShelf();
        }

        // ---------------- choosing a sticker (section 1.7) ----------------

        public void OpenChoose()
        {
            if (!_screenShown)
                return;
            if (_albums.AlbumStars < _albums.ChoicePrice)
            {
                _view.ShowNote(Text(AlbumText.NotEnoughStars));
                Sound(AudioEventId.ErrorSoft);
                return;
            }
            Sound(AudioEventId.UiTap);
            ShowChooseList();
        }

        private void ShowChooseList()
        {
            _mode = AlbumScreenMode.ChooseList;
            _page = -1;
            _view.SetMode(AlbumScreenMode.ChooseList);
            _rowCount = 0;
            int pages = Mathf.Min(_albums.PageCount, _albumPages.Length);
            // In album order, so the rows read like the shelf.
            for (int i = 0; i < pages && _rowCount < AlbumLayout.PageCapacity; i++)
            {
                int page = _albumPages[i];
                if (!_albums.IsPageOpen(page))
                    continue;
                int missing = MissingOn(page);
                if (missing <= 0)
                    continue;
                _rowPage[_rowCount] = page;
                _view.SetChoiceRow(_rowCount, new AlbumChoiceRowModel
                {
                    Shown = true, Title = Text(_pageNameKey[page]), Missing = missing, Owned = _albums.OwnedOnPage(page),
                });
                _rowCount++;
            }
            for (int row = _rowCount; row < AlbumLayout.PageCapacity; row++)
                _view.SetChoiceRow(row, default);
            BindStars();
            KeepAtlases();
        }

        private int MissingOn(int page)
        {
            int first = _albums.PageAt(page).FirstSticker, missing = 0;
            for (int k = 0; k < AlbumLayout.SlotsPerPage; k++)
                if (!_albums.Owned(first + k) && _albums.Available(first + k))
                    missing++;
            return missing;
        }

        public void PickChoicePage(int row)
        {
            if (!_screenShown || _mode != AlbumScreenMode.ChooseList || (uint)row >= (uint)_rowCount)
                return;
            Sound(AudioEventId.UiTap);
            ShowPage(AlbumScreenMode.ChoosePage, _rowPage[row], 0);
        }

        public void ConfirmChoice()
        {
            if (!_screenShown || _confirmSticker < 0)
                return;
            int sticker = _confirmSticker;
            _confirmSticker = -1;
            _view.ShowConfirm(false, default, _albums.ChoicePrice);
            if (!_albums.TryChoose(sticker))
            {
                _view.ShowNote(Text(AlbumText.NotEnoughStars));
                Sound(AudioEventId.ErrorSoft);
                return;
            }
            Sound(AudioEventId.UiConfirm);
            Haptic(HapticEventId.Confirm);
            // The reveal pastes it straight onto this page; out of stars, the page is a plain page again.
            if (_albums.AlbumStars < _albums.ChoicePrice)
                _mode = AlbumScreenMode.Page;
            _view.SetMode(_mode);
            BindPage();
        }

        public void CancelChoice()
        {
            if (_confirmSticker < 0 && (_view == null || !_view.ConfirmShown))
                return;
            _confirmSticker = -1;
            _view?.ShowConfirm(false, default, _albums.ChoicePrice);
        }

        // ---------------- page atlases ----------------

        /// <summary>The page shown and its two neighbours stay loaded (and the sticker being revealed); others are released.</summary>
        private void KeepAtlases()
        {
            int previous = _page >= 0 ? NextOpenPage(_album, _page, -1) : -1;
            int next = _page >= 0 ? NextOpenPage(_album, _page, +1) : -1;
            for (int i = 0; i < _atlases.Length; i++)
            {
                int page = _atlases[i].Page;
                if (page >= 0 && page != _page && page != previous && page != next && page != _revealPage)
                    ReleaseAtlas(_atlases[i]);
            }
            WantAtlas(_page);
            WantAtlas(previous);
            WantAtlas(next);
            WantAtlas(_revealPage);
        }

        private void WantAtlas(int page)
        {
            if (page < 0 || page >= _pageAtlas.Length || SlotOf(page) != null)
                return;
            string address = _pageAtlas[page];
            if (string.IsNullOrEmpty(address))
                return;
            AtlasSlot free = null;
            for (int i = 0; i < _atlases.Length && free == null; i++)
                if (_atlases[i].Page < 0)
                    free = _atlases[i];
            if (free == null)
                return;
            free.Page = page;
            free.Address = address;
            if (AtlasOverride != null)
            {
                Fill(free, AtlasOverride(address));
                return;
            }
            if (!AddressExists(address, typeof(SpriteAtlas)))
            {
                GameLog.Warning(LogCategory.Presentation, "Albums: no page atlas " + address + " (its stickers show their silhouettes).");
                free.Ready = true;
                return;
            }
            free.Handle = Addressables.LoadAssetAsync<SpriteAtlas>(address);
            free.Loading = true;
        }

        private void AdvanceAtlases()
        {
            for (int i = 0; i < _atlases.Length; i++)
            {
                var slot = _atlases[i];
                if (!slot.Loading || !slot.Handle.IsValid() || !slot.Handle.IsDone)
                    continue;
                slot.Loading = false;
                if (slot.Handle.Status == AsyncOperationStatus.Succeeded)
                    Fill(slot, slot.Handle.Result);
                else
                    slot.Ready = true;
            }
        }

        /// <summary>The page's 12 sprites taken out of its atlas once (SpriteAtlas.GetSprite makes copies, destroyed on release).</summary>
        private void Fill(AtlasSlot slot, SpriteAtlas atlas)
        {
            slot.Ready = true;
            if (atlas != null && (uint)slot.Page < (uint)_albums.PageCount)
            {
                int first = _albums.PageAt(slot.Page).FirstSticker;
                for (int k = 0; k < AlbumLayout.SlotsPerPage; k++)
                {
                    int sticker = first + k;
                    string name = (uint)sticker < (uint)_spriteName.Length ? _spriteName[sticker] : null;
                    slot.Sprites[k] = string.IsNullOrEmpty(name) ? null : atlas.GetSprite(name);
                }
            }
            if (slot.Page == _page)
                _slotsDirty = true;
        }

        private void ReleaseAtlas(AtlasSlot slot)
        {
            for (int k = 0; k < slot.Sprites.Length; k++)
            {
                var sprite = slot.Sprites[k];
                if (sprite != null && sprite.name.EndsWith("(Clone)", StringComparison.Ordinal))
                    DestroyObject(sprite);
                slot.Sprites[k] = null;
            }
            if (slot.Handle.IsValid())
                Addressables.Release(slot.Handle);
            slot.Handle = default;
            slot.Loading = false;
            slot.Ready = false;
            slot.Page = -1;
            slot.Address = null;
        }

        private AtlasSlot SlotOf(int page)
        {
            if (page < 0)
                return null;
            for (int i = 0; i < _atlases.Length; i++)
                if (_atlases[i].Page == page)
                    return _atlases[i];
            return null;
        }

        private Sprite SpriteOf(int sticker)
        {
            var info = _albums.StickerAt(sticker);
            var slot = SlotOf(info.Page);
            return slot != null && slot.Ready && (uint)info.Slot < AlbumLayout.SlotsPerPage ? slot.Sprites[info.Slot] : null;
        }

        // ---------------- the reveal (section 1.10) ----------------

        private void OnGained()
        {
            // Picked up by the next Tick (the reveal starts when the screen allows it).
            _nextCheck = 0f;
        }

        private bool CanReveal()
        {
            if (!_albums.IsOpen && !_screenOpen)
                return false;
            if (_screenOpen)
                return _screenShown;
            var blocked = RevealBlocked;
            return blocked == null || !blocked();
        }

        private void TickReveal(float deltaTime)
        {
            if (!_revealing)
            {
                if (_albums.PendingCount <= 0 || !CanReveal())
                    return;
                BeginBatch();
            }
            if (_view == null)
            {
                EnsureView();
                return;
            }
            if (!_revealShown)
            {
                if (_gapLeft > 0f)
                {
                    _gapLeft -= deltaTime;
                    return;
                }
                // The pouch waits for its page's art a moment (then a silhouette shows); the atlas is asked for once the
                // screen prefab (and its catalog) is there.
                if (_waited <= 0f && SlotOf(_revealPage) == null)
                {
                    ReadCatalog();
                    KeepAtlases();
                }
                var slot = SlotOf(_revealPage);
                _waited += deltaTime;
                if (slot != null && !slot.Ready && _waited < AtlasWait)
                    return;
                ShowCurrent();
                return;
            }
            float before = _sinceOpen;
            _pouchAge += deltaTime;
            if (!_opened && _pouchAge >= StickerRevealTiming.PouchIn + StickerRevealTiming.PouchAutoOpen)
                Open();
            else if (_opened)
                _sinceOpen += deltaTime;
            var gain = _queue[_queueAt];
            var rarity = _albums.StickerAt(gain.Sticker).Rarity;
            Beats(rarity, gain, before, _sinceOpen);
            _view.SetRevealTime(_pouchAge, _opened, _sinceOpen);
            if (_opened && _sinceOpen >= StickerRevealTiming.Total(rarity))
                EndCurrent();
        }

        /// <summary>The gains waiting, oldest first, copied out and sorted Common, Rare, Shiny (the peak last).</summary>
        private void BeginBatch()
        {
            int count = Mathf.Min(_albums.PendingCount, _queue.Length);
            for (int i = 0; i < count; i++)
                _queue[i] = _albums.PendingAt(i);
            _albums.ClearPending();
            // Stable insertion sort by rarity.
            for (int i = 1; i < count; i++)
            {
                var gain = _queue[i];
                var rarity = RarityOf(gain.Sticker);
                int j = i - 1;
                while (j >= 0 && RarityOf(_queue[j].Sticker) > rarity)
                {
                    _queue[j + 1] = _queue[j];
                    j--;
                }
                _queue[j + 1] = gain;
            }
            _queueCount = count;
            _queueAt = 0;
            _revealing = count > 0;
            _revealShown = false;
            _gapLeft = 0f;
            PrepareCurrent();
            EnsureView();
        }

        private StickerRarity RarityOf(int sticker) =>
            (uint)sticker < (uint)_albums.StickerCount ? _albums.StickerAt(sticker).Rarity : StickerRarity.Common;

        private void PrepareCurrent()
        {
            _waited = 0f;
            _pouchAge = 0f;
            _opened = false;
            _sinceOpen = 0f;
            _revealPage = (uint)_queue[_queueAt].Sticker < (uint)_albums.StickerCount ? _albums.StickerAt(_queue[_queueAt].Sticker).Page : -1;
            if (_view != null)
            {
                ReadCatalog();
                KeepAtlases();
            }
        }

        private void ShowCurrent()
        {
            if (!_catalogRead)
            {
                ReadCatalog();
                KeepAtlases();
            }
            var gain = _queue[_queueAt];
            var info = _albums.StickerAt(gain.Sticker);
            bool direct = _screenShown && _page == info.Page
                          && (_mode == AlbumScreenMode.Page || _mode == AlbumScreenMode.ChoosePage);
            Vector2 target;
            if (direct)
                target = _view.SlotScreenPosition(info.Slot);
            else if (_screenShown)
                target = _view.TitleScreenPosition;
            else
                target = _hud != null && _hud.AlbumsVisible ? _hud.AlbumsScreenPosition : new Vector2(Screen.width * 0.5f, Screen.height * 0.85f);
            string banner = gain.AlbumComplete ? Text(AlbumText.AlbumComplete)
                : gain.PageComplete ? Text(AlbumText.PageComplete)
                : gain.PageHalf ? Text(AlbumText.PageHalf)
                : null;
            if (!_screenShown)
                _view.Show();
            _view.ReduceMotion = ReduceMotion;
            _view.ShowReveal(new StickerRevealModel
            {
                Rarity = info.Rarity,
                Art = SpriteOf(gain.Sticker),
                Name = StickerName(gain.Sticker),
                New = gain.New,
                Stars = gain.New ? 0 : gain.StarsAdded,
                Banner = banner,
                Direct = direct,
                Target = target,
                StarsTarget = _screenShown ? _view.StarsScreenPosition : target,
                Full = _queueAt == 0 || _queueAt == _queueCount - 1,
            });
            _revealShown = true;
            _view.SetRevealTime(0f, false, 0f);
        }

        private void Open()
        {
            _opened = true;
            _sinceOpen = 0f;
            Sound(AudioEventId.UiTap);
        }

        /// <summary>The sounds and taps on their beats (crossings between two frames).</summary>
        private void Beats(StickerRarity rarity, in StickerGain gain, float before, float now)
        {
            if (!_opened)
                return;
            float exit = StickerRevealTiming.Exit(rarity);
            if (rarity == StickerRarity.Shiny && Crossed(before, now, exit - 0.25f))
                Haptic(HapticEventId.Rare);
            float land = exit + (gain.New ? StickerRevealTiming.Paste * 0.95f : StickerRevealTiming.SquashEnd);
            if (Crossed(before, now, exit + StickerRevealTiming.PopEnd))
            {
                Sound(_pasteSound);
                Haptic(HapticEventId.TapLight);
            }
            if (!gain.New && Crossed(before, now, land))
                Sound(_starSound);
        }

        private static bool Crossed(float before, float now, float beat) => before < beat && now >= beat;

        public void TapReveal()
        {
            if (!_revealing || !_revealShown)
                return;
            if (!_opened)
            {
                Open();
                return;
            }
            // A touch goes to the end of the reveal playing.
            _sinceOpen = Mathf.Max(_sinceOpen, StickerRevealTiming.Total(RarityOf(_queue[_queueAt].Sticker)));
        }

        private void EndCurrent()
        {
            _revealShown = false;
            _view.HideReveal();
            // The screen shows what was just pasted.
            if (_screenShown)
            {
                if (_mode == AlbumScreenMode.Page || _mode == AlbumScreenMode.ChoosePage)
                    BindPage();
                else if (_mode == AlbumScreenMode.Shelf)
                    BindShelf();
                else
                    BindStars();
            }
            _queueAt++;
            if (_queueAt < _queueCount)
            {
                _gapLeft = StickerRevealTiming.Gap;
                PrepareCurrent();
                return;
            }
            // More may have come while this batch played: they start at the next frame.
            _revealing = false;
            _queueCount = 0;
            _queueAt = 0;
            _revealPage = -1;
            _nextCheck = 0f;
            if (_screenShown)
            {
                KeepAtlases();
                return;
            }
            if (_albums.PendingCount > 0 && CanReveal())
                return;
            _view.Hide();
            if (!_screenOpen)
                Release();
        }

        // ---------------- helpers ----------------

        private bool ReduceMotion => ReduceMotionOverride ?? (_context != null && _context.Save != null && _context.State.Settings.ReduceMotion);

        private string StickerName(int sticker)
        {
            if ((uint)sticker >= (uint)_stickerNameKey.Length)
                return string.Empty;
            var key = _stickerNameKey[sticker] ??= AlbumText.Name(_albums.StickerAt(sticker).Key);
            return Text(key);
        }

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
            _albums.Gained -= _onGained;
            if (_screenOpen)
                Close();
            _revealing = false;
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
