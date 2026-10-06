using System;
using PocketWilds.Core;
using UnityEngine;
using UnityEngine.Audio;

namespace PocketWilds.Services.Feedback
{
    /// <summary>Mixer groups (doc 08). Values index the catalog's group table: append only.</summary>
    public enum AudioGroup : byte
    {
        Music = 0,
        Ambience = 1,
        Animals = 2,
        UI = 3,
        Rewards = 4,
        World = 5,
    }

    /// <summary>
    /// One named sound (doc 08 AUDIO EVENT DATA): variants, level and pitch ranges, cooldown, voice cap, group,
    /// priority. Gameplay only knows the AudioEventId.
    /// </summary>
    [Serializable]
    public sealed class AudioEventDefinition
    {
        public AudioEventId id;
        [Tooltip("Variants, picked at random (never the same twice in a row).")]
        public AudioClip[] clips = Array.Empty<AudioClip>();
        public AudioGroup group = AudioGroup.UI;
        [Range(0f, 1f)] public float volume = 1f;
        [Tooltip("Random level change, +/- fraction.")]
        [Range(0f, 0.5f)] public float volumeJitter = 0.05f;
        [Tooltip("Random pitch change, +/- fraction (0.03 is about half a semitone).")]
        [Range(0f, 0.2f)] public float pitchJitter = 0.02f;
        [Tooltip("Minimum seconds between two starts of this sound.")]
        [Min(0f)] public float cooldown = 0.05f;
        [Tooltip("Simultaneous voices of this sound.")]
        [Min(1)] public int maxVoices = 2;
        [Tooltip("1 purchase and critical UI, 2 hatch and rewards, 3 focused animal, 4 animals, 5 ambient one-shots.")]
        [Range(1, 5)] public int priority = 2;
        [Tooltip("Placed in the world: fades with the distance to the view and pans with its side.")]
        public bool world;
        [Tooltip("Skipped in battery saver mode (ambient one-shots only).")]
        public bool skipOnBatterySaver;
        [Tooltip("Lowers the music a little while it plays (reveals).")]
        public bool duckMusic;

        [Header("Burst (collect: a bounded run of rising notes, never one sound per mote)")]
        [Min(0)] public int burstCount;
        [Min(0f)] public float burstDelay;
        [Min(0f)] public float burstInterval = 0.1f;
        [Tooltip("Pitch of each note of the run, in semitones above the clip.")]
        public int[] burstSemitones = Array.Empty<int>();
    }

    /// <summary>Voice of a species: its own clips (signature) or its family bank, and its pitch (doc 08 SpeciesAudioProfile).</summary>
    [Serializable]
    public sealed class SpeciesVoice
    {
        [Tooltip("Species content id source, for example species.rabbit.")]
        public string species = string.Empty;
        public AudioClip[] happy = Array.Empty<AudioClip>();
        public AudioClip[] sleep = Array.Empty<AudioClip>();
        [Tooltip("T34B2: nibbles while eating.")]
        public AudioClip[] eat = Array.Empty<AudioClip>();
        [Range(0.5f, 2f)] public float pitch = 1f;
    }

    /// <summary>
    /// DEF_AudioEvents (M-DEF-AUDIOEVENT): every sound of the game, the mixer, the voice pool size. Built by
    /// Pocket Wilds/Audio/Build Audio (AudioPipeline); clips come from SourceArt/Audio/Tools.
    /// </summary>
    [CreateAssetMenu(menuName = "Pocket Wilds/Audio/Audio Catalog", fileName = "DEF_AudioEvents")]
    public sealed class AudioCatalog : ScriptableObject
    {
        public AudioMixer mixer;
        [Tooltip("Mixer group per AudioGroup value (Music, Ambience, Animals, UI, Rewards, World).")]
        public AudioMixerGroup[] groups = Array.Empty<AudioMixerGroup>();
        [Tooltip("One-shot voices (pooled AudioSources). Doc 08: bounded, the lowest priority is dropped when full.")]
        [Range(4, 32)] public int voices = 12;
        public AudioEventDefinition[] events = Array.Empty<AudioEventDefinition>();
        public SpeciesVoice[] species = Array.Empty<SpeciesVoice>();
        [Tooltip("Used for a species without its own entry (small quadruped family bank).")]
        public SpeciesVoice familyFallback = new SpeciesVoice();

        public AudioEventDefinition Find(AudioEventId id)
        {
            for (int i = 0; i < events.Length; i++)
                if (events[i] != null && events[i].id == id)
                    return events[i];
            return null;
        }

        public SpeciesVoice FindSpecies(ContentId species)
        {
            for (int i = 0; i < this.species.Length; i++)
            {
                var voice = this.species[i];
                if (voice != null && !string.IsNullOrEmpty(voice.species) && ContentId.FromSource(voice.species) == species)
                    return voice;
            }
            return familyFallback;
        }

        public AudioMixerGroup Group(AudioGroup group) => (int)group < groups.Length ? groups[(int)group] : null;
    }
}
