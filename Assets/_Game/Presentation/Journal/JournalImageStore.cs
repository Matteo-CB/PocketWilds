using System;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace PocketWilds.Presentation
{
    /// <summary>
    /// T44C: the journal's captures on disk (doc section 6 « Images »): Journal/journal_{id}.jpg under the persistent data path,
    /// written whole (a temporary file moved over the name), read back into pooled textures by the journal screen, and the
    /// pictures of pages no longer captured (the oldest repeatable ones over the 300 kept) deleted off the main thread.
    /// </summary>
    public sealed class JournalImageStore
    {
        public JournalImageStore() : this(Application.persistentDataPath)
        {
        }

        public JournalImageStore(string root)
        {
            Folder = Path.Combine(root ?? throw new ArgumentNullException(nameof(root)), JournalLayout.Folder);
        }

        public string Folder { get; }

        public string PathOf(int pageId) => Path.Combine(Folder, JournalLayout.FileName(pageId));

        public bool Exists(int pageId) => File.Exists(PathOf(pageId));

        /// <summary>journal_12.jpg → 12.</summary>
        public static bool TryParseId(string fileName, out int pageId)
        {
            pageId = 0;
            if (string.IsNullOrEmpty(fileName) || !fileName.StartsWith(JournalLayout.Prefix, StringComparison.Ordinal)
                || !fileName.EndsWith(JournalLayout.Extension, StringComparison.OrdinalIgnoreCase))
                return false;
            int start = JournalLayout.Prefix.Length;
            int length = fileName.Length - start - JournalLayout.Extension.Length;
            return length > 0 && int.TryParse(fileName.Substring(start, length), NumberStyles.None, CultureInfo.InvariantCulture, out pageId);
        }

        /// <summary>Writes a picture (any thread): a temporary file, then moved over the page's name.</summary>
        public void Write(int pageId, byte[] jpg)
        {
            if (jpg == null || jpg.Length == 0)
                throw new ArgumentException("No picture to write.", nameof(jpg));
            Directory.CreateDirectory(Folder);
            string path = PathOf(pageId);
            string temporary = path + ".tmp";
            File.WriteAllBytes(temporary, jpg);
            if (File.Exists(path))
                File.Delete(path);
            File.Move(temporary, path);
        }

        /// <summary>The picture's bytes (any thread), or null when it is not there.</summary>
        public byte[] Read(int pageId)
        {
            string path = PathOf(pageId);
            return File.Exists(path) ? File.ReadAllBytes(path) : null;
        }

        /// <summary>Deletes the pictures whose page is not in <paramref name="kept"/>, and any half-written file (any thread).</summary>
        public int Prune(int[] kept, int count)
        {
            if (!Directory.Exists(Folder))
                return 0;
            int deleted = 0;
            foreach (string path in Directory.GetFiles(Folder))
            {
                string name = Path.GetFileName(path);
                if (name.StartsWith(JournalLayout.Prefix, StringComparison.Ordinal) && name.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase))
                {
                    TryDelete(path);
                    continue;
                }
                if (!TryParseId(name, out int id) || Keeps(kept, count, id))
                    continue;
                if (TryDelete(path))
                    deleted++;
            }
            return deleted;
        }

        private static bool Keeps(int[] kept, int count, int id)
        {
            for (int i = 0; i < count && kept != null && i < kept.Length; i++)
                if (kept[i] == id)
                    return true;
            return false;
        }

        private static bool TryDelete(string path)
        {
            try
            {
                File.Delete(path);
                return true;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }
    }
}
