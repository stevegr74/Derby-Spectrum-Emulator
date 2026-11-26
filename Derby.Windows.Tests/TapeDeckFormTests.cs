using System.Windows.Forms;
using System.Runtime.InteropServices;
using Derby.Tape;
using Xunit;

namespace Derby.Tests;

public sealed class TapeDeckFormTests
{
    [Fact]
    public void TimelineDragCommitsOnReleaseAndOnlyWhenStopped()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var machine = new SpectrumMachine(Path.Combine(AppContext.BaseDirectory, "ROMs"));
                machine.MountTape(new MountedTape("test.tzx", [TapeBlock.CreatePureTone(3500, 1000)]), startStopped: true);
                int previews = 0;
                using var form = new TapeDeckForm(machine.CaptureTapeDeck, machine.CaptureTapeCue,
                    (deck, _, _) => machine.TryToggleTapeDeck(deck.Timeline, deck.State), new TapeBookmarkStore(), _ => { },
                    scrubTape: machine.TryScrubTape, renderPreview: _ => { previews++; return null; });
                form.Size = form.MinimumSize;
                form.Show();
                var strip = Descendants(form).OfType<TapeTimelineStrip>().Single();
                var preview = GetButton(form, "Preview");
                Assert.True(preview.Enabled);
                Assert.True(strip.TabStop);
                Assert.True(preview.Right < GetButton(form, "Choose tape...").Left +
                    GetButton(form, "Choose tape...").Parent!.Left);
                SendMouse(0x0201, strip.Width / 4);
                SendMouse(0x0200, strip.Width / 2);
                Assert.Equal(0.0, machine.CaptureTapeDeck()!.Position);
                SendMouse(0x0202, strip.Width / 2);
                Assert.InRange(machine.CaptureTapeDeck()!.Position, 0.48, 0.52);
                preview.PerformClick();
                Assert.Equal(1, previews);
                Assert.Equal(TapeTransportState.Stopped, machine.TapeTransportState);
                GetButton(form, "Play tape").PerformClick();
                Assert.False(preview.Enabled);
                Assert.False(strip.TabStop);
                SendMouse(0x0201, strip.Width / 4);
                SendMouse(0x0202, strip.Width / 4);
                Assert.Equal(TapeTransportState.Playing, machine.TapeTransportState);
                Assert.Equal(TapeTransportTransition.ManualResume, machine.LastTapeTransportTransition);

                void SendMouse(uint message, int x)
                {
                    int y = strip.LogicalToDeviceUnits(17);
                    SendMessage(strip.Handle, message, message == 0x0202 ? 0 : 1, (y << 16) | x);
                }
            }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(15)), "Tape scrubbing UI test timed out.");
        if (failure != null)
            throw new Xunit.Sdk.XunitException(failure.ToString());
    }

    [DllImport("user32.dll", EntryPoint = "SendMessageW")]
    private static extern nint SendMessage(nint window, uint message, nint wParam, nint lParam);

    [Fact]
    public void SideOptionsLeaveTheTimelineAndBookmarkListLayoutUnchanged()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                bool invoked = false;
                using var form = new TapeDeckForm(() => null, _ => null, (_, _, _) => false,
                    new TapeBookmarkStore(), _ => { }, (_, _) =>
                    [
                        new("Switch to Side 2", () => invoked = true),
                        new("Remove pairing", () => { })
                    ]);
                form.Size = form.MinimumSize;
                form.Show();
                var timeline = Descendants(form).OfType<TapeTimelineStrip>().Single();
                var timelineBounds = timeline.Bounds;
                var markers = MarkerList(form);
                var markerBounds = markers.Bounds;
                Descendants(form).OfType<Button>().Single(button => button.AccessibleName == "Tape side options").PerformClick();
                var action = GetButton(form, "Switch to Side 2");
                Assert.True(action.Visible);
                Assert.Equal(timelineBounds, timeline.Bounds);
                Assert.Equal(markerBounds, markers.Bounds);
                action.PerformClick();
                Assert.True(invoked);
                Assert.False(action.Visible);
            }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(15)), "Tape side options test timed out.");
        if (failure != null)
            throw new Xunit.Sdk.XunitException(failure.ToString());
    }

    [Fact]
    public void TapeWindowRestoresAndEditsBookmarksInFreshEmulatorSessions()
    {
        string folder = Path.Combine(Path.GetTempPath(), "derby-bookmarks-test-" + Guid.NewGuid().ToString("N"));
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var machine = new SpectrumMachine(Path.Combine(AppContext.BaseDirectory, "ROMs"));
                var store = new TapeBookmarkStore(folder);
                using (var form = CreateForm())
                {
                    form.Show();
                    Assert.Equal(EmulatorUiStyle.Paper, form.BackColor);
                    Assert.True(MarkerList(form).Height >= form.LogicalToDeviceUnits(45),
                        "The compact window should have room to read bookmarks.");
                    Assert.False(GetButton(form, "Add bookmark...").Enabled);
                    GetButton(form, "Choose tape...").PerformClick();
                    Assert.NotNull(machine.MountedTape);
                    Assert.Contains(Descendants(form).OfType<Label>(), label => label.Text == "Side A.tzx");
                    Assert.DoesNotContain(Descendants(form).OfType<Button>(), button =>
                        button.Text is "Details" or "Go to block" or "Replace tape..." or "Load tape...");
                    ListBox positions = MarkerList(form);
                    positions.SelectedIndex = positions.FindStringExact("Section: Side passage");
                    Assert.False(GetButton(form, "Edit...").Enabled);
                    GetButton(form, "Go to").PerformClick();
                    Assert.Equal(2, machine.CaptureTapeDeck()!.BlockIndex);
                    var field = Descendants(form).OfType<TextBox>().Single();
                    Assert.False(field.Visible);
                    GetButton(form, "Add bookmark...").PerformClick();
                    Assert.True(field.Visible);
                    field.Text = "Level entry";
                    GetButton(form, "Save").PerformClick();
                    Assert.False(field.Visible);
                    Assert.Contains("Level entry", MarkerList(form).SelectedItem!.ToString());
                    GetButton(form, "Play tape").PerformClick();
                    Assert.False(GetButton(form, "Rewind").Enabled);
                    GetButton(form, "Stop tape").PerformClick();
                    Assert.True(GetButton(form, "Rewind").Enabled);
                }

                machine = new SpectrumMachine(Path.Combine(AppContext.BaseDirectory, "ROMs"));
                store = new TapeBookmarkStore(folder);
                Mount("Renamed side.tzx");
                using (var reopened = CreateForm())
                {
                    reopened.Show();
                    ListBox markers = MarkerList(reopened);
                    markers.SelectedIndex = markers.FindStringExact("Level entry");
                    Assert.True(markers.SelectedIndex >= 0);
                    GetButton(reopened, "Go to").PerformClick();
                    Assert.Equal(2, machine.CaptureTapeDeck()!.BlockIndex);
                    Assert.Equal(TapeTransportTransition.ManualSeek, machine.LastTapeTransportTransition);
                    Assert.Equal(TapeTransportState.Stopped, machine.TapeTransportState);
                    markers.SelectedIndex = markers.FindStringExact("Level entry");
                    GetButton(reopened, "Edit...").PerformClick();
                    Descendants(reopened).OfType<TextBox>().Single().Text = "Level 2";
                    GetButton(reopened, "Save").PerformClick();
                }
                store = new TapeBookmarkStore(folder);
                using (var reopened = CreateForm())
                {
                    reopened.Show();
                    ListBox markers = MarkerList(reopened);
                    Assert.Equal(-1, markers.FindStringExact("Level entry"));
                    markers.SelectedIndex = markers.FindStringExact("Level 2");
                    Assert.True(markers.SelectedIndex >= 0);
                    GetButton(reopened, "Edit...").PerformClick();
                    GetButton(reopened, "Delete").PerformClick();
                }
                Assert.Empty(new TapeBookmarkStore(folder).For(machine.CaptureTapeDeck()!.Timeline));

                void Mount(string fileName)
                {
                    machine.MountTape(new MountedTape(fileName,
                        [TapeBlock.CreatePureTone(100, 10), TapeBlock.CreateMetadata("Side passage"),
                            TapeBlock.CreatePureTone(200, 10)]));
                    machine.ToggleTapeTransport();
                }

                TapeDeckForm CreateForm() => new(machine.CaptureTapeDeck, machine.CaptureTapeCue,
                    (view, index, cue) => index is int block
                        ? machine.TrySeekTape(view.Timeline, block, cue)
                        : machine.TryToggleTapeDeck(view.Timeline, view.State), store, owner =>
                    {
                        Assert.IsType<TapeDeckForm>(owner);
                        Mount("Side A.tzx");
                    });
            }
            catch (Exception ex)
            {
                failure = ex;
            }
            finally
            {
                if (Directory.Exists(folder))
                    Directory.Delete(folder, recursive: true);
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(15)), "Tape window test timed out.");
        if (failure != null)
            throw new Xunit.Sdk.XunitException(failure.ToString());
    }

    [Fact]
    public void ChooseTapeDefaultsToKeepingTheCurrentGame()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                using (var form = new TapeFileActionForm("Side B.tzx"))
                {
                    form.Show();
                    Assert.Same(GetButton(form, "Change tape"), form.AcceptButton);
                    form.AcceptButton!.PerformClick();
                    Assert.Equal(DialogResult.OK, form.DialogResult);
                    Assert.Equal(TapeFileAction.MountStopped, form.Action);
                }
                using (var form = new TapeFileActionForm("Another game.tzx"))
                {
                    form.Show();
                    GetButton(form, "Start new game").PerformClick();
                    Assert.Equal(DialogResult.OK, form.DialogResult);
                    Assert.Equal(TapeFileAction.StartNewGame, form.Action);
                }
                using (var form = new TapeFileActionForm("Another game.tzx"))
                {
                    form.Show();
                    form.CancelButton!.PerformClick();
                    Assert.Equal(DialogResult.Cancel, form.DialogResult);
                }
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(15)), "Tape chooser test timed out.");
        if (failure != null)
            throw new Xunit.Sdk.XunitException(failure.ToString());
    }

    private static ListBox MarkerList(Control form) =>
        Descendants(form).OfType<ListBox>().Single(list => list.AccessibleName == "Tape sections and bookmarks");

    private static Button GetButton(Control form, string label) =>
        Descendants(form).OfType<Button>().Single(button => button.Text == label);

    private static IEnumerable<Control> Descendants(Control parent) =>
        parent.Controls.Cast<Control>().SelectMany(child => new[] { child }.Concat(Descendants(child)));
}
