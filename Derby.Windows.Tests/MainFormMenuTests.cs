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
                            "F5 - Stop / Resume Tape",
                            "---",
                            "F6 - Debugger / Disassembler",
                            "F7 - Quick State Manager...",
                            "---",
                            "F9 - Load .sna Snapshot",
                            "F10 - Load .z80/.szx Snapshot / Play .rzx",
                            "F11 - Mount .tap or .tzx Tape Image",
                            "Shift+F11 - Replace Tape (Preserve Machine)",
                            "---",
                            "F12 - Write Machine Diagnostic Dump",
                            "---",
                            "Joystick",
                            "Poke Manager..."
                        ],
                        labels);

                    ToolStripMenuItem machineMenu = menu.Items
                        .OfType<ToolStripMenuItem>()
                        .Single(item => item.Text == "F3 - Machine");
                    Assert.Equal(
                        ["ZX Spectrum 128K", "ZX Spectrum 48K", "ZX Spectrum +2 (Grey)", "ZX Spectrum +2A"],
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
                        "Sinclair ZX Spectrum 128K (bundled)"
                    ];

                    foreach (string name in expected)
                    {
                        cycle.Invoke(form, null);
                        SpectrumRomSet selected = (SpectrumRomSet)type
                            .GetField("selectedRomSet", BindingFlags.Instance | BindingFlags.NonPublic)!
                            .GetValue(form)!;
                        Assert.Equal(name, selected.Name);
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
                    Spectrum128Machine machine = (Spectrum128Machine)type
                        .GetField("machine", BindingFlags.Instance | BindingFlags.NonPublic)!
                        .GetValue(form)!;
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
        [InlineData(Keys.F9)]
        [InlineData(Keys.F10)]
        [InlineData(Keys.F11)]
        [InlineData(Keys.F12)]
        public void FileAndDiagnosticShortcuts_RequireAnUnmodifiedFunctionKey(Keys key)
        {
            MethodInfo method = typeof(MainForm).GetMethod(
                "IsPlainShortcut",
                BindingFlags.Static | BindingFlags.NonPublic)!;

            Assert.True((bool)method.Invoke(null, [new KeyEventArgs(key), key])!);
            Assert.False((bool)method.Invoke(null, [new KeyEventArgs(key | Keys.Shift), key])!);
            Assert.False((bool)method.Invoke(null, [new KeyEventArgs(key | Keys.Control), key])!);
            Assert.False((bool)method.Invoke(null, [new KeyEventArgs(key | Keys.Alt), key])!);
        }

        [Fact]
        public void TapeReplacementShortcut_RequiresShiftF11()
        {
            MethodInfo method = typeof(MainForm).GetMethod(
                "IsTapeReplacementShortcut",
                BindingFlags.Static | BindingFlags.NonPublic)!;

            Assert.True((bool)method.Invoke(null, [new KeyEventArgs(Keys.F11 | Keys.Shift)])!);
            Assert.False((bool)method.Invoke(null, [new KeyEventArgs(Keys.F11)])!);
            Assert.False((bool)method.Invoke(null, [new KeyEventArgs(Keys.F11 | Keys.Control)])!);
            Assert.False((bool)method.Invoke(null, [new KeyEventArgs(Keys.F11 | Keys.Shift | Keys.Alt)])!);
        }

        [Fact]
        public void ReplaceMountedTape_PreservesRunningMachineState()
        {
            string tapePath = Path.Combine(Path.GetTempPath(), $"replacement-{Guid.NewGuid():N}.tap");
            File.WriteAllBytes(tapePath, [0x02, 0x00, 0xFF, 0xFF]);
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
                    FieldInfo machineField = typeof(MainForm).GetField(
                        "machine",
                        BindingFlags.Instance | BindingFlags.NonPublic)!;
                    var machine = (Spectrum128Machine)machineField.GetValue(form)!;
                    machine.Reset(SpectrumMachineModel.Spectrum48K);
                    machine.Cpu.Regs.PC = 0x8123;
                    machine.PokeMemory(0x9000, 0x5A);
                    machine.MountTape(new Tap.MountedTape("side-1.tzx", Array.Empty<Tap.TapeBlock>()));

                    MethodInfo replaceMethod = typeof(MainForm).GetMethod(
                        "ReplaceMountedTape",
                        BindingFlags.Instance | BindingFlags.NonPublic)!;
                    var result = (Tap.TapMountResult)replaceMethod.Invoke(form, [tapePath])!;

                    Assert.Same(machine, machineField.GetValue(form));
                    Assert.Equal(SpectrumMachineModel.Spectrum48K, machine.MachineModel);
                    Assert.Equal((ushort)0x8123, machine.Cpu.Regs.PC);
                    Assert.Equal((byte)0x5A, machine.PeekMemory(0x9000));
                    Assert.Equal(Path.GetFileName(tapePath), machine.MountedTapeName);
                    Assert.Equal(1, result.TotalBlockCount);
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
            if (failure != null)
                throw new Xunit.Sdk.XunitException(failure.ToString());
        }
    }
}
