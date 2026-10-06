using UnityEngine;

namespace PocketWilds.Services.Feedback
{
    /// <summary>
    /// The beds of one place (J-AMB, J-MUS): its ambience loop and its music loop, both streamed. Loaded with the
    /// biome through Addressables (group Audio_Grassland, address audio.grassland) and released with it.
    /// </summary>
    [CreateAssetMenu(menuName = "Pocket Wilds/Audio/Biome Audio Set", fileName = "DEF_BiomeAudio")]
    public sealed class BiomeAudioSet : ScriptableObject
    {
        public AudioClip music;
        [Tooltip("Day ambience (birds).")]
        public AudioClip ambience;
        [Tooltip("T34B2: evening and night layer (crickets), crossfaded by the local hour and the night mood.")]
        public AudioClip ambienceEvening;
        [Tooltip("T34B2: light rain layer, mixed by the shower strength.")]
        public AudioClip rain;
        [Tooltip("Cross-fade when the place changes (doc 08: 1 to 4 s).")]
        [Range(1f, 4f)] public float fadeSeconds = 2.5f;
    }
}
