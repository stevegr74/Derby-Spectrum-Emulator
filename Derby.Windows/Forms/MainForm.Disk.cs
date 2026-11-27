using Derby.Disk;

namespace Derby;

public partial class MainForm
{
    private static readonly Guid PlusDFirmwarePickerId = new("ff1ce3df-f3b4-4e45-a5c3-bacdf4592724");
    private readonly PlusDFirmwareSettings plusDFirmwareSettings = new();

    private void OpenDiskDrive() => ShowDiskDrive();

    private void ShowDiskDrive(string? pendingDisk = null)
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
        int actionsY = pendingDisk == null ? 150 : 177;
        if (pendingDisk != null)
            dialog.ClientSize = new Size(445, 223);
        var selected = new Label
        {
            Location = new Point(18, 92), Size = new Size(409, 23), AutoEllipsis = true,
            Visible = pendingDisk != null, ForeColor = EmulatorUiStyle.Muted
        };
        if (pendingDisk != null)
        {
            instruction.Top += 27;
            status.Height = 22;
        }
        var insert = new Button { Name = "DiskInsert", Location = new Point(18, actionsY), Size = new Size(120, 30) };
        var eject = new Button { Text = "Eject", Location = new Point(146, actionsY), Size = new Size(90, 30) };
        var close = new Button { Text = "Close", DialogResult = DialogResult.Cancel, Location = new Point(337, actionsY), Size = new Size(90, 30) };
        EmulatorUiStyle.StyleButton(insert, primary: true);
        EmulatorUiStyle.StyleButton(eject);
        EmulatorUiStyle.StyleButton(close);
        dialog.CancelButton = close;
        dialog.Controls.AddRange([title, subtitle, status, selected, instruction, insert, eject, close]);
        insert.Click += (_, _) =>
        {
            string? path = pendingDisk;
            if (path == null)
            {
                using var picker = new OpenFileDialog
                {
                    Title = "Choose Disk Image",
                    Filter = "Disk images (*.dsk; *.mgt)|*.dsk;*.mgt|+3 disks (*.dsk)|*.dsk|+D disks (*.mgt)|*.mgt",
                    CheckFileExists = true
                };
                if (picker.ShowDialog(dialog) != DialogResult.OK)
                    return;
                path = picker.FileName;
            }
            try
            {
                string extension = Path.GetExtension(path).ToLowerInvariant();
                bool replacing = extension == ".mgt" ? machine.MountedPlusDDiskName != null : machine.MountedDiskName != null;
                bool inserted = extension switch
                {
                    ".dsk" => InsertPlus3FromDialog(dialog, path),
                    ".mgt" => InsertPlusDFromDialog(dialog, path),
                    _ => throw new InvalidDataException("Select a +3 .dsk or a +D .mgt disk image.")
                };
                if (!inserted)
                    return;
                if (!replacing)
                    gameLibrary.Value.Remember(path);
                pendingDisk = null;
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
            selected.Text = pendingDisk == null ? string.Empty : $"Selected: {Path.GetFileName(pendingDisk)}";
            selected.Visible = pendingDisk != null;
            bool hasTargetDisk = string.Equals(Path.GetExtension(pendingDisk), ".mgt", StringComparison.OrdinalIgnoreCase)
                ? machine.MountedPlusDDiskName != null : machine.MountedDiskName != null;
            insert.Text = pendingDisk != null ? hasTargetDisk ? "Replace selected" : "Insert selected"
                : name == null ? "Insert..." : "Replace...";
            eject.Enabled = name != null;
        }
    }

    private bool InsertPlus3FromDialog(IWin32Window owner, string path)
    {
        DskImage image = DskImage.Load(path);
        if (!machine.HasPlus3DiskDrive)
        {
            if (!ConfirmDiskReset(owner, "Switch to ZX Spectrum +3 and reset the current machine?"))
                return false;
            ApplyRomSet(bundledPlus3RomSet);
        }
        lock (machineLock)
        {
            machine.InsertDisk(image, Path.GetFileName(path));
            presentationState.Publish(machine);
        }
        return true;
    }

    private bool InsertPlusDFromDialog(IWin32Window owner, string path)
    {
        MgtImage image = MgtImage.Load(path);
        byte[]? firmware = null;
        if (!machine.HasPlusD)
        {
            bool incompatible = selectedRomSet.PagingModel == SpectrumMemoryPagingModel.Plus2APlus3;
            string prompt = incompatible
                ? "Switch to ZX Spectrum 128K, attach +D, and reset the current machine?"
                : "Attach the +D interface and reset the current machine?";
            firmware = plusDFirmwareSettings.ResolveForAttachment(romFolder,
                () => BrowseForPlusDFirmware(owner), () => ConfirmDiskReset(owner, prompt));
            if (firmware == null)
                return false;

            ApplyRomSet(incompatible ? bundled128RomSet : selectedRomSet);
        }

        lock (machineLock)
        {
            if (firmware != null)
                machine.AttachPlusD(firmware);
            machine.InsertPlusDDisk(image, Path.GetFileName(path));
            presentationState.Publish(machine);
        }
        return true;
    }

    private static string? BrowseForPlusDFirmware(IWin32Window owner)
    {
        if (!ConfirmPlusDFirmwareBrowse(owner))
            return null;

        using var picker = new OpenFileDialog
        {
            Title = "Select the 8 KB +D ROM",
            Filter = "ROM files (*.rom; *.bin)|*.rom;*.bin|All files (*.*)|*.*",
            ClientGuid = PlusDFirmwarePickerId,
            CheckFileExists = true
        };
        return picker.ShowDialog(owner) == DialogResult.OK ? picker.FileName : null;
    }

    private static bool ConfirmPlusDFirmwareBrowse(IWin32Window owner)
    {
        using var dialog = new Form
        {
            Text = "+D Firmware Required",
            AutoScaleMode = AutoScaleMode.Dpi,
            ClientSize = new Size(445, 165),
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            ShowInTaskbar = false,
            StartPosition = FormStartPosition.CenterParent
        };
        EmulatorUiStyle.ApplyForm(dialog);
        var title = EmulatorUiStyle.CreateTitle("+D FIRMWARE REQUIRED");
        title.Location = new Point(18, 14);
        var instruction = new Label
        {
            Location = new Point(18, 48),
            Size = new Size(409, 60),
            Text = "An .mgt disk needs an 8 KB +D G+DOS ROM. DERBY did not find a usable ROM. " +
                "Place plusd.rom in the ROMs folder beside DERBY, or browse to your ROM file."
        };
        var browse = new Button { Text = "Browse...", DialogResult = DialogResult.OK, Location = new Point(229, 117), Size = new Size(100, 30) };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(337, 117), Size = new Size(90, 30) };
        EmulatorUiStyle.StyleButton(browse, primary: true);
        EmulatorUiStyle.StyleButton(cancel);
        dialog.Controls.AddRange([title, instruction, browse, cancel]);
        dialog.AcceptButton = browse;
        dialog.CancelButton = cancel;
        return dialog.ShowDialog(owner) == DialogResult.OK;
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
