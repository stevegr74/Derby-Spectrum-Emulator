using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Derby;

internal enum GameMediaKind { Tape, Snapshot, Recording, Disk }

internal sealed record GameLibraryEntry(string Path, DateTimeOffset? LastOpened = null)
{
    [JsonIgnore] public string FileName => System.IO.Path.GetFileName(Path);
    [JsonIgnore] public string Title => System.IO.Path.GetFileNameWithoutExtension(Path).Replace('_', ' ');
    [JsonIgnore] public string Format => System.IO.Path.GetExtension(Path).ToUpperInvariant();
    [JsonIgnore] public GameMediaKind Kind => GameLibrary.GetMediaKind(Path)!.Value;

    public bool Matches(IEnumerable<string> words) =>
        words.All(word => FileName.Contains(word, StringComparison.OrdinalIgnoreCase));
}

internal sealed class GameLibrary
{
    private const int RecentLimit = 12;
    private const int MaxSettingsBytes = 1024 * 1024;
    private static readonly StringComparer PathComparer = StringComparer.OrdinalIgnoreCase;
    private readonly string settingsPath;
    private readonly List<GameLibraryEntry> recent = [];

    private sealed record Document(string? Folder, List<GameLibraryEntry>? Recent);

    public GameLibrary(string? settingsPath = null)
    {
        this.settingsPath = settingsPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Derby", "game-library.json");
        try
        {
            using FileStream stream = File.OpenRead(this.settingsPath);
            if (stream.Length > MaxSettingsBytes || JsonSerializer.Deserialize<Document>(stream) is not { } data)
                return;
            Folder = IsAbsolutePath(data.Folder) ? data.Folder : null;
            recent.AddRange((data.Recent ?? [])
                .Where(entry => entry is { LastOpened: not null } && IsAbsolutePath(entry.Path) && GetMediaKind(entry.Path) != null)
                .OrderByDescending(entry => entry.LastOpened)
                .DistinctBy(entry => entry.Path, PathComparer)
                .Take(RecentLimit));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            System.Diagnostics.Debug.WriteLine(ex.Message);
        }
    }

    public string? Folder { get; private set; }
    public IReadOnlyList<GameLibraryEntry> Recent => recent;
    public string? SaveError { get; private set; }

    public void SetFolder(string path)
    {
        Folder = Path.GetFullPath(path);
        Save();
    }

    public void Remember(string path, DateTimeOffset? opened = null)
    {
        if (GetMediaKind(path) == null)
            return;
        path = Path.GetFullPath(path);
        recent.RemoveAll(entry => PathComparer.Equals(entry.Path, path));
        recent.Insert(0, new GameLibraryEntry(path, opened ?? DateTimeOffset.UtcNow));
        if (recent.Count > RecentLimit)
            recent.RemoveAt(RecentLimit);
        Save();
    }

    public static GameLibraryEntry[] ScanFolder(string folder, CancellationToken cancellationToken)
    {
        var entries = new List<GameLibraryEntry>();
        foreach (string path in Directory.EnumerateFiles(folder))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (GetMediaKind(path) != null)
                entries.Add(new GameLibraryEntry(path));
        }
        return entries.OrderBy(entry => entry.Title, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.FileName, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public static string[] SearchWords(string query) =>
        Regex.Matches(query, @"[\p{L}\p{N}]+").Select(word => word.Value).ToArray();

    public static GameMediaKind? GetMediaKind(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".tap" or ".tzx" => GameMediaKind.Tape,
        ".sna" or ".z80" or ".szx" => GameMediaKind.Snapshot,
        ".rzx" => GameMediaKind.Recording,
        ".dsk" or ".mgt" => GameMediaKind.Disk,
        _ => null
    };

    private static bool IsAbsolutePath(string? value) =>
        !string.IsNullOrWhiteSpace(value) && !value.Contains('\0') && Path.IsPathFullyQualified(value);

    private void Save()
    {
        string temporary = settingsPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(settingsPath))!);
            File.WriteAllBytes(temporary, JsonSerializer.SerializeToUtf8Bytes(new Document(Folder, recent)));
            File.Move(temporary, settingsPath, overwrite: true);
            SaveError = null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            SaveError = "Library settings could not be saved. Changes will last for this session.";
            System.Diagnostics.Debug.WriteLine(ex.Message);
        }
        finally
        {
            try { File.Delete(temporary); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
            }
        }
    }
}
