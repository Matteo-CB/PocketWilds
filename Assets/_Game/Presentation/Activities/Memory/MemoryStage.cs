using System;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Simulation;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Object = UnityEngine.Object;

namespace PocketWilds.Presentation
{
    /// <summary>
    /// T41 « Memory des empreintes », the screen side (doc 01 Contenu S-ACT-004; doc « Mini-jeux &amp; activités » S-ACT-004,
    /// sections 1 and 6): the first activity that is an interface screen only. PF_UI_Memory (Addressables group
    /// Activity_Memory, address activity.memory.screen: loaded on the first game, released when the player leaves the
    /// activities) is a screen-space canvas under the activity HUD (its score, time, pause and tips stay on top): a cream
    /// tablecloth and a grid of pooled card views, Prairie clover backs, cream faces with the player's animals (their
    /// capture thumbnails, borrowed from the codex catalog the scene already holds) or their pawprints (the species' paw in
    /// the variant's coat colour). The refuge stops rendering while the screen shows. A tap on a card turns it (raycast-free
    /// hit tests on the cards' rects); the game's events play the flips, the matches' glints and heart, a mismatch's gentle
    /// shake, the glow card's gold pulse and challenge 6's swaps. The first game shows a tapping paw on a card and then its
    /// pair with « Trouve les paires » until the first match. Everything is built while the game loads: nothing is
    /// instantiated during a game, and nothing allocates per frame.
    /// </summary>
    public sealed partial class MemoryStage : IActivityStage, IActivityStagePreparation, IDisposable
    {
        /// <summary>The Prairie, the only theme of this build (the other biomes' backs and cloths: data only, doc 01).</summary>
        public static readonly ContentId PrairieTheme = ContentId.FromSource("biome.grassland");

        /// <summary>The most variants the game takes (contract: at most 64).</summary>
        public const int MaxVariants = 64;

        private readonly ActivityStageHost _host;
        private readonly GameContext _context;
        private readonly IActivityController _controller;
        private readonly IMemoryGame _game;

        // Loading (doc Mini-jeux, section 1: open in under a second).
        private AsyncOperationHandle<GameObject> _handle;
        private bool _ownsHandle;
        private GameObject _screenObject;
        private IMemoryScreenView _view;
        private bool _bound;
        private bool _failed;
        private bool _disposed;

        // The variants dealt (doc 01: the player's own first, then the codex's, then any): built in Prepare, given to the
        // game before the runner begins it (filtered by the portraits the codex knows once the screen is there).
        private readonly uint[] _candidates = new uint[MaxVariants];
        private readonly uint[] _candidateSpecies = new uint[MaxVariants];
        private readonly uint[] _variants = new uint[MaxVariants];
        private readonly uint[] _species = new uint[MaxVariants];
        private readonly Color[] _coats = new Color[MaxVariants];
        private readonly uint[] _biomes = new uint[MaxVariants];
        private readonly bool[] _candidateOwned = new bool[MaxVariants];
        private int _candidateCount;
        private int _variantCount;
        private bool _hasPrepared;
        private bool _variantsGiven;

        // The game shown.
        private ActivityStageHost _activeHost;
        private bool _entered;
        private bool _paused;
        private bool _ended;

        /// <summary>The shipped stage: its screen comes from Addressables.</summary>
        public MemoryStage(ActivityStageHost host) : this(host, null, null)
        {
        }

        /// <summary>
        /// Tests and captures: a given controller (else the memory game) and a given screen prefab (else the Addressables
        /// screen). With a prefab the screen is built at once (edit mode captures).
        /// </summary>
        public MemoryStage(ActivityStageHost host, GameObject screenPrefab, IActivityController controller)
        {
            _host = host;
            _context = host?.Context;
            _controller = controller ?? CreateController();
            _game = _controller as IMemoryGame ?? throw new ArgumentException("The memory stage needs an IMemoryGame.", nameof(controller));
            if (screenPrefab != null)
                BuildScreen(screenPrefab);
            else
                StartLoading();
        }

        /// <summary>The one place the stage's game is made (MS's simulation).</summary>
        public static IActivityController CreateController() => new MemoryController();

        /// <summary>
        /// Registers the memory stage in the presenter's registry (the presenter calls it once): the factory, and the theme it
        /// ships (the Prairie; the hub shows only those).
        /// </summary>
        public static void Register(ActivityStageRegistry registry)
        {
            if (registry == null)
                return;
            registry.Register(ActivityId.PawMemory, host => new MemoryStage(host));
            registry.SetThemes(ActivityId.PawMemory, PrairieTheme);
        }

        // ---------------- IActivityStage ----------------

        public ActivityId Activity => ActivityId.PawMemory;

        public IActivityController Controller => _controller;

        /// <summary>The memory game the stage plays (tests).</summary>
        public IMemoryGame Game => _game;

        /// <summary>
        /// The screen is loaded and built and the variants are given to the game. Polled by the presenter each frame while
        /// it loads. A screen that cannot load never blocks the game (it then plays on without its visuals, and says so in
        /// the log).
        /// </summary>
        public bool Ready => Advance();

        /// <summary>The screen's view (null before it loads, after a failure, and once released).</summary>
        public IMemoryScreenView View => _view;

        /// <summary>The screen's object (tests and captures: its canvas, its parts).</summary>
        public GameObject ScreenObject => _screenObject;

        /// <summary>The screen's Addressables handle is held (released with <see cref="Dispose"/>).</summary>
        public bool ScreenLoaded => _handle.IsValid();

        public bool Shown => _entered;

        /// <summary>The variants given to the game, best first (tests).</summary>
        public int VariantCount => _variantCount;

        public uint VariantAt(int index) => (uint)index < (uint)_variantCount ? _variants[index] : 0u;

        public void Prepare(in ActivityRequest request)
        {
            BuildCandidates();
            _hasPrepared = true;
            _variantsGiven = false;
            Advance();
        }

        public void Enter(in ActivitySetup setup, ActivityStageHost host)
        {
            Advance();
            _activeHost = host ?? _host;
            _entered = true;
            _paused = false;
            _ended = false;
            ResetPlay();
            if (!_hasPrepared)
                GameLog.Warning(LogCategory.Presentation, "Memory: the stage was not prepared before the game (call Prepare after Acquire).");
            if (_view == null)
            {
                GameLog.Warning(LogCategory.Presentation, "Memory: no screen, the game plays without its visuals.");
                return;
            }
            if (!_bound)
            {
                _view.Bind(_activeHost != null ? _activeHost.Hud : null);
                _bound = true;
            }
            _view.ReduceMotion = ReduceMotionSetting();
            // Every card dealt face down: a preview begun in Begin flips them up on the first tick, along the diagonals.
            DealAll();
            _view.Show();
            _view.Tick(0f);
            _activeHost?.SetWorldVisible?.Invoke(false);
            StartTutorial(_activeHost != null && _activeHost.Tutorial);
        }

        public void SetPaused(bool paused)
        {
            if (!_entered || _paused == paused)
                return;
            _paused = paused;
            if (paused)
                CancelPointer();
            _view?.SetPaused(paused);
        }

        public void End()
        {
            if (!_entered || _ended)
                return;
            DrainEvents();
            _ended = true;
            CancelPointer();
            HideAids();
        }

        public void Exit()
        {
            if (!_entered)
                return;
            _entered = false;
            _ended = false;
            _paused = false;
            CancelPointer();
            HideAids();
            _game.ClearEvents();
            _hasPrepared = false;
            if (_view == null)
                return;
            _view.SetPaused(false);
            _view.Hide();
            _activeHost?.SetWorldVisible?.Invoke(true);
        }

        /// <summary>Leaving the activities (the registry's ReleaseAll): the screen goes, its bundle is released.</summary>
        public void Dispose()
        {
            if (_disposed)
                return;
            Exit();
            _disposed = true;
            _view = null;
            if (_screenObject != null)
                DestroyObject(_screenObject);
            _screenObject = null;
            if (_ownsHandle && _handle.IsValid())
                Addressables.Release(_handle);
            _handle = default;
        }

        // ---------------- loading ----------------

        private void StartLoading()
        {
            if (!AddressExists(MemoryLayout.ScreenAddress))
            {
                _failed = true;
                GameLog.Error(LogCategory.Presentation,
                    $"Memory: no Addressables entry {MemoryLayout.ScreenAddress} (run Pocket Wilds/Activities/Build Memory (T41)).");
                return;
            }
            _handle = Addressables.LoadAssetAsync<GameObject>(MemoryLayout.ScreenAddress);
            _ownsHandle = true;
        }

        private static bool AddressExists(string address)
        {
            foreach (var locator in Addressables.ResourceLocators)
                if (locator.Locate(address, typeof(GameObject), out _))
                    return true;
            return false;
        }

        /// <summary>Moves the loading on: the screen, then the variants given to the game.</summary>
        private bool Advance()
        {
            if (_disposed)
                return false;
            if (_view == null && !_failed)
            {
                if (!_handle.IsValid() || !_handle.IsDone)
                    return false;
                if (_handle.Status != AsyncOperationStatus.Succeeded || _handle.Result == null)
                {
                    _failed = true;
                    GameLog.Error(LogCategory.Presentation, "Memory: the screen did not load (" + MemoryLayout.ScreenAddress + ").");
                }
                else
                {
                    BuildScreen(_handle.Result);
                }
            }
            if (_hasPrepared && !_variantsGiven && !_entered)
                GiveVariants();
            return true;
        }

        private void BuildScreen(GameObject prefab)
        {
            var parent = _host != null ? _host.Root : null;
            _screenObject = Object.Instantiate(prefab, parent);
            _screenObject.name = MemoryLayout.ScreenPrefabName;
            _view = _screenObject.GetComponent<IMemoryScreenView>();
            if (_view == null || !_view.Valid)
            {
                GameLog.Error(LogCategory.Presentation, "Memory: the screen prefab has no complete MemoryScreenView (rebuild it with MemoryPipeline).");
                DestroyObject(_screenObject);
                _screenObject = null;
                _view = null;
                _failed = true;
                return;
            }
            _bound = false;
            _view.Hide();
        }

        // ---------------- the variants (doc 01 S-ACT-004: « deux portraits identiques d'un animal du joueur ») ----------------

        /// <summary>
        /// The variants the cards may show, best first: the variants of the animals the player owns (favorites first), then
        /// the variants the codex knows, then any variant of the catalog (never a secret one the player has not met). Once
        /// per game start, never per frame.
        /// </summary>
        private void BuildCandidates()
        {
            _candidateCount = 0;
            var content = _context != null ? _context.Content : null;
            var collection = _context != null && _context.Save != null ? _context.State.Collection : null;
            if (collection != null)
            {
                for (int pass = 0; pass < 2; pass++)
                {
                    for (int i = 0; i < collection.Count; i++)
                    {
                        ref readonly var animal = ref collection[i];
                        if (animal.IsFavorite != (pass == 0))
                            continue;
                        AddCandidate(animal.Variant.Value, animal.Species.Value, true);
                    }
                }
            }
            if (content == null)
                return;
            var variants = content.Variants;
            if (collection != null)
            {
                for (int i = 0; i < variants.Count; i++)
                {
                    var definition = variants[i];
                    if (collection.DiscoveredVariants.Contains(definition.Id))
                        AddCandidate(definition.Id.Value, definition.Species.Value, false);
                }
            }
            for (int i = 0; i < variants.Count; i++)
            {
                var definition = variants[i];
                if (definition.IsSecret)
                    continue;
                if (content.Species.TryGet(definition.Species, out var species) && species.IsSecret
                    && (collection == null || !collection.DiscoveredSpecies.Contains(definition.Species)))
                    continue;
                AddCandidate(definition.Id.Value, definition.Species.Value, false);
            }
        }

        private void AddCandidate(uint variant, uint species, bool owned)
        {
            if (variant == 0 || _candidateCount >= MaxVariants)
                return;
            for (int i = 0; i < _candidateCount; i++)
                if (_candidates[i] == variant)
                    return;
            _candidates[_candidateCount] = variant;
            _candidateSpecies[_candidateCount] = species;
            _candidateOwned[_candidateCount] = owned;
            _candidateCount++;
        }

        /// <summary>
        /// Gives the game its variants: those with a portrait first (every pair shows at least one), and among them, the ones
        /// whose pawprint would look like an earlier one of the same species (coats too close: a white and a spotted
        /// rabbit) after the distinct ones, so every pawprint pair stays unique when the player owns enough variants.
        /// </summary>
        private void GiveVariants()
        {
            _variantsGiven = true;
            _variantCount = 0;
            // Pass 0: portrait and distinct coat; pass 1: portrait, coat alike; pass 2: no portrait known.
            for (int pass = 0; pass < 3 && _variantCount < MaxVariants; pass++)
            {
                for (int i = 0; i < _candidateCount; i++)
                {
                    uint variant = _candidates[i];
                    if (IndexOfVariant(variant) >= 0)
                        continue;
                    bool portrait = _view == null || _view.HasPortrait(variant);
                    if (pass == 2)
                    {
                        if (portrait)
                            continue;
                    }
                    else
                    {
                        if (!portrait)
                            continue;
                        var coat = CoatOf(variant);
                        bool alike = CoatAlike(_candidateSpecies[i], coat);
                        if (alike != (pass == 1))
                            continue;
                    }
                    _variants[_variantCount] = variant;
                    _species[_variantCount] = _candidateSpecies[i];
                    _coats[_variantCount] = CoatOf(variant);
                    _variantCount++;
                }
            }
            if (_variantCount == 0)
            {
                GameLog.Warning(LogCategory.Presentation, "Memory: no variant to deal (empty catalog?).");
                return;
            }
            _game.SetVariants(_variantCount, _variants, _species);
            GiveBiomes();
        }

        /// <summary>
        /// Challenge 9 « Même biome »: each variant's biome (its species'), and how many of the first ones are the player's
        /// own (the biome they have the most of decides). Today only the Prairie exists.
        /// </summary>
        private void GiveBiomes()
        {
            var content = _context != null ? _context.Content : null;
            if (content == null || !(_controller is MemoryController memory))
                return;
            int owned = 0;
            bool leading = true;
            for (int i = 0; i < _variantCount; i++)
            {
                _biomes[i] = content.Species.TryGet(new ContentId(_species[i]), out var species) ? species.Biome.Value : 0u;
                bool mine = IsOwned(_variants[i]);
                if (leading && mine)
                    owned++;
                else
                    leading = false;
            }
            memory.SetBiomes(_variantCount, _biomes, owned);
        }

        private bool IsOwned(uint variant)
        {
            for (int i = 0; i < _candidateCount; i++)
                if (_candidates[i] == variant)
                    return _candidateOwned[i];
            return false;
        }

        private int IndexOfVariant(uint variant)
        {
            for (int i = 0; i < _variantCount; i++)
                if (_variants[i] == variant)
                    return i;
            return -1;
        }

        /// <summary>An earlier variant of the same species already has a coat this close.</summary>
        private bool CoatAlike(uint species, Color coat)
        {
            for (int i = 0; i < _variantCount; i++)
                if (_species[i] == species && MemoryLayout.CoatDistance(_coats[i], coat) < MemoryLayout.CoatAlike)
                    return true;
            return false;
        }

        /// <summary>The pawprint's tint: the pipeline's table, else the variant's palette primary, else a soft brown.</summary>
        public Color CoatOf(uint variant)
        {
            if (_view != null && _view.TryCoat(variant, out var coat))
                return coat;
            var content = _context != null ? _context.Content : null;
            if (content != null && content.Variants.TryGet(new ContentId(variant), out var definition)
                && definition.Palette != null && definition.Palette.Length > 0)
                return MemoryLayout.Coat(definition.Palette[0]);
            return new Color(0.69f, 0.48f, 0.32f, 1f);
        }

        // ---------------- the cards ----------------

        /// <summary>Every card of the grid dealt to its view (the game's deal, read after Begin); the views beyond are hidden.</summary>
        private void DealAll()
        {
            _view.Layout(_game.Columns, _game.Rows);
            int count = Mathf.Min(_game.CardCount, _view.Capacity);
            if (_game.CardCount > _view.Capacity)
                GameLog.Warning(LogCategory.Presentation, $"Memory: {_game.CardCount} cards, the screen has {_view.Capacity} views.");
            for (int card = 0; card < count; card++)
                Deal(card);
        }

        private void Deal(int card)
        {
            uint variant = _game.VariantOf(card);
            _view.Deal(card, variant, _game.SpeciesOf(card), _game.FaceOf(card), _game.GlowCard == card, _game.IsDouble(card), CoatOf(variant));
        }

        private bool ReduceMotionSetting() => _context != null && _context.Save != null && _context.State.Settings.ReduceMotion;

        private static void DestroyObject(Object target)
        {
            if (target == null)
                return;
            if (Application.isPlaying)
                Object.Destroy(target);
            else
                Object.DestroyImmediate(target);
        }
    }
}
