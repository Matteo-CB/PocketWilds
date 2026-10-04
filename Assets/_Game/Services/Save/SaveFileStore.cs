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

            using (var stream = new FileStream(TempPath, FileMode.Create, FileAccess.Write, FileShare.None, 64 * 1024, FileOptions.WriteThrough))
            {
                stream.Write(data, 0, length);
                stream.Flush(true);
            }

            var check = File.ReadAllBytes(TempPath);
            if (!SaveFileFormat.TryValidate(check, check.Length, out _, out var error))
                throw new IOException("Written save failed verification: " + error);

            if (File.Exists(CurrentPath))
            {
                if (File.Exists(PreviousPath))
                    File.Delete(PreviousPath);
                File.Move(CurrentPath, PreviousPath);
            }

            File.Move(TempPath, CurrentPath);
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
