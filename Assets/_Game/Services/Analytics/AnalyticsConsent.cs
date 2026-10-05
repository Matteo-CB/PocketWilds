#nullable enable
using System;
using UnityEngine;

namespace PocketWilds.Services.Analytics
{
    /// <summary>Player choice about analytics. Stored on the phone, never in the profile save.</summary>
    public enum AnalyticsConsent : byte
    {
        /// <summary>Not asked yet: events are kept on the phone, never sent.</summary>
        Unknown = 0,
        Granted = 1,

        /// <summary>Nothing is collected; what was kept is deleted and the analytics id is reset.</summary>
        Denied = 2,
    }

    /// <summary>
    /// PlayerPrefs storage for the consent and the pseudonymous analytics id: a random id made on this phone, not
    /// derived from the device or the profile, reset when the player refuses analytics. Main thread only.
    /// </summary>
    public static class AnalyticsDeviceStore
    {
        private const string ConsentKey = "pw.analytics.consent";
        private const string IdKey = "pw.analytics.id";

        public static AnalyticsConsent LoadConsent()
        {
            int value = PlayerPrefs.GetInt(ConsentKey, 0);
            return value == 1 || value == 2 ? (AnalyticsConsent)value : AnalyticsConsent.Unknown;
        }

        public static void SaveConsent(AnalyticsConsent consent)
        {
            PlayerPrefs.SetInt(ConsentKey, (int)consent);
            PlayerPrefs.Save();
        }

        public static string LoadOrCreateId()
        {
            string id = PlayerPrefs.GetString(IdKey, string.Empty);
            if (id.Length == 32)
                return id;
            id = Guid.NewGuid().ToString("N");
            PlayerPrefs.SetString(IdKey, id);
            PlayerPrefs.Save();
            return id;
        }

        public static void ResetId()
        {
            PlayerPrefs.DeleteKey(IdKey);
            PlayerPrefs.Save();
        }
    }
}
