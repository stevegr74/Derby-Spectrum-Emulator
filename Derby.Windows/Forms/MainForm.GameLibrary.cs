namespace Derby;

public partial class MainForm
{
    private readonly Lazy<GameLibrary> gameLibrary;

    private void OpenGameLibrary()
    {
        using IDisposable pause = AcquireEmulationPauseLease();
        using var dialog = new GameLibraryForm(gameLibrary.Value);
        if (dialog.ShowDialog(this) == DialogResult.OK && dialog.SelectedMedia is { } entry)
            OpenLibraryMedia(entry);
        screenBox.Focus();
    }

    internal void OpenLibraryMedia(GameLibraryEntry entry)
    {
        using IDisposable pause = AcquireEmulationPauseLease();
        switch (entry.Kind)
        {
            case GameMediaKind.Tape:
                OpenTapePath(this, TapeFileAction.StartNewGame, entry.Path);
                break;
            case GameMediaKind.Snapshot:
            case GameMediaKind.Recording:
                OpenSnapshotOrRecordingPath(entry.Path);
                break;
            case GameMediaKind.Disk:
                ShowDiskDrive(entry.Path);
                break;
        }
    }
}
