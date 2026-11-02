using System.IO.Compression;
using System.Text.Json;
using Derby.Pokes;

namespace Derby
{
    internal sealed class QuickStateArchiveStore
    {
        private const int ArchiveVersion = 1;
        private readonly string path;

        public QuickStateArchiveStore(string? path = null)
        {
            string localData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            this.path = path ?? System.IO.Path.Combine(localData, "Derby", "quick-states.dat");
        }

        public string Path => path;

        public QuickStateSlot?[] Load(
            int slotCount,
            Func<string, SpectrumRomSet?> resolveRomSet)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(slotCount);
            ArgumentNullException.ThrowIfNull(resolveRomSet);
            var result = new QuickStateSlot?[slotCount];
            if (!File.Exists(path))
                return result;

            try
            {
                using FileStream file = File.OpenRead(path);
                using var gzip = new GZipStream(file, CompressionMode.Decompress);
                ArchiveDocument? document = JsonSerializer.Deserialize<ArchiveDocument>(gzip);
                if (document == null || document.Version != ArchiveVersion || document.Slots == null)
                    return result;

                foreach (ArchiveSlot saved in document.Slots)
                {
                    if ((uint)saved.Index >= (uint)slotCount || result[saved.Index] != null)
                        continue;
                    SpectrumRomSet? romSet = resolveRomSet(saved.RomSetName ?? string.Empty);
                    if (romSet == null || saved.PersistentState == null || saved.ThumbnailPng == null)
                        continue;

                    using var source = new MemoryStream(saved.ThumbnailPng, writable: false);
                    using var decoded = new Bitmap(source);
                    var thumbnail = new Bitmap(decoded);
                    PokeMediaIdentity? identity =
                        string.IsNullOrWhiteSpace(saved.MediaFileName) || string.IsNullOrWhiteSpace(saved.MediaSha256)
                            ? null
                            : new PokeMediaIdentity(saved.MediaFileName, saved.MediaSha256);
                    result[saved.Index] = QuickStateSlot.FromPersistent(
                        saved.PersistentState,
                        romSet,
                        saved.MediaDisplayName,
                        identity,
                        thumbnail,
                        saved.CapturedAtUtc);
                }
            }
            catch
            {
                foreach (QuickStateSlot? slot in result)
                    slot?.Dispose();
                return new QuickStateSlot?[slotCount];
            }

            return result;
        }

        public void Save(IReadOnlyList<QuickStateSlot?> slots)
        {
            ArgumentNullException.ThrowIfNull(slots);
            var document = new ArchiveDocument { Version = ArchiveVersion };
            for (int index = 0; index < slots.Count; index++)
            {
                QuickStateSlot? slot = slots[index];
                if (slot == null)
                    continue;
                using var thumbnail = new MemoryStream();
                slot.Thumbnail.Save(thumbnail, System.Drawing.Imaging.ImageFormat.Png);
                document.Slots.Add(new ArchiveSlot
                {
                    Index = index,
                    RomSetName = slot.RomSet.Name,
                    MediaDisplayName = slot.MediaDisplayName,
                    MediaFileName = slot.MediaIdentity?.FileName,
                    MediaSha256 = slot.MediaIdentity?.Sha256,
                    CapturedAtUtc = slot.CapturedAtUtc,
                    PersistentState = slot.PersistentState,
                    ThumbnailPng = thumbnail.ToArray()
                });
            }

            string? directory = System.IO.Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);
            string temporaryPath = path + ".tmp";
            try
            {
                using (FileStream file = File.Create(temporaryPath))
                using (var gzip = new GZipStream(file, CompressionLevel.SmallestSize))
                    JsonSerializer.Serialize(gzip, document);
                File.Move(temporaryPath, path, overwrite: true);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
        }

        private sealed class ArchiveDocument
        {
            public int Version { get; set; }
            public List<ArchiveSlot> Slots { get; set; } = [];
        }

        private sealed class ArchiveSlot
        {
            public int Index { get; set; }
            public string? RomSetName { get; set; }
            public string? MediaDisplayName { get; set; }
            public string? MediaFileName { get; set; }
            public string? MediaSha256 { get; set; }
            public DateTime CapturedAtUtc { get; set; }
            public byte[]? PersistentState { get; set; }
            public byte[]? ThumbnailPng { get; set; }
        }
    }
}
