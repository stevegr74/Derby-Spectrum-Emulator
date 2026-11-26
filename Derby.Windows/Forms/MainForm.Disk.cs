using Derby.Disk;

namespace Derby;

public partial class MainForm
{
    private static readonly Guid PlusDFirmwarePickerId = new("ff1ce3df-f3b4-4e45-a5c3-bacdf4592724");
    private readonly PlusDFirmwareSettings plusDFirmwareSettings = new();

    private void OpenDiskDrive()
    {
        using IDisposable pauseLease = AcquireEmulationPauseLease();
        using var dialog = new Form
        {
            Text = "Disk Drive",
            AutoScaleMode = AutoScaleMode.Dpi,
            ClientSize = new Size(445, 196),
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            ShowInTaskbar = false,
            StartPosition = FormStartPosition.CenterParent
        };
        EmulatorUiStyle.ApplyForm(dialog);
        var title = EmulatorUiStyle.CreateTitle("DISK DRIVE");
        title.Location = new Point(18, 14);
        var subtitle = EmulatorUiStyle.CreateSubtitle("READ-ONLY  |  EMULATION PAUSED", accent: true);
        subtitle.Location = new Point(18, 44);
        var status = new Label
        {
            Location = new Point(18, 68), Size = new Size(409, 37), AutoEllipsis = true,
            BackColor = EmulatorUiStyle.Panel, TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(8, 0, 8, 0)
        };
        var instruction = new Label { Location = new Point(18, 112), Size = new Size(409, 35) };
        var insert = new Button { Location = new Point(18, 150), Size = new Size(120, 30) };
        var eject = new Button { Text = "Eject", Location = new Point(146, 150), Size = new Size(90, 30) };
        var close = new Button { Text = "Close", DialogResult = DialogResult.Cancel, Location = new Point(337, 150), Size = new Size(90, 30) };
        EmulatorUiStyle.StyleButton(insert, primary: true);
        EmulatorUiStyle.StyleButton(eject);
        EmulatorUiStyle.StyleButton(close);
        dialog.CancelButton = close;
        dialog.Controls.AddRange([title, subtitle, status, instruction, insert, eject, close]);
        insert.Click += (_, _) =>
        {
            using var picker = new OpenFileDialog
            {
                Title = "Choose Disk Image",
                Filter = "Disk images (*.dsk; *.mgt)|*.dsk;*.mgt|+3 disks (*.dsk)|*.dsk|+D disks (*.mgt)|*.mgt",
                CheckFileExists = true
            };
            if (picker.ShowDialog(dialog) != DialogResult.OK)
                return;
            try
            {
                switch (Path.GetExtension(picker.FileName).ToLowerInvariant())
                {
                    case ".dsk":
                        InsertPlus3FromDialog(dialog, picker.FileName);
                        break;
                    case ".mgt":
                        InsertPlusDFromDialog(dialog, picker.FileName);
                        break;
                    default:
                        throw new InvalidDataException("Select a +3 .dsk or a +D .mgt disk image.");
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
                if (machine.HasPlusD)
                    machine.EjectPlusDDisk();
                else
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
            bool plusD = machine.HasPlusD;
            bool plus3 = machine.HasPlus3DiskDrive;
            string? name = plusD ? machine.MountedPlusDDiskName : machine.MountedDiskName;
            title.Text = plusD ? "+D DRIVE 1" : plus3 ? "+3 DRIVE A:" : "DISK DRIVE";
            status.Text = name ?? "No disk inserted";
            instruction.Text = plusD
                ? selectedRomSet.Model == SpectrumMachineModel.Spectrum48K
                    ? "At the BASIC prompt, press R for RUN, then Enter."
                    : "To boot a system disk, choose 128 BASIC and type RUN."
                : plus3 ? "To boot, choose Loader in the +3 menu."
                : "Choose a +3 disk (.dsk) or a +D disk (.mgt).";
            insert.Text = name == null ? "Insert..." : "Replace...";
            eject.Enabled = name != null;
        }
    }

    private void InsertPlus3FromDialog(IWin32Window owner, string path)
    {
        DskImage image = DskImage.Load(path);
        if (!machine.HasPlus3DiskDrive)
        {
            if (!ConfirmDiskReset(owner, "Switch to ZX Spectrum +3 and reset the current machine?"))
                return;
            ApplyRomSet(bundledPlus3RomSet);
        }
        lock (machineLock)
        {
            machine.InsertDisk(image, Path.GetFileName(path));
            presentationState.Publish(machine);
        }
    }

    private void InsertPlusDFromDialog(IWin32Window owner, string path)
    {
        MgtImage image = MgtImage.Load(path);
        byte[]? firmware = null;
        if (!machine.HasPlusD)
        {
            firmware = plusDFirmwareSettings.TryLoad();
            string? newFirmwarePath = null;
            if (firmware == null)
            {
                using var picker = new OpenFileDialog
                {
                    Title = "Select the 8 KB +D ROM",
                    Filter = "ROM files (*.rom; *.bin)|*.rom;*.bin|All files (*.*)|*.*",
                    ClientGuid = PlusDFirmwarePickerId,
                    CheckFileExists = true
                };
                if (picker.ShowDialog(owner) != DialogResult.OK)
                    return;
                newFirmwarePath = picker.FileName;
                firmware = PlusDFirmwareSettings.ReadFirmware(newFirmwarePath);
            }

            bool incompatible = selectedRomSet.PagingModel == SpectrumMemoryPagingModel.Plus2APlus3;
            string prompt = incompatible
                ? "Switch to ZX Spectrum 128K, attach +D, and reset the current machine?"
                : "Attach the +D interface and reset the current machine?";
            if (!ConfirmDiskReset(owner, prompt))
                return;

            if (newFirmwarePath != null)
                plusDFirmwareSettings.Remember(newFirmwarePath);
            ApplyRomSet(incompatible ? bundled128RomSet : selectedRomSet);
        }

        lock (machineLock)
        {
            if (firmware != null)
                machine.AttachPlusD(firmware);
            machine.InsertPlusDDisk(image, Path.GetFileName(path));
            presentationState.Publish(machine);
        }
    }

    private static bool ConfirmDiskReset(IWin32Window owner, string prompt) =>
        MessageBox.Show(owner, prompt, "Switch Disk Interface", MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.Yes;

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
