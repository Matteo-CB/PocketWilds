using System;
using System.Collections.Generic;
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
    /// T44C « Journal du refuge » (doc « 01 Succès, titres, cadres, poses et journal », section 6; screen I-SCR-037). The
    /// profile card's « Journal » button (a dot while pages are unseen) opens PF_UI_Journal (Addressables group Journal,
    /// address journal.screen: loaded when opened, released when closed) over the profile screen: a timeline of months, newest
    /// first, each page a card (its date, its picture shown at 300, its memory with the variables filled, the small icon of
    /// its system, « Nouveau » for the pages added since the last visit) and a « Moments » tab listing the surprises seen.
    /// The lists are recycled (6 cards and 4 headers for every page); a stored capture (Journal/journal_{id}.jpg) or a postcard
    /// is read into the card's pooled texture when the card shows, one a frame, and the moment's thumbnail (the animal's codex
    /// portrait, else the system's icon) stands in until then or when there is none. Texts are filled into char buffers;
    /// nothing allocates per frame (the memory templates are read once per moment, the timeline is rebuilt on a new page).
    /// </summary>
    public sealed class JournalScreenPresenter : IJournalScreenActions, IDisposable
    {
        private static readonly DateTime Epoch = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);

        private readonly GameContext _context;
        private readonly IJournal _journal;
        private readonly ILocalization _text;
        private readonly Transform _parent;
        private readonly GameObject _prefab;
        private readonly JournalImageStore _store;
        private readonly Action _onPageAdded;
        private PostcardStore _postcards;

        private AsyncOperationHandle<GameObject> _handle;
        private bool _ownsHandle;
        private GameObject _screenObject;
        private IJournalScreenView _view;
        private bool _failed;
        private bool _screenOpen;
        private bool _screenShown;
        private bool _ownsWorld;
        private JournalTab _tab;
        private bool _dirty;
        private int _newCount;

        // The timeline (rebuilt on a new page; grows only).
        private bool[] _headers = new bool[16];
        private int[] _entryValue = new int[16];
        private int _entryCount;

        // The moments' names (read when their count changes).
        private string[] _momentNames = Array.Empty<string>();
        private int _momentCount;

        // Texts read at Bind.
        private bool _textsRead;
        private string _dateTemplate = "{day} {month} {year}";
        private string _headerTemplate = "{month} {year}";
        private readonly string[] _months = new string[12];
        private readonly string[] _monthsInDate = new string[12];
        private bool _french;
        private readonly Dictionary<string, string> _templates = new Dictionary<string, string>(64, StringComparer.Ordinal);

        // Buffers.
        private readonly char[] _textBuffer = new char[JournalLayout.TextCapacity];
        private readonly char[] _dateBuffer = new char[48];
        private readonly char[] _headerBuffer = new char[48];
        private readonly string[] _varNames = new string[JournalLayout.MaxVariables];
        private readonly string[] _varValues = new string[JournalLayout.MaxVariables];

        // The card cells' pooled pictures: what each holds and what it wants (page ids, postcards offset by 1 << 32).
        private readonly Texture2D[] _cellTexture = new Texture2D[JournalLayout.CardCells];
        private readonly long[] _cellLoaded = new long[JournalLayout.CardCells];
        private readonly bool[] _cellLoadedOk = new bool[JournalLayout.CardCells];
        private readonly long[] _cellWant = new long[JournalLayout.CardCells];
        private readonly int[] _cellItem = new int[JournalLayout.CardCells];

        private const long PostcardKey = 1L << 32;
        private const long NoPicture = -1L;

        /// <summary>The shipped wiring: the screen under the activity host's root, opened over the profile screen (which already hides the refuge).</summary>
        public static JournalScreenPresenter Create(GameContext context, IJournal journal, ActivityPresenter activities, ProfileScreenPresenter profile)
        {
            if (journal == null)
                return null;
            var presenter = new JournalScreenPresenter(context, journal, activities?.Host?.Root, null);
            if (activities != null)
            {
                presenter.SetWorldVisible = visible => activities.Host?.SetWorldVisible?.Invoke(visible);
                presenter.SetHudHidden = hidden => activities.PlayHud?.SetPlaying(hidden);
            }
            if (profile != null)
            {
                presenter.OverScreen = () => profile.IsOpen;
                profile.Journal = presenter;
            }
            return presenter;
        }

        /// <summary>Tests and captures: a given parent, screen prefab (else the Addressables one), picture folder and texts (else the context's).</summary>
        public JournalScreenPresenter(GameContext context, IJournal journal, Transform parent, GameObject screenPrefab, JournalImageStore store = null,
            ILocalization text = null)
        {
            _context = context;
            _journal = journal ?? throw new ArgumentNullException(nameof(journal));
            _text = text ?? context?.Localization ?? KeysOnly.Instance;
            _parent = parent;
            _prefab = screenPrefab;
            _store = store ?? new JournalImageStore();
            _onPageAdded = OnPageAdded;
            _journal.PageAdded += _onPageAdded;
            for (int i = 0; i < JournalLayout.CardCells; i++)
            {
                _cellLoaded[i] = NoPicture;
                _cellWant[i] = NoPicture;
                _cellItem[i] = -1;
            }
        }

        public Action<bool> SetWorldVisible { get; set; }

        public Action<bool> SetHudHidden { get; set; }

        /// <summary>The journal opens over another screen (the profile): the refuge and its HUD are already hidden, left as they are.</summary>
        public Func<bool> OverScreen { get; set; }

        /// <summary>Where the postcards are read (tests give another folder).</summary>
        public PostcardStore Postcards
        {
            get => _postcards ??= new PostcardStore();
            set => _postcards = value;
        }

        public bool? ReduceMotionOverride { get; set; }

        public IJournal Journal => _journal;

        public JournalImageStore Store => _store;

        public bool IsOpen => _screenOpen;

        public bool ScreenShown => _screenShown;

        public bool ScreenLoaded => _handle.IsValid();

        public JournalTab Tab => _tab;

        public IJournalScreenView View => _view;

        public GameObject ScreenObject => _screenObject;

        /// <summary>The timeline's items (headers and cards) and the pages marked « Nouveau » this visit.</summary>
        public int TimelineCount => _entryCount;

        public int NewCount => _newCount;

        public bool IsHeader(int item) => (uint)item < (uint)_entryCount && _headers[item];

        /// <summary>The timeline item showing a page (by its index in the journal, newest first), -1 when none.</summary>
        public int ItemOfPage(int pageIndex)
        {
            for (int i = 0; i < _entryCount; i++)
                if (!_headers[i] && _entryValue[i] == pageIndex)
                    return i;
            return -1;
        }

        /// <summary>Whether the card showing timeline <paramref name="item"/> holds its stored picture (tests).</summary>
        public bool PictureLoaded(int item)
        {
            int cell = _view != null ? _view.CardCellOf(item) : -1;
            return cell >= 0 && _cellLoadedOk[cell] && _cellLoaded[cell] == _cellWant[cell] && _cellWant[cell] != NoPicture;
        }

        private void OnPageAdded() => _dirty = true;

        // ---------------- opening and closing ----------------

        public void OpenJournal()
        {
            if (_screenOpen)
                return;
            _screenOpen = true;
            _tab = JournalTab.Pages;
            Sound(AudioEventId.PanelOpen);
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
            if (_screenShown)
            {
                _screenShown = false;
                if (_ownsWorld)
                {
                    SetWorldVisible?.Invoke(true);
                    SetHudHidden?.Invoke(false);
                }
            }
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
            if (!AddressExists(JournalLayout.ScreenAddress, typeof(GameObject)))
            {
                _failed = true;
                GameLog.Error(LogCategory.Presentation,
                    $"Journal: no Addressables entry {JournalLayout.ScreenAddress} (run Pocket Wilds/Journal/Build Journal Screen (T44C)).");
                return;
            }
            _handle = Addressables.LoadAssetAsync<GameObject>(JournalLayout.ScreenAddress);
            _ownsHandle = true;
        }

        private static bool AddressExists(string address, Type type)
        {
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
                    GameLog.Error(LogCategory.Presentation, "Journal: the screen did not load (" + JournalLayout.ScreenAddress + ").");
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
            _screenObject.name = JournalLayout.ScreenPrefabName;
            _view = _screenObject.GetComponent<IJournalScreenView>();
            if (_view == null || !_view.Valid)
            {
                GameLog.Error(LogCategory.Presentation, "Journal: the screen prefab has no complete JournalScreenView (rebuild it with JournalScreenPipeline).");
                DestroyObject(_screenObject);
                _screenObject = null;
                _view = null;
                _failed = true;
                return;
            }
            ReadTexts();
            _view.Bind(_text, this);
            _view.Hide();
        }

        private void ShowScreen()
        {
            _screenShown = true;
            _ownsWorld = OverScreen == null || !OverScreen();
            _view.ReduceMotion = ReduceMotion;
            _view.Show();
            if (_ownsWorld)
            {
                SetWorldVisible?.Invoke(false);
                SetHudHidden?.Invoke(true);
            }
            // The pages added since the last visit say « Nouveau » during this one; the entry's dot goes.
            _newCount = Mathf.Max(0, _journal.UnseenCount);
            if (_newCount > 0)
                _journal.MarkSeen();
            Rebuild();
            ReadMoments();
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
            for (int i = 0; i < _cellTexture.Length; i++)
            {
                if (_cellTexture[i] != null)
                    DestroyObject(_cellTexture[i]);
                _cellTexture[i] = null;
                _cellLoaded[i] = NoPicture;
                _cellLoadedOk[i] = false;
                _cellWant[i] = NoPicture;
                _cellItem[i] = -1;
            }
        }

        // ---------------- the timeline ----------------

        /// <summary>Month headers and page cards, newest first (allocates only when the timeline outgrows its arrays).</summary>
        private void Rebuild()
        {
            int pages = Mathf.Max(0, _journal.PageCount);
            int needed = pages * 2;
            if (_headers.Length < needed)
            {
                int size = Mathf.Max(needed, _headers.Length * 2);
                _headers = new bool[size];
                _entryValue = new int[size];
            }
            _entryCount = 0;
            int lastMonth = int.MinValue;
            for (int i = 0; i < pages; i++)
            {
                var page = _journal.PageAt(i);
                int month = MonthKey(page.Day);
                if (month != lastMonth)
                {
                    lastMonth = month;
                    _headers[_entryCount] = true;
                    _entryValue[_entryCount++] = month;
                }
                _headers[_entryCount] = false;
                _entryValue[_entryCount++] = i;
            }
        }

        private static int MonthKey(int day)
        {
            var date = Epoch.AddDays(Mathf.Max(0, day));
            return date.Year * 12 + (date.Month - 1);
        }

        private void ReadMoments()
        {
            int count = Mathf.Max(0, _journal.SeenSurpriseCount);
            if (count == _momentCount && _momentNames.Length >= count)
                return;
            if (_momentNames.Length < count)
                Array.Resize(ref _momentNames, Mathf.Max(count, JournalLayout.MomentsTotal));
            for (int i = 0; i < count; i++)
            {
                string key = _journal.SeenSurpriseAt(i);
                _momentNames[i] = string.IsNullOrEmpty(key) ? string.Empty : _text.Get(JournalText.SurpriseName(key));
            }
            _momentCount = count;
        }

        public int MomentCount => _momentCount;

        public string MomentName(int index) => (uint)index < (uint)_momentCount ? _momentNames[index] : null;

        // ---------------- Tick ----------------

        /// <summary>Per frame (unscaled time). No allocation while the screen shows; one picture read a frame at most.</summary>
        public void Tick(float deltaTime)
        {
            if (_view == null && _screenOpen)
                Advance();
            if (!_screenShown || _view == null)
                return;
            if (_dirty)
            {
                _dirty = false;
                Rebuild();
                ReadMoments();
                if (_journal.UnseenCount > 0)
                {
                    _newCount += _journal.UnseenCount;
                    _journal.MarkSeen();
                }
                if (_tab == JournalTab.Pages)
                    _view.SetTimeline(_headers, _entryCount, false);
                else
                    _view.SetMoments(_momentCount, false);
            }
            _view.Tick(deltaTime);
            LoadOnePicture();
        }

        // ---------------- tabs ----------------

        public void SelectTab(JournalTab tab)
        {
            if (!_screenShown || tab == _tab)
                return;
            Sound(AudioEventId.UiTab);
            ShowTab(tab, true);
        }

        private void ShowTab(JournalTab tab, bool toTop)
        {
            _tab = tab;
            _view.SetTab(tab);
            if (tab == JournalTab.Pages)
                _view.SetTimeline(_headers, _entryCount, toTop);
            else
                _view.SetMoments(_momentCount, toTop);
        }

        // ---------------- the cells ----------------

        public void FillTimeline(int item, bool header, int cell)
        {
            if (_view == null)
                return;
            if ((uint)item >= (uint)_entryCount)
            {
                if (header)
                    _view.SetHeader(cell, default);
                else
                    _view.SetCard(cell, default);
                return;
            }
            if (header)
            {
                int month = _entryValue[item];
                int length = WriteDate(_headerBuffer, _headerTemplate, 1, month % 12 + 1, month / 12, _months);
                _view.SetHeader(cell, new JournalHeaderModel { Shown = true, Text = _headerBuffer, Length = length });
                return;
            }
            int pageIndex = _entryValue[item];
            var page = _journal.PageAt(pageIndex);
            var date = Epoch.AddDays(Mathf.Max(0, page.Day));
            int dateLength = WriteDate(_dateBuffer, _dateTemplate, date.Day, date.Month, date.Year, _monthsInDate);
            int textLength = WriteText(page, dateLength);
            var model = new JournalCardModel
            {
                Shown = true,
                Date = _dateBuffer,
                DateLength = dateLength,
                Text = _textBuffer,
                TextLength = textLength,
                System = JournalText.SystemOf(page.Key, page.Image),
                New = pageIndex < _newCount,
                PictureRect = new Rect(0f, 0f, 1f, 1f),
            };
            if ((uint)cell < (uint)_cellItem.Length)
            {
                _cellItem[cell] = item;
                long want = PictureKey(page);
                _cellWant[cell] = want;
                if (want != NoPicture && _cellLoaded[cell] == want && _cellLoadedOk[cell] && _cellTexture[cell] != null)
                {
                    model.Source = JournalPictureSource.Texture;
                    model.Picture = _cellTexture[cell];
                    if (page.Image == JournalImageKind.Postcard)
                    {
                        // The postcard's middle square (512 × 768).
                        float h = PostcardLayout.Width / (float)PostcardLayout.Height;
                        model.PictureRect = new Rect(0f, (1f - h) * 0.5f, 1f, h);
                    }
                }
            }
            if (model.Source != JournalPictureSource.Texture)
            {
                model.Variant = ThumbnailVariant(page);
                model.Source = model.Variant != ContentId.None ? JournalPictureSource.Codex : JournalPictureSource.Icon;
            }
            _view.SetCard(cell, model);
        }

        /// <summary>The picture a page shows when it is stored: its capture, or its postcard.</summary>
        private static long PictureKey(in JournalPage page)
        {
            switch (page.Image)
            {
                case JournalImageKind.AnimalCapture:
                case JournalImageKind.RefugeCapture:
                    return page.Captured ? page.Id : NoPicture;
                case JournalImageKind.Postcard:
                    return PostcardKey + page.ImageRef;
                default:
                    return NoPicture;
            }
        }

        /// <summary>The moment's thumbnail: the animal's codex portrait (its variant), or a content id that may be a variant.</summary>
        private ContentId ThumbnailVariant(in JournalPage page)
        {
            if (page.Image == JournalImageKind.AnimalCapture)
            {
                var collection = _context != null && _context.Save != null ? _context.State.Collection : null;
                return collection != null && collection.TryGet(page.ImageRef, out var animal) ? animal.Variant : ContentId.None;
            }
            if (page.Image == JournalImageKind.Thumbnail && page.ImageRef != 0u)
                return new ContentId(page.ImageRef);
            return ContentId.None;
        }

        public void FillMoment(int cell, int item)
        {
            if (_view == null)
                return;
            if ((uint)item >= (uint)_momentCount)
            {
                _view.SetMoment(cell, default);
                return;
            }
            _view.SetMoment(cell, new JournalMomentModel { Shown = true, Name = _momentNames[item] });
        }

        // ---------------- pictures ----------------

        /// <summary>One card whose picture changed reads it now (the capture's JPEG or the postcard's PNG into the cell's pooled texture).</summary>
        private void LoadOnePicture()
        {
            for (int cell = 0; cell < _cellWant.Length; cell++)
            {
                long want = _cellWant[cell];
                if (want == NoPicture || want == _cellLoaded[cell] || !_view.CardCellShown(cell))
                    continue;
                var texture = _cellTexture[cell];
                if (texture == null)
                {
                    texture = new Texture2D(2, 2, TextureFormat.RGB24, false)
                    {
                        name = "JournalPicture" + cell, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear,
                    };
                    _cellTexture[cell] = texture;
                }
                bool ok;
                try
                {
                    if (want >= PostcardKey)
                    {
                        ok = Postcards.TryLoad((int)(want - PostcardKey), texture);
                    }
                    else
                    {
                        var bytes = _store.Read((int)want);
                        ok = bytes != null && texture.LoadImage(bytes, false);
                    }
                }
                catch (Exception e)
                {
                    GameLog.Warning(LogCategory.Presentation, "Journal: a picture could not be read (" + e.Message + ").");
                    ok = false;
                }
                _cellLoaded[cell] = want;
                _cellLoadedOk[cell] = ok;
                int item = _cellItem[cell];
                if (ok && item >= 0)
                    FillTimeline(item, false, cell);
                return;
            }
        }

        // ---------------- texts ----------------

        private void ReadTexts()
        {
            if (_textsRead)
                return;
            _textsRead = true;
            _french = string.Equals(_text.Language, "fr", StringComparison.Ordinal);
            _dateTemplate = TextOr(JournalText.Date, _french ? "{day} {month} {year}" : "{month} {day}, {year}");
            _headerTemplate = TextOr(JournalText.MonthHeader, "{month} {year}");
            for (int m = 0; m < 12; m++)
            {
                _months[m] = _text.Get(JournalText.Months[m]);
                _monthsInDate[m] = _text.Get(JournalText.MonthsInDate[m]);
            }
        }

        private string TextOr(string key, string fallback)
        {
            string value = _text.Get(key);
            return string.IsNullOrEmpty(value) || value == key ? fallback : value;
        }

        /// <summary>The memory's template, read once per moment key.</summary>
        private string Template(string key)
        {
            if (string.IsNullOrEmpty(key))
                return string.Empty;
            if (_templates.TryGetValue(key, out var template))
                return template;
            template = _text.Get(JournalText.PageText(key)) ?? string.Empty;
            _templates[key] = template;
            return template;
        }

        /// <summary>The memory with its variables filled ({date} with the page's date), into the text buffer.</summary>
        private int WriteText(in JournalPage page, int dateLength)
        {
            string template = Template(page.Key);
            int count = 0;
            try
            {
                count = Mathf.Clamp(_journal.Variables(page.Id, _varNames, _varValues), 0, JournalLayout.MaxVariables);
            }
            catch (Exception e)
            {
                GameLog.Warning(LogCategory.Presentation, "Journal: page " + page.Id + " variables failed (" + e.Message + ").");
            }
            var buffer = _textBuffer;
            int length = 0;
            for (int c = 0; c < template.Length && length < buffer.Length; c++)
            {
                char ch = template[c];
                if (ch == '{')
                {
                    int end = template.IndexOf('}', c + 1);
                    if (end > c + 1)
                    {
                        int start = c + 1, size = end - start;
                        int v = FindVariable(template, start, size, count);
                        if (v >= 0)
                        {
                            length = Append(buffer, length, _varValues[v]);
                            c = end;
                            continue;
                        }
                        if (Matches(template, start, size, "date"))
                        {
                            for (int d = 0; d < dateLength && length < buffer.Length; d++)
                                buffer[length++] = _dateBuffer[d];
                            c = end;
                            continue;
                        }
                    }
                }
                buffer[length++] = ch;
            }
            return length;
        }

        /// <summary>The variable named like the template's token (with or without its braces).</summary>
        private int FindVariable(string template, int start, int size, int count)
        {
            for (int i = 0; i < count; i++)
            {
                string name = _varNames[i];
                if (string.IsNullOrEmpty(name))
                    continue;
                if (name.Length == size + 2 && name[0] == '{' && name[name.Length - 1] == '}')
                {
                    if (string.CompareOrdinal(name, 1, template, start, size) == 0)
                        return i;
                }
                else if (name.Length == size && string.CompareOrdinal(name, 0, template, start, size) == 0)
                {
                    return i;
                }
            }
            return -1;
        }

        private static bool Matches(string template, int start, int size, string word) =>
            word.Length == size && string.CompareOrdinal(word, 0, template, start, size) == 0;

        /// <summary>A date or a month header from its template ({day}, {month}, {year}); French writes « 1er ».</summary>
        private int WriteDate(char[] buffer, string template, int day, int month, int year, string[] monthNames)
        {
            int length = 0;
            for (int c = 0; c < template.Length && length < buffer.Length; c++)
            {
                char ch = template[c];
                if (ch == '{')
                {
                    int end = template.IndexOf('}', c + 1);
                    if (end > c + 1)
                    {
                        int start = c + 1, size = end - start;
                        if (Matches(template, start, size, "day"))
                        {
                            length = AppendInt(buffer, length, day);
                            if (_french && day == 1)
                                length = Append(buffer, length, "er");
                            c = end;
                            continue;
                        }
                        if (Matches(template, start, size, "month"))
                        {
                            length = Append(buffer, length, monthNames[Mathf.Clamp(month - 1, 0, 11)]);
                            c = end;
                            continue;
                        }
                        if (Matches(template, start, size, "year"))
                        {
                            length = AppendInt(buffer, length, year);
                            c = end;
                            continue;
                        }
                    }
                }
                buffer[length++] = ch;
            }
            return length;
        }

        private static int Append(char[] buffer, int length, string value)
        {
            if (value == null)
                return length;
            for (int i = 0; i < value.Length && length < buffer.Length; i++)
                buffer[length++] = value[i];
            return length;
        }

        private static int AppendInt(char[] buffer, int length, int value)
        {
            if (value < 0)
                value = 0;
            int digits = 1;
            for (int v = value; v >= 10; v /= 10)
                digits++;
            if (length + digits > buffer.Length)
                return length;
            for (int i = digits - 1; i >= 0; i--)
            {
                buffer[length + i] = (char)('0' + value % 10);
                value /= 10;
            }
            return length + digits;
        }

        // ---------------- helpers ----------------

        private bool ReduceMotion => ReduceMotionOverride ?? (_context != null && _context.Save != null && _context.State.Settings.ReduceMotion);

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
            _journal.PageAdded -= _onPageAdded;
            if (_screenOpen)
                Close();
            Release();
        }

        private sealed class KeysOnly : ILocalization
        {
            public static readonly KeysOnly Instance = new KeysOnly();

            public string Language => "en";

            public string Get(string key) => key;
        }
    }
}
