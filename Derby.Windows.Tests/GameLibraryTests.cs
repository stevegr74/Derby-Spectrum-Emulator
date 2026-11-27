using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Windows.Forms;
using Xunit;

namespace Derby.Tests;

public sealed class GameLibraryTests
{
    [Fact]
    public void FolderAndRecentsPersist_WithTwelveDistinctPathsInOpenedOrder()
    {
        WithFolder(root =>
        {
            GameLibrary library = CreateLibrary(root);
            var epoch = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
            string[] paths = Enumerable.Range(0, 13).Select(i => Path.Combine(root, $"game{i}.tap")).ToArray();
            for (int i = 0; i < paths.Length; i++)
                library.Remember(paths[i], epoch.AddMinutes(i));
            library.Remember(paths[4].ToUpperInvariant(), epoch.AddHours(1));
            library.Remember(Path.Combine(root, "unrelated.txt"));

            GameLibrary reopened = CreateLibrary(root, configure: false);
            Assert.Equal(root, reopened.Folder);
            Assert.Equal(12, reopened.Recent.Count);
            Assert.Equal(paths[4], reopened.Recent[0].Path, ignoreCase: true);
            Assert.Equal(paths[12], reopened.Recent[1].Path);
            Assert.DoesNotContain(reopened.Recent, item => item.Path == paths[0]);
        });
    }

    [Fact]
    public void Dialog_SearchesAcrossReleases_AndDoesNotOpenMissingRecents()
    {
        WithFolder(root => RunSta(() =>
        {
            string snapshot = Write(root, "Space_Raiders.sna", []);
            string tape = Write(root, "Space Raiders (Side 2).TZX", []);
            Write(root, "readme.txt", []);
            Directory.CreateDirectory(Path.Combine(root, "other"));
            Write(Path.Combine(root, "other"), "nested.tzx", []);
            GameLibrary library = CreateLibrary(root);
            library.Remember(snapshot);
            library.Remember(Path.Combine(root, "Moved Game.tap"));

            using var form = new GameLibraryForm(library);
            form.Show();
            ListView list = Find<ListView>(form, "LibraryList");
            Assert.True(SpinWait.SpinUntil(() => { Application.DoEvents(); return list.Items.Count == 2; },
                TimeSpan.FromSeconds(10)));
            TextBox search = Find<TextBox>(form, "LibrarySearch");
            Button action = Find<Button>(form, "LibraryOpen");
            form.Size = form.MinimumSize;
            Application.DoEvents();
            Assert.True(form.ClientRectangle.Contains(form.RectangleToClient(action.RectangleToScreen(action.ClientRectangle))));
            Assert.True(list.Height > 50);
            search.Text = "raiders space";
            Assert.Equal(2, list.Items.Count);
            search.Text = "side 2 tzx";
            Assert.Single(list.Items);
            Assert.Equal("Load tape", action.Text);
            Assert.Null(form.SelectedMedia);
            search.Text = "not found";
            Assert.Empty(list.Items);
            Assert.False(action.Enabled);

            search.Clear();
            Find<Button>(form, "LibraryRecent").PerformClick();
            Assert.Contains("(missing)", list.Items[0].Text);
            list.Items[0].Selected = true;
            Assert.False(action.Enabled);
            Assert.Null(form.SelectedMedia);
            list.Items[1].Selected = true;
            Assert.Equal("Load snapshot", action.Text);
            Find<Button>(form, "LibraryAll").PerformClick();
            search.Text = "side 2";
            action.PerformClick();
            Assert.Equal(tape, form.SelectedMedia?.Path);
        }));
    }

    [Fact]
    public void Open_UsesTheMainWindowLoaders_AndRemembersSuccessfulOpensOnly()
    {
        WithFolder(root => RunSta(() =>
        {
            GameLibrary library = CreateLibrary(root);
            using var form = new MainForm(new QuickStateArchiveStore(Path.Combine(root, "states.dat")), library);
            byte[] sna = new byte[27 + 48 * 1024];
            sna[24] = 0x80; // Stack at 8000h.
            sna[27] = 0x5A;
            string snapshot = Write(root, "Snapshot.sna", sna);
            form.OpenLibraryMedia(new(snapshot));
            Assert.Equal((byte)0x5A, form.MachineForTesting.PeekMemory(0x4000));
            Assert.Equal(snapshot, Assert.Single(library.Recent).Path);

            string tape = Write(root, "Game.tzx", [.. "ZXTape!"u8, 0x1A, 1, 20, 0x12, 0x78, 0x08, 0x20, 0]);
            form.OpenLibraryMedia(new(tape));
            Assert.Equal("Game.tzx", form.MachineForTesting.MountedTapeName);
            Assert.Equal(tape, library.Recent[0].Path);
            string recording = Write(root, "Recording.rzx", BuildRecording(sna));
            form.OpenLibraryMedia(new(recording));
            Assert.Equal((byte)0x5A, form.MachineForTesting.PeekMemory(0x4000));
            Assert.True(form.MachineForTesting.HasRzxPlayback);
            Assert.Equal(recording, library.Recent[0].Path);

            SpectrumMachine current = form.MachineForTesting;
            var menu = (ContextMenuStrip)typeof(MainForm)
                .GetField("displayContextMenu", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(form)!;
            RunWithDialog(() => menu.Items.Cast<ToolStripItem>().Single(item => item.Text == "Game Library").PerformClick(),
                dialog => { Assert.IsType<GameLibraryForm>(dialog); dialog.Close(); });
            Assert.Same(current, form.MachineForTesting);
            Assert.Equal(3, library.Recent.Count);
        }));
    }

    [Fact]
    public void DiskSelection_DoesNotInsertOnCancel_AndReplacementsAreNotRecentGames()
    {
        WithFolder(root => RunSta(() =>
        {
            GameLibrary library = CreateLibrary(root);
            using var form = new MainForm(new QuickStateArchiveStore(Path.Combine(root, "states.dat")), library);
            var menu = (ContextMenuStrip)typeof(MainForm)
                .GetField("displayContextMenu", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(form)!;
            var machine = (ToolStripMenuItem)menu.Items.Cast<ToolStripItem>().Single(item => item.Text == "F3 - Machine");
            machine.DropDownItems.Cast<ToolStripItem>().Single(item => item.Text == "ZX Spectrum +3").PerformClick();
            string disk = Write(root, "Disk 1.dsk", BuildDisk());
            string replacement = Write(root, "Disk 2.dsk", BuildDisk());

            RunWithDialog(() => form.OpenLibraryMedia(new(disk)), dialog =>
            {
                Assert.Null(form.MachineForTesting.MountedDiskName);
                Assert.Equal("Insert selected", Find<Button>(dialog, "DiskInsert").Text);
                dialog.Close();
            });
            Assert.Empty(library.Recent);

            RunWithDialog(() => form.OpenLibraryMedia(new(disk)), dialog =>
            {
                Find<Button>(dialog, "DiskInsert").PerformClick();
                Assert.Equal("Disk 1.dsk", form.MachineForTesting.MountedDiskName);
                dialog.Close();
            });
            Assert.Equal(disk, Assert.Single(library.Recent).Path);

            RunWithDialog(() => form.OpenLibraryMedia(new(replacement)), dialog =>
            {
                Assert.Equal("Replace selected", Find<Button>(dialog, "DiskInsert").Text);
                Find<Button>(dialog, "DiskInsert").PerformClick();
                Assert.Equal("Disk 2.dsk", form.MachineForTesting.MountedDiskName);
                dialog.Close();
            });
            Assert.Equal(disk, Assert.Single(library.Recent).Path);
        }));
    }

    private static byte[] BuildRecording(byte[] sna)
    {
        using var output = new MemoryStream();
        using var writer = new BinaryWriter(output);
        writer.Write("RZX!"u8);
        writer.Write((ushort)0x0D00);
        writer.Write(0);
        writer.Write((byte)0x30);
        writer.Write(17 + sna.Length);
        writer.Write(0);
        writer.Write("SNA\0"u8);
        writer.Write(sna.Length);
        writer.Write(sna);
        writer.Write((byte)0x80);
        writer.Write(22);
        writer.Write(1);
        writer.Write((byte)0);
        writer.Write(0);
        writer.Write(0);
        writer.Write((ushort)1);
        writer.Write((ushort)0);
        return output.ToArray();
    }

    private static byte[] BuildDisk()
    {
        byte[] data = new byte[768];
        "MV - CPCEMU Disk-File\r\nDisk-Info\r\n"u8.CopyTo(data);
        data[48] = data[49] = 1;
        data[51] = 2;
        "Track-Info\r\n"u8.CopyTo(data.AsSpan(256));
        data[277] = 1;
        data[282] = 1;
        data[286] = 128;
        return data;
    }

    private static void RunWithDialog(Action open, Action<Form> check)
    {
        Exception? failure = null;
        bool seen = false;
        using var timer = new System.Windows.Forms.Timer { Interval = 20 };
        timer.Tick += (_, _) =>
        {
            if (Application.OpenForms.Cast<Form>().LastOrDefault(form => form.Modal) is not { } dialog)
                return;
            timer.Stop();
            seen = true;
            try { check(dialog); }
            catch (Exception ex) { failure = ex; dialog.Close(); }
        };
        timer.Start();
        open();
        Assert.True(seen);
        if (failure != null)
            ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private static T Find<T>(Form form, string name) where T : Control => Assert.IsType<T>(Assert.Single(form.Controls.Find(name, true)));

    private static string Write(string root, string name, byte[] bytes)
    {
        string path = Path.Combine(root, name);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private static GameLibrary CreateLibrary(string root, bool configure = true)
    {
        var library = new GameLibrary(Path.Combine(root, "library.json"));
        if (configure)
            library.SetFolder(root);
        return library;
    }

    private static void WithFolder(Action<string> test)
    {
        DirectoryInfo root = Directory.CreateTempSubdirectory("derby-library-");
        try { test(root.FullName); }
        finally { root.Delete(recursive: true); }
    }

    private static void RunSta(Action test)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { test(); }
            catch (Exception ex) { failure = ex; }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(20)), "The Game Library interaction test timed out.");
        if (failure != null)
            ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
