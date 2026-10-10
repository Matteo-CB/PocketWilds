using System;
using UnityEngine;

namespace PocketWilds.Presentation
{
    /// <summary>
    /// T44A: the captured sticker art (doc « 01 Albums et autocollants », section 1.11), filled by the sticker capture pipeline:
    /// one entry per captured sticker with its 256 × 256 game sprite, which lives in its page's atlas (one Addressables entry
    /// per page, loaded with the page and its neighbours, released after). The keys listed here are the stickers « available
    /// in this build »: the simulation never draws a sticker whose art is missing.
    /// </summary>
    [CreateAssetMenu(menuName = "Pocket Wilds/Sticker Art Catalog")]
    public sealed class StickerArtCatalog : ScriptableObject
    {
        public const string AssetPath = "Assets/_Game/Content/Albums/DEF_StickerArt.asset";

        /// <summary>The Addressables group of the albums screen and the page atlases.</summary>
        public const string Group = "Albums";

        [Serializable]
        public struct Entry
        {
            [Tooltip("sticker.gl.01")]
            public string stickerKey;

            [Tooltip("The page's atlas address (albums.page.grassland.1), loaded when the page shows.")]
            public string pageAtlasAddress;

            [Tooltip("The sprite's name inside the page atlas (STK-GL-01).")]
            public string spriteName;
        }

        public Entry[] entries = Array.Empty<Entry>();

        public bool TryGet(string stickerKey, out Entry entry)
        {
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].stickerKey == stickerKey)
                {
                    entry = entries[i];
                    return true;
                }
            }
            entry = default;
            return false;
        }
    }
}
