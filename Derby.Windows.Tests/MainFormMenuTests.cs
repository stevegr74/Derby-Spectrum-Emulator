using System.Reflection;
using System.Windows.Forms;
using Xunit;

namespace Derby.Tests
{
    public class MainFormMenuTests
    {
        [Fact]
        public void ContextMenu_KeepsFunctionKeysOrderedBeforePokeManager()
        {
            Exception? failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    using var form = new MainForm();
                    var menu = (ContextMenuStrip)typeof(MainForm)
                        .GetField("displayContextMenu", BindingFlags.Instance | BindingFlags.NonPublic)!
                        .GetValue(form)!;
                    string[] labels = menu.Items
                        .Cast<ToolStripItem>()
                        .Select(item => item is ToolStripSeparator ? "---" : item.Text ?? string.Empty)
                        .ToArray();

                    Assert.Equal(
                        [
                            "F1 - Help",
                            "---",
                            "F2 - Status Overlay",
                            "F3 - Machine",
                            "F4 - Display Size",
                            "---",
                            "F5 - No Tape Inserted",
                            "Shift+F5 - Tape Transport",
                            "F6 - Load Tape...",
                            "F7 - Insert Tape...",
                            "F8 - Disk Drive",
                            "---",
                            "F9 - Load Snapshot / Play Recording...",
                            "F10 - Quick State Manager",
                            "---",
                            "F11 - Debugger / Disassembler",
                            "F12 - Write Machine Diagnostic Dump",
                            "Shift+F12 - Debug Dumps",
                            "---",
                            "Keyboard",
                            "Joystick",
                            "Mouse",
                            "---",
                            "Poke Manager"
                        ],
                        labels);

                    MethodInfo shortcut = typeof(MainForm).GetMethod(
                        "GetMainShortcutAction", BindingFlags.Instance | BindingFlags.NonPublic)!;
                    (Keys Key, string Method)[] mediaShortcuts =
                    [
                        (Keys.F5, "ToggleTapeTransport"),
                        (Keys.F5 | Keys.Shift, "OpenTapeDeck"),
                        (Keys.F6, "LoadTapeFromDialog"),
                        (Keys.F7, "MountStoppedTapeFromDialog"),
                        (Keys.F8, "OpenDiskDrive"),
                        (Keys.F9, "LoadSnapshotOrRecordingFromDialog"),
                        (Keys.F10, "OpenQuickStateManager"),
                        (Keys.F11, "ToggleDisassemblerWindow"),
                        (Keys.F12, "DumpMachineDebugState"),
                        (Keys.F12 | Keys.Shift, "OpenDebugDumps")
                    ];
                    foreach (var (key, method) in mediaShortcuts)
                    {
                        Assert.Equal(method, Assert.IsType<Action>(shortcut.Invoke(form, [key])).Method.Name);
                        Assert.Null(shortcut.Invoke(form, [key | Keys.Control]));
                        Assert.Null(shortcut.Invoke(form, [key | Keys.Alt]));
                        if (key is not (Keys.F5 or Keys.F12) && (key & Keys.Shift) == 0)
                            Assert.Null(shortcut.Invoke(form, [key | Keys.Shift]));
                    }

                    var shiftF5 = new KeyEventArgs(Keys.Shift | Keys.F5);
                    typeof(MainForm).GetMethod("MainForm_KeyDown", BindingFlags.Instance | BindingFlags.NonPublic)!
                        .Invoke(form, [null, shiftF5]);
                    Assert.True(shiftF5.Handled);
                    using var tapeDeck = Assert.IsType<TapeDeckForm>(typeof(MainForm)
                        .GetField("tapeDeck", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(form));
                    Assert.True(tapeDeck.Visible);
                    tapeDeck.Close();

                    var mouseMenu = menu.Items.OfType<ToolStripMenuItem>().Single(item => item.Text == "Mouse");
                    var choices = mouseMenu.DropDownItems.OfType<ToolStripMenuItem>().ToArray();
                    Assert.Equal(["Off", "AMX Mouse", "Kempston Mouse"], choices.Select(item => item.Text));
                    Assert.True(choices[0].Checked);
                    Assert.Equal(SpectrumMouseType.Off, form.MachineForTesting.MouseType);
                    choices[1].PerformClick();
                    Assert.Equal(SpectrumMouseType.Amx, form.MachineForTesting.MouseType);
                    choices[2].PerformClick();
                    Assert.Equal(SpectrumMouseType.Kempston, form.MachineForTesting.MouseType);

                    ToolStripMenuItem machineMenu = menu.Items
                        .OfType<ToolStripMenuItem>()
                        .Single(item => item.Text == "F3 - Machine");
                    Assert.Equal(
                        ["ZX Spectrum 128K", "ZX Spectrum 48K", "ZX Spectrum +2 (Grey)", "ZX Spectrum +2A", "ZX Spectrum +3"],
                        machineMenu.DropDownItems.Cast<ToolStripItem>()
                            .Select(item => item is ToolStripSeparator ? "---" : item.Text ?? string.Empty));
                    Assert.True(machineMenu.DropDownItems.OfType<ToolStripMenuItem>()
                        .Single(item => item.Text == "ZX Spectrum 128K").Checked);
                    Assert.DoesNotContain(menu.Items.OfType<ToolStripMenuItem>(), item => item.Text == "ROM Set");
                }
                catch (Exception exception)
                {
                    failure = exception;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            Assert.True(thread.Join(TimeSpan.FromSeconds(15)), "The MainForm menu test timed out.");
            if (failure != null)
                throw new Xunit.Sdk.XunitException(failure.ToString());
        }

        [Fact]
        public void F3MachineSwitch_CyclesEveryBundledProfileInMenuOrder()
        {
            Exception? failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    using var form = new MainForm();
                    Type type = typeof(MainForm);
                    MethodInfo cycle = type.GetMethod("CycleMachineProfile", BindingFlags.Instance | BindingFlags.NonPublic)!;
                    string[] expected =
                    [
                        "Sinclair ZX Spectrum 48K (bundled)",
                        "Sinclair ZX Spectrum +2 Grey (bundled)",
                        "Sinclair ZX Spectrum +2A (bundled)",
                        "Sinclair ZX Spectrum +3 (bundled)",
                        "Sinclair ZX Spectrum 128K (bundled)"
                    ];
                    string[] expectedDebuggerNames = ["48K", "+2", "+2A", "+3", "128K"];

                    for (int index = 0; index < expected.Length; index++)
                    {
                        cycle.Invoke(form, null);
                        SpectrumRomSet selected = (SpectrumRomSet)type
                            .GetField("selectedRomSet", BindingFlags.Instance | BindingFlags.NonPublic)!
                            .GetValue(form)!;
                        DisassemblySnapshot snapshot = (DisassemblySnapshot)type
                            .GetMethod("CaptureDisassemblySnapshot", BindingFlags.Instance | BindingFlags.NonPublic)!
                            .Invoke(form, null)!;
                        Assert.Equal(expected[index], selected.Name);
                        Assert.Equal(expectedDebuggerNames[index], snapshot.MachineProfileName);
                    }
                }
                catch (Exception exception)
                {
                    failure = exception;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            Assert.True(thread.Join(TimeSpan.FromSeconds(15)), "The machine-profile switching test timed out.");
            if (failure != null)
                throw new Xunit.Sdk.XunitException(failure.ToString());
        }

        [Fact]
        public void QuickStateRestore_ReinstatesItsFourRomMachineProfile()
        {
            Exception? failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    using var form = new MainForm();
                    Type type = typeof(MainForm);
                    SpectrumRomSet plus2A = (SpectrumRomSet)type
                        .GetField("bundledPlus2ARomSet", BindingFlags.Instance | BindingFlags.NonPublic)!
                        .GetValue(form)!;
                    SpectrumRomSet standard128 = (SpectrumRomSet)type
                        .GetField("bundled128RomSet", BindingFlags.Instance | BindingFlags.NonPublic)!
                        .GetValue(form)!;
                    MethodInfo apply = type.GetMethod("ApplyRomSet", BindingFlags.Instance | BindingFlags.NonPublic)!;

                    apply.Invoke(form, [plus2A]);
                    type.GetMethod("SaveQuickStateSlot", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(form, [0]);
                    apply.Invoke(form, [standard128]);
                    bool restored = (bool)type.GetMethod("RestoreQuickStateSlot", BindingFlags.Instance | BindingFlags.NonPublic)!
                        .Invoke(form, [0])!;

                    SpectrumRomSet selected = (SpectrumRomSet)type
                        .GetField("selectedRomSet", BindingFlags.Instance | BindingFlags.NonPublic)!
                        .GetValue(form)!;
                    SpectrumMachine machine = form.MachineForTesting;
                    Assert.Equal(SpectrumMemoryPagingModel.Plus2APlus3, selected.PagingModel);
                    Assert.Equal(SpectrumMemoryPagingModel.Plus2APlus3, machine.MemoryPagingModel);
                    Assert.True(restored);
                }
                catch (Exception exception)
                {
                    failure = exception;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            Assert.True(thread.Join(TimeSpan.FromSeconds(15)), "The Quick State profile test timed out.");
            if (failure != null)
                throw new Xunit.Sdk.XunitException(failure.ToString());
        }

        [Theory]
        [InlineData(".SNA")]
        [InlineData(".z80")]
        [InlineData(".szx")]
        [InlineData(".rzx")]
        public void SharedMediaLoader_LoadsEachSupportedFormatAndClearsTheDeck(string extension)
        {
            DirectoryInfo folder = Directory.CreateTempSubdirectory("derby-media-");
            string path = Path.Combine(folder.FullName, "Synthetic fixture" + extension);
            File.WriteAllBytes(path, BuildMediaFixture(extension.ToLowerInvariant()));
            Exception? failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    using var form = new MainForm();
                    const BindingFlags privateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
                    Type type = typeof(MainForm);
                    using var pause = (IDisposable)type.GetMethod("AcquireEmulationPauseLease", privateInstance)!
                        .Invoke(form, null)!;
                    form.MachineForTesting.MountTape(new Tape.MountedTape("Old side.tap",
                        [Tape.TapeBlock.CreatePureTone(100, 3)]), startStopped: true);

                    type.GetMethod("LoadSnapshotOrRecording", privateInstance)!.Invoke(form, [path]);

                    Assert.Equal((byte)0x5A, form.MachineForTesting.PeekMemory(0x4000));
                    Assert.Equal(extension == ".rzx", form.MachineForTesting.HasRzxPlayback);
                    Assert.False(form.MachineForTesting.HasTapeInDeck);
                    Assert.Equal("DERBY - Synthetic fixture", form.Text);
                }
                catch (Exception ex)
                {
                    failure = ex;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            Assert.True(thread.Join(TimeSpan.FromSeconds(15)), "The shared media loader test timed out.");
            File.Delete(path);
            folder.Delete();
            if (failure != null)
                throw new Xunit.Sdk.XunitException(failure.ToString());
        }

        private static byte[] BuildMediaFixture(string extension)
        {
            const int ramSize = 48 * 1024;
            byte[] sna = new byte[27 + ramSize];
            sna[24] = 0x80; // Stack at 8000h, where the SNA stores the resume address.
            sna[27] = 0x5A;
            if (extension == ".sna")
                return sna;
            if (extension == ".z80")
            {
                byte[] z80 = new byte[30 + ramSize];
                z80[7] = 0x80; // Nonzero PC selects the V1 header.
                z80[30] = 0x5A;
                return z80;
            }

            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            if (extension == ".szx")
            {
                writer.Write("ZXST"u8);
                writer.Write(new byte[] { 1, 5, 1, 0 });
                WriteChunk("Z80R"u8, new byte[37]);
                WriteChunk("SPCR"u8, new byte[8]);
                foreach (byte bank in new byte[] { 5, 2, 0 })
                {
                    byte[] page = new byte[3 + 16 * 1024];
                    page[2] = bank;
                    page[3] = 0x5A;
                    WriteChunk("RAMP"u8, page);
                }
            }
            else
            {
                writer.Write("RZX!"u8);
                writer.Write(new byte[] { 0, 13, 0, 0, 0, 0 });
                writer.Write((byte)0x30);
                writer.Write(17 + sna.Length);
                writer.Write(0); // Uncompressed embedded snapshot.
                writer.Write("SNA\0"u8);
                writer.Write(sna.Length);
                writer.Write(sna);
                writer.Write((byte)0x80);
                writer.Write(23);
                writer.Write(1); // One input frame.
                writer.Write(new byte[9]);
                writer.Write((ushort)1);
                writer.Write((ushort)1);
                writer.Write((byte)0xFE);
            }
            return stream.ToArray();

            void WriteChunk(ReadOnlySpan<byte> name, byte[] data)
            {
                writer.Write(name);
                writer.Write(data.Length);
                writer.Write(data);
            }
        }

        [Theory]
        [InlineData(Tape.TapeTransportState.NoTape)]
        [InlineData(Tape.TapeTransportState.Stopped)]
        [InlineData(Tape.TapeTransportState.Ended)]
        public void MountStoppedTape_PreservesGameAndWaitsForPlay(Tape.TapeTransportState initialState)
        {
            DirectoryInfo files = Directory.CreateTempSubdirectory("derby-tape-");
            string tapePath = Path.Combine(files.FullName, "Tape side 2.tap");
            string gamePath = Path.Combine(files.FullName, "Example Game.sna");
            File.WriteAllBytes(tapePath, [0x02, 0x00, 0xFF, 0xFF]);
            File.WriteAllBytes(gamePath, [0x01]);
            Exception? failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    using var form = new MainForm();
                    MethodInfo acquirePauseMethod = typeof(MainForm).GetMethod(
                        "AcquireEmulationPauseLease",
                        BindingFlags.Instance | BindingFlags.NonPublic)!;
                    using var pauseLease = (IDisposable)acquirePauseMethod.Invoke(form, null)!;
                    SpectrumMachine machine = form.MachineForTesting;
                    machine.Reset(SpectrumMachineModel.Spectrum48K);
                    machine.Cpu.Regs.PC = 0x8123;
                    machine.PokeMemory(0x8123, 0xC3); // JP 8123h
                    machine.PokeMemory(0x8124, 0x23);
                    machine.PokeMemory(0x8125, 0x81);
                    machine.PokeMemory(0x9000, 0x5A);
                    if (initialState != Tape.TapeTransportState.NoTape)
                    {
                        machine.MountTape(new Tape.MountedTape("side-1.tzx", [Tape.TapeBlock.CreatePureTone(100, 3)]),
                            startStopped: initialState == Tape.TapeTransportState.Stopped);
                        if (initialState == Tape.TapeTransportState.Ended)
                        {
                            machine.ExecuteFrame();
                            machine.ExecuteFrame();
                        }
                    }
                    Assert.Equal(initialState, machine.TapeTransportState);

                    const BindingFlags privateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
                    Type type = typeof(MainForm);
                    MethodInfo applyTape = type.GetMethod("ApplyTapeFileAction", privateInstance)!;
                    MethodInfo resetScheduler = type.GetMethod("ResetFrameScheduler", privateInstance)!;
                    if (initialState == Tape.TapeTransportState.NoTape)
                    {
                        applyTape.Invoke(form, [TapeFileAction.MountStopped, tapePath]);
                        Assert.Equal("DERBY | Tape: Tape side 2", form.Text);
                        machine.EjectTape();
                        resetScheduler.Invoke(form, null);
                        Assert.Equal("DERBY", form.Text);
                    }
                    type.GetMethod("SetLoadedMediaDisplayName", privateInstance)!.Invoke(form, [gamePath]);
                    var menu = (ToolStripMenuItem)type.GetField("stoppedTapeMenuItem", privateInstance)!.GetValue(form)!;
                    var f5 = (ToolStripMenuItem)type.GetField("tapeTransportMenuItem", privateInstance)!.GetValue(form)!;
                    MethodInfo updateMenu = type.GetMethod("UpdateTapeTransportMenuItem", privateInstance)!;
                    updateMenu.Invoke(form, null);
                    Assert.Equal(initialState == Tape.TapeTransportState.NoTape
                        ? "F7 - Insert Tape..." : "F7 - Change Tape...", menu.Text);
                    Assert.True(menu.Enabled);
                    Assert.Equal(initialState switch
                    {
                        Tape.TapeTransportState.Stopped => "F5 - Play Tape",
                        Tape.TapeTransportState.Ended => "F5 - Tape at End",
                        _ => "F5 - No Tape Inserted"
                    }, f5.Text);
                    Assert.Equal(initialState == Tape.TapeTransportState.Stopped, f5.Enabled);

                    var overlay = (TapeTransportOverlay)type.GetField("tapeTransportOverlay", privateInstance)!.GetValue(form)!;
                    MethodInfo updateOverlay = type.GetMethod("UpdateTapeTransportFeedback", privateInstance)!;
                    const long now = 1;
                    overlay.Show(Tape.TapeTransportState.Playing, markerStop: false, now);
                    Assert.True(overlay.IsVisible(now));
                    applyTape.Invoke(form, [TapeFileAction.MountStopped, tapePath]);
                    Assert.False(overlay.IsVisible(now));
                    updateOverlay.Invoke(form, [now]);
                    Assert.False(overlay.IsVisible(now));
                    Assert.Equal("F7 - Change Tape...", menu.Text);
                    Assert.Equal("F5 - Play Tape", f5.Text);
                    Assert.True(f5.Enabled);
                    const string titleWithTape = "DERBY - Example Game | Tape: Tape side 2";
                    Assert.Equal(titleWithTape, form.Text);

                    Assert.Same(machine, form.MachineForTesting);
                    Assert.Equal(SpectrumMachineModel.Spectrum48K, machine.MachineModel);
                    Assert.Equal((ushort)0x8123, machine.Cpu.Regs.PC);
                    Assert.Equal((byte)0x5A, machine.PeekMemory(0x9000));
                    Assert.Equal(Path.GetFileName(tapePath), machine.MountedTapeName);
                    Assert.Equal(Path.GetFileName(tapePath), machine.CaptureTapeDeck()!.Timeline.FileName);
                    Assert.Equal(Tape.TapeTransportState.Stopped, machine.TapeTransportState);
                    string startingPosition = machine.GetMountedTapeDebugState();
                    for (int i = 0; i < 10; i++)
                        machine.ExecuteFrame();
                    Assert.Equal(Tape.TapeTransportState.Stopped, machine.TapeTransportState);
                    Assert.Equal(startingPosition, machine.GetMountedTapeDebugState());
                    SpectrumMachine.QuickState saved = machine.CaptureQuickState();
                    Assert.Equal(Tape.TapeTransportState.Playing, machine.ToggleTapeTransport());
                    updateOverlay.Invoke(form, [now]);
                    Assert.True(overlay.IsVisible(now));
                    Assert.Equal(Tape.TapeTransportState.Playing, overlay.FeedbackState);
                    Assert.Equal("F5 - Stop Tape", f5.Text);
                    Assert.Equal(Tape.TapeTransportState.Stopped, machine.ToggleTapeTransport());
                    updateOverlay.Invoke(form, [now]);
                    Assert.True(overlay.IsVisible(now));
                    Assert.Equal(Tape.TapeTransportState.Stopped, overlay.FeedbackState);
                    Assert.Equal("F5 - Resume Tape", f5.Text);
                    type.GetMethod("UpdateWindowTitle", privateInstance)!.Invoke(form, null);
                    Assert.Equal(titleWithTape, form.Text);

                    machine.EjectTape();
                    resetScheduler.Invoke(form, null);
                    Assert.Equal("DERBY - Example Game", form.Text);
                    Assert.Equal("F5 - No Tape Inserted", f5.Text);
                    Assert.False(f5.Enabled);
                    machine.RestoreQuickState(saved);
                    resetScheduler.Invoke(form, null);
                    Assert.Equal(titleWithTape, form.Text);
                    Assert.Equal("F5 - Play Tape", f5.Text);

                    if (initialState == Tape.TapeTransportState.NoTape)
                    {
                        applyTape.Invoke(form, [TapeFileAction.StartNewGame, tapePath]);
                        Assert.True(form.MachineForTesting.HasTapeInDeck);
                        Assert.Equal("DERBY - Tape side 2", form.Text);
                    }
                }
                catch (Exception exception)
                {
                    failure = exception;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            Assert.True(thread.Join(TimeSpan.FromSeconds(15)), "The tape replacement test timed out.");
            File.Delete(tapePath);
            File.Delete(gamePath);
            files.Delete();
            if (failure != null)
                throw new Xunit.Sdk.XunitException(failure.ToString());
        }
    }
}
