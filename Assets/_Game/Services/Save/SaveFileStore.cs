#nullable enable
using System;
using System.Collections.Generic;
using System.IO;

namespace PocketWilds.Services.Save
{
    public enum SaveSlotKind
    {
        Current,
        Temp,
        Previous,
    }

    public sealed class SaveFileCandidate
    {
        public SaveFileCandidate(SaveSlotKind kind, string path, byte[] data)
        {
            Kind = kind;
            Path = path;
            Data = data;
        }

        public SaveSlotKind Kind { get; }
        public string Path { get; }
        public byte[] Data { get; }
    }

    /// <summary>
    /// Atomic file writes for one profile slot:
    /// 1. write slot.tmp and flush to disk, 2. read it back and verify the checksum,
    /// 3. rotate slot.pwsave to slot.prev, 4. rename slot.tmp to slot.pwsave.
    /// A crash at any step leaves at least one valid file; load picks the highest valid revision.
    /// File IO only, safe to call from a worker thread.
    /// </summary>
    public sealed class SaveFileStore
    {
        public const string Extension = ".pwsave";

        public SaveFileStore(string directory, string slotName = "profile0")
        {
            Directory = directory;
            CurrentPath = Path.Combine(directory, slotName + Extension);
            PreviousPath = Path.Combine(directory, slotName + ".prev" + Extension);
            TempPath = Path.Combine(directory, slotName + ".tmp" + Extension);
        }

        public string Directory { get; }
        public string CurrentPath { get; }
        public string PreviousPath { get; }
        public string TempPath { get; }

        public void WriteAtomic(byte[] data, int length)
        {
            System.IO.Directory.CreateDirectory(Directory);

            // One Write call with the whole snapshot: no FileStream buffer (bufferSize 1), nothing to copy.
            using (var stream = new FileStream(TempPath, FileMode.Create, FileAccess.Write, FileShare.None, 1, FileOptions.WriteThrough))
            {
                stream.Write(data, 0, length);
                stream.Flush(true);
            }

            // Read back into a reused buffer: verifying a save does not allocate its size every time (T22).
            int read = ReadIntoVerifyBuffer(TempPath);
            if (!SaveFileFormat.TryValidate(_verify, read, out _, out var error))
                throw new IOException("Written save failed verification: " + error);

            if (File.Exists(CurrentPath))
            {
                if (File.Exists(PreviousPath))
                    File.Delete(PreviousPath);
                File.Move(CurrentPath, PreviousPath);
            }

            File.Move(TempPath, CurrentPath);
        }

        private byte[] _verify = new byte[64 * 1024];

        /// <summary>Reads a whole file into the verification buffer (grown when needed). Not thread-safe: one writer at a time.</summary>
        private int ReadIntoVerifyBuffer(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1, FileOptions.None))
            {
                long size = stream.Length;
                if (size > int.MaxValue)
                    throw new IOException("Save file too large.");
                if (_verify.Length < size)
                    _verify = new byte[Math.Max((int)size, _verify.Length * 2)];
                int total = 0;
                while (total < size)
                {
                    int n = stream.Read(_verify, total, (int)size - total);
                    if (n <= 0)
                        break;
                    total += n;
                }
                return total;
            }
        }

        /// <summary>Every slot file that exists, newest kinds first. Content is not validated here.</summary>
        public List<SaveFileCandidate> ReadCandidates()
        {
            var result = new List<SaveFileCandidate>(3);
            TryAdd(result, SaveSlotKind.Current, CurrentPath);
            TryAdd(result, SaveSlotKind.Temp, TempPath);
            TryAdd(result, SaveSlotKind.Previous, PreviousPath);
            return result;
        }

        /// <summary>Moves a damaged file aside so it can never replace a good backup. It is kept for diagnostics.</summary>
        public string Quarantine(string path)
        {
            string target = path + ".corrupt-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
            if (File.Exists(path))
                File.Move(path, target);
            return target;
        }

        public void DeleteTemp()
        {
            if (File.Exists(TempPath))
                File.Delete(TempPath);
        }

        private static void TryAdd(List<SaveFileCandidate> list, SaveSlotKind kind, string path)
        {
            if (!File.Exists(path))
                return;
            try
            {
                list.Add(new SaveFileCandidate(kind, path, File.ReadAllBytes(path)));
            }
            catch (IOException)
            {
                // Unreadable file is treated as missing; other candidates may still load.
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
