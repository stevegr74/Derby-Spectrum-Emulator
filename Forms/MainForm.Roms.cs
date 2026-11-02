namespace Derby
{
    public partial class MainForm
    {
        private ToolStripMenuItem bundled128RomMenuItem = null!;
        private ToolStripMenuItem bundled48RomMenuItem = null!;
        private ToolStripMenuItem bundledPlus2RomMenuItem = null!;
        private ToolStripMenuItem bundledPlus2ARomMenuItem = null!;

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
            bundledPlus2ARomMenuItem = new ToolStripMenuItem(
                "ZX Spectrum +2A",
                null,
                (_, _) => ApplyRomSet(bundledPlus2ARomSet));

            machineMenuItem.DropDownItems.Add(bundled128RomMenuItem);
            machineMenuItem.DropDownItems.Add(bundled48RomMenuItem);
            machineMenuItem.DropDownItems.Add(bundledPlus2RomMenuItem);
            machineMenuItem.DropDownItems.Add(bundledPlus2ARomMenuItem);
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
            bundledPlus2ARomMenuItem.Checked = SameRomSet(selectedRomSet, bundledPlus2ARomSet);
        }

        private static bool SameRomSet(SpectrumRomSet left, SpectrumRomSet right) =>
            left.Model == right.Model &&
            left.PagingModel == right.PagingModel &&
            left.RomPaths.Select(Path.GetFullPath).SequenceEqual(
                right.RomPaths.Select(Path.GetFullPath),
                StringComparer.OrdinalIgnoreCase);

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
            SuppressSpectrumHostInputForMilliseconds(PostLoadInputSuppressionMilliseconds);
            PresentCurrentMachineFrame(frameClock.ElapsedTicks + PresentationIntervalTicks);
            screenBox.Focus();
        }
    }
}
