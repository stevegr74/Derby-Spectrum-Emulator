using System.Drawing;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Windows.Forms;
using Xunit;

namespace Derby.Tests;

public class OnScreenKeyboardTests
{
    [Theory]
    [InlineData(false, 40)]
    [InlineData(true, 58)]
    public void LayoutHasEveryMatrixContactAndSupportsHoldAndRelease(bool use128K, int count)
    {
        var layout = use128K ? OnScreenKeyboardLayout.Spectrum128K : OnScreenKeyboardLayout.Spectrum48K;
        var keys = OnScreenKeyboardLayouts.GetRows(layout).SelectMany(row => row.Keys).ToArray();
        Assert.Equal(count, keys.Length);
        Assert.Equal(40, keys.SelectMany(key => key.CreateStroke(key.Shift).Positions).Distinct().Count());

        WithMachine((machine, input) =>
        {
            OnScreenKeyStroke stroke = keys.Single(key => key.Legend == "A").CreateStroke(OnScreenShift.None);
            Stroke(input, machine, stroke, true);
            for (ulong frame = 1; frame <= 5; frame++)
            {
                input.Advance(machine.GetKeyboardRowScanCount, frame * (ulong)machine.FrameTStates, machine.SetKey);
                Assert.Equal(0, machine.DebugReadPort(0xFDFE) & 1);
            }
            Stroke(input, machine, stroke, false);
            Assert.Equal(1, machine.DebugReadPort(0xFDFE) & 1);
        }, use128K);
    }

    [Theory]
    [InlineData("TRUE\nVIDEO", 0xFEFE, 1, 0xF7FE, 4)]
    [InlineData("INV\nVIDEO", 0xFEFE, 1, 0xF7FE, 8)]
    [InlineData("DELETE", 0xFEFE, 1, 0xEFFE, 1)]
    [InlineData("GRAPH", 0xFEFE, 1, 0xEFFE, 2)]
    [InlineData("EDIT", 0xFEFE, 1, 0xF7FE, 1)]
    [InlineData("CAPS\nLOCK", 0xFEFE, 1, 0xF7FE, 2)]
    [InlineData("EXTEND\nMODE", 0xFEFE, 1, 0x7FFE, 2)]
    [InlineData("BREAK", 0xFEFE, 1, 0x7FFE, 1)]
    [InlineData("←", 0xFEFE, 1, 0xF7FE, 16)]
    [InlineData("→", 0xFEFE, 1, 0xEFFE, 4)]
    [InlineData("↑", 0xFEFE, 1, 0xEFFE, 8)]
    [InlineData("↓", 0xFEFE, 1, 0xEFFE, 16)]
    [InlineData(",", 0x7FFE, 2, 0x7FFE, 8)]
    [InlineData(".", 0x7FFE, 2, 0x7FFE, 4)]
    [InlineData(";", 0x7FFE, 2, 0xDFFE, 2)]
    [InlineData("\"", 0x7FFE, 2, 0xDFFE, 1)]
    public void Dedicated128KKeysUseHardwareChords(string legend, int port1, int bit1, int port2, int bit2)
    {
        var key = OnScreenKeyboardLayouts.GetRows(OnScreenKeyboardLayout.Spectrum128K)
            .SelectMany(row => row.Keys).Single(key => key.Legend == legend);
        WithMachine((machine, input) =>
        {
            OnScreenKeyStroke stroke = key.CreateStroke(OnScreenShift.None);
            Assert.Equal(2, stroke.Positions.Length);
            Stroke(input, machine, stroke, true);
            Stroke(input, machine, stroke, false);

            Assert.Equal(0, machine.DebugReadPort((ushort)port1) & bit1);
            if (port1 != port2)
                input.Advance(machine.GetKeyboardRowScanCount, 1, machine.SetKey);
            Assert.Equal(0, machine.DebugReadPort((ushort)port2) & bit2);
            input.Advance(machine.GetKeyboardRowScanCount, 2, machine.SetKey);
            Assert.Equal(bit1, machine.DebugReadPort((ushort)port1) & bit1);
            Assert.Equal(bit2, machine.DebugReadPort((ushort)port2) & bit2);
        });
    }

    [Fact]
    public void PointerCancellationDoesNotReleaseThePhysicalOwnerOrWaitForAScan()
    {
        WithMachine((machine, input) =>
        {
            input.HandleHostKeyEvent(Keys.A, true, machine.GetKeyboardRowScanCount, 0,
                (ulong)machine.FrameTStates, machine.SetKey);
            var stroke = new OnScreenKeyStroke([Keys.ShiftKey, Keys.A]);
            Stroke(input, machine, stroke, true);
            input.CancelOnScreenKeys(machine.SetKey);
            Assert.Equal(0, machine.DebugReadPort(0xFDFE) & 1);
            Assert.Equal(1, machine.DebugReadPort(0xFEFE) & 1);
            input.HandleHostKeyEvent(Keys.A, false, machine.GetKeyboardRowScanCount, 1,
                (ulong)machine.FrameTStates, machine.SetKey);
            Assert.Equal(1, machine.DebugReadPort(0xFDFE) & 1);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FormWiresPointerHoldReleaseAndCancellation(bool use128K)
    {
        OnSta(() => WithMachine((machine, input) =>
        {
            var layout = use128K ? OnScreenKeyboardLayout.Spectrum128K : OnScreenKeyboardLayout.Spectrum48K;
            using var form = new OnScreenKeyboardForm(layout,
                (stroke, down) => Stroke(input, machine, stroke, down),
                () => input.CancelOnScreenKeys(machine.SetKey));
            form.Show();
            Application.DoEvents();

            Mouse(Find(form, "CAPS SHIFT"), "OnMouseDown");
            Control letter = Find(form, "A");
            Mouse(letter, "OnMouseDown");
            Assert.True(letter.Capture);
            Assert.Equal(0, machine.DebugReadPort(0xFEFE) & 1);
            Assert.Equal(0, machine.DebugReadPort(0xFDFE) & 1);
            Mouse(letter, "OnMouseUp", x: -10);
            Assert.Equal(1, machine.DebugReadPort(0xFEFE) & 1);
            Assert.Equal(1, machine.DebugReadPort(0xFDFE) & 1);

            Mouse(letter, "OnMouseDown");
            Assert.Equal(0, machine.DebugReadPort(0xFDFE) & 1);
            Assert.Equal(1, machine.DebugReadPort(0xFEFE) & 1); // The latch was consumed.
            letter.Capture = false;
            Assert.Equal(1, machine.DebugReadPort(0xFDFE) & 1);

            Mouse(letter, "OnMouseDown");
            typeof(Form).GetMethod("OnDeactivate", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(form, [EventArgs.Empty]);
            Assert.Equal(1, machine.DebugReadPort(0xFDFE) & 1);

            Mouse(letter, "OnMouseDown");
            form.SelectLayout(use128K ? OnScreenKeyboardLayout.Spectrum48K : OnScreenKeyboardLayout.Spectrum128K);
            Assert.Equal(1, machine.DebugReadPort(0xFDFE) & 1);
            Assert.NotEqual(layout, form.LayoutChoice);

            Mouse(Find(form, "A"), "OnMouseDown");
            form.Close();
            Assert.Equal(1, machine.DebugReadPort(0xFDFE) & 1);
        }, use128K));
    }

    [Fact]
    public void LayoutShortcutsDoNotConsumeSpectrumKeys()
    {
        OnSta(() =>
        {
            var routed = new List<(Keys Key, bool Down)>();
            var filter = new SpectrumKeyboardMessageFilter(() => true, (key, down) => routed.Add((key, down)));
            using var form = new OnScreenKeyboardForm(OnScreenKeyboardLayout.Spectrum128K, (_, _) => { }, () => { });
            form.Show();
            Application.DoEvents();
            var button = Descendants(form).OfType<Button>().First();
            foreach ((Keys key, OnScreenKeyboardLayout layout) in new[]
            {
                (Keys.PageUp, OnScreenKeyboardLayout.Spectrum48K),
                (Keys.PageDown, OnScreenKeyboardLayout.Spectrum128K)
            })
            {
                var message = Message.Create(button.Handle, SpectrumKeyboardMessageFilter.KeyDownMessage, (nint)key, 0);
                Assert.False(filter.PreFilterMessage(ref message));
                Assert.True(button.PreProcessMessage(ref message));
                Assert.Equal(layout, form.LayoutChoice);
                Assert.Equal(layout == OnScreenKeyboardLayout.Spectrum48K ? "48K rubber-key" : "128K",
                    form.ActiveControl?.Text);
                Assert.Empty(routed);
            }

            foreach (Keys key in new[] { Keys.A, Keys.Tab, Keys.Left, Keys.Enter, Keys.Space, Keys.Escape })
            {
                var down = Message.Create(button.Handle, SpectrumKeyboardMessageFilter.KeyDownMessage, (nint)key, 0);
                var up = Message.Create(button.Handle, SpectrumKeyboardMessageFilter.KeyUpMessage, (nint)key, 0);
                Assert.True(filter.PreFilterMessage(ref down));
                Assert.True(filter.PreFilterMessage(ref up));
            }
            Assert.Equal(12, routed.Count);
            Assert.Equal(OnScreenKeyboardLayout.Spectrum128K, form.LayoutChoice);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MinimumSizeFitsControlsAndLegendsAtDesktopDpiAndScaledDpis(bool use128K)
    {
        OnSta(() =>
        {
            var layout = use128K ? OnScreenKeyboardLayout.Spectrum128K : OnScreenKeyboardLayout.Spectrum48K;
            using var form = new OnScreenKeyboardForm(layout, (_, _) => { }, () => { });
            form.Show();
            form.Size = form.MinimumSize;
            Application.DoEvents();
            Assert.Equal(form.MinimumSize, form.Size);

            foreach (Control control in Descendants(form).Where(control => control is Label or Button))
            {
                Assert.True(control.Parent!.ClientRectangle.Contains(control.Bounds), $"{control.Text} is out of bounds");
                Assert.True(control.PreferredSize.Width <= control.Width && control.PreferredSize.Height <= control.Height,
                    $"{control.Text} clips at {form.DeviceDpi} DPI");
            }

            Control panel = Find(form, "A").Parent!;
            var rows = OnScreenKeyboardLayouts.GetRows(layout);
            foreach (int dpi in new[] { 96, 120, 144, 192, form.DeviceDpi }.Distinct())
            {
                var panelSize = new Size(panel.Width * dpi / form.DeviceDpi, panel.Height * dpi / form.DeviceDpi);
                using var bitmap = new Bitmap(panelSize.Width, panelSize.Height);
                bitmap.SetResolution(dpi, dpi);
                using var graphics = Graphics.FromImage(bitmap);
                foreach ((int row, int column, Rectangle bounds) in OnScreenKeyboardForm.GetKeyBounds(rows, panelSize, dpi))
                {
                    OnScreenKey key = rows[row].Keys[column];
                    Assert.True(new Rectangle(Point.Empty, panelSize).Contains(bounds));
                    var metrics = OnScreenKeyboardForm.PaintLegends(graphics, key, bounds, dpi);
                    Assert.True(metrics.Fits && metrics.MainPt >= 7 && metrics.DetailPt >= 6,
                        $"{key.Legend}/{key.Keyword}/{key.Symbol} clips or becomes too small at {dpi} DPI " +
                        $"({bounds.Size}; font sizes: {metrics.MainPt}, {metrics.DetailPt})");
                }
            }
        });
    }

    [Fact]
    public void SettingsRoundTripAndKeepKeyboardBesideTheMainWindowWhenThereIsRoom()
    {
        string path = Path.Combine(Path.GetTempPath(), $"derby-keyboard-{Guid.NewGuid():N}.json");
        try
        {
            Rectangle workArea = new(0, 0, 1280, 720);
            Rectangle owner = new(25, 50, 640, 510);
            Rectangle bounds = OnScreenKeyboardSettings.Place(null, new Size(600, 320),
                new Size(590, 300), owner, workArea);
            Assert.False(bounds.IntersectsWith(owner));
            Assert.True(workArea.Contains(bounds));

            var settings = new OnScreenKeyboardSettings(OnScreenKeyboardLayout.Spectrum48K, bounds);
            settings.Save(path);
            Assert.Equal(settings, OnScreenKeyboardSettings.Load(path));
            Rectangle relocated = OnScreenKeyboardSettings.Place(new Rectangle(5000, 5000, 600, 320),
                new Size(600, 320), new Size(590, 300), owner, workArea);
            Assert.Equal(bounds, relocated);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static Control Find(Control root, string name) =>
        Descendants(root).First(control => control.AccessibleName == name);

    private static IEnumerable<Control> Descendants(Control root) =>
        root.Controls.Cast<Control>().SelectMany(control => Descendants(control).Prepend(control));

    private static void Mouse(Control control, string method, int x = 5) =>
        typeof(Control).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(control, [new MouseEventArgs(MouseButtons.Left, 1, x, 5, 0)]);

    private static void Stroke(SpectrumKeyboardInputController input, SpectrumMachine machine, OnScreenKeyStroke stroke, bool down) =>
        input.HandleOnScreenKeyEvent(stroke, down, machine.GetKeyboardRowScanCount, 0,
            (ulong)machine.FrameTStates, machine.SetKey);

    private static void WithMachine(Action<SpectrumMachine, SpectrumKeyboardInputController> test, bool use128K = true)
    {
        string roms = Path.Combine(AppContext.BaseDirectory, "ROMs");
        var machine = new SpectrumMachine(use128K ? SpectrumRomSet.Bundled128K(roms) : SpectrumRomSet.Bundled48K(roms));
        test(machine, new SpectrumKeyboardInputController());
    }

    private static void OnSta(Action test)
    {
        ExceptionDispatchInfo? failure = null;
        var thread = new Thread(() =>
        {
            try { test(); }
            catch (Exception exception) { failure = ExceptionDispatchInfo.Capture(exception); }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(15)), "The keyboard interaction test timed out.");
        failure?.Throw();
    }
}
