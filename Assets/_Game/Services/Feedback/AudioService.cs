using System;
using PocketWilds.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PocketWilds.Services.Feedback
{
    /// <summary>
    /// T34B audio service (doc 08, doc 05 Audio). Gameplay and presentation emit AudioEventIds; this service owns the
    /// clips, a bounded pool of one-shot voices, the music and ambience beds and the mixer levels.
    ///
    /// - Voice pool: a fixed set of AudioSources made once. Cooldown and voice cap per sound; when the pool is full,
    ///   the least important sound is dropped or replaced (purchase/UI, then hatch and rewards, then the focused
    ///   animal, then animals, then ambient one-shots). Nothing is created or destroyed per sound.
    /// - World sounds (animals) fade with their distance to the view and pan with their side; an animal far from
    ///   the view never takes a voice.
    /// - Collect plays a bounded run of rising notes, never one sound per mote.
    /// - Beds: music and ambience loops, streamed, cross-faded; reveals lower the music a little.
    /// - No allocation per sound: tables are built at start, random numbers come from a small xorshift.
    /// </summary>
    public sealed class AudioService : IAudioService, IDisposable
    {
        public const string MusicParameter = "MusicVolume";
        public const string AmbienceParameter = "AmbienceVolume";
        public const string SfxParameter = "SfxVolume";

        private const float SilentDb = -80f;
        private const float DuckDb = -5f;
        private const float DuckHold = 1.3f;
        private const int MaxBursts = 4;

        private struct Voice
        {
            public AudioSource Source;
            public AudioEventId Id;
            public int Priority;
            public float Start;
            public float End;
        }

        private struct Burst
        {
            public AudioEventDefinition Def;
            public int Note;
            public int Count;
            public float Next;
            public float Gain;
            public float Pan;
        }

        private struct SpeciesEntry
        {
            public ContentId Id;
            public SpeciesVoice Voice;
        }

        /// <summary>A looping bed (music or ambience): two sources cross-fading.</summary>
        private sealed class Bed
        {
            private readonly AudioSource[] _sources = new AudioSource[2];
            private readonly float[] _target = new float[2];
            private int _active;
            private float _speed = 1f;

            public Bed(GameObject root, AudioService owner, AudioGroup group)
            {
                for (int i = 0; i < 2; i++)
                {
                    var source = root.AddComponent<AudioSource>();
                    Configure(source);
                    source.loop = true;
                    source.priority = 0;
                    source.volume = 0f;
                    source.outputAudioMixerGroup = owner._catalog != null ? owner._catalog.Group(group) : null;
                    _sources[i] = source;
                }
            }

            public AudioClip Clip => _sources[_active].clip;
            public bool IsPlaying => _sources[_active].isPlaying;
            public float Volume => _sources[_active].volume;

            public void Set(AudioClip clip, float fadeSeconds)
            {
                _speed = 1f / Mathf.Max(fadeSeconds, 0.01f);
                if (clip == _sources[_active].clip && clip != null)
                {
                    _target[_active] = 1f;
                    return;
                }
                _target[_active] = 0f;
                if (clip == null)
                    return;
                int next = 1 - _active;
                var source = _sources[next];
                source.Stop();
                source.clip = clip;
                source.volume = 0f;
                source.Play();
                _target[next] = 1f;
                _active = next;
            }

            public void Tick(float deltaTime, bool enabled)
            {
                for (int i = 0; i < 2; i++)
                {
                    var source = _sources[i];
                    if (source.clip == null)
                        continue;
                    if (!enabled)
                    {
                        // Music off: nothing decodes; the clip stays for when it comes back.
                        if (source.isPlaying)
                            source.Stop();
                        source.volume = 0f;
                        continue;
                    }
                    if (_target[i] > 0f && !source.isPlaying)
                        source.Play();
                    float volume = Mathf.MoveTowards(source.volume, _target[i], _speed * deltaTime);
                    source.volume = volume;
                    if (volume <= 0f && _target[i] <= 0f && source.isPlaying)
                    {
                        source.Stop();
                        if (i != _active)
                            source.clip = null;
                    }
                }
            }

            public void StopNow()
            {
                for (int i = 0; i < 2; i++)
                {
                    if (_sources[i] == null)
                        continue;
                    _sources[i].Stop();
                    _sources[i].clip = null;
                    _sources[i].volume = 0f;
                    _target[i] = 0f;
                }
            }
        }

        private readonly AudioCatalog _catalog;
        private readonly GameObject _root;
        private readonly Voice[] _voices;
        private readonly AudioEventDefinition[] _byId;
        private readonly float[] _lastStart;
        private readonly int[] _lastVariant;
        private readonly SpeciesEntry[] _species;
        private readonly Burst[] _bursts = new Burst[MaxBursts];
        private readonly Bed _music;
        private readonly Bed _ambience;

        private uint _rng = 0x9E3779B9u;
        private float _time;
        private Vector3 _focus;
        private Vector3 _right = Vector3.right;
        private float _cameraDistance = 12f;
        private bool _hasView;
        private float _sfxLevel = 1f;
        private float _musicLevel = 0.8f;
        private bool _batterySaver;
        private float _duck;
        private float _duckUntil = -1f;
        private float _appliedMusicDb = float.NaN;
        private float _appliedSfxDb = float.NaN;

        /// <param name="listener">
        /// Adds the game's only AudioListener on the audio root (every sound is 2D: it never needs to follow a camera),
        /// so boot, scene changes and screens never run without one.
        /// </param>
        public AudioService(AudioCatalog catalog, Transform parent = null, bool listener = false)
        {
            _catalog = catalog;
            _root = new GameObject("PW_Audio");
            if (parent != null)
                _root.transform.SetParent(parent, false);
            if (listener)
                _root.AddComponent<AudioListener>();

            int voices = catalog != null ? catalog.voices : 0;
            _voices = new Voice[voices];
            for (int i = 0; i < voices; i++)
            {
                var source = _root.AddComponent<AudioSource>();
                Configure(source);
                _voices[i].Source = source;
                _voices[i].End = -1f;
            }

            int maxId = 0;
            var events = catalog != null ? catalog.events : Array.Empty<AudioEventDefinition>();
            foreach (var def in events)
                if (def != null && (int)def.id > maxId)
                    maxId = (int)def.id;
            _byId = new AudioEventDefinition[maxId + 1];
            foreach (var def in events)
                if (def != null && def.id != AudioEventId.None)
                    _byId[(int)def.id] = def;
            _lastStart = new float[maxId + 1];
            _lastVariant = new int[maxId + 1];
            for (int i = 0; i <= maxId; i++)
            {
                _lastStart[i] = -1000f;
                _lastVariant[i] = -1;
            }

            var species = catalog != null ? catalog.species : Array.Empty<SpeciesVoice>();
            _species = new SpeciesEntry[species.Length];
            for (int i = 0; i < species.Length; i++)
                _species[i] = new SpeciesEntry { Id = species[i] != null ? ContentId.FromSource(species[i].species) : ContentId.None, Voice = species[i] };

            _music = new Bed(_root, this, AudioGroup.Music);
            _ambience = new Bed(_root, this, AudioGroup.Ambience);
            ApplyMixer(true);
        }

        public GameObject Root => _root;
        public int VoiceCount => _voices.Length;

        /// <summary>One-shots started, dropped (cooldown, cap, off view, full pool) and replaced since start.</summary>
        public int Started { get; private set; }
        public int Dropped { get; private set; }
        public int Stolen { get; private set; }
        public int PeakVoices { get; private set; }

        public int ActiveVoices
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _voices.Length; i++)
                    if (_voices[i].End > _time)
                        n++;
                return n;
            }
        }

        /// <summary>Voices of one sound playing now.</summary>
        public int ActiveVoicesOf(AudioEventId id)
        {
            int n = 0;
            for (int i = 0; i < _voices.Length; i++)
                if (_voices[i].End > _time && _voices[i].Id == id)
                    n++;
            return n;
        }

        public AudioClip MusicClip => _music.Clip;
        public AudioClip AmbienceClip => _ambience.Clip;
        public bool MusicPlaying => _music.IsPlaying;
        public float MusicFade => _music.Volume;
        public float DuckDecibels => _duck;

        public void ResetStats()
        {
            Started = Dropped = Stolen = PeakVoices = 0;
        }

        /// <summary>Player settings (SettingsState): sounds level (effects and ambience), music level, battery saver.</summary>
        public void ApplySettings(float sfxVolume, float musicVolume, bool batterySaver)
        {
            _sfxLevel = Mathf.Clamp01(sfxVolume);
            _musicLevel = Mathf.Clamp01(musicVolume);
            _batterySaver = batterySaver;
            ApplyMixer(true);
        }

        public void SetView(Vector3 focus, Vector3 right, float cameraDistance)
        {
            _focus = focus;
            right.y = 0f;
            _right = right.sqrMagnitude > 1e-6f ? right.normalized : Vector3.right;
            _cameraDistance = Mathf.Max(cameraDistance, 0.5f);
            _hasView = true;
        }

        public void ClearView() => _hasView = false;

        public void SetBeds(AudioClip music, AudioClip ambience, float fadeSeconds)
        {
            _music.Set(music, fadeSeconds);
            _ambience.Set(ambience, fadeSeconds);
        }

        /// <summary>Scene exit before its clips are released: beds stop at once.</summary>
        public void StopBeds()
        {
            if (_root == null)
                return;
            _music.StopNow();
            _ambience.StopNow();
        }

        public void Play(AudioEventId id, Vector3 position, ContentId context)
        {
            int index = (int)id;
            if (index <= 0 || index >= _byId.Length)
                return;
            var def = _byId[index];
            if (def == null || _sfxLevel <= 0f || _root == null)
                return;
            if ((_batterySaver && def.skipOnBatterySaver) || _time - _lastStart[index] < def.cooldown)
            {
                Dropped++;
                return;
            }
            float gain = 1f, pan = 0f;
            if (def.world)
            {
                gain = ViewGain(position, out pan);
                if (gain < 0.04f)
                {
                    Dropped++;
                    return;
                }
            }
            if (def.burstCount > 0)
            {
                if (StartBurst(def, gain, pan))
                    _lastStart[index] = _time;
                return;
            }

            float pitch = 1f;
            var clips = def.clips;
            if (context.IsValid && (id == AudioEventId.AnimalHappy || id == AudioEventId.AnimalChatter || id == AudioEventId.AnimalSleep))
            {
                var voice = FindSpecies(context);
                if (voice != null)
                {
                    var own = id == AudioEventId.AnimalSleep ? voice.sleep : voice.happy;
                    if (own != null && own.Length > 0)
                        clips = own;
                    pitch = voice.pitch;
                }
            }
            if (clips == null || clips.Length == 0)
                return;
            var clip = clips[PickVariant(index, clips.Length)];
            if (clip == null)
                return;
            if (StartVoice(def, clip, pitch * Jitter(def.pitchJitter), def.volume * gain * Jitter(def.volumeJitter), pan))
            {
                _lastStart[index] = _time;
                if (def.duckMusic)
                    _duckUntil = _time + DuckHold;
            }
        }

        /// <summary>Called once per frame (GameLoop). Bursts, bed fades, music ducking.</summary>
        public void Tick(float deltaTime)
        {
            if (_root == null)
                return;
            _time += deltaTime;
            for (int b = 0; b < _bursts.Length; b++)
            {
                ref var burst = ref _bursts[b];
                while (burst.Count > 0 && _time >= burst.Next)
                {
                    var def = burst.Def;
                    int semis = def.burstSemitones != null && def.burstSemitones.Length > 0 ? def.burstSemitones[burst.Note % def.burstSemitones.Length] : 0;
                    var clip = def.clips[burst.Note % def.clips.Length];
                    if (clip != null && _sfxLevel > 0f)
                        StartVoice(def, clip, Mathf.Pow(2f, semis / 12f) * Jitter(def.pitchJitter * 0.5f),
                            def.volume * burst.Gain * Jitter(def.volumeJitter), burst.Pan);
                    burst.Note++;
                    burst.Next += def.burstInterval;
                    if (burst.Note >= burst.Count)
                        burst.Count = 0;
                }
            }

            _music.Tick(deltaTime, _musicLevel > 0f);
            _ambience.Tick(deltaTime, _sfxLevel > 0f);

            float duckTarget = _time < _duckUntil ? DuckDb : 0f;
            _duck = Mathf.MoveTowards(_duck, duckTarget, (duckTarget < _duck ? 30f : 6f) * deltaTime);
            ApplyMixer(false);
        }

        public void Dispose()
        {
            if (_root != null)
            {
                if (Application.isPlaying)
                    Object.Destroy(_root);
                else
                    Object.DestroyImmediate(_root);
            }
        }

        private SpeciesVoice FindSpecies(ContentId species)
        {
            for (int i = 0; i < _species.Length; i++)
                if (_species[i].Id == species)
                    return _species[i].Voice;
            return _catalog != null ? _catalog.familyFallback : null;
        }

        private bool StartBurst(AudioEventDefinition def, float gain, float pan)
        {
            if (def.clips == null || def.clips.Length == 0)
                return false;
            for (int b = 0; b < _bursts.Length; b++)
            {
                if (_bursts[b].Count > 0)
                    continue;
                _bursts[b] = new Burst
                {
                    Def = def,
                    Note = 0,
                    Count = Mathf.Min(def.burstCount, 8),
                    Next = _time + def.burstDelay,
                    Gain = gain,
                    Pan = pan,
                };
                return true;
            }
            Dropped++;
            return false;
        }

        private bool StartVoice(AudioEventDefinition def, AudioClip clip, float pitch, float volume, float pan)
        {
            int same = 0, oldestSame = -1, free = -1, victim = -1;
            for (int i = 0; i < _voices.Length; i++)
            {
                ref var v = ref _voices[i];
                if (v.End <= _time)
                {
                    if (free < 0)
                        free = i;
                    continue;
                }
                if (v.Id == def.id)
                {
                    same++;
                    if (oldestSame < 0 || v.Start < _voices[oldestSame].Start)
                        oldestSame = i;
                }
                if (v.Priority > def.priority
                    && (victim < 0 || v.Priority > _voices[victim].Priority
                        || (v.Priority == _voices[victim].Priority && v.Start < _voices[victim].Start)))
                    victim = i;
            }

            int slot;
            if (same >= def.maxVoices)
            {
                // Ambient sounds wait their turn; feedback restarts its oldest voice (spam taps stay responsive).
                if (def.priority >= 4 || oldestSame < 0)
                {
                    Dropped++;
                    return false;
                }
                slot = oldestSame;
                Stolen++;
            }
            else if (free >= 0)
                slot = free;
            else if (victim >= 0)
            {
                slot = victim;
                Stolen++;
            }
            else
            {
                Dropped++;
                return false;
            }

            var source = _voices[slot].Source;
            source.Stop();
            source.clip = clip;
            source.outputAudioMixerGroup = _catalog != null ? _catalog.Group(def.group) : null;
            source.pitch = pitch;
            source.volume = Mathf.Clamp01(volume);
            source.panStereo = pan;
            source.priority = 32 * def.priority;
            source.Play();
            _voices[slot] = new Voice
            {
                Source = source,
                Id = def.id,
                Priority = def.priority,
                Start = _time,
                End = _time + clip.length / Mathf.Max(pitch, 0.1f) + 0.02f,
            };
            Started++;
            int active = ActiveVoices;
            if (active > PeakVoices)
                PeakVoices = active;
            return true;
        }

        /// <summary>Level and pan of a world sound for the current view (no view: full level, centre).</summary>
        private float ViewGain(Vector3 position, out float pan)
        {
            pan = 0f;
            if (!_hasView)
                return 1f;
            var d = position - _focus;
            d.y = 0f;
            float distance = d.magnitude;
            float inner = _cameraDistance * 0.35f;
            float outer = _cameraDistance * 0.95f;
            float gain = distance <= inner ? 1f : distance >= outer ? 0f : 1f - (distance - inner) / (outer - inner);
            gain *= gain;
            // Zoomed out, the meadow is quieter; zoomed in on an animal, it is close.
            gain *= Mathf.Clamp(9.5f / _cameraDistance, 0.55f, 1f);
            pan = Mathf.Clamp(Vector3.Dot(d, _right) / (_cameraDistance * 0.45f), -1f, 1f) * 0.6f;
            return gain;
        }

        private void ApplyMixer(bool force)
        {
            var mixer = _catalog != null ? _catalog.mixer : null;
            if (mixer == null)
                return;
            float musicDb = ToDb(_musicLevel) + _duck;
            float sfxDb = ToDb(_sfxLevel);
            if (force || Mathf.Abs(musicDb - _appliedMusicDb) > 0.05f || float.IsNaN(_appliedMusicDb))
            {
                mixer.SetFloat(MusicParameter, musicDb);
                _appliedMusicDb = musicDb;
            }
            if (force || Mathf.Abs(sfxDb - _appliedSfxDb) > 0.05f || float.IsNaN(_appliedSfxDb))
            {
                mixer.SetFloat(SfxParameter, sfxDb);
                // The ambience follows the sounds setting: with the music off, the place still breathes.
                mixer.SetFloat(AmbienceParameter, sfxDb);
                _appliedSfxDb = sfxDb;
            }
        }

        private static float ToDb(float level) => level <= 0.0001f ? SilentDb : Mathf.Max(SilentDb, 20f * Mathf.Log10(level));

        private int PickVariant(int index, int count)
        {
            if (count <= 1)
                return 0;
            int last = _lastVariant[index];
            int pick;
            if (last < 0 || last >= count)
                pick = (int)(Next() % (uint)count);
            else
            {
                pick = (int)(Next() % (uint)(count - 1));
                if (pick >= last)
                    pick++;
            }
            _lastVariant[index] = pick;
            return pick;
        }

        private float Jitter(float fraction) => 1f + (Next01() * 2f - 1f) * fraction;

        private float Next01() => (Next() >> 8) * (1f / 16777216f);

        private uint Next()
        {
            uint x = _rng;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            _rng = x;
            return x;
        }

        private static void Configure(AudioSource source)
        {
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            source.reverbZoneMix = 0f;
            source.bypassReverbZones = true;
        }
    }
}
