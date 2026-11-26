using System.Text.Json;
using Derby.Tape;

namespace Derby;

internal sealed class TapeBookmarkStore(string? directory = null)
{
    private const int Version = 1;
    private const int MaxFileBytes = 1024 * 1024;
    private readonly string directory = directory ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Derby", "tape-bookmarks");
    private readonly Dictionary<string, Bookmark[]> loaded = new();

    internal sealed record Bookmark(Guid Id, string Name, TapeCue Cue);
    private sealed record SavedBookmark(Guid Id, string Name, JsonElement Position);
    private sealed record Document(int Version, SavedBookmark[] Bookmarks);

    public IReadOnlyList<Bookmark> For(TapeTimeline timeline)
    {
        if (!loaded.TryGetValue(timeline.Identity, out Bookmark[]? entries))
        {
            try { entries = Read(timeline); }
            catch (Exception ex) when (IsStorageError(ex)) { entries = []; }
            loaded.Add(timeline.Identity, entries);
        }
        return entries;
    }

    public bool TryChange(TapeTimeline timeline, Bookmark? previous, Bookmark? replacement)
    {
        try
        {
            // Pick up completed edits made since the list was displayed.
            var entries = Read(timeline).ToList();
            if (previous != null)
            {
                int index = entries.FindIndex(entry => entry.Id == previous.Id);
                if (index < 0)
                    return false;
                if (replacement != null)
                    entries[index] = replacement;
                else
                    entries.RemoveAt(index);
            }
            else if (replacement != null)
                entries.Add(replacement);
            else
                return false;

            Write(timeline, entries);
            loaded[timeline.Identity] = entries.ToArray();
            return true;
        }
        catch (Exception ex) when (IsStorageError(ex)) { return false; }
    }

    private Bookmark[] Read(TapeTimeline timeline)
    {
        try
        {
            using FileStream file = File.OpenRead(GetPath(timeline));
            if (file.Length > MaxFileBytes)
                throw new InvalidDataException("Bookmark file is too large.");
            Document? document = JsonSerializer.Deserialize<Document>(file);
            if (document is not { Version: Version, Bookmarks: not null })
                throw new InvalidDataException("Unsupported bookmark file.");
            var entries = new List<Bookmark>();
            var ids = new HashSet<Guid>();
            foreach (SavedBookmark entry in document.Bookmarks)
            {
                if (entry is not { Name.Length: > 0 and <= 60 } || string.IsNullOrWhiteSpace(entry.Name) ||
                    entry.Id == Guid.Empty || !ids.Add(entry.Id) ||
                    TapeCue.ReadBookmarkPosition(entry.Position, timeline) is not TapeCue cue)
                    throw new InvalidDataException("Invalid tape bookmark.");
                entries.Add(new(entry.Id, entry.Name, cue));
            }
            return entries.ToArray();
        }
        catch (FileNotFoundException) { return []; }
        catch (DirectoryNotFoundException) { return []; }
    }

    private void Write(TapeTimeline timeline, List<Bookmark> entries)
    {
        string path = GetPath(timeline);
        var document = new Document(Version, entries.Select(entry =>
            new SavedBookmark(entry.Id, entry.Name, entry.Cue.SaveBookmarkPosition())).ToArray());
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(document);
        if (bytes.Length > MaxFileBytes)
            throw new InvalidDataException("Too many tape bookmarks.");

        Directory.CreateDirectory(directory);
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllBytes(temporary, bytes);
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    private string GetPath(TapeTimeline timeline)
    {
        if (timeline.Identity is not { Length: 64 } || !timeline.Identity.All(char.IsAsciiHexDigitUpper))
            throw new InvalidDataException("Invalid tape identity.");
        return Path.Combine(directory, timeline.Identity + ".json");
    }

    private static bool IsStorageError(Exception ex) =>
        ex is IOException or UnauthorizedAccessException or JsonException or InvalidDataException;
}
