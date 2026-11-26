using System.Text.Json;
using System.Text.Json.Serialization;

namespace Derby;

internal sealed class TapeSidePairStore(string? path = null)
{
    private const int Version = 1;
    private const int MaxFileBytes = 1024 * 1024;
    private readonly string path = path ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Derby", "tape-side-pairs.json");

    internal sealed record Side(string Identity, string Path)
    {
        [JsonIgnore] public string Name => System.IO.Path.GetFileName(Path);
    }

    private sealed record Pair(Side First, Side Second)
    {
        public bool Contains(string identity) => First.Identity == identity || Second.Identity == identity;
    }

    private sealed record Document(int Version, List<Pair> Pairs);

    public Side? FindOther(string identity)
    {
        Pair? pair = Load().Find(pair => pair.Contains(identity));
        return pair == null ? null : pair.First.Identity == identity ? pair.Second : pair.First;
    }

    public void Add(Side first, Side second)
    {
        if (!IsSide(first) || !IsSide(second) || first.Identity == second.Identity)
            throw new InvalidOperationException("Choose a different .tap or .tzx tape image to pair.");
        List<Pair> pairs = Load();
        if (pairs.Exists(pair => pair.Contains(first.Identity) || pair.Contains(second.Identity)))
            throw new InvalidOperationException("One of these images is already paired. Remove that pairing first.");
        pairs.Add(new(first, second));
        Save(pairs);
    }

    public void Remove(string identity)
    {
        List<Pair> pairs = Load();
        if (pairs.RemoveAll(pair => pair.Contains(identity)) != 0)
            Save(pairs);
    }

    public void Relocate(Side side)
    {
        if (!IsSide(side))
            throw new InvalidOperationException("Invalid tape image location.");
        List<Pair> pairs = Load();
        int index = pairs.FindIndex(pair => pair.Contains(side.Identity));
        if (index < 0)
            return;
        Pair pair = pairs[index];
        Pair updated = pair.First.Identity == side.Identity ? pair with { First = side } : pair with { Second = side };
        if (updated == pair)
            return;
        pairs[index] = updated;
        Save(pairs);
    }

    private List<Pair> Load()
    {
        try
        {
            using FileStream stream = File.OpenRead(path);
            if (stream.Length > MaxFileBytes ||
                JsonSerializer.Deserialize<Document>(stream) is not { Version: Version, Pairs: not null } document)
                throw new InvalidDataException("The saved tape pairings could not be read.");
            var identities = new HashSet<string>(StringComparer.Ordinal);
            foreach (Pair pair in document.Pairs)
                if (pair == null || !IsSide(pair.First) || !IsSide(pair.Second) ||
                    !identities.Add(pair.First.Identity) || !identities.Add(pair.Second.Identity))
                    throw new InvalidDataException("The saved tape pairings contain an invalid or duplicate image.");
            return document.Pairs;
        }
        catch (FileNotFoundException) { return []; }
        catch (DirectoryNotFoundException) { return []; }
    }

    private void Save(List<Pair> pairs)
    {
        byte[] data = JsonSerializer.SerializeToUtf8Bytes(new Document(Version, pairs));
        if (data.Length > MaxFileBytes)
            throw new InvalidDataException("The saved tape pairings exceed the storage limit.");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllBytes(temporary, data);
            File.Move(temporary, path, overwrite: true);
        }
        finally { File.Delete(temporary); }
    }

    private static bool IsSide(Side? side) => side?.Identity is { Length: 64 } identity &&
        identity.All(char.IsAsciiHexDigitUpper) && !string.IsNullOrWhiteSpace(side.Path) &&
        Path.IsPathFullyQualified(side.Path) && TapeSideImage.IsTapePath(side.Path);
}
