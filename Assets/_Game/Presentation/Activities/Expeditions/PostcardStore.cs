#nullable enable
using System;
using System.IO;
using PocketWilds.Simulation;
using UnityEngine;

namespace PocketWilds.Presentation
{
    /// <summary>
    /// T42: the postcards' pictures on disk (doc 01 Contenu, section 7.1): <c>Postcards/postcard_{id}.png</c> under the
    /// persistent data path (<see cref="PostcardLayout"/>). The renderer writes them off the main thread (a temporary file
    /// moved into place: a picture is whole or absent, never half written); <see cref="Prune(IExpeditions)"/> deletes the
    /// pictures the expeditions no longer keep (the first of each destination for ever, then the most recent). Paths are
    /// built from a root read once on the main thread; <see cref="Write"/> and <see cref="Prune(int[], int)"/> may run on
    /// any thread.
    /// </summary>
    public sealed class PostcardStore
    {
        private const string Prefix = "postcard_";
        private const string Extension = ".png";

        /// <summary>The persistent data path (read now, on the main thread).</summary>
        public PostcardStore() : this(Application.persistentDataPath)
        {
        }

        /// <summary>Tests: any root folder; the postcards live in its <see cref="PostcardLayout.Folder"/>.</summary>
        public PostcardStore(string root)
        {
            Folder = Path.Combine(root ?? throw new ArgumentNullException(nameof(root)), PostcardLayout.Folder);
        }

        public string Folder { get; }

        public string PathOf(int postcardId) => Path.Combine(Folder, PostcardLayout.FileName(postcardId));

        public bool Exists(int postcardId) => File.Exists(PathOf(postcardId));

        /// <summary>The id of a postcard file name (« postcard_12.png » → 12); false for any other file.</summary>
        public static bool TryParseId(string fileName, out int postcardId)
        {
            postcardId = 0;
            if (string.IsNullOrEmpty(fileName))
                return false;
            string name = Path.GetFileName(fileName);
            if (!name.StartsWith(Prefix, StringComparison.Ordinal) || !name.EndsWith(Extension, StringComparison.OrdinalIgnoreCase))
                return false;
            int length = name.Length - Prefix.Length - Extension.Length;
            if (length <= 0 || length > 10)
                return false;
            long value = 0;
            for (int i = 0; i < length; i++)
            {
                char c = name[Prefix.Length + i];
                if (c < '0' || c > '9')
                    return false;
                value = value * 10 + (c - '0');
            }
            if (value > int.MaxValue || PostcardLayout.FileName((int)value) != name)
                return false;
            postcardId = (int)value;
            return true;
        }

        /// <summary>Writes a picture (any thread): a temporary file, then moved over the postcard's name.</summary>
        public void Write(int postcardId, byte[] png)
        {
            if (png == null || png.Length == 0)
                throw new ArgumentException("No picture to write.", nameof(png));
            Directory.CreateDirectory(Folder);
            string path = PathOf(postcardId);
            string temporary = path + ".tmp";
            File.WriteAllBytes(temporary, png);
            if (File.Exists(path))
                File.Delete(path);
            File.Move(temporary, path);
        }

        /// <summary>Reads a stored picture into a texture (main thread; the refuge journal, T44). False when it is not there.</summary>
        public bool TryLoad(int postcardId, Texture2D into)
        {
            string path = PathOf(postcardId);
            if (into == null || !File.Exists(path))
                return false;
            return into.LoadImage(File.ReadAllBytes(path), false);
        }

        /// <summary>
        /// Deletes the postcard pictures the expeditions no longer keep (<see cref="IExpeditions.PostcardAt"/>), and any
        /// half-written temporary file. Other files are left alone. Returns how many pictures went. Main thread (it reads the
        /// expeditions); a few file names, once after a welcome.
        /// </summary>
        public int Prune(IExpeditions expeditions)
        {
            if (expeditions == null)
                return 0;
            int count = Math.Max(0, expeditions.PostcardCount);
            var kept = new int[count];
            for (int i = 0; i < count; i++)
                kept[i] = expeditions.PostcardAt(i);
            return Prune(kept, count);
        }

        /// <summary>The same with the kept ids already read (any thread).</summary>
        public int Prune(int[] kept, int count)
        {
            if (!Directory.Exists(Folder))
                return 0;
            int deleted = 0;
            foreach (string path in Directory.GetFiles(Folder))
            {
                string name = Path.GetFileName(path);
                if (name.StartsWith(Prefix, StringComparison.Ordinal) && name.EndsWith(Extension + ".tmp", StringComparison.OrdinalIgnoreCase))
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
