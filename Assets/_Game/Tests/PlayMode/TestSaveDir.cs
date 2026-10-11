using System;
using System.Collections.Generic;
using System.IO;

namespace PocketWilds.Tests.PlayMode
{
    /// <summary>
    /// A finished test's save folder is deleted one test later: the destroyed bootstrap still flushes its save at the end
    /// of the frame, and deleting the folder under that write failed a test at random (« Save write failed »).
    /// </summary>
    internal static class TestSaveDir
    {
        private static readonly List<string> Pending = new List<string>();

        public static void Release(string path)
        {
            for (int i = Pending.Count - 1; i >= 0; i--)
            {
                // A folder the next test reuses by name stays until a later test.
                if (string.Equals(Pending[i], path, StringComparison.OrdinalIgnoreCase))
                    continue;
                try
                {
                    if (Directory.Exists(Pending[i]))
                        Directory.Delete(Pending[i], true);
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
                Pending.RemoveAt(i);
            }
            if (!string.IsNullOrEmpty(path) && !Pending.Contains(path))
                Pending.Add(path);
        }
    }
}
