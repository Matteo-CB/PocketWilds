using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Services.Feedback;
using UnityEngine;

namespace PocketWilds.Presentation
{
    /// <summary>Callbacks of a multi hatch, without delegates (no allocation per hatch).</summary>
    public interface IMultiHatchListener
    {
        /// <summary>Every shell burst: the newborns are out.</summary>
        void OnMultiRevealed();

        /// <summary>A Rare or better result, or a new species or color, gets its short moment (index in the hatch).</summary>
        void OnMultiMoment(int index);

        /// <summary>
        /// Before the newborns join the meadow: a free place for each (<paramref name="exits"/>, the animal's own position
        /// when none) and when it sets off (<paramref name="delays"/>, seconds). False: they stay where they are.
        /// </summary>
        bool OnMultiExits(Vector3[] exits, float[] delays);

        void OnMultiFinished();
    }

    /// <summary>
    /// T35B, doc 02 Œufs, section 5: the staging of a × 3 or × 10 hatch, results already decided by the simulation. The eggs
    /// pop onto measured spots around the pedestal, wobble and crack together (1.5 s), burst at once (at most three full
    /// hatch effects, the others a small shell puff), the newborns pop out, then each Rare or better result and each new
    /// species or color has a 0.8 s moment of its own (the camera comes closer; a touch passes it), and each newborn walks
    /// out to a free place its listener found (no animal ever stands in another, rule « aucune collision »). Evaluated from
    /// elapsed time like <see cref="HatchSequencePlayer"/>: no coroutine, no physics, no allocation after construction.
    /// </summary>
    public sealed class MultiHatchPlayer
    {
        public const int MaxEggs = 10;
        public const int FullEffects = 3;
        public const float CrackTogetherSeconds = 1.5f;
        public const float MomentSeconds = 0.8f;

        private const float AppearStagger = 0.035f;
        private const float AppearSeconds = 0.25f;
        private const float WobbleStart = 0.35f;
        private const float Crack1At = 0.85f;
        private const float Crack2At = 1.4f;
        private const float BurstAt = WobbleStart + CrackTogetherSeconds;
        private const float RevealStagger = 0.03f;
        /// <summary>The walk out: a newborn's lively pace (m/s), and the longest a group may take.</summary>
        private const float ExitSpeed = 1.6f;
        private const float ExitMaxSeconds = 1.6f;

        private readonly HatchPresentationConfig _config;
        private readonly FeedbackVfx _vfx;
        private readonly IAudioService _audio;
        private readonly IHapticService _haptics;

        private readonly EggView[] _eggs = new EggView[MaxEggs];
        private readonly AnimalView[] _animals = new AnimalView[MaxEggs];
        private readonly ContentId[] _species = new ContentId[MaxEggs];
        private readonly RarityTier[] _rarity = new RarityTier[MaxEggs];
        private readonly HatchNews[] _news = new HatchNews[MaxEggs];
        private readonly bool[] _full = new bool[MaxEggs];
        private readonly int[] _moments = new int[MaxEggs];
        private readonly Vector3[] _exits = new Vector3[MaxEggs];
        private readonly Vector3[] _exitFrom = new Vector3[MaxEggs];
        private readonly float[] _exitDelay = new float[MaxEggs];
        private readonly float[] _exitDuration = new float[MaxEggs];
        private readonly bool[] _exitArrived = new bool[MaxEggs];
        private readonly bool[] _exitWalking = new bool[MaxEggs];
        private bool _exiting;
        private float _exitStart;
        private float _exitEnd;
        private int _count;
        private int _momentCount;
        private int _moment;
        private float _momentStart;
        private IMultiHatchListener _listener;
        private float _now;
        private float _elapsed;
        private float _revealEnd;
        private bool _crack1Done, _crack2Done, _burstDone, _happyDone, _momentsDone;

        public MultiHatchPlayer(HatchPresentationConfig config, FeedbackVfx vfx, IAudioService audio, IHapticService haptics)
        {
            _config = config;
            _vfx = vfx;
            _audio = audio ?? NullAudioService.Instance;
            _haptics = haptics ?? NullHapticService.Instance;
        }

        public bool IsPlaying { get; private set; }
        public int Count => _count;
        public float Elapsed => _elapsed;

        /// <summary>The moment playing (index in the hatch), or -1.</summary>
        public int CurrentMoment => _moment >= 0 && _moment < _momentCount ? _moments[_moment] : -1;

        public int MomentCount => _momentCount;
        public bool Burst => _burstDone;
        public EggView Egg(int index) => _eggs[index];
        public AnimalView Animal(int index) => _animals[index];

        /// <summary>How many eggs play a full hatch effect (the best results first).</summary>
        public int FullEffectCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _count; i++)
                    if (_full[i])
                        n++;
                return n;
            }
        }

        /// <summary>
        /// Starts the staging. The eggs are configured on their spots (scale 0 until they pop in), the animals configured at
        /// the eggs' reveal points. Arrays are read once; the player keeps its own copies.
        /// </summary>
        public bool Play(EggView[] eggs, AnimalView[] animals, ContentId[] species, RarityTier[] rarity, HatchNews[] news, int count,
            IMultiHatchListener listener)
        {
            if (IsPlaying || count <= 0 || count > MaxEggs)
                return false;
            _count = count;
            _listener = listener;
            _elapsed = 0f;
            _moment = -1;
            _exiting = false;
            _crack1Done = _crack2Done = _burstDone = _happyDone = _momentsDone = false;
            _momentCount = 0;
            for (int i = 0; i < count; i++)
            {
                _eggs[i] = eggs[i];
                _animals[i] = animals[i];
                _species[i] = species[i];
                _rarity[i] = rarity[i];
                _news[i] = news[i];
                _full[i] = false;
                _eggs[i].SetShellPose(Vector3.zero, Quaternion.identity, Vector3.zero);
                if (_animals[i] != null)
                {
                    var reveal = _eggs[i].RevealPoint;
                    _animals[i].SetPose(reveal.position, reveal.eulerAngles.y);
                    _animals[i].SetScale(0.001f);
                }
                if (rarity[i] >= RarityTier.Rare || news[i] != HatchNews.None)
                    _moments[_momentCount++] = i;
            }

            // The full effects go to the best results: new species, then the rarest, then new colors.
            for (int k = 0; k < FullEffects && k < count; k++)
            {
                int best = -1;
                for (int i = 0; i < count; i++)
                    if (!_full[i] && (best < 0 || Score(i) > Score(best)))
                        best = i;
                _full[best] = true;
            }
            _revealEnd = BurstAt + _config.reveal + RevealStagger * count;
            _audio.Play(AudioEventId.EggTap, _eggs[0].VfxPoint.position, default);
            _haptics.Play(HapticEventId.TapLight);
            IsPlaying = true;
            return true;
        }

        private int Score(int i) => (_news[i] == HatchNews.NewSpecies ? 100 : 0) + (int)_rarity[i] * 10 + (_news[i] == HatchNews.NewVariant ? 5 : 0);

        public bool Exiting => _exiting;

        /// <summary>
        /// Before the burst: everything bursts now. During the moments: the next moment (the touch passes it). During the walk
        /// out: every newborn is at its place.
        /// </summary>
        public void Skip()
        {
            if (!IsPlaying)
                return;
            if (_exiting)
            {
                EvaluateExits(float.MaxValue);
                Finish();
                return;
            }
            if (!_burstDone)
            {
                if (!_crack1Done)
                    CrackAll(1);
                if (!_crack2Done)
                    CrackAll(2);
                BurstAll();
                _elapsed = Mathf.Max(_elapsed, BurstAt);
                for (int i = 0; i < _count; i++)
                {
                    _eggs[i].SetFragmentsVisible(false);
                    _animals[i]?.SetScale(1f);
                }
                if (!_happyDone)
                    PlayHappy();
                _elapsed = _revealEnd;
                return;
            }
            if (_elapsed < _revealEnd)
            {
                for (int i = 0; i < _count; i++)
                {
                    _eggs[i].SetFragmentsVisible(false);
                    _animals[i]?.SetScale(1f);
                }
                if (!_happyDone)
                    PlayHappy();
                _elapsed = _revealEnd;
                return;
            }
            NextMoment();
        }

        /// <summary>Scene exit: drops references without callbacks.</summary>
        public void Clear()
        {
            for (int i = 0; i < MaxEggs; i++)
            {
                _eggs[i] = null;
                _animals[i] = null;
            }
            _listener = null;
            IsPlaying = false;
            _count = 0;
        }

        public void Tick(float time, float deltaTime)
        {
            _now = time;
            if (!IsPlaying)
                return;
            _elapsed += deltaTime;
            float t = _elapsed;

            if (!_burstDone)
            {
                if (!_crack1Done && t >= Crack1At)
                    CrackAll(1);
                if (!_crack2Done && t >= Crack2At)
                    CrackAll(2);
                if (t >= BurstAt)
                    BurstAll();
                else
                    EvaluateShells(t);
            }
            if (_burstDone && t < _revealEnd + 0.01f)
            {
                float since = t - BurstAt;
                EvaluateFragments(since);
                EvaluateReveals(since);
                if (!_happyDone && since >= _config.happyDelay)
                    PlayHappy();
            }
            if (_burstDone && t >= _revealEnd && !_momentsDone)
            {
                if (_moment < 0)
                    NextMoment();
                else if (_moment < _momentCount && _now - _momentStart >= MomentSeconds)
                    NextMoment();
            }
            if (_exiting)
            {
                float since = _now - _exitStart;
                EvaluateExits(since);
                if (since >= _exitEnd)
                    Finish();
            }
        }

        private void StartExits()
        {
            for (int i = 0; i < _count; i++)
            {
                _exits[i] = _animals[i] != null ? _animals[i].transform.position : Vector3.zero;
                _exitDelay[i] = 0f;
            }
            if (_listener == null || !_listener.OnMultiExits(_exits, _exitDelay))
            {
                Finish();
                return;
            }
            _exitEnd = 0f;
            for (int i = 0; i < _count; i++)
            {
                _exitArrived[i] = true;
                _exitWalking[i] = false;
                _exitDuration[i] = 0f;
                if (_animals[i] == null)
                    continue;
                var from = _animals[i].transform.position;
                _exitFrom[i] = from;
                var flat = _exits[i] - from;
                flat.y = 0f;
                float distance = flat.magnitude;
                if (distance < 0.02f)
                    continue;
                _exitArrived[i] = false;
                _exitDuration[i] = Mathf.Min(distance / ExitSpeed, ExitMaxSeconds);
                _exitEnd = Mathf.Max(_exitEnd, _exitDelay[i] + _exitDuration[i]);
            }
            _exiting = true;
            _exitStart = _now;
            if (_exitEnd <= 0f)
                Finish();
        }

        /// <summary>Each newborn turns toward its place and walks there with its own gait, then settles.</summary>
        private void EvaluateExits(float since)
        {
            for (int i = 0; i < _count; i++)
            {
                if (_exitArrived[i] || _animals[i] == null)
                    continue;
                float local = since - _exitDelay[i];
                if (local < 0f)
                    continue;
                var view = _animals[i];
                var from = _exitFrom[i];
                var to = _exits[i];
                float yaw = Mathf.Atan2(to.x - from.x, to.z - from.z) * Mathf.Rad2Deg;
                if (!_exitWalking[i])
                {
                    _exitWalking[i] = true;
                    view.PlayState(AnimalStateHashes.Move, 0.1f);
                }
                float k = Mathf.Clamp01(local / _exitDuration[i]);
                // Eased at both ends: a lively start, a soft stop.
                float s = Mathf.SmoothStep(0f, 1f, k);
                view.SetPose(Vector3.Lerp(from, to, s), yaw);
                if (k >= 1f)
                {
                    _exitArrived[i] = true;
                    view.PlayState(AnimalStateHashes.Idle, 0.15f);
                }
            }
        }

        private void EvaluateShells(float t)
        {
            for (int i = 0; i < _count; i++)
            {
                float appear = Mathf.Clamp01((t - i * AppearStagger) / AppearSeconds);
                // Pop in past full size, then settle.
                float scale = appear < 0.7f ? Mathf.Lerp(0f, 1.12f, Mathf.SmoothStep(0f, 1f, appear / 0.7f))
                    : Mathf.Lerp(1.12f, 1f, Mathf.SmoothStep(0f, 1f, (appear - 0.7f) / 0.3f));
                float wobble = t < WobbleStart ? 0f
                    : t < Crack1At ? _config.wobbleDegrees1 * Mathf.SmoothStep(0f, 1f, (t - WobbleStart) / (Crack1At - WobbleStart))
                    : t < Crack2At ? _config.wobbleDegrees1 : _config.wobbleDegrees2;
                // Each egg keeps its own phase: together, never in lockstep.
                float phase = (t * _config.wobbleHz + i * 0.37f) * Mathf.PI * 2f;
                float swell = Mathf.Clamp01((t - (BurstAt - _config.swellSeconds)) / _config.swellSeconds);
                float grow = 1f + _config.swellBeforeBurst * swell * swell;
                var rotation = Quaternion.Euler(wobble * 0.35f * Mathf.Sin(phase * 0.7f + 1.3f), 0f, wobble * Mathf.Sin(phase));
                _eggs[i].SetShellPose(Vector3.zero, rotation, new Vector3(scale * grow, scale * grow, scale * grow));
                _eggs[i].SetGlow(Mathf.RoundToInt(swell * 15f));
            }
        }

        private void CrackAll(int stage)
        {
            if (stage == 1)
                _crack1Done = true;
            else
                _crack2Done = true;
            for (int i = 0; i < _count; i++)
                _eggs[i].SetCrackStage(stage);
            // One crack sound and one small puff for the group: a chorus, not ten cracks at once.
            var position = _eggs[0].VfxPoint.position;
            _vfx?.Play(VfxId.Crack, position, _now);
            _audio.Play(stage == 1 ? AudioEventId.Crack1 : AudioEventId.Crack2, position, default);
            _haptics.Play(HapticEventId.TapLight);
        }

        private void BurstAll()
        {
            _burstDone = true;
            var best = RarityTier.Common;
            for (int i = 0; i < _count; i++)
            {
                var egg = _eggs[i];
                egg.SetGlow(0);
                egg.SetShellPose(Vector3.zero, Quaternion.identity, Vector3.one);
                egg.SetShellVisible(false);
                // Shell pieces fly only from the full hatches; the other eggs open with a small shell puff (H-VFX-002).
                egg.SetFragmentsVisible(_full[i]);
                if (_rarity[i] > best)
                    best = _rarity[i];
                if (_vfx == null)
                    continue;
                var position = egg.VfxPoint.position;
                if (_full[i])
                {
                    _vfx.Play(VfxId.HatchCommon, position, _now);
                    var accent = HatchSequencePlayer.RarityVfx(_rarity[i]);
                    if (accent != VfxId.Count)
                        _vfx.Play(accent, position, _now);
                    if (_news[i] != HatchNews.None)
                        _vfx.Play(_news[i] == HatchNews.NewSpecies ? VfxId.NewSpecies : VfxId.NewVariant, egg.RevealPoint.position, _now);
                }
                else
                {
                    _vfx.Play(VfxId.Crack, position, _now);
                }
            }
            var center = _eggs[0].VfxPoint.position;
            _audio.Play(AudioEventId.HatchPop, center, default);
            var reveal = RevealAudio(best);
            if (reveal != AudioEventId.None)
                _audio.Play(reveal, center, default);
            _haptics.Play(best >= RarityTier.Legendary ? HapticEventId.Legendary : best >= RarityTier.Rare ? HapticEventId.Rare : HapticEventId.HatchCommon);
            _listener?.OnMultiRevealed();
        }

        private void EvaluateFragments(float since)
        {
            float flight = _config.fragmentFlightSeconds;
            float shrinkStart = flight - _config.fragmentShrinkSeconds;
            float scale = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((since - shrinkStart) / _config.fragmentShrinkSeconds));
            var flights = _config.fragments;
            for (int e = 0; e < _count; e++)
            {
                var egg = _eggs[e];
                if (!egg.FragmentsVisible)
                    continue;
                if (since >= flight)
                {
                    egg.SetFragmentsVisible(false);
                    continue;
                }
                for (int i = 0; i < EggView.FragmentCount && i < flights.Length; i++)
                {
                    var rest = egg.FragmentRestPosition(i);
                    // Smaller flights than a single hatch: ten eggs throw their shells close by.
                    var position = rest + flights[i].velocity * (since * 0.7f);
                    position.y -= 0.5f * _config.gravity * since * since;
                    // Local to the egg, which stands on the ground: a shell lands on the grass, never under it.
                    if (position.y < 0f)
                        position.y = 0f;
                    egg.SetFragmentPose(i, position, Quaternion.Euler(flights[i].spin * since), scale);
                }
            }
        }

        private void EvaluateReveals(float since)
        {
            for (int i = 0; i < _count; i++)
            {
                if (_animals[i] == null)
                    continue;
                float k = Mathf.Clamp01((since - i * RevealStagger) / _config.reveal);
                float overshoot = _config.revealOvershoot;
                float scale = k < 0.6f
                    ? Mathf.Lerp(0.001f, overshoot, Mathf.SmoothStep(0f, 1f, k / 0.6f))
                    : Mathf.Lerp(overshoot, 1f, Mathf.SmoothStep(0f, 1f, (k - 0.6f) / 0.4f));
                _animals[i].SetScale(scale);
            }
        }

        private void PlayHappy()
        {
            _happyDone = true;
            for (int i = 0; i < _count; i++)
                _animals[i]?.PlayState(AnimalStateHashes.Happy, 0.08f);
            // One happy voice for the group, from the best result.
            int best = 0;
            for (int i = 1; i < _count; i++)
                if (Score(i) > Score(best))
                    best = i;
            if (_animals[best] != null)
                _audio.Play(AudioEventId.AnimalHappy, _animals[best].transform.position, _species[best]);
        }

        private void NextMoment()
        {
            _moment++;
            _momentStart = _now;
            if (_moment < _momentCount)
            {
                _listener?.OnMultiMoment(_moments[_moment]);
                return;
            }
            _momentsDone = true;
            StartExits();
        }

        private void Finish()
        {
            for (int i = 0; i < _count; i++)
            {
                _animals[i]?.SetScale(1f);
                if (_eggs[i].FragmentsVisible)
                    _eggs[i].SetFragmentsVisible(false);
            }
            IsPlaying = false;
            _exiting = false;
            var listener = _listener;
            _listener = null;
            listener?.OnMultiFinished();
        }

        private static AudioEventId RevealAudio(RarityTier rarity)
        {
            switch (rarity)
            {
                case RarityTier.Rare: return AudioEventId.RareReveal;
                case RarityTier.Epic: return AudioEventId.EpicReveal;
                case RarityTier.Legendary: return AudioEventId.LegendaryReveal;
                case RarityTier.Fabulous: return AudioEventId.FabulousReveal;
                default: return AudioEventId.None;
            }
        }
    }
}
