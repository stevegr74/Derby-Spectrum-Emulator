using System.Windows.Forms;
using Derby.Tape;
using Xunit;

namespace Derby.Tests;

public sealed class TapeDeckFormTests
{
    [Fact]
    public void TapeWindowKeepsNamedBookmarksForSameImageAcrossWindowReopen()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var machine = new SpectrumMachine(Path.Combine(AppContext.BaseDirectory, "ROMs"));
                var session = new TapeBookmarkSession();
                using (var form = CreateForm())
                {
                    form.Show();
                    Assert.Equal(EmulatorUiStyle.Paper, form.BackColor);
                    Assert.True(MarkerList(form).Height >= form.LogicalToDeviceUnits(45),
                        "The compact window should have room to read bookmarks.");
                    Assert.False(GetButton(form, "Add bookmark...").Enabled);
                    GetButton(form, "Choose tape...").PerformClick();
                    Assert.NotNull(machine.MountedTape);
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

                using var reopened = CreateForm();
                reopened.Show();
                ListBox markers = MarkerList(reopened);
                Assert.Contains(markers.Items.Cast<object>(), item => item.ToString()!.Contains("Level entry"));
                markers.SelectedIndex = markers.FindStringExact("Level entry");
                GetButton(reopened, "Go to").PerformClick();
                Assert.Equal(TapeTransportTransition.ManualSeek, machine.LastTapeTransportTransition);
                Assert.Equal(TapeTransportState.Stopped, machine.TapeTransportState);

                TapeDeckForm CreateForm() => new(machine.CaptureTapeDeck, machine.CaptureTapeCue,
                    (view, index, cue) => index is int block
                        ? machine.TrySeekTape(view.Timeline, block, cue)
                        : machine.TryToggleTapeDeck(view.Timeline, view.State), session, owner =>
                    {
                        Assert.IsType<TapeDeckForm>(owner);
                        machine.MountTape(new MountedTape("Side A.tzx",
                            [TapeBlock.CreatePureTone(100, 10), TapeBlock.CreateMetadata("Side passage"),
                                TapeBlock.CreatePureTone(200, 10)]));
                        machine.ToggleTapeTransport();
                    });
            }
            catch (Exception ex)
            {
                failure = ex;
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
                    Assert.Equal(TapeFileAction.ChangeTape, form.Action);
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
