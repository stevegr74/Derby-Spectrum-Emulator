namespace Derby
{
    public partial class MainForm
    {
        private ToolStripMenuItem bundled128RomMenuItem = null!;
        private ToolStripMenuItem bundled48RomMenuItem = null!;
        private ToolStripMenuItem activeRomMenuItem = null!;

        private void InitializeRomSetMenu()
        {
            activeRomMenuItem = new ToolStripMenuItem { Enabled = false };
            bundled128RomMenuItem = new ToolStripMenuItem(
                "Sinclair 128K (bundled)",
                null,
                (_, _) => ApplyRomSet(SpectrumRomSet.Bundled128K(romFolder)));
            bundled48RomMenuItem = new ToolStripMenuItem(
                "Sinclair 48K / Spectrum+ (bundled)",
                null,
                (_, _) => ApplyRomSet(SpectrumRomSet.Bundled48K(romFolder)));

            romSetMenuItem.DropDownItems.Add(activeRomMenuItem);
            romSetMenuItem.DropDownItems.Add(new ToolStripSeparator());
            romSetMenuItem.DropDownItems.Add(bundled128RomMenuItem);
            romSetMenuItem.DropDownItems.Add(bundled48RomMenuItem);
            romSetMenuItem.DropDownItems.Add(new ToolStripSeparator());
            romSetMenuItem.DropDownItems.Add(
                "Load custom 48K / Spectrum+ ROM...",
                null,
                (_, _) => SelectCustom48kRom());
            romSetMenuItem.DropDownItems.Add(
                "Load custom 128K / +2 ROM pair...",
                null,
                (_, _) => SelectCustom128kRomPair());
            romSetMenuItem.DropDownItems.Add(new ToolStripSeparator());
            romSetMenuItem.DropDownItems.Add(new ToolStripMenuItem(
                "+2A / +3 ROM sets are not yet supported")
            {
                Enabled = false
            });
            romSetMenuItem.DropDownOpening += (_, _) => UpdateRomSetMenu();
            UpdateRomSetMenu();
        }

        private void UpdateRomSetMenu()
        {
            if (activeRomMenuItem == null)
                return;

            activeRomMenuItem.Text = $"Active: {selectedRomSet.Name}";
            bundled128RomMenuItem.Checked = selectedRomSet.Name == SpectrumRomSet.Bundled128K(romFolder).Name;
            bundled48RomMenuItem.Checked = selectedRomSet.Name == SpectrumRomSet.Bundled48K(romFolder).Name;
        }

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
                    "Unable to load ROM set",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        private void ApplyRomSet(SpectrumRomSet romSet)
        {
            ExecuteWithEmulationPaused(() =>
            {
                selectedRomSet = romSet;
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
            UpdateRomSetMenu();
            SuppressSpectrumHostInputForMilliseconds(PostLoadInputSuppressionMilliseconds);
            PresentCurrentMachineFrame(frameClock.ElapsedTicks + PresentationIntervalTicks);
            screenBox.Focus();
        }
    }
}
