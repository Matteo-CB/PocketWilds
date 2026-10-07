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

        void OnMultiFinished();
    }

    /// <summary>
    /// T35B, doc 02 Œufs section 5 and doc 05 Sensations section 30: the staging of a × 3 or × 10 hatch, results already
    /// decided by the simulation. The eggs pop onto measured spots (400 ms each, 40 ms apart), wobble and crack together
    /// (1.5 s, one crack sound for all), then burst in a wave from left to right (60 ms apart): at most three full hatch
    /// effects (the best results), a small shell puff for the others, at most five pop sounds, then the sound of the
    /// rarest result. Each Rare or better result and each new species or color has a 0.8 s moment of its own (the camera
    /// comes closer; a touch passes it; the rarest one's vibration plays at its moment). Then the listener hands the
    /// newborns to the meadow, where each walks out to a free place (rule « aucune collision »). Reduced motion: no pop,
    /// no wobble, no flying shell.
    /// Evaluated from elapsed time like <see cref="HatchSequencePlayer"/>: no coroutine, no physics, no allocation.
    /// </summary>
    public sealed class MultiHatchPlayer
    {
        public const int MaxEggs = 10;
        public const int FullEffects = 3;
        public const float CrackTogetherSeconds = 1.5f;
        public const float MomentSeconds = 0.8f;
        public const float AppearSeconds = 0.4f;
        public const float AppearStagger = 0.04f;
        public const float BurstWaveStep = 0.06f;
        public const int MaxPops = 5;

        private const float WobbleStart = 0.4f;
        private const float Crack1At = 0.9f;
        private const float Crack2At = 1.45f;
        private const float BurstAt = WobbleStart + CrackTogetherSeconds;

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
        private readonly int[] _wave = new int[MaxEggs];
        private readonly float[] _burstAt = new float[MaxEggs];
        private readonly bool[] _burst = new bool[MaxEggs];
        private int _count;
        private int _burstCount;
        private int _popsPlayed;
        private int _popStep;
        private int _momentCount;
        private int _moment;
        private int _bestMoment;
        private float _momentStart;
        private IMultiHatchListener _listener;
        private float _now;
        private float _elapsed;
        private float _lastBurst;
        private float _revealEnd;
        private RarityTier _best;
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

        /// <summary>Player setting: eggs and newborns appear without pop, no wobble, no flying shell.</summary>
        public bool ReduceMotion { get; set; }

        /// <summary>The moment playing (index in the hatch), or -1.</summary>
        public int CurrentMoment => _moment >= 0 && _moment < _momentCount ? _moments[_moment] : -1;

        public int MomentCount => _momentCount;
        public bool Burst => _burstDone;
        public int BurstCount => _burstCount;
        public int PopsPlayed => _popsPlayed;
        public EggView Egg(int index) => _eggs[index];
        public AnimalView Animal(int index) => _animals[index];

        /// <summary>Seconds after the start at which this egg bursts (its place in the left to right wave).</summary>
        public float BurstTime(int index) => _burstAt[index];

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
            _bestMoment = -1;
            _burstCount = 0;
            _popsPlayed = 0;
            _popStep = (count + MaxPops - 1) / MaxPops;
            _best = RarityTier.Common;
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
                _burst[i] = false;
                if (rarity[i] > _best)
                    _best = rarity[i];
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

            // The burst wave runs from left to right as the camera sees it (its yaw is fixed: world x).
            for (int i = 0; i < count; i++)
                _wave[i] = i;
            for (int i = 1; i < count; i++)
            {
                int item = _wave[i];
                float x = _eggs[item].transform.position.x;
                int j = i - 1;
                while (j >= 0 && _eggs[_wave[j]].transform.position.x > x)
                {
                    _wave[j + 1] = _wave[j];
                    j--;
                }
                _wave[j + 1] = item;
            }
            for (int rank = 0; rank < count; rank++)
                _burstAt[_wave[rank]] = BurstAt + rank * BurstWaveStep;
            _lastBurst = BurstAt + (count - 1) * BurstWaveStep;
            _revealEnd = _lastBurst + _config.reveal;

            // The rarest result's vibration waits for its own moment.
            if (_best >= RarityTier.Rare)
            {
                for (int m = 0; m < _momentCount; m++)
                {
                    if (_rarity[_moments[m]] == _best)
                    {
                        _bestMoment = m;
                        break;
                    }
                }
            }

            _audio.Play(AudioEventId.EggTap, _eggs[0].VfxPoint.position, default);
            _haptics.Play(HapticEventId.TapLight);
            IsPlaying = true;
            return true;
        }

        private int Score(int i) => (_news[i] == HatchNews.NewSpecies ? 100 : 0) + (int)_rarity[i] * 10 + (_news[i] == HatchNews.NewVariant ? 5 : 0);

        /// <summary>Before the bursts: every egg bursts now. During the moments: the next moment (the touch passes it).</summary>
        public void Skip()
        {
            if (!IsPlaying)
                return;
            if (!_burstDone || _elapsed < _revealEnd)
            {
                if (!_crack1Done)
                    CrackAll(1);
                if (!_crack2Done)
                    CrackAll(2);
                for (int i = 0; i < _count; i++)
                {
                    if (!_burst[i])
                        BurstEgg(i);
                    _eggs[i].SetFragmentsVisible(false);
                    _animals[i]?.SetScale(1f);
                }
                if (!_burstDone)
                    EndWave();
                if (!_happyDone)
                    PlayHappy();
                _elapsed = Mathf.Max(_elapsed, _revealEnd);
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
                for (int i = 0; i < _count; i++)
                {
                    if (_burst[i])
                        continue;
                    if (t >= _burstAt[i])
                        BurstEgg(i);
                    else
                        EvaluateShell(i, t);
                }
                if (_burstCount == _count)
                    EndWave();
            }
            if (_burstCount > 0 && t < _revealEnd + 0.01f)
            {
                EvaluateFragments(t);
                EvaluateReveals(t);
                if (!_happyDone && t >= _lastBurst + _config.happyDelay)
                    PlayHappy();
            }
            if (_burstDone && t >= _revealEnd && !_momentsDone)
            {
                if (_moment < 0)
                    NextMoment();
                else if (_moment < _momentCount && _now - _momentStart >= MomentSeconds)
                    NextMoment();
            }
        }

        private void EvaluateShell(int i, float t)
        {
            float appear = Mathf.Clamp01((t - i * AppearStagger) / AppearSeconds);
            // Pop in past full size, then settle (reduced motion: a plain grow, no overshoot).
            float scale = ReduceMotion ? Mathf.SmoothStep(0f, 1f, appear)
                : appear < 0.7f ? Mathf.Lerp(0f, 1.12f, Mathf.SmoothStep(0f, 1f, appear / 0.7f))
                : Mathf.Lerp(1.12f, 1f, Mathf.SmoothStep(0f, 1f, (appear - 0.7f) / 0.3f));
            if (ReduceMotion)
            {
                _eggs[i].SetShellPose(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, scale));
                return;
            }
            float wobble = t < WobbleStart ? 0f
                : t < Crack1At ? _config.wobbleDegrees1 * Mathf.SmoothStep(0f, 1f, (t - WobbleStart) / (Crack1At - WobbleStart))
                : t < Crack2At ? _config.wobbleDegrees1 : _config.wobbleDegrees2;
            // Each egg keeps its own phase: together, never in lockstep.
            float phase = (t * _config.wobbleHz + i * 0.37f) * Mathf.PI * 2f;
            float swell = Mathf.Clamp01((t - (_burstAt[i] - _config.swellSeconds)) / _config.swellSeconds);
            float grow = 1f + _config.swellBeforeBurst * swell * swell;
            var rotation = Quaternion.Euler(wobble * 0.35f * Mathf.Sin(phase * 0.7f + 1.3f), 0f, wobble * Mathf.Sin(phase));
            _eggs[i].SetShellPose(Vector3.zero, rotation, new Vector3(scale * grow, scale * grow, scale * grow));
            _eggs[i].SetGlow(Mathf.RoundToInt(swell * 15f));
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

        /// <summary>One egg of the wave opens: its effect, and a pop sound for one egg in <see cref="_popStep"/>.</summary>
        private void BurstEgg(int i)
        {
            int rank = _burstCount++;
            _burst[i] = true;
            var egg = _eggs[i];
            egg.SetGlow(0);
            egg.SetShellPose(Vector3.zero, Quaternion.identity, Vector3.one);
            egg.SetShellVisible(false);
            // Shell pieces fly only from the full hatches; the other eggs open with a small shell puff (H-VFX-002).
            egg.SetFragmentsVisible(_full[i] && !ReduceMotion);
            var position = egg.VfxPoint.position;
            if (_vfx != null)
            {
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
            if (rank % _popStep == 0 && _popsPlayed < MaxPops)
            {
                _popsPlayed++;
                _audio.Play(AudioEventId.HatchPop, position, default);
            }
            if (rank == 0)
                _haptics.Play(HapticEventId.HatchCommon);
        }

        /// <summary>The last egg of the wave is open: the rarest result's sound, then the listener.</summary>
        private void EndWave()
        {
            _burstDone = true;
            var reveal = RevealAudio(_best);
            if (reveal != AudioEventId.None)
                _audio.Play(reveal, _eggs[_wave[_count - 1]].VfxPoint.position, default);
            _listener?.OnMultiRevealed();
        }

        private void EvaluateFragments(float t)
        {
            float flight = _config.fragmentFlightSeconds;
            float shrinkStart = flight - _config.fragmentShrinkSeconds;
            var flights = _config.fragments;
            for (int e = 0; e < _count; e++)
            {
                var egg = _eggs[e];
                if (!_burst[e] || !egg.FragmentsVisible)
                    continue;
                float since = t - _burstAt[e];
                if (since >= flight)
                {
                    egg.SetFragmentsVisible(false);
                    continue;
                }
                float scale = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((since - shrinkStart) / _config.fragmentShrinkSeconds));
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

        private void EvaluateReveals(float t)
        {
            for (int i = 0; i < _count; i++)
            {
                if (_animals[i] == null || !_burst[i])
                    continue;
                float k = Mathf.Clamp01((t - _burstAt[i]) / _config.reveal);
                float overshoot = ReduceMotion ? 1f : _config.revealOvershoot;
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
                if (_moment == _bestMoment)
                    _haptics.Play(_best >= RarityTier.Legendary ? HapticEventId.Legendary : HapticEventId.Rare);
                _listener?.OnMultiMoment(_moments[_moment]);
                return;
            }
            _momentsDone = true;
            Finish();
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
