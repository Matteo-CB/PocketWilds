using UnityEngine;

namespace PocketWilds.Presentation
{
    /// <summary>
    /// T49B everything the refuge scene needs to show one biome (Biome Wizard, ED-004): its chunk set, the four lighting moods,
    /// the weather and ambient life kits, and the Addressables keys of its zone kit, audio beds and basic egg. One per biome
    /// (DEF_World_Grassland, DEF_World_Forest), Addressable under « world.{key} » in the biome's kit group with the label
    /// biome_{key}, so a biome's assets load only with it. The vertical slice still wires the Grassland's fields directly;
    /// the biome switch (T50) reads these profiles.
    /// </summary>
    [CreateAssetMenu(menuName = "Pocket Wilds/World/Biome World Profile", fileName = "DEF_World_New")]
    public sealed class BiomeWorldProfile : ScriptableObject
    {
        [Tooltip("Biome source id (biome.forest).")]
        public string biomeId = string.Empty;

        [Tooltip("Short key (forest): chunk.{key}.*, zones.{key}, audio.{key}.")]
        public string key = string.Empty;

        public WorldChunkSet chunkSet;

        [Header("Lighting moods")]
        public LightingProfile morning;
        public LightingProfile day;
        public LightingProfile evening;
        public LightingProfile night;

        [Header("Kits")]
        public WeatherKit weatherKit;
        public AmbientLifeKit ambientLife;

        [Header("Addressables keys")]
        [Tooltip("Zone kit (hedges, signs, gate): zones.{key}.")]
        public string zoneKitKey = string.Empty;

        [Tooltip("Biome audio beds (BiomeAudioSet): audio.{key}.")]
        public string audioKey = string.Empty;

        [Tooltip("The biome's basic egg (egg.{key}.basic).")]
        public string basicEggId = string.Empty;

        /// <summary>The profile's own address: world.{key}.</summary>
        public static string AddressOf(string key) => "world." + key;
    }
}
