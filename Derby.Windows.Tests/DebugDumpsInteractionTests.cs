using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using Xunit;

namespace Derby.Tests;

public sealed class DebugDumpsInteractionTests
{
    [Fact]
    public void F12_SavesWithoutOpeningAWindow_AndShiftF12OpensHistory()
    {
        RunSta(() =>
        {
            using var owner = new MainForm();
            string folder = Path.Combine(AppContext.BaseDirectory, "debug");
            string[] ListDumps() => Directory.Exists(folder)
                ? Directory.GetFiles(folder, "machine-debug-*.txt") : [];
            string[] before = ListDumps();
            const BindingFlags privateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
            MethodInfo keyDown = typeof(MainForm).GetMethod("MainForm_KeyDown", privateInstance)!;
            bool Press(Keys keys)
            {
                var args = new KeyEventArgs(keys);
                keyDown.Invoke(owner, [null, args]);
                return args.Handled;
            }

            Assert.True(Press(Keys.F12));
            string file = Assert.Single(ListDumps().Except(before));
            try
            {
                Assert.Empty(owner.OwnedForms);
                Assert.Contains("Manual F12 debug dump.", File.ReadAllText(file));
                var overlay = Assert.IsType<DebugDumpSavedOverlay>(typeof(MainForm)
                    .GetField("debugDumpSavedOverlay", privateInstance)!.GetValue(owner));
                var clock = Assert.IsType<Stopwatch>(typeof(MainForm).GetField("frameClock", privateInstance)!.GetValue(owner));
                Assert.True(overlay.IsVisible(clock.ElapsedTicks));

                Assert.True(Press(Keys.Shift | Keys.F12));
                using var history = Assert.IsType<DebugDumpsForm>(Assert.Single(owner.OwnedForms));
                Assert.True(history.Visible);
                Assert.False(history.Modal);
                Assert.Contains(Descendants(history).OfType<ListView>().Single().Items.Cast<ListViewItem>(),
                    item => Equals(item.Tag, file));
                Assert.True(Press(Keys.Shift | Keys.F12));
                Assert.Same(history, Assert.Single(owner.OwnedForms));
                Assert.True((bool)typeof(Form).GetMethod("ProcessDialogKey", privateInstance)!.Invoke(history, [Keys.Escape])!);
                Assert.True(history.IsDisposed);
            }
            finally
            {
                File.Delete(file);
            }
        });
    }

    [Fact]
    public void History_SortsRecognisedDumps_AndOpensOrRevealsTheSelectedFile()
    {
        DirectoryInfo folder = Directory.CreateTempSubdirectory("derby debug dumps ");
        DateTime time = new(2026, 11, 24, 13, 0, 0, DateTimeKind.Utc);
        string manual = Write("machine-debug-20261124-130000000.txt", "=== REASON ===\nManual F12 debug dump.\n", 0);
        string auto = Write("machine-debug-auto-20261124-130100000.txt", "Auto dump", 1);
        string crash = Write("emulation-loop-crash-20261124-130200000.txt", "Crash report", 2);
        string unrelated = Write("unrelated.txt", "Not a diagnostic", 3);
        try
        {
            RunSta(() =>
            {
                string? opened = null;
                string? revealed = null;
                using var history = new DebugDumpsForm(folder.FullName, path => opened = path, path => revealed = path);
                history.Show();
                ListView list = Descendants(history).OfType<ListView>().Single();
                Assert.Equal([crash, auto, manual], list.Items.Cast<ListViewItem>().Select(row => (string)row.Tag!));
                Assert.Equal(["Crash", "Automatic", "Manual"], list.Items.Cast<ListViewItem>().Select(row => row.SubItems[2].Text));
                Assert.Equal(crash, Assert.Single(list.SelectedItems.Cast<ListViewItem>()).Tag);
                list.Items[1].Selected = true;
                Descendants(history).OfType<Button>().Single(button => button.Text == "Open Dump").PerformClick();
                Descendants(history).OfType<Button>().Single(button => button.Text == "Show in Folder").PerformClick();
                Assert.Equal(auto, opened);
                Assert.Equal(auto, revealed);

                File.Delete(auto);
                Descendants(history).OfType<Button>().Single(button => button.Text == "Open Dump").PerformClick();
                Assert.Contains(Descendants(history).OfType<Label>(), label => label.Text == "That dump is no longer available.");
                Assert.Equal(2, list.Items.Count);
            });
        }
        finally
        {
            foreach (string file in new[] { manual, auto, crash, unrelated })
                File.Delete(file);
            folder.Delete();
        }

        string Write(string name, string body, int minutes)
        {
            string path = Path.Combine(folder.FullName, name);
            File.WriteAllText(path, body);
            File.SetLastWriteTimeUtc(path, time.AddMinutes(minutes));
            return path;
        }
    }

    [Fact]
    public void EmptyHistory_DisablesActionsWithoutCreatingTheFolder()
    {
        RunSta(() =>
        {
            string folder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            using var history = new DebugDumpsForm(folder);
            history.Show();
            Assert.False(Directory.Exists(folder));
            Assert.Empty(Descendants(history).OfType<ListView>().Single().Items);
            Assert.All(Descendants(history).OfType<Button>()
                .Where(button => button.Text is "Open Dump" or "Show in Folder"), button => Assert.False(button.Enabled));
        });
    }

    [Fact]
    public void SavedBadge_ExpiresAndMovesClearOfMouseCapture()
    {
        const long now = 10;
        var overlay = new DebugDumpSavedOverlay();
        Assert.False(overlay.IsVisible(now));
        overlay.Show(now);
        Assert.True(overlay.IsVisible(now + Stopwatch.Frequency * 5 / 2 - 1));
        Assert.False(overlay.IsVisible(now + Stopwatch.Frequency * 5 / 2));

        for (int scale = 1; scale <= 3; scale++)
        {
            var image = new Size(320 * scale, 240 * scale);
            Rectangle normal = DebugDumpSavedOverlay.GetBounds(image, scale, mouseCaptured: false);
            Rectangle captured = DebugDumpSavedOverlay.GetBounds(image, scale, mouseCaptured: true);
            Assert.True(new Rectangle(Point.Empty, image).Contains(normal));
            Assert.True(new Rectangle(Point.Empty, image).Contains(captured));
            Assert.True(captured.Bottom < image.Height - 22 * scale);
        }
    }

    private static IEnumerable<Control> Descendants(Control parent) =>
        parent.Controls.Cast<Control>().SelectMany(control => new[] { control }.Concat(Descendants(control)));

    private static void RunSta(Action test)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { test(); }
            catch (Exception exception) { failure = exception; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(15)), "The debug dumps test timed out.");
        if (failure != null)
            throw new Xunit.Sdk.XunitException(failure.ToString());
    }
}
