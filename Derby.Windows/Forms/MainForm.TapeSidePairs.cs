using System.Text.Json;
using Derby.Tape;

namespace Derby;

public partial class MainForm
{
    private void RememberTapePath(string path)
    {
        if (ReadTapeDeck() is { } deck)
            tapePaths[deck.Timeline.Identity] = Path.GetFullPath(path);
    }

    private TapeSideAction[] GetTapeSideActions(Form owner, TapeDeckView? deck)
    {
        if (deck == null)
            return [new("Insert a tape to pair sides", null)];
        try
        {
            TapeSidePairStore.Side? other = tapeSidePairs.FindOther(deck.Timeline.Identity);
            if (other == null)
                return [new("Pair another side...", () => RunTapePairAction(owner, () => PairTapeSide(owner, deck)))];
            string name = Path.GetFileNameWithoutExtension(other.Name);
            return
            [
                TapeSideImage.CanSwitch(deck)
                    ? new($"Switch to {name}", () => RunTapePairAction(owner, () => SwitchPairedTapeSide(owner, deck)))
                    : new("Stop the tape before switching sides", null),
                new("Remove pairing", () => RunTapePairAction(owner, () => tapeSidePairs.Remove(deck.Timeline.Identity)))
            ];
        }
        catch (Exception ex) when (IsTapePairError(ex))
        {
            return [new("Saved tape pairings are unavailable", null)];
        }
    }

    private void PairTapeSide(Form owner, TapeDeckView deck)
    {
        if (!tapePaths.TryGetValue(deck.Timeline.Identity, out string? path))
            throw new InvalidOperationException("Insert the current side from a tape file before pairing it.");

        using IDisposable pause = AcquireEmulationPauseLease();
        string? otherPath = PickTapeSide(owner, "Choose the other tape side");
        if (otherPath == null)
            return;
        if (!ReferenceEquals(ReadTapeDeck()?.Timeline, deck.Timeline))
            throw new InvalidOperationException("The inserted tape changed. Open Tape Transport and try again.");
        TapeSideImage other = TapeSideImage.Read(otherPath);
        tapeSidePairs.Add(new(deck.Timeline.Identity, path), other.Side);
    }

    private void SwitchPairedTapeSide(Form owner, TapeDeckView expected)
    {
        using IDisposable pause = AcquireEmulationPauseLease();
        TapeDeckView? deck = ReadTapeDeck();
        if (!TapeSideImage.CanSwitch(deck) || !ReferenceEquals(deck?.Timeline, expected.Timeline))
            throw new InvalidOperationException("Stop the current tape before switching sides.");
        TapeSidePairStore.Side? paired = tapeSidePairs.FindOther(expected.Timeline.Identity);
        if (paired == null)
            return;

        TapeSideImage? image = null;
        string preferredPath = tapePaths.GetValueOrDefault(paired.Identity, paired.Path);
        try
        {
            TapeSideImage found = TapeSideImage.Read(preferredPath);
            if (found.Side.Identity == paired.Identity)
                image = found;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }

        if (image == null)
        {
            string? located = PickTapeSide(owner, $"Locate original tape side: {paired.Name}");
            if (located == null)
                return;
            image = TapeSideImage.Read(located);
            if (image.Side.Identity != paired.Identity)
                throw new InvalidOperationException("That file is not the saved tape side. The current tape has not changed.");
        }

        lock (machineLock)
        {
            if (!image.TryMount(machine, expected.Timeline, paired.Identity))
                throw new InvalidOperationException("The inserted tape changed or is no longer stopped.");
        }
        ClearHostAndSpectrumInputState();
        tapePaths[image.Side.Identity] = image.Side.Path;
        RefreshAfterStoppedTapeInsert();
        SuppressSpectrumHostInputForMilliseconds(PostLoadInputSuppressionMilliseconds);
        PresentCurrentMachineFrame(frameClock.ElapsedTicks, force: true);

        try
        {
            if (tapePaths.TryGetValue(expected.Timeline.Identity, out string? source))
                tapeSidePairs.Relocate(new(expected.Timeline.Identity, source));
            tapeSidePairs.Relocate(image.Side);
        }
        catch (Exception ex) when (IsTapePairError(ex))
        {
            MessageBox.Show(owner, $"The tape was switched, but the saved location could not be updated.\n{ex.Message}",
                "Tape Side Pairing", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private static string? PickTapeSide(Form owner, string title)
    {
        using var picker = new OpenFileDialog
        {
            Title = title,
            Filter = "Spectrum tape images (*.tap;*.tzx)|*.tap;*.tzx",
            CheckFileExists = true
        };
        return picker.ShowDialog(owner) == DialogResult.OK ? picker.FileName : null;
    }

    private static void RunTapePairAction(Form owner, Action action)
    {
        try { action(); }
        catch (Exception ex) when (IsTapePairError(ex))
        {
            MessageBox.Show(owner, ex.Message, "Tape Side Pairing", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private static bool IsTapePairError(Exception ex) =>
        ex is IOException or UnauthorizedAccessException or InvalidDataException or InvalidOperationException or JsonException;
}
