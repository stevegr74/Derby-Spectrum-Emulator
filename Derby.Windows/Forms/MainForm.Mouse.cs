using System.Collections.Concurrent;

namespace Derby;

public partial class MainForm
{
    private readonly ConcurrentQueue<(int X, int Y, SpectrumMouseButtons Buttons)> mouseInput = new();
    private SpectrumMouseType selectedMouseType;
    private bool mouseCaptured;
    private bool consumedMouseEscape;
    private bool ignoreCaptureClick;
    private SpectrumMouseButtons hostMouseButtons;
    private SpectrumMouseButtons appliedMouseButtons;
    private Point lastHostMousePosition;

    private ToolStripMenuItem CreateMouseMenu()
    {
        var menu = new ToolStripMenuItem("Mouse");
        var off = new ToolStripMenuItem("Off", null, (_, _) => SelectMouse(SpectrumMouseType.Off)) { Checked = true };
        var amx = new ToolStripMenuItem("AMX Mouse", null, (_, _) => SelectMouse(SpectrumMouseType.Amx));
        var kempston = new ToolStripMenuItem("Kempston Mouse", null, (_, _) => SelectMouse(SpectrumMouseType.Kempston));
        menu.DropDownItems.AddRange([off, amx, kempston]);
        menu.DropDownOpening += (_, _) =>
        {
            off.Checked = selectedMouseType == SpectrumMouseType.Off;
            amx.Checked = selectedMouseType == SpectrumMouseType.Amx;
            kempston.Checked = selectedMouseType == SpectrumMouseType.Kempston;
        };
        screenBox.MouseDown += (_, e) =>
        {
            if (e.Button == MouseButtons.Left && selectedMouseType != SpectrumMouseType.Off && !mouseCaptured)
                CaptureSpectrumMouse();
        };
        return menu;
    }

    private void SelectMouse(SpectrumMouseType type)
    {
        if (selectedMouseType == type)
            return;
        ReleaseSpectrumMouse();
        selectedMouseType = type;
        lock (machineLock)
            machine.SetMouseType(type);
    }

    private void CaptureSpectrumMouse()
    {
        if (!ContainsFocus && Form.ActiveForm != this)
            return;
        if (!WindowsMouseInput.SetRegistered(Handle, true))
        {
            MessageBox.Show(this, "Windows could not capture mouse input.", "Mouse",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        ignoreCaptureClick = true;
        lastHostMousePosition = Cursor.Position;
        mouseCaptured = true;
        Cursor.Clip = screenBox.RectangleToScreen(screenBox.ClientRectangle);
        Cursor.Hide();
    }

    private void ReleaseSpectrumMouse()
    {
        if (!mouseCaptured)
            return;
        mouseCaptured = false;
        WindowsMouseInput.SetRegistered(IntPtr.Zero, false);
        Cursor.Clip = Rectangle.Empty;
        Cursor.Show();
        hostMouseButtons = SpectrumMouseButtons.None;
        mouseInput.Clear();
        lock (machineLock)
        {
            appliedMouseButtons = SpectrumMouseButtons.None;
            machine.ReleaseMouseInput();
        }
    }

    private bool TryConsumeMouseEscape(Keys key, bool down)
    {
        if (key != Keys.Escape || (!mouseCaptured && !consumedMouseEscape))
            return false;
        consumedMouseEscape = down;
        ReleaseSpectrumMouse();
        return true;
    }

    protected override void WndProc(ref Message message)
    {
        const int WmInput = 0x00FF;
        if (message.Msg == WmInput && mouseCaptured && WindowsMouseInput.TryRead(message.LParam, out var mouse))
            QueueSpectrumMouseInput(mouse);
        base.WndProc(ref message);
    }

    private void QueueSpectrumMouseInput(WindowsMouseInput.RawMouse mouse)
    {
        const ushort LeftDown = 0x0001, LeftUp = 0x0002, RightDown = 0x0004, RightUp = 0x0008,
            MiddleDown = 0x0010, MiddleUp = 0x0020;
        SpectrumMouseButtons before = hostMouseButtons;
        if (!ignoreCaptureClick && (mouse.ButtonFlags & LeftDown) != 0)
            hostMouseButtons |= SpectrumMouseButtons.Left;
        if ((mouse.ButtonFlags & LeftUp) != 0)
        {
            ignoreCaptureClick = false;
            hostMouseButtons &= ~SpectrumMouseButtons.Left;
        }
        if ((mouse.ButtonFlags & RightDown) != 0)
            hostMouseButtons |= SpectrumMouseButtons.Right;
        if ((mouse.ButtonFlags & RightUp) != 0)
            hostMouseButtons &= ~SpectrumMouseButtons.Right;
        if ((mouse.ButtonFlags & MiddleDown) != 0)
            hostMouseButtons |= SpectrumMouseButtons.Middle;
        if ((mouse.ButtonFlags & MiddleUp) != 0)
            hostMouseButtons &= ~SpectrumMouseButtons.Middle;

        int x = mouse.X, y = mouse.Y;
        if ((mouse.Flags & 1) != 0)
        {
            Rectangle desktop = (mouse.Flags & 2) != 0
                ? SystemInformation.VirtualScreen : Screen.PrimaryScreen!.Bounds;
            var position = new Point(desktop.Left + (int)((long)x * desktop.Width / 65535),
                desktop.Top + (int)((long)y * desktop.Height / 65535));
            x = position.X - lastHostMousePosition.X;
            y = position.Y - lastHostMousePosition.Y;
            lastHostMousePosition = position;
        }
        else
        {
            lastHostMousePosition = Cursor.Position;
        }
        if (x != 0 || y != 0 || before != hostMouseButtons)
            mouseInput.Enqueue((x, -y, hostMouseButtons));
    }

    private void DrainSpectrumMouseInput(SpectrumMachine activeMachine)
    {
        while (mouseInput.TryDequeue(out var input))
        {
            activeMachine.UpdateMouse(input.X, input.Y, input.Buttons);
            // Give the guest an execution slice between button edges, even for a fast tap.
            if (appliedMouseButtons != input.Buttons)
            {
                appliedMouseButtons = input.Buttons;
                break;
            }
        }
    }

    private static void DrawMouseCaptureOverlay(Bitmap bitmap, int scale)
    {
        using Graphics graphics = Graphics.FromImage(bitmap);
        using var background = new SolidBrush(Color.FromArgb(196, 8, 12, 14));
        using var ink = new SolidBrush(Color.FromArgb(225, 60, 220, 190));
        using var font = new Font(FontFamily.GenericMonospace, 9 * scale, FontStyle.Bold, GraphicsUnit.Pixel);
        const string caption = "MOUSE CAPTURED | ESC RELEASES";
        SizeF size = graphics.MeasureString(caption, font);
        float x = bitmap.Width - size.Width - 8 * scale;
        float y = bitmap.Height - size.Height - 8 * scale;
        graphics.FillRectangle(background, x - 3 * scale, y - 2 * scale, size.Width + 6 * scale, size.Height + 4 * scale);
        graphics.DrawString(caption, font, ink, x, y);
    }
}
