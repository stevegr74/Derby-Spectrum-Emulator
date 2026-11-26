using Derby.Tape;

namespace Derby;

public partial class MainForm
{
    private TapeDeckForm? tapeDeck;
    private readonly TapeBookmarkStore tapeBookmarks = new();
    private readonly TapeSidePairStore tapeSidePairs = new();
    private readonly Dictionary<string, string> tapePaths = new(StringComparer.Ordinal);

    private void OpenTapeDeck()
    {
        if (tapeDeck is { IsDisposed: false })
        {
            tapeDeck.Activate();
            return;
        }

        var form = new TapeDeckForm(ReadTapeDeck, CaptureTapeBookmark, ControlTapeDeck,
            tapeBookmarks, ChooseTapeFromDialog, GetTapeSideActions);
        tapeDeck = form;
        form.FormClosed += (_, _) => tapeDeck = null;
        form.Show(this);
    }

    private TapeDeckView? ReadTapeDeck()
    {
        lock (machineLock)
            return machine.CaptureTapeDeck();
    }

    private TapeCue? CaptureTapeBookmark(TapeTimeline timeline)
    {
        lock (machineLock)
            return machine.CaptureTapeCue(timeline);
    }

    private bool ControlTapeDeck(TapeDeckView view, int? blockIndex, TapeCue? cue)
    {
        bool changed;
        TapeTransportState state;
        bool marker;
        lock (machineLock)
        {
            changed = blockIndex.HasValue
                ? machine.TrySeekTape(view.Timeline, blockIndex.Value, cue)
                : machine.TryToggleTapeDeck(view.Timeline, view.State);
            state = machine.TapeTransportState;
            marker = machine.IsTapeStoppedAtMarker;
        }

        if (!changed)
            return false;
        tapeTransportOverlay.SetObservedState(state, marker);
        if (state is TapeTransportState.Playing or TapeTransportState.Stopped)
            tapeTransportOverlay.Show(state, marker, frameClock.ElapsedTicks);
        UpdateTapeTransportMenuItem();
        return true;
    }
}
