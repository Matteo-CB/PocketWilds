using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Services.Feedback;
using UnityEngine;

namespace PocketWilds.Presentation
{
    public enum HatchPhase : byte
    {
        Idle = 0,
        Waiting = 1,
        Anticipation = 2,
        Cracked1 = 3,
        Cracked2 = 4,
        Revealing = 5,
        Celebrating = 6,
        Done = 7,
    }

    /// <summary>Optional callbacks without delegates (no allocation per hatch).</summary>
    public interface IHatchListener
    {
        void OnHatchRevealed();
        void OnHatchFinished();
    }

    /// <summary>
    /// Plays the hatch staging on an <see cref="EggView"/>: waiting pulse, anticipation wobble, two cracks,
    /// shell burst, animal reveal, short celebration. One Tick per frame from a scene manager; everything
    /// is evaluated from elapsed time, so Skip and speed changes are exact. No coroutine, no physics,
    /// no allocation after construction. The hatch result itself is already decided by the simulation:
    /// this class only presents it, and skipping never changes it.
    /// </summary>
    public sealed class HatchSequencePlayer
    {
        private const float ShakeHzMultiplier = 2.2f;

        private readonly HatchPresentationConfig _config;
        private readonly VfxPool _vfx;
        private readonly int _crackVfx;
        private readonly int _commonVfx;
        private readonly int _rareVfx;
        private readonly IAudioService _audio;
        private readonly IHapticService _haptics;

        private EggView _egg;
        private AnimalView _animal;
        private IHatchListener _listener;
        private ContentId _species;
        private RarityTier _rarity;
        private float _now;
        private float _waiting;
        private float _elapsed;
        private float _speed = 1f;
        private float _tCrack1, _tCrack2, _tBurst, _tRevealEnd, _tEnd;
        private float _lastCrackTime = -10f;
        private int _glow = -1;
        private bool _crack1Done, _crack2Done, _burstDone, _happyDone;

        /// <param name="rareVfx">-1 when no rare treatment is registered.</param>
        public HatchSequencePlayer(HatchPresentationConfig config, VfxPool vfx, int crackVfx, int commonVfx, int rareVfx,
            IAudioService audio, IHapticService haptics)
        {
            _config = config;
            _vfx = vfx;
            _crackVfx = crackVfx;
            _commonVfx = commonVfx;
            _rareVfx = rareVfx;
            _audio = audio ?? NullAudioService.Instance;
            _haptics = haptics ?? NullHapticService.Instance;
        }

        public HatchPhase Phase { get; private set; }
        public EggView Egg => _egg;
        public AnimalView Animal => _animal;
        public bool IsPlaying => Phase >= HatchPhase.Anticipation && Phase < HatchPhase.Done;

        /// <summary>Sequence time already played, in seconds at speed 1.</summary>
        public float Elapsed => _elapsed;

        /// <summary>Total length of the current sequence, in seconds at speed 1.</summary>
        public float Duration => _tEnd;
        public float Speed => _speed;

        /// <summary>Shows an egg waiting for the touch: a light pulse that invites the tap.</summary>
        public void Present(EggView egg)
        {
            _egg = egg;
            _animal = null;
            _listener = null;
            _waiting = 0f;
            Phase = HatchPhase.Waiting;
        }

        /// <summary>Starts the staging for an already resolved hatch. The animal view must be configured.</summary>
        public bool Hatch(AnimalView animal, ContentId species, RarityTier rarity, bool repeat, IHatchListener listener)
        {
            if (Phase != HatchPhase.Waiting || _egg == null || animal == null)
                return false;

            _animal = animal;
            _species = species;
            _rarity = rarity;
            _listener = listener;
            _speed = repeat ? _config.repeatSpeed : 1f;
            _elapsed = 0f;
            _lastCrackTime = -10f;
            _glow = -1;
            _crack1Done = _crack2Done = _burstDone = _happyDone = false;

            _tCrack1 = _config.anticipation;
            _tCrack2 = _tCrack1 + _config.afterCrack1;
            _tBurst = _tCrack2 + _config.afterCrack2;
            _tRevealEnd = _tBurst + _config.reveal;
            _tEnd = _tRevealEnd + _config.celebrate + _config.RarityExtra(rarity);

            var reveal = _egg.RevealPoint;
            _animal.SetPose(reveal.position, reveal.eulerAngles.y);
            _animal.SetScale(0.001f);

            var position = _egg.VfxPoint.position;
            _audio.Play(AudioEventId.EggTap, position, default);
            _haptics.Play(HapticEventId.TapLight);
            Phase = HatchPhase.Anticipation;
            return true;
        }

        /// <summary>Jumps to the revealed animal. The result was decided before the staging began.</summary>
        public void Skip()
        {
            if (!IsPlaying)
                return;
            if (!_burstDone)
                Burst();
            _egg.SetFragmentsVisible(false);
            _animal.SetScale(1f);
            if (!_happyDone)
                PlayHappy();
            Finish();
        }

        /// <summary>Scene exit: drops references without callbacks.</summary>
        public void Clear()
        {
            _egg = null;
            _animal = null;
            _listener = null;
            Phase = HatchPhase.Idle;
        }

        public void Tick(float time, float deltaTime)
        {
            _now = time;
            if (Phase == HatchPhase.Waiting)
            {
                _waiting += deltaTime;
                float pulse = 1f + _config.idlePulseScale * (0.5f - 0.5f * Mathf.Cos(_waiting * _config.idlePulseHz * Mathf.PI * 2f));
                _egg.SetShellPose(Vector3.zero, Quaternion.identity, new Vector3(pulse, pulse, pulse));
                return;
            }
            if (!IsPlaying)
                return;

            _elapsed += deltaTime * _speed;
            float t = _elapsed;

            if (!_crack1Done && t >= _tCrack1)
                Crack(1);
            if (!_crack2Done && t >= _tCrack2)
                Crack(2);
            if (!_burstDone && t >= _tBurst)
                Burst();

            if (!_burstDone)
            {
                EvaluateShell(t);
            }
            else
            {
                float since = t - _tBurst;
                EvaluateFragments(since);
                EvaluateReveal(since);
                if (!_happyDone && since >= _config.happyDelay)
                    PlayHappy();
                if (Phase == HatchPhase.Revealing && t >= _tRevealEnd)
                    Phase = HatchPhase.Celebrating;
            }

            if (t >= _tEnd)
                Finish();
        }

        private void Crack(int stage)
        {
            if (stage == 1)
                _crack1Done = true;
            else
                _crack2Done = true;
            _lastCrackTime = stage == 1 ? _tCrack1 : _tCrack2;
            _egg.SetCrackStage(stage);
            var position = _egg.VfxPoint.position;
            if (_crackVfx >= 0)
                _vfx.Play(_crackVfx, position, Quaternion.identity, _now);
            _audio.Play(stage == 1 ? AudioEventId.Crack1 : AudioEventId.Crack2, position, default);
            _haptics.Play(HapticEventId.TapLight);
            Phase = stage == 1 ? HatchPhase.Cracked1 : HatchPhase.Cracked2;
        }

        private void EvaluateShell(float t)
        {
            float amplitude;
            if (t < _tCrack1)
                amplitude = _config.wobbleDegrees1 * Mathf.SmoothStep(0f, 1f, t / _tCrack1);
            else if (t < _tCrack2)
                amplitude = _config.wobbleDegrees1;
            else
                amplitude = _config.wobbleDegrees2;

            float swellStart = _tBurst - _config.swellSeconds;
            float swell = t > swellStart ? Mathf.Clamp01((t - swellStart) / _config.swellSeconds) : 0f;
            float hz = _config.wobbleHz * (1f + (ShakeHzMultiplier - 1f) * swell);
            float phase = t * hz * Mathf.PI * 2f;
            var rotation = Quaternion.Euler(amplitude * 0.35f * Mathf.Sin(phase * 0.7f + 1.3f), 0f, amplitude * Mathf.Sin(phase));

            // Squash punch on each crack, then a swell right before the burst.
            float punch = _config.crackSquash * Mathf.Exp(-(t - _lastCrackTime) * 10f);
            float grow = 1f + _config.swellBeforeBurst * swell * swell;
            var scale = new Vector3((1f + punch * 0.6f) * grow, (1f - punch) * grow, (1f + punch * 0.6f) * grow);
            _egg.SetShellPose(Vector3.zero, rotation, scale);

            int glow = Mathf.RoundToInt(swell * 15f);
            if (glow != _glow)
            {
                _glow = glow;
                _egg.SetGlow(glow);
            }
        }

        private void Burst()
        {
            _burstDone = true;
            _egg.SetGlow(0);
            _egg.SetShellVisible(false);
            _egg.SetFragmentsVisible(true);

            var position = _egg.VfxPoint.position;
            if (_commonVfx >= 0)
                _vfx.Play(_commonVfx, position, Quaternion.identity, _now);
            if (_rareVfx >= 0 && _rarity >= RarityTier.Rare)
                _vfx.Play(_rareVfx, position, Quaternion.identity, _now);

            _audio.Play(AudioEventId.HatchPop, position, default);
            var reveal = RevealAudio(_rarity);
            if (reveal != AudioEventId.None)
                _audio.Play(reveal, position, default);
            _haptics.Play(RevealHaptic(_rarity));
            Phase = HatchPhase.Revealing;
            _listener?.OnHatchRevealed();
        }

        private void EvaluateFragments(float since)
        {
            if (!_egg.FragmentsVisible)
                return;
            float flight = _config.fragmentFlightSeconds;
            if (since >= flight)
            {
                _egg.SetFragmentsVisible(false);
                return;
            }

            float shrinkStart = flight - _config.fragmentShrinkSeconds;
            float scale = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((since - shrinkStart) / _config.fragmentShrinkSeconds));
            var flights = _config.fragments;
            for (int i = 0; i < EggView.FragmentCount && i < flights.Length; i++)
            {
                var rest = _egg.FragmentRestPosition(i);
                var position = rest + flights[i].velocity * since;
                position.y -= 0.5f * _config.gravity * since * since;
                if (position.y < 0f)
                    position.y = 0f;
                _egg.SetFragmentPose(i, position, Quaternion.Euler(flights[i].spin * since), scale);
            }
        }

        private void EvaluateReveal(float since)
        {
            float k = Mathf.Clamp01(since / _config.reveal);
            float overshoot = _config.revealOvershoot;
            // Pop past full size, then settle: reads as a happy jump out of the shell.
            float scale = k < 0.6f
                ? Mathf.Lerp(0.001f, overshoot, Mathf.SmoothStep(0f, 1f, k / 0.6f))
                : Mathf.Lerp(overshoot, 1f, Mathf.SmoothStep(0f, 1f, (k - 0.6f) / 0.4f));
            _animal.SetScale(scale);
        }

        private void PlayHappy()
        {
            _happyDone = true;
            _animal.PlayState(AnimalStateHashes.Happy, 0.08f);
            _audio.Play(AudioEventId.AnimalHappy, _animal.transform.position, _species);
        }

        private void Finish()
        {
            _animal.SetScale(1f);
            if (_egg.FragmentsVisible)
                _egg.SetFragmentsVisible(false);
            Phase = HatchPhase.Done;
            var listener = _listener;
            _listener = null;
            listener?.OnHatchFinished();
        }

        private static AudioEventId RevealAudio(RarityTier rarity)
        {
            switch (rarity)
            {
                case RarityTier.Rare: return AudioEventId.RareReveal;
                case RarityTier.Epic: return AudioEventId.EpicReveal;
                case RarityTier.Legendary: return AudioEventId.LegendaryReveal;
                case RarityTier.Mythic: return AudioEventId.MythicReveal;
                default: return AudioEventId.None;
            }
        }

        private static HapticEventId RevealHaptic(RarityTier rarity)
        {
            switch (rarity)
            {
                case RarityTier.Rare:
                case RarityTier.Epic:
                    return HapticEventId.Rare;
                case RarityTier.Legendary: return HapticEventId.Legendary;
                case RarityTier.Mythic: return HapticEventId.Mythic;
                default: return HapticEventId.HatchCommon;
            }
        }
    }
}
