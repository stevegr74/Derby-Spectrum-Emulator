using Derby.Disk;

namespace Derby;

public partial class MainForm
{
    private void OpenDiskDrive()
    {
        using IDisposable pauseLease = AcquireEmulationPauseLease();
        using var dialog = new Form
        {
            Text = "+3 Disk Drive",
            AutoScaleMode = AutoScaleMode.Dpi,
            ClientSize = new Size(445, 165),
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            ShowInTaskbar = false,
            StartPosition = FormStartPosition.CenterParent
        };
        EmulatorUiStyle.ApplyForm(dialog);
        var title = EmulatorUiStyle.CreateTitle("DRIVE A:");
        title.Location = new Point(18, 14);
        var subtitle = EmulatorUiStyle.CreateSubtitle("READ-ONLY  |  EMULATION PAUSED", accent: true);
        subtitle.Location = new Point(18, 44);
        var status = new Label
        {
            Location = new Point(18, 68), Size = new Size(409, 37), AutoEllipsis = true,
            BackColor = EmulatorUiStyle.Panel, TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(8, 0, 8, 0)
        };
        var insert = new Button { Location = new Point(18, 118), Size = new Size(120, 30) };
        var eject = new Button { Text = "Eject", Location = new Point(146, 118), Size = new Size(90, 30) };
        var close = new Button { Text = "Close", DialogResult = DialogResult.Cancel, Location = new Point(337, 118), Size = new Size(90, 30) };
        EmulatorUiStyle.StyleButton(insert, primary: true);
        EmulatorUiStyle.StyleButton(eject);
        EmulatorUiStyle.StyleButton(close);
        dialog.CancelButton = close;
        dialog.Controls.AddRange([title, subtitle, status, insert, eject, close]);
        insert.Click += (_, _) =>
        {
            using var picker = new OpenFileDialog
            {
                Title = "Insert +3 Disk Image", Filter = "Disk images (*.dsk)|*.dsk", CheckFileExists = true
            };
            if (picker.ShowDialog(dialog) != DialogResult.OK)
                return;
            try
            {
                DskImage image = DskImage.Load(picker.FileName);
                if (!machine.HasPlus3DiskDrive)
                {
                    if (MessageBox.Show(dialog, "Switch to ZX Spectrum +3 and reset the current machine?",
                        "Switch Machine", MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
                        MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                        return;
                    ApplyRomSet(bundledPlus3RomSet);
                }
                lock (machineLock)
                {
                    machine.InsertDisk(image, Path.GetFileName(picker.FileName));
                    presentationState.Publish(machine);
                }
                RefreshDriveStatus();
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or InvalidOperationException)
            {
                MessageBox.Show(dialog, ex.Message, "Disk Insert Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };
        eject.Click += (_, _) =>
        {
            lock (machineLock)
            {
                machine.EjectDisk();
                presentationState.Publish(machine);
            }
            RefreshDriveStatus();
        };
        RefreshDriveStatus();
        dialog.ShowDialog(this);
        PresentCurrentMachineFrame(frameClock.ElapsedTicks, force: true);
        screenBox.Focus();

        void RefreshDriveStatus()
        {
            string? name = machine.MountedDiskName;
            status.Text = !machine.HasPlus3DiskDrive ? "A +3 is required to insert a disk." : name ?? "No disk inserted";
            insert.Text = name == null ? "Insert..." : "Replace...";
            eject.Enabled = name != null;
        }
    }

    private static void DrawDiskActivity(Bitmap bitmap, int scale)
    {
        using Graphics graphics = Graphics.FromImage(bitmap);
        using var background = new SolidBrush(Color.FromArgb(166, 8, 12, 14));
        using var ink = new SolidBrush(Color.FromArgb(225, 60, 220, 190));
        using var font = new Font(FontFamily.GenericMonospace, 10 * scale, FontStyle.Bold, GraphicsUnit.Pixel);
        int y = bitmap.Height - 21 * scale;
        graphics.FillRectangle(background, 7 * scale, y, 23 * scale, 15 * scale);
        graphics.DrawString("A:", font, ink, 11 * scale, y + 2 * scale);
    }
}
