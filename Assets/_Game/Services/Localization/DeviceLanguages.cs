#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using PocketWilds.Core;
using UnityEngine;
#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace PocketWilds.Services.Localization
{
    /// <summary>
    /// The device's preferred languages, most preferred first, as BCP 47 tags ("pt-BR", "zh-Hant-TW", "es-419"), for
    /// <see cref="LanguageMatcher"/> (Docs/Design/Localization_Plan.md). <see cref="UnityEngine.Application.systemLanguage"/>
    /// alone cannot tell pt-BR from pt-PT or es-ES from es-419, nor see a second language.
    /// <list type="bullet">
    /// <item>Android: the per-app language the player set in the system settings (Android 13+, LocaleManager), then the
    /// system list (LocaleManager, else LocaleList.getDefault through JNI; the project's minimum is API 26).</item>
    /// <item>iOS: NSLocale.preferredLanguages through Plugins/iOS/PWLanguages.mm. It starts with the per-app language the
    /// player set in the system settings (iOS 13+, needs CFBundleLocalizations in Info.plist).</item>
    /// <item>Editor and desktop: the OS UI culture, then <see cref="UnityEngine.Application.systemLanguage"/>.</item>
    /// </list>
    /// Read once on first use and cached: no allocation after the first call. <see cref="Refresh"/> asks again (for
    /// example when the app comes back to the front). Main thread only. Not wired yet: T39B.
    /// </summary>
    public static class DeviceLanguages
    {
        private static IReadOnlyList<string>? _cached;
        private static IReadOnlyList<string>? _override;

        /// <summary>Where the list came from: "android-localemanager", "android-localelist", "ios", "desktop", "systemlanguage" or "override".</summary>
        public static string Source { get; private set; } = string.Empty;

        /// <summary>The ordered tags, never null, possibly empty when the OS gives nothing usable.</summary>
        public static IReadOnlyList<string> Preferred
        {
            get
            {
                if (_override != null)
                    return _override;
                return _cached ??= Read();
            }
        }

        /// <summary>Forget the cached list: the next read asks the OS again (allocates once).</summary>
        public static void Refresh() => _cached = null;

        /// <summary>Pretend the device has these languages (tests, the pseudo-localization mode, a QA menu); null restores the OS.</summary>
        public static void SetOverride(IReadOnlyList<string>? tags)
        {
            _override = tags;
            if (tags != null)
                Source = "override";
        }

        /// <summary>
        /// Splits an OS list ("fr-FR,en-US" from LocaleList.toLanguageTags or the iOS plugin) into tags: trimmed, empty
        /// entries dropped, duplicates (any case) kept once, order kept.
        /// </summary>
        public static string[] Parse(string? joined)
        {
            if (string.IsNullOrWhiteSpace(joined))
                return Array.Empty<string>();
            string[] parts = joined!.Split(',');
            var tags = new List<string>(parts.Length);
            for (int i = 0; i < parts.Length; i++)
            {
                string tag = parts[i].Trim();
                if (tag.Length == 0)
                    continue;
                bool seen = false;
                for (int j = 0; j < tags.Count && !seen; j++)
                    seen = string.Equals(tags[j], tag, StringComparison.OrdinalIgnoreCase);
                if (!seen)
                    tags.Add(tag);
            }
            return tags.ToArray();
        }

        /// <summary>The tag of a Unity system language: the last resort when the OS list is empty.</summary>
        public static string FromSystemLanguage(SystemLanguage language)
        {
            switch (language)
            {
                case SystemLanguage.Afrikaans: return "af";
                case SystemLanguage.Arabic: return "ar";
                case SystemLanguage.Basque: return "eu";
                case SystemLanguage.Belarusian: return "be";
                case SystemLanguage.Bulgarian: return "bg";
                case SystemLanguage.Catalan: return "ca";
                case SystemLanguage.Chinese: return "zh";
                case SystemLanguage.ChineseSimplified: return "zh-Hans";
                case SystemLanguage.ChineseTraditional: return "zh-Hant";
                case SystemLanguage.Czech: return "cs";
                case SystemLanguage.Danish: return "da";
                case SystemLanguage.Dutch: return "nl";
                case SystemLanguage.English: return "en";
                case SystemLanguage.Estonian: return "et";
                case SystemLanguage.Faroese: return "fo";
                case SystemLanguage.Finnish: return "fi";
                case SystemLanguage.French: return "fr";
                case SystemLanguage.German: return "de";
                case SystemLanguage.Greek: return "el";
                case SystemLanguage.Hebrew: return "he";
                case SystemLanguage.Hindi: return "hi";
                case SystemLanguage.Hungarian: return "hu";
                case SystemLanguage.Icelandic: return "is";
                case SystemLanguage.Indonesian: return "id";
                case SystemLanguage.Italian: return "it";
                case SystemLanguage.Japanese: return "ja";
                case SystemLanguage.Korean: return "ko";
                case SystemLanguage.Latvian: return "lv";
                case SystemLanguage.Lithuanian: return "lt";
                case SystemLanguage.Norwegian: return "nb";
                case SystemLanguage.Polish: return "pl";
                case SystemLanguage.Portuguese: return "pt";
                case SystemLanguage.Romanian: return "ro";
                case SystemLanguage.Russian: return "ru";
                case SystemLanguage.SerboCroatian: return "sh";
                case SystemLanguage.Slovak: return "sk";
                case SystemLanguage.Slovenian: return "sl";
                case SystemLanguage.Spanish: return "es";
                case SystemLanguage.Swedish: return "sv";
                case SystemLanguage.Thai: return "th";
                case SystemLanguage.Turkish: return "tr";
                case SystemLanguage.Ukrainian: return "uk";
                case SystemLanguage.Vietnamese: return "vi";
                default: return string.Empty;
            }
        }

        private static IReadOnlyList<string> Read()
        {
            string[] tags;
            try
            {
                tags = Parse(ReadPlatform());
            }
            catch (Exception e)
            {
                GameLog.Warning(LogCategory.Boot, "Device languages unreadable: " + e.Message);
                tags = Array.Empty<string>();
            }
            if (tags.Length == 0)
            {
                string fromUnity = FromSystemLanguage(Application.systemLanguage);
                tags = fromUnity.Length > 0 ? new[] { fromUnity } : Array.Empty<string>();
                Source = "systemlanguage";
            }
            return new ReadOnlyCollection<string>(tags);
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static string ReadPlatform()
        {
            int sdk;
            using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                sdk = version.GetStatic<int>("SDK_INT");
            string app = string.Empty, system = string.Empty;
            if (sdk >= 33)
            {
                // Context.LOCALE_SERVICE is "locale". Application locales are empty unless the player chose one for this app.
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var manager = activity.Call<AndroidJavaObject>("getSystemService", "locale"))
                {
                    if (manager != null)
                    {
                        app = Tags(manager.Call<AndroidJavaObject>("getApplicationLocales"));
                        system = Tags(manager.Call<AndroidJavaObject>("getSystemLocales"));
                    }
                }
                Source = "android-localemanager";
            }
            if (system.Length == 0)
            {
                using (var localeList = new AndroidJavaClass("android.os.LocaleList"))
                    system = Tags(localeList.CallStatic<AndroidJavaObject>("getDefault"));
                Source = "android-localelist";
            }
            return app.Length == 0 ? system : app + "," + system;
        }

        /// <summary>LocaleList.toLanguageTags(): "fr-FR,en-US". Disposes the Java list.</summary>
        private static string Tags(AndroidJavaObject? list)
        {
            if (list == null)
                return string.Empty;
            using (list)
                return list.Call<string>("toLanguageTags") ?? string.Empty;
        }
#elif UNITY_IOS && !UNITY_EDITOR
        /// <summary>Plugins/iOS/PWLanguages.mm: preferredLanguages joined with ",", malloc'd (the marshaller frees it).</summary>
        [DllImport("__Internal")]
        private static extern string PWLanguages_Preferred();

        private static string ReadPlatform()
        {
            Source = "ios";
            return PWLanguages_Preferred() ?? string.Empty;
        }
#else
        private static string ReadPlatform()
        {
            Source = "desktop";
            string ui = CultureInfo.CurrentUICulture.Name;
            string system = FromSystemLanguage(Application.systemLanguage);
            if (ui.Length == 0)
                return system;
            return system.Length == 0 ? ui : ui + "," + system;
        }
#endif
    }
}
