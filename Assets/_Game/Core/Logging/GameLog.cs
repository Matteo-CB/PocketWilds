#nullable enable
using System.Diagnostics;

namespace PocketWilds.Core
{
    public enum LogCategory
    {
        Boot,
        Content,
        Save,
        Economy,
        Hatch,
        Presentation,
        Pool,
        UI,
        Audio,
        Network,
        Benchmark,
    }

    /// <summary>
    /// Categorized logging. Info and Warning calls are compiled out of release players,
    /// including their string arguments. Never log per animal or per frame.
    /// </summary>
    public static class GameLog
    {
        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void Info(LogCategory category, string message)
        {
            UnityEngine.Debug.Log($"[{category}] {message}");
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void Warning(LogCategory category, string message)
        {
            UnityEngine.Debug.LogWarning($"[{category}] {message}");
        }

        /// <summary>Errors stay in release builds. Keep context minimal, never dump a save.</summary>
        public static void Error(LogCategory category, string message)
        {
            UnityEngine.Debug.LogError($"[{category}] {message}");
        }
    }
}
