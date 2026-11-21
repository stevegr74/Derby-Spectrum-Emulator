using System.Collections.Concurrent;

namespace Derby;

public partial class MainForm
{
    private readonly ConcurrentQueue<(int X, int Y, SpectrumMouseButtons Buttons)> mouseInput = new();
    private readonly CapturedPointerMotion pointerMotion = new();
    private SpectrumMouseType selectedMouseType;
    private bool mouseCaptured;
    private bool rawMouseRegistered;
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
            else if (mouseCaptured)
                UpdateCapturedMouseButton(e.Button, down: true);
        };
        screenBox.MouseUp += (_, e) =>
        {
            if (mouseCaptured)
                UpdateCapturedMouseButton(e.Button, down: false);
        };
        screenBox.MouseMove += (_, _) => QueueWindowsPointerMotion();
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
        rawMouseRegistered = WindowsMouseInput.SetRegistered(Handle, true);
        ignoreCaptureClick = true;
        lastHostMousePosition = Cursor.Position;
        pointerMotion.Reset(lastHostMousePosition);
        mouseCaptured = true;
        Cursor.Clip = screenBox.RectangleToScreen(screenBox.ClientRectangle);
        Cursor.Hide();
    }

    private void ReleaseSpectrumMouse()
    {
        if (!mouseCaptured)
            return;
        mouseCaptured = false;
        if (rawMouseRegistered)
            WindowsMouseInput.SetRegistered(IntPtr.Zero, false);
        rawMouseRegistered = false;
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
        if (message.Msg == WmInput && mouseCaptured && rawMouseRegistered &&
            WindowsMouseInput.TryRead(message.LParam, out var mouse, out IntPtr device) && device != IntPtr.Zero)
            QueueRawMouseMotion(mouse);
        base.WndProc(ref message);
    }

    private void UpdateCapturedMouseButton(MouseButtons button, bool down)
    {
        SpectrumMouseButtons mapped = button switch
        {
            MouseButtons.Left => SpectrumMouseButtons.Left,
            MouseButtons.Right => SpectrumMouseButtons.Right,
            MouseButtons.Middle => SpectrumMouseButtons.Middle,
            _ => SpectrumMouseButtons.None
        };
        if (mapped == SpectrumMouseButtons.None)
            return;
        QueueWindowsPointerMotion();
        if (ignoreCaptureClick && mapped == SpectrumMouseButtons.Left)
        {
            if (!down)
                ignoreCaptureClick = false;
            return;
        }

        SpectrumMouseButtons before = hostMouseButtons;
        hostMouseButtons = down ? before | mapped : before & ~mapped;
        if (before != hostMouseButtons)
            mouseInput.Enqueue((0, 0, hostMouseButtons));
    }

    private void QueueRawMouseMotion(WindowsMouseInput.RawMouse mouse)
    {
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
        if ((x != 0 || y != 0) && pointerMotion.TryUseRawMotion())
            mouseInput.Enqueue((x, -y, hostMouseButtons));
    }

    private void QueueWindowsPointerMotion()
    {
        if (!mouseCaptured)
            return;

        Point cursor = Cursor.Position;
        int scale = SpectrumDisplayModes.GetLayout(displayMode).Scale;
        Point delta = pointerMotion.UseWindowsPointer(cursor, scale);
        if (!pointerMotion.UsesWindowsPointer)
            return;
        lastHostMousePosition = cursor;
        if (!delta.IsEmpty)
            mouseInput.Enqueue((delta.X, -delta.Y, hostMouseButtons));

        Rectangle area = screenBox.RectangleToScreen(screenBox.ClientRectangle);
        Rectangle inner = Rectangle.Inflate(area, -12, -12);
        if (area.Contains(cursor) && !inner.Contains(cursor))
        {
            Cursor.Position = new Point(area.Left + area.Width / 2, area.Top + area.Height / 2);
            lastHostMousePosition = Cursor.Position;
            pointerMotion.Rebase(lastHostMousePosition);
        }
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
