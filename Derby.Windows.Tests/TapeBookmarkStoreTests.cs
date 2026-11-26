using Derby.Tape;
using Xunit;

namespace Derby.Tests;

public sealed class TapeBookmarkStoreTests
{
    [Fact]
    public void ChangedMediaDoesNotInheritBookmarksAndAnUnreadableFileIsNotOverwritten()
    {
        DirectoryInfo folder = Directory.CreateTempSubdirectory("derby-bookmarks-");
        try
        {
            const string tapeName = "Same name.tzx";
            var machine = new SpectrumMachine(Path.Combine(AppContext.BaseDirectory, "ROMs"));
            machine.MountTape(new MountedTape(tapeName, [TapeBlock.CreatePureTone(100, 10)]));
            machine.ToggleTapeTransport();
            TapeTimeline original = machine.CaptureTapeDeck()!.Timeline;
            TapeCue cue = Assert.IsType<TapeCue>(machine.CaptureTapeCue(original));
            var bookmark = new TapeBookmarkStore.Bookmark(Guid.NewGuid(), "Level", cue);
            Assert.True(new TapeBookmarkStore(folder.FullName).TryChange(original, null, bookmark));

            machine.MountTape(new MountedTape(tapeName, [TapeBlock.CreatePureTone(200, 10)]));
            Assert.Empty(new TapeBookmarkStore(folder.FullName).For(machine.CaptureTapeDeck()!.Timeline));

            string path = Assert.Single(folder.GetFiles("*.json")).FullName;
            const string unreadable = "{\"Version\":42,\"Bookmarks\":[]}";
            File.WriteAllText(path, unreadable);
            var restarted = new TapeBookmarkStore(folder.FullName);
            Assert.Empty(restarted.For(original));
            Assert.False(restarted.TryChange(original, null, bookmark));
            Assert.Equal(unreadable, File.ReadAllText(path));
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }
}
