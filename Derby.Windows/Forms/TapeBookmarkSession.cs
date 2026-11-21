using Derby.Tape;

namespace Derby;

internal sealed class TapeBookmarkSession
{
    internal sealed record Bookmark(string Name, TapeCue Cue)
    {
        public override string ToString() => Name;
    }

    private readonly Dictionary<string, List<Bookmark>> bookmarks = new();

    public List<Bookmark> For(TapeTimeline timeline)
    {
        if (!bookmarks.TryGetValue(timeline.Identity, out var list))
            bookmarks.Add(timeline.Identity, list = new());
        return list;
    }
}
