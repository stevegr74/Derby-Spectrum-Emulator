namespace Derby
{
    public partial class MainForm
    {
        private ToolStripMenuItem bundled128RomMenuItem = null!;
        private ToolStripMenuItem bundled48RomMenuItem = null!;
        private ToolStripMenuItem bundledPlus2RomMenuItem = null!;
        private ToolStripMenuItem customRomMenuItem = null!;

        private void InitializeMachineMenu()
        {
            bundled128RomMenuItem = new ToolStripMenuItem(
                "ZX Spectrum 128K",
                null,
                (_, _) => ApplyRomSet(bundled128RomSet));
            bundled48RomMenuItem = new ToolStripMenuItem(
                "ZX Spectrum 48K",
                null,
                (_, _) => ApplyRomSet(bundled48RomSet));
            bundledPlus2RomMenuItem = new ToolStripMenuItem(
                "ZX Spectrum +2 (Grey)",
                null,
                (_, _) => ApplyRomSet(bundledPlus2RomSet));

            customRomMenuItem = new ToolStripMenuItem("Custom Machine / ROM");
            customRomMenuItem.DropDownItems.Add(
                "Load 48K / Spectrum+ ROM...",
                null,
                (_, _) => SelectCustom48kRom());
            customRomMenuItem.DropDownItems.Add(
                "Load 128K / +2 ROM pair...",
                null,
                (_, _) => SelectCustom128kRomPair());

            machineMenuItem.DropDownItems.Add(bundled128RomMenuItem);
            machineMenuItem.DropDownItems.Add(bundled48RomMenuItem);
            machineMenuItem.DropDownItems.Add(bundledPlus2RomMenuItem);
            machineMenuItem.DropDownItems.Add(new ToolStripSeparator());
            machineMenuItem.DropDownItems.Add(customRomMenuItem);
            machineMenuItem.DropDownOpening += (_, _) => UpdateMachineMenu();
            UpdateMachineMenu();
        }

        private void UpdateMachineMenu()
        {
            if (bundled128RomMenuItem == null)
                return;

            bundled128RomMenuItem.Checked = SameRomSet(selectedRomSet, bundled128RomSet);
            bundled48RomMenuItem.Checked = SameRomSet(selectedRomSet, bundled48RomSet);
            bundledPlus2RomMenuItem.Checked = SameRomSet(selectedRomSet, bundledPlus2RomSet);
            customRomMenuItem.Checked =
                !bundled128RomMenuItem.Checked &&
                !bundled48RomMenuItem.Checked &&
                !bundledPlus2RomMenuItem.Checked;
        }

        private static bool SameRomSet(SpectrumRomSet left, SpectrumRomSet right) =>
            left.Model == right.Model &&
            string.Equals(Path.GetFullPath(left.Rom0Path), Path.GetFullPath(right.Rom0Path), StringComparison.OrdinalIgnoreCase) &&
            string.Equals(Path.GetFullPath(left.Rom1Path), Path.GetFullPath(right.Rom1Path), StringComparison.OrdinalIgnoreCase);

        private void SelectCustom48kRom()
        {
            using var dialog = CreateRomDialog("Select a 48K or Spectrum+ 16KB ROM");
            if (dialog.ShowDialog(this) != DialogResult.OK)
                return;

            TryApplyRomSet(() => SpectrumRomSet.Create48K(
                $"Custom 48K: {Path.GetFileNameWithoutExtension(dialog.FileName)}",
                dialog.FileName));
        }

        private void SelectCustom128kRomPair()
        {
            using var rom0Dialog = CreateRomDialog("Select ROM 0 (128K editor/menu ROM)");
            if (rom0Dialog.ShowDialog(this) != DialogResult.OK)
                return;

            using var rom1Dialog = CreateRomDialog("Select ROM 1 (48K BASIC ROM)");
            if (rom1Dialog.ShowDialog(this) != DialogResult.OK)
                return;

            TryApplyRomSet(() => SpectrumRomSet.Create128K(
                $"Custom 128K/+2: {Path.GetFileNameWithoutExtension(rom0Dialog.FileName)} + {Path.GetFileNameWithoutExtension(rom1Dialog.FileName)}",
                rom0Dialog.FileName,
                rom1Dialog.FileName));
        }

        private static OpenFileDialog CreateRomDialog(string title) => new()
        {
            Title = title,
            Filter = "Spectrum ROM images (*.rom;*.bin)|*.rom;*.bin|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        private void TryApplyRomSet(Func<SpectrumRomSet> create)
        {
            try
            {
                ApplyRomSet(create());
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            {
                MessageBox.Show(
                    this,
                    ex.Message,
                    "Unable to load machine profile",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        private void ApplyRomSet(SpectrumRomSet romSet)
        {
            ExecuteWithEmulationPaused(() =>
            {
                selectedRomSet = romSet;
                if (romSet.Model == SpectrumMachineModel.Spectrum48K)
                    selected48RomSet = romSet;
                else
                    selected128RomSet = romSet;
                selectedMachineModel = romSet.Model;
                loadedMediaDisplayName = null;
                loadedMediaIdentity = null;
                pokeApplications.Clear();
                ClearHostAndSpectrumInputState();
                lock (machineLock)
                {
                    machine = CreateConfiguredMachine();
                    machine.ClearDebugHistory();
                }
                ResetFrameScheduler();
                RecreateAudioPipeline();
            });

            UpdateMachineModelMenuItems();
            SuppressSpectrumHostInputForMilliseconds(PostLoadInputSuppressionMilliseconds);
            PresentCurrentMachineFrame(frameClock.ElapsedTicks + PresentationIntervalTicks);
            screenBox.Focus();
        }
    }
}
