#nullable enable
using System;
using PocketWilds.Core;

namespace PocketWilds.Simulation
{
    /// <summary>
    /// The memory game (doc 01 Contenu, S-ACT-004; doc « Mini-jeux &amp; activités », S-ACT-004), ticked by the
    /// <see cref="ActivityRunner"/> and played by its interface screen through <see cref="IMemoryGame"/>.
    /// <list type="bullet">
    /// <item>The deal, from the game's seed: the grid of the d (4 × 3, 4 × 4 or 5 × 4) or of the challenge; one variant per
    /// pair, distinct, a seeded choice among the first 1.5 × pairs variants the stage gave (<see cref="SetVariants"/>: the
    /// player's own first), the list repeated when it is shorter than the pairs; two identical portraits per pair, or a
    /// pawprint and a portrait (d6 to d10, challenges 4 and 10); a plain seeded shuffle, except that the top-left card's pair
    /// is never right beside or under it; one glow card (gold edge) and, from d4, one double pair worth 200 (never the glow
    /// card's, never a repeated variant).</item>
    /// <item>The preview (2 s at d1 and d2, 1 s at d3 and d4, none from d5; challenge 2 none): every face shows, touches are
    /// ignored. It does not count as seen for the mistake rule and its seconds are part of the game's time.</item>
    /// <item>A touch (<see cref="Flip"/>) turns a face-down card up, except while previewing, while a mismatch waits (Busy)
    /// and on a card already up or matched. The second card of a try: two cards that go together stay (+100, 200 for the
    /// double pair); otherwise both turn back after 0.8 s (Busy meanwhile). Two cards go together when they show the same
    /// variant (and, in the pawprint mode, one pawprint and one portrait): with a repeated variant any two such cards are a
    /// pair, the pairs re-forming around them.</item>
    /// <item>The mistake rule (doc 01): a miss is a mistake only when the first card and a card of its right pair had both been
    /// seen face up before this try; a first try at a card is never one.</item>
    /// <item>The glow card, turned up while its pair is still face down: a <see cref="MemoryEventKind.Glow"/> event, the card
    /// of its pair glows for 1 s.</item>
    /// <item>The end: every pair found gives the end bonus (50 × pairs − 20 × mistakes, at least 0, plus 3 per whole second
    /// left; challenge 5 takes 50 a mistake), then the game ends; at the time (45, 60 or 90 s by grid; challenge 7 45 s,
    /// challenge 10 75 s) the game counts the pairs found, without the bonus.</item>
    /// <item>Challenge 6: every 4 pairs found, two face-down cards of different pairs swap places (seen ones first): the two
    /// indexes exchange everything (pair, variant, face, glow edge, seen).</item>
    /// <item>Challenge 9: every card from the biome the player has the most variants of (<see cref="SetBiomes"/>; without it,
    /// all the variants count as one biome: today only the Prairie exists).</item>
    /// <item>« Activités plus lentes »: the game lasts × 1.25 (the setup's seconds); the 0.8 s turn-back, the preview and the
    /// glow keep their length.</item>
    /// </list>
    /// Events go to a fixed ring the stage drains each frame. Nothing allocates after <see cref="SetVariants"/>.
    /// </summary>
    public sealed class MemoryController : IActivityController, IMemoryGame, IActivityRewardCounts
    {
        /// <summary>The variants the stage may give (« at most 64, copied »).</summary>
        public const int VariantCapacity = 64;

        public const int EventCapacity = 64;

        private const uint DealSalt = 0x4D454D44u;
        private const uint ChoiceSalt = 0x4D454D56u;
        private const uint ItemSalt = 0x4D454D49u;
        private const uint SwapSalt = 0x4D454D53u;

        private static ActivityRules? _defaultRules;

        /// <summary>The variants dealt when the stage never gives any (tests, tools): the 18 Prairie variants, 6 per species.</summary>
        private static readonly uint[] DefaultVariantIds = BuildDefaults(out DefaultSpeciesIds);
        private static readonly uint[] DefaultSpeciesIds;

        // ---------------- the variants the stage gave (copied, deduplicated) ----------------
        private readonly uint[] _listVariant = new uint[VariantCapacity];
        private readonly uint[] _listSpecies = new uint[VariantCapacity];
        private readonly uint[] _listBiome = new uint[VariantCapacity];
        private readonly int[] _listSource = new int[VariantCapacity];
        private int _listCount;
        private bool _listSet;
        private bool _hasBiomes;
        private int _owned;

        // Scratch for the deal (candidates, then the chosen list indexes).
        private readonly int[] _candidates = new int[VariantCapacity];
        private readonly int[] _chosen = new int[MemoryRules.MaxPairs];
        private readonly int[] _order = new int[MemoryRules.MaxCards];
        private readonly int[] _scratch = new int[MemoryRules.MaxCards];

        // ---------------- the cards (by index, row by row from the top left) ----------------
        private readonly int[] _pair = new int[MemoryRules.MaxCards];
        private readonly uint[] _variant = new uint[MemoryRules.MaxCards];
        private readonly uint[] _species = new uint[MemoryRules.MaxCards];
        private readonly MemoryFace[] _face = new MemoryFace[MemoryRules.MaxCards];
        private readonly bool[] _faceUp = new bool[MemoryRules.MaxCards];
        private readonly bool[] _matched = new bool[MemoryRules.MaxCards];
        private readonly bool[] _seen = new bool[MemoryRules.MaxCards];
        private readonly bool[] _pairDouble = new bool[MemoryRules.MaxPairs];
        private readonly bool[] _pairRepeated = new bool[MemoryRules.MaxPairs];

        // ---------------- events ----------------
        private readonly MemoryEvent[] _events = new MemoryEvent[EventCapacity];
        private int _eventStart;
        private int _eventCount;

        // ---------------- parameters of this game ----------------
        private int _grid;
        private int _columns = 4;
        private int _rows = 3;
        private int _cards = 12;
        private int _pairs = 6;
        private bool _pawprints;
        private float _preview;
        private bool _hasDouble;
        private bool _hasGlow;
        private int _penalty = MemoryRules.MistakePoints;
        private int _swapEvery;
        private bool _sameBiome;
        private uint _biome;
        private float _seconds = MemoryRules.SmallSeconds;

        // ---------------- state ----------------
        private bool _begun;
        private bool _done;
        private bool _paused;
        private bool _allMatched;
        private double _elapsed;
        private float _previewLeft;
        private bool _previewing;
        private int _glow = -1;
        private int _first = -1;
        private bool _firstSeenBefore;
        private int _busyA = -1;
        private int _busyB = -1;
        private float _turnBackLeft;
        private int _score;
        private int _pairsFound;
        private int _mistakes;
        private int _moves;
        private int _tries;
        private int _swaps;
        private int _endBonus;
        private DeterministicRandom _swapRng;

        private bool Running => _begun && !_done;

        // ================================================================== variants

        /// <summary>
        /// The variants the cards may show, best first (the player's own, then the codex's, then any), with each variant's
        /// species: at most 64, copied, without zeros or repeats. Forgets the biomes given before. Allocation-free.
        /// </summary>
        public void SetVariants(int count, uint[] variants, uint[] species)
        {
            _listCount = 0;
            _hasBiomes = false;
            _owned = 0;
            int n = variants == null ? 0 : Math.Min(count, variants.Length);
            for (int i = 0; i < n && _listCount < VariantCapacity; i++)
            {
                uint variant = variants![i];
                if (variant == 0u || IndexInList(variant) >= 0)
                    continue;
                _listVariant[_listCount] = variant;
                _listSpecies[_listCount] = species != null && i < species.Length ? species[i] : 0u;
                _listBiome[_listCount] = 0u;
                _listSource[_listCount] = i;
                _listCount++;
            }
            _listSet = _listCount > 0;
        }

        /// <summary>
        /// Challenge 9 « Même biome »: the biome (ContentId value) of each variant given to <see cref="SetVariants"/>, in the
        /// same order; <paramref name="owned"/>: how many of the first ones are the player's own (the biome they have the most
        /// of decides; 0: all of them count). Optional: without it every variant counts as one biome (today only the Prairie
        /// exists). Allocation-free.
        /// </summary>
        public void SetBiomes(int count, uint[] biomes, int owned = 0)
        {
            if (biomes == null || !_listSet)
                return;
            int n = Math.Min(count, biomes.Length);
            for (int i = 0; i < _listCount; i++)
            {
                int source = _listSource[i];
                _listBiome[i] = source < n ? biomes[source] : 0u;
            }
            _owned = Math.Max(0, owned);
            _hasBiomes = true;
        }

        /// <summary>The variants given (after the repeats were dropped); the 18 Prairie defaults when none were.</summary>
        public int VariantCount => _listSet ? _listCount : DefaultVariantIds.Length;

        public uint VariantAt(int index) =>
            (uint)index < (uint)VariantCount ? (_listSet ? _listVariant[index] : DefaultVariantIds[index]) : 0u;

        public uint SpeciesAt(int index) =>
            (uint)index < (uint)VariantCount ? (_listSet ? _listSpecies[index] : DefaultSpeciesIds[index]) : 0u;

        // ================================================================== IActivityController

        /// <summary>A new game: the parameters at d (or the challenge's), the deal from the seed, the preview. Allocation-free.</summary>
        public void Begin(in ActivitySetup setup)
        {
            var rules = setup.Rules ?? (_defaultRules ??= MemoryRules.Create());
            _grid = MemoryRules.ClampGrid((int)Math.Round(Read(rules, in setup, MemoryRules.Grid), MidpointRounding.AwayFromZero));
            _columns = MemoryRules.Columns(_grid);
            _rows = MemoryRules.Rows(_grid);
            _cards = _columns * _rows;
            _pairs = _cards / 2;
            _pawprints = Read(rules, in setup, MemoryRules.Pawprints) > 0.5;
            _preview = (float)Math.Max(0.0, Read(rules, in setup, MemoryRules.Preview));
            _hasDouble = Read(rules, in setup, MemoryRules.DoublePair) > 0.5;
            _hasGlow = Read(rules, in setup, MemoryRules.GlowCard) > 0.5;
            _penalty = Math.Max(0, (int)Math.Round(Read(rules, in setup, MemoryRules.MistakePenalty), MidpointRounding.AwayFromZero));
            _swapEvery = Math.Max(0, (int)Math.Round(Read(rules, in setup, MemoryRules.SwapEvery), MidpointRounding.AwayFromZero));
            _sameBiome = Read(rules, in setup, MemoryRules.SameBiome) > 0.5;
            _seconds = setup.Seconds > 0f
                ? setup.Seconds
                : MemoryRules.SecondsOf(_grid) * (setup.Slower ? ActivityConfig.SlowerTimeMultiplier : 1f);

            _begun = true;
            _done = false;
            _paused = false;
            _allMatched = false;
            _elapsed = 0.0;
            _first = -1;
            _firstSeenBefore = false;
            _busyA = -1;
            _busyB = -1;
            _turnBackLeft = 0f;
            _score = 0;
            _pairsFound = 0;
            _mistakes = 0;
            _moves = 0;
            _tries = 0;
            _swaps = 0;
            _endBonus = 0;
            _swapRng = new DeterministicRandom(SeedHash.Mix(setup.Seed, SwapSalt));
            ClearEvents();

            Deal(setup.Seed);

            _previewLeft = _preview;
            _previewing = _preview > 0f;
            if (_previewing)
                Push(MemoryEventKind.PreviewStarted, -1, -1, 0, false, _preview);
        }

        /// <summary>Per frame: the game's clock, the preview, the turn-back of a mismatch, the time. No allocation.</summary>
        public void Tick(float deltaSeconds)
        {
            if (!Running || _paused || deltaSeconds <= 0f)
                return;
            _elapsed += deltaSeconds;
            if (_previewing)
            {
                _previewLeft -= deltaSeconds;
                if (_previewLeft <= 0f)
                {
                    _previewLeft = 0f;
                    _previewing = false;
                    Push(MemoryEventKind.PreviewEnded, -1, -1, 0, false, 0f);
                }
            }
            if (_busyA >= 0)
            {
                _turnBackLeft -= deltaSeconds;
                if (_turnBackLeft <= 0f)
                    TurnBack();
            }
            if (_elapsed >= _seconds)
                End(false);
        }

        public void Pause() => _paused = true;

        public void Resume() => _paused = false;

        public int Score => _score;

        /// <summary>Every challenge measures the score.</summary>
        public int ChallengeScore => _score;

        /// <summary>Memory has no streak.</summary>
        public int Streak => 0;

        public bool Finished => _begun && _done;

        // ================================================================== IMemoryGame: the grid

        public int Columns => _columns;

        public int Rows => _rows;

        public int CardCount => _cards;

        public int PairCount => _pairs;

        public int PairOf(int card) => Valid(card) ? _pair[card] : -1;

        public uint VariantOf(int card) => Valid(card) ? _variant[card] : 0u;

        public uint SpeciesOf(int card) => Valid(card) ? _species[card] : 0u;

        public MemoryFace FaceOf(int card) => Valid(card) ? _face[card] : MemoryFace.Portrait;

        /// <summary>Turned up by a touch (or matched); false during the preview, when the stage shows every face anyway.</summary>
        public bool FaceUp(int card) => Valid(card) && _faceUp[card];

        public bool Matched(int card) => Valid(card) && _matched[card];

        public int GlowCard => _glow;

        public bool IsDouble(int card) => Valid(card) && _pairDouble[_pair[card]];

        public bool Previewing => Running && _previewing;

        public bool Busy => Running && _busyA >= 0;

        public int PairsFound => _pairsFound;

        public int Mistakes => _mistakes;

        /// <summary>Flips so far (two per try).</summary>
        public int Moves => _moves;

        public float Elapsed => (float)_elapsed;

        public float Seconds => _seconds;

        // ================================================================== IMemoryGame: input

        /// <summary>A touch on a card: it turns face up when it may (face down, not matched, not previewing, not busy). No allocation.</summary>
        public void Flip(int card)
        {
            if (!Running || _paused || _previewing || _busyA >= 0 || !Valid(card) || _faceUp[card] || _matched[card])
                return;
            bool seenBefore = _seen[card];
            _faceUp[card] = true;
            _seen[card] = true;
            _moves++;
            Push(MemoryEventKind.Flipped, card, -1, 0, false, 0f);

            // The glow card: the card of its pair glows for 1 s (unless it is already up).
            if (card == _glow)
            {
                int partner = PartnerOf(card);
                if (partner >= 0 && !_faceUp[partner])
                    Push(MemoryEventKind.Glow, card, partner, 0, false, MemoryRules.GlowSeconds);
            }

            if (_first < 0)
            {
                _first = card;
                _firstSeenBefore = seenBefore;
                return;
            }

            int first = _first;
            _first = -1;
            _tries++;
            if (GoTogether(first, card))
            {
                Match(first, card);
                return;
            }

            // A miss: a mistake only when the first card and a card of its right pair had both been seen before this try.
            bool mistake = _firstSeenBefore && RightPairSeen(first, card);
            if (mistake)
                _mistakes++;
            _busyA = first;
            _busyB = card;
            _turnBackLeft = MemoryRules.TurnBackSeconds;
            Push(MemoryEventKind.Mismatched, first, card, 0, mistake, MemoryRules.TurnBackSeconds);
        }

        // ================================================================== events

        public int EventCount => _eventCount;

        /// <summary>The events since the last <see cref="ClearEvents"/>, oldest first (the ring keeps the last 64).</summary>
        public MemoryEvent Event(int index) =>
            (uint)index < (uint)_eventCount ? _events[(_eventStart + index) % EventCapacity] : default;

        public void ClearEvents()
        {
            _eventStart = 0;
            _eventCount = 0;
        }

        // ================================================================== the game's numbers (beyond the contract)

        /// <summary>Tries so far (a try is two cards): WISH-059 « Finis un Memory en {n} coups ou moins » counts them.</summary>
        public int Tries => _tries;

        /// <summary>WISH-059's n for this game's grid: 2 × pairs + 6.</summary>
        public int MovesTarget => MemoryRules.MovesTarget(_pairs);

        /// <summary>Every pair was found (the game ended on its own rule, the end bonus given).</summary>
        public bool AllMatched => _allMatched;

        /// <summary>The end bonus given (0 before, and at time up).</summary>
        public int EndBonus => _endBonus;

        public float SecondsLeft => (float)Math.Max(0.0, _seconds - _elapsed);

        /// <summary>The grid (<see cref="MemoryRules.SmallGrid"/>, <see cref="MemoryRules.MediumGrid"/>, <see cref="MemoryRules.LargeGrid"/>).</summary>
        public int Grid => _grid;

        /// <summary>Each pair is a pawprint and a portrait.</summary>
        public bool Pawprints => _pawprints;

        public float PreviewSeconds => _preview;

        public float PreviewLeft => _previewing ? _previewLeft : 0f;

        /// <summary>Points a mistake takes from the end bonus (20; 50 in challenge 5).</summary>
        public int MistakePenalty => _penalty;

        /// <summary>Two cards swap every this many pairs (challenge 6: 4); 0: never.</summary>
        public int SwapEvery => _swapEvery;

        public int Swaps => _swaps;

        /// <summary>Challenge 9: the biome dealt (ContentId value; 0 when the stage gave no biomes).</summary>
        public uint Biome => _biome;

        public bool SameBiome => _sameBiome;

        /// <summary>The first card of the try in progress, −1 when none is up.</summary>
        public int FirstCard => _first;

        /// <summary>Seconds before the mismatched cards turn back (0 when not busy).</summary>
        public float TurnBackLeft => _busyA >= 0 ? Math.Max(0f, _turnBackLeft) : 0f;

        /// <summary>The card was turned face up at least once by a touch (the preview does not count).</summary>
        public bool Seen(int card) => Valid(card) && _seen[card];

        /// <summary>The other card of a card's pair (−1 for an unknown card).</summary>
        public int PartnerOf(int card)
        {
            if (!Valid(card))
                return -1;
            int pair = _pair[card];
            for (int i = 0; i < _cards; i++)
                if (i != card && _pair[i] == pair)
                    return i;
            return -1;
        }

        /// <summary>Two cards go together: the same variant, and in the pawprint mode one pawprint and one portrait.</summary>
        public bool GoTogether(int a, int b) =>
            Valid(a) && Valid(b) && a != b && _variant[a] == _variant[b] && (!_pawprints || _face[a] != _face[b]);

        /// <summary>The seed counted item: 1 for a game played to its end (the reward service gives it at 3 stars, the Garden open).</summary>
        public int RewardCount(int index) => index == MemoryRules.SeedCount && _begun && _done ? 1 : 0;

        // ================================================================== the game

        private void Match(int a, int b)
        {
            // A repeated variant: the pairs re-form around the two cards found together.
            int pa = _pair[a], pb = _pair[b];
            if (pa != pb)
            {
                int partnerA = PartnerOf(a); // read before the pairs change
                _pair[b] = pa;
                if (partnerA >= 0)
                    _pair[partnerA] = pb;
            }
            _matched[a] = true;
            _matched[b] = true;
            _faceUp[a] = true;
            _faceUp[b] = true;
            _pairsFound++;
            int points = _pairDouble[_pair[a]] ? MemoryRules.DoublePairPoints : MemoryRules.PairPoints;
            _score += points;
            Push(MemoryEventKind.Matched, a, b, points, false, 0f);

            if (_pairsFound >= _pairs)
            {
                End(true);
                return;
            }
            if (_swapEvery > 0 && _pairsFound % _swapEvery == 0)
                Swap();
        }

        private void TurnBack()
        {
            int a = _busyA, b = _busyB;
            _busyA = -1;
            _busyB = -1;
            _turnBackLeft = 0f;
            if (a >= 0)
                _faceUp[a] = false;
            if (b >= 0)
                _faceUp[b] = false;
            Push(MemoryEventKind.FlippedBack, a, b, 0, false, 0f);
        }

        private void End(bool allMatched)
        {
            if (_done)
                return;
            if (allMatched)
            {
                _allMatched = true;
                float left = SecondsLeft;
                _endBonus = MemoryRules.EndBonus(_pairs, _mistakes, _penalty, left);
                _score += _endBonus;
                Push(MemoryEventKind.AllMatched, -1, -1, _endBonus, false, left);
            }
            _done = true;
            _previewing = false;
            Push(MemoryEventKind.Finished, -1, -1, 0, false, (float)_elapsed);
        }

        /// <summary>A card of the first card's right pair (any unmatched card that goes with it) had been seen before this try.</summary>
        private bool RightPairSeen(int first, int second)
        {
            for (int i = 0; i < _cards; i++)
            {
                if (i == first || i == second || _matched[i] || !_seen[i])
                    continue;
                if (GoTogether(first, i))
                    return true;
            }
            return false;
        }

        /// <summary>Challenge 6: two face-down cards of different pairs swap places (seen ones first). Seeded, allocation-free.</summary>
        private void Swap()
        {
            int count = Collect(true);
            if (!HasTwoPairs(count))
                count = Collect(false);
            if (!HasTwoPairs(count))
                return;
            int a = _scratch[_swapRng.Next(count)];
            int others = 0;
            for (int i = 0; i < count; i++)
            {
                int c = _scratch[i];
                if (_pair[c] != _pair[a] && !GoTogether(a, c))
                    _order[others++] = c;
            }
            if (others == 0)
                return;
            int b = _order[_swapRng.Next(others)];
            SwapContents(a, b);
            _swaps++;
            Push(MemoryEventKind.Swapped, a, b, 0, false, 0f);
        }

        private int Collect(bool seenOnly)
        {
            int count = 0;
            for (int i = 0; i < _cards; i++)
                if (!_matched[i] && !_faceUp[i] && (!seenOnly || _seen[i]))
                    _scratch[count++] = i;
            return count;
        }

        private bool HasTwoPairs(int count)
        {
            for (int i = 1; i < count; i++)
                if (_pair[_scratch[i]] != _pair[_scratch[0]] && !GoTogether(_scratch[i], _scratch[0]))
                    return true;
            // The first one may go with all the others; any two others of different pairs will do.
            for (int i = 1; i < count; i++)
                for (int j = i + 1; j < count; j++)
                    if (_pair[_scratch[i]] != _pair[_scratch[j]] && !GoTogether(_scratch[i], _scratch[j]))
                        return true;
            return false;
        }

        private void SwapContents(int a, int b)
        {
            (_pair[a], _pair[b]) = (_pair[b], _pair[a]);
            (_variant[a], _variant[b]) = (_variant[b], _variant[a]);
            (_species[a], _species[b]) = (_species[b], _species[a]);
            (_face[a], _face[b]) = (_face[b], _face[a]);
            (_faceUp[a], _faceUp[b]) = (_faceUp[b], _faceUp[a]);
            (_matched[a], _matched[b]) = (_matched[b], _matched[a]);
            (_seen[a], _seen[b]) = (_seen[b], _seen[a]);
            if (_glow == a)
                _glow = b;
            else if (_glow == b)
                _glow = a;
        }

        // ---------------- the deal ----------------

        private void Deal(uint seed)
        {
            _glow = -1;
            var choice = new DeterministicRandom(SeedHash.Mix(seed, ChoiceSalt));
            int candidates = Candidates();

            // One variant per pair: a seeded choice among the first 1.5 × pairs candidates (best first), the list repeated
            // when it is shorter than the pairs.
            int window = Math.Min(candidates, _pairs + _pairs / 2);
            int picked = Math.Min(window, _pairs);
            for (int i = 0; i < picked; i++)
            {
                int j = i + choice.Next(window - i);
                (_candidates[i], _candidates[j]) = (_candidates[j], _candidates[i]);
                _chosen[i] = _candidates[i];
            }
            for (int p = 0; p < _pairs; p++)
            {
                if (p >= picked)
                    _chosen[p] = picked > 0 ? _chosen[p % picked] : -1;
                _pairDouble[p] = false;
                _pairRepeated[p] = picked < _pairs && (p >= picked || p < _pairs - picked);
            }

            // The cards in pairs, then a plain seeded shuffle of their places.
            for (int c = 0; c < _cards; c++)
                _order[c] = c;
            var deal = new DeterministicRandom(SeedHash.Mix(seed, DealSalt));
            for (int i = _cards - 1; i > 0; i--)
            {
                int j = deal.Next(i + 1);
                (_order[i], _order[j]) = (_order[j], _order[i]);
            }
            for (int place = 0; place < _cards; place++)
            {
                int original = _order[place];
                int p = original / 2;
                int index = _chosen[p];
                _pair[place] = p;
                _variant[place] = index >= 0 ? VariantAt(index) : 0u;
                _species[place] = index >= 0 ? SpeciesAt(index) : 0u;
                _face[place] = _pawprints && (original & 1) == 0 ? MemoryFace.Pawprint : MemoryFace.Portrait;
                _faceUp[place] = false;
                _matched[place] = false;
                _seen[place] = false;
            }

            // The top-left card's pair is never right beside or under it.
            int partner = PartnerOf(0);
            if (partner == 1 || partner == _columns)
            {
                int far = 0;
                for (int c = 0; c < _cards; c++)
                    if (c != 0 && c != 1 && c != _columns)
                        _scratch[far++] = c;
                int to = _scratch[deal.Next(far)];
                SwapContents(partner, to);
            }

            // The double pair (from d4; never a repeated variant) and the glow card (never the double pair's).
            var items = new DeterministicRandom(SeedHash.Mix(seed, ItemSalt));
            int doublePair = -1;
            if (_hasDouble)
            {
                int unique = 0;
                for (int p = 0; p < _pairs; p++)
                    if (!_pairRepeated[p])
                        _scratch[unique++] = p;
                if (unique > 0)
                {
                    doublePair = _scratch[items.Next(unique)];
                    _pairDouble[doublePair] = true;
                }
            }
            _glow = -1;
            if (_hasGlow)
            {
                int count = 0;
                for (int c = 0; c < _cards; c++)
                    if (_pair[c] != doublePair)
                        _scratch[count++] = c;
                if (count > 0)
                    _glow = _scratch[items.Next(count)];
            }
        }

        /// <summary>The list indexes the deal may use, in the list's order: all, or challenge 9's biome alone.</summary>
        private int Candidates()
        {
            int total = VariantCount;
            _biome = 0u;
            if (_sameBiome && _listSet && _hasBiomes)
            {
                // The biome the player has the most variants of (their own ones when the stage said how many; ties: the first).
                _biome = MostCommonBiome(_owned > 0);
                if (_biome == 0u && _owned > 0)
                    _biome = MostCommonBiome(false);
                if (_biome != 0u)
                {
                    int count = 0;
                    for (int i = 0; i < total; i++)
                        if (_listBiome[i] == _biome)
                            _candidates[count++] = i;
                    return count;
                }
            }
            for (int i = 0; i < total; i++)
                _candidates[i] = i;
            return total;
        }

        /// <summary>The biome most entries have (only the player's own with <paramref name="ownedOnly"/>; ties: the first); 0: none.</summary>
        private uint MostCommonBiome(bool ownedOnly)
        {
            uint found = 0u;
            int best = 0;
            for (int i = 0; i < _listCount; i++)
            {
                uint biome = _listBiome[i];
                if (biome == 0u || (ownedOnly && _listSource[i] >= _owned))
                    continue;
                int n = 0;
                for (int j = 0; j < _listCount; j++)
                    if (_listBiome[j] == biome && (!ownedOnly || _listSource[j] < _owned))
                        n++;
                if (n > best)
                {
                    best = n;
                    found = biome;
                }
            }
            return found;
        }

        // ---------------- helpers ----------------

        private bool Valid(int card) => _begun && (uint)card < (uint)_cards;

        private int IndexInList(uint variant)
        {
            for (int i = 0; i < _listCount; i++)
                if (_listVariant[i] == variant)
                    return i;
            return -1;
        }

        private void Push(MemoryEventKind kind, int card, int other, int points, bool mistake, float seconds)
        {
            int slot;
            if (_eventCount < EventCapacity)
            {
                slot = (_eventStart + _eventCount) % EventCapacity;
                _eventCount++;
            }
            else
            {
                // Full (nobody drained): the oldest event makes room.
                slot = _eventStart;
                _eventStart = (_eventStart + 1) % EventCapacity;
            }
            _events[slot] = new MemoryEvent(kind, card, other, points, mistake, seconds);
        }

        /// <summary>A parameter; a rule set without it (another activity's rules) falls back to Memory's at d.</summary>
        private static double Read(ActivityRules rules, in ActivitySetup setup, int parameter)
        {
            double value = rules.Value(parameter, setup.Difficulty, setup.Challenge);
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                var fallback = _defaultRules ??= MemoryRules.Create();
                value = fallback.Value(parameter, setup.Difficulty, -1);
            }
            return double.IsNaN(value) ? 0.0 : value;
        }

        private static uint[] BuildDefaults(out uint[] species)
        {
            string[] names = { "rabbit", "squirrel", "hedgehog" };
            string[][] coats =
            {
                new[] { "brown", "white", "spotted", "grey", "black", "sakura" },
                new[] { "red", "grey", "autumn", "brown", "cream", "black" },
                new[] { "brown", "cream", "mushroom", "grey", "white", "black" },
            };
            var variants = new uint[18];
            species = new uint[18];
            int k = 0;
            // Interleaved by species, so the first ones mix the three.
            for (int coat = 0; coat < 6; coat++)
                for (int s = 0; s < names.Length; s++)
                {
                    variants[k] = ContentId.FromSource("variant." + names[s] + "." + coats[s][coat]).Value;
                    species[k] = ContentId.FromSource("species." + names[s]).Value;
                    k++;
                }
            return variants;
        }
    }
}
