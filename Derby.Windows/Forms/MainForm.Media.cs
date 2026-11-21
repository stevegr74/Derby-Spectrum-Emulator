namespace Derby
{
    public partial class MainForm
    {
        private void LoadSnaSnapshotFromDialog()
        {
            using var dialog = new OpenFileDialog
            {
                Title = "Load .sna Snapshot",
                Filter = "Spectrum snapshots (*.sna)|*.sna|All files (*.*)|*.*",
                CheckFileExists = true,
                Multiselect = false
            };
            using IDisposable pauseLease = AcquireEmulationPauseLease();

            if (dialog.ShowDialog(this) != DialogResult.OK)
                return;

            try
            {
                ExecuteWithEmulationPaused(() =>
                {
                    ClearHostAndSpectrumInputState();
                    lock (machineLock)
                    {
                        SpectrumMachine newMachine = CreateConfiguredMachine();
                        SnapshotLoader.Load(newMachine, dialog.FileName, selectedMachineModel);
                        machine = newMachine;
                    }
                    ResetFrameScheduler();
                    RecreateAudioPipeline();
                    lock (machineLock)
                        machine.ClearDebugHistory();
                });
                SetLoadedMediaDisplayName(dialog.FileName);
                SuppressSpectrumHostInputForMilliseconds(PostLoadInputSuppressionMilliseconds);
                PresentCurrentMachineFrame(frameClock.ElapsedTicks, force: true);
                screenBox.Focus();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    $"Failed to load snapshot:\n{ex.Message}",
                    "Snapshot Load Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void MountTapFromDialog() => ChooseTapeFromDialog(this, TapeFileAction.StartNewGame);

        private void ReplaceMountedTapeFromDialog() => ChooseTapeFromDialog(this, TapeFileAction.ChangeTape);

        private void ChooseTapeFromDialog(Form owner) => ChooseTapeFromDialog(owner, null);

        private void ChooseTapeFromDialog(Form owner, TapeFileAction? shortcutAction)
        {
            using IDisposable pauseLease = AcquireEmulationPauseLease();
            bool hasTape;
            lock (machineLock)
                hasTape = machine.HasTapeInDeck;
            if (!hasTape && shortcutAction == TapeFileAction.ChangeTape)
                return;

            using var dialog = new OpenFileDialog
            {
                Title = "Choose Tape",
                Filter = "Spectrum tape images (*.tap;*.tzx)|*.tap;*.tzx|All files (*.*)|*.*",
                CheckFileExists = true,
                Multiselect = false
            };
            if (dialog.ShowDialog(owner) != DialogResult.OK)
                return;

            TapeFileAction action = shortcutAction ?? TapeFileAction.StartNewGame;
            if (shortcutAction == null && hasTape)
            {
                using var choice = new TapeFileActionForm(Path.GetFileName(dialog.FileName));
                if (choice.ShowDialog(owner) != DialogResult.OK)
                    return;
                action = choice.Action;
            }

            try
            {
                ClearHostAndSpectrumInputState();
                if (action == TapeFileAction.ChangeTape)
                    ReplaceMountedTape(dialog.FileName);
                else
                    LoadTapeForNewGame(dialog.FileName);
                SetLoadedMediaDisplayName(dialog.FileName);
                SuppressSpectrumHostInputForMilliseconds(PostLoadInputSuppressionMilliseconds);
                ShowMountedTapeTransportFeedback();
                PresentCurrentMachineFrame(frameClock.ElapsedTicks, force: true);
                if (owner == this)
                    screenBox.Focus();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    owner,
                    $"Failed to open tape:\n{ex.Message}",
                    "Tape Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void LoadTapeForNewGame(string path)
        {
            lock (machineLock)
            {
                SpectrumMachine newMachine = CreateConfiguredMachine();
                if (Path.GetExtension(path).Equals(".tzx", StringComparison.OrdinalIgnoreCase))
                    Tape.TzxLoader.LoadWithPolicy(newMachine, path);
                else
                    Tape.TapLoader.LoadWithPolicy(newMachine, path);
                machine = newMachine;
            }
            ResetFrameScheduler();
            RecreateAudioPipeline();
            lock (machineLock)
                machine.ClearDebugHistory();
        }

        private Tape.TapMountResult ReplaceMountedTape(string path)
        {
            string extension = Path.GetExtension(path);
            lock (machineLock)
            {
                if (!machine.HasTapeInDeck)
                    throw new InvalidOperationException("There is no mounted tape to replace.");

                return extension.Equals(".tzx", StringComparison.OrdinalIgnoreCase)
                    ? Tape.TzxLoader.Mount(machine, path)
                    : extension.Equals(".tap", StringComparison.OrdinalIgnoreCase)
                        ? Tape.TapLoader.Mount(machine, path)
                        : throw new InvalidOperationException("Tape replacement requires a .tap or .tzx image.");
            }
        }

        private void LoadSnapshotOrRecordingFromDialog()
        {
            using var dialog = new OpenFileDialog
            {
                Title = "Load Snapshot or RZX Recording",
                Filter = "Spectrum snapshots and recordings (*.z80;*.szx;*.rzx)|*.z80;*.szx;*.rzx|All files (*.*)|*.*",
                CheckFileExists = true,
                Multiselect = false
            };
            using IDisposable pauseLease = AcquireEmulationPauseLease();

            if (dialog.ShowDialog(this) != DialogResult.OK)
                return;

            try
            {
                string extension = Path.GetExtension(dialog.FileName);
                if (extension.Equals(".rzx", StringComparison.OrdinalIgnoreCase))
                {
                    ExecuteWithEmulationPaused(() =>
                    {
                        ClearHostAndSpectrumInputState();
                        lock (machineLock)
                        {
                            SpectrumMachine newMachine = CreateConfiguredMachine();
                            RzxLoader.Load(newMachine, dialog.FileName);
                            machine = newMachine;
                        }
                        ResetFrameScheduler();
                        RecreateAudioPipeline();
                        lock (machineLock)
                            machine.ClearDebugHistory();
                    });
                }
                else if (extension.Equals(".szx", StringComparison.OrdinalIgnoreCase))
                {
                    ExecuteWithEmulationPaused(() =>
                    {
                        ClearHostAndSpectrumInputState();
                        lock (machineLock)
                        {
                            SpectrumMachine newMachine = CreateConfiguredMachine();
                            SzxSnapshotLoader.Load(newMachine, dialog.FileName);
                            machine = newMachine;
                        }
                        ResetFrameScheduler();
                        RecreateAudioPipeline();
                        lock (machineLock)
                            machine.ClearDebugHistory();
                    });
                }
                else if (extension.Equals(".z80", StringComparison.OrdinalIgnoreCase))
                {
                    ExecuteWithEmulationPaused(() =>
                    {
                        ClearHostAndSpectrumInputState();
                        lock (machineLock)
                        {
                            SpectrumMachine newMachine = CreateConfiguredMachine();
                            Z80SnapshotLoader.Load(newMachine, dialog.FileName, selectedMachineModel);
                            machine = newMachine;
                        }
                        ResetFrameScheduler();
                        RecreateAudioPipeline();
                        lock (machineLock)
                            machine.ClearDebugHistory();
                    });
                }
                else
                {
                    throw new InvalidOperationException("Select a .z80 or .szx snapshot, or an .rzx recording.");
                }
                SetLoadedMediaDisplayName(dialog.FileName);
                SuppressSpectrumHostInputForMilliseconds(PostLoadInputSuppressionMilliseconds);
                PresentCurrentMachineFrame(frameClock.ElapsedTicks, force: true);
                screenBox.Focus();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    $"Failed to load snapshot:\n{ex.Message}",
                    "Snapshot Load Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
    }
}
