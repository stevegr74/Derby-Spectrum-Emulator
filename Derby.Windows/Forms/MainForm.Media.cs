namespace Derby
{
    public partial class MainForm
    {
        private void LoadTapeFromDialog() => ChooseTapeFromDialog(this, TapeFileAction.StartNewGame);

        private void MountStoppedTapeFromDialog() => ChooseTapeFromDialog(this, TapeFileAction.MountStopped);

        private static string StoppedTapeActionLabel(bool hasTape) => hasTape ? "Change Tape" : "Insert Tape";

        private void ChooseTapeFromDialog(Form owner) => ChooseTapeFromDialog(owner, null);

        private void ChooseTapeFromDialog(Form owner, TapeFileAction? shortcutAction)
        {
            using IDisposable pauseLease = AcquireEmulationPauseLease();
            bool hasTape;
            lock (machineLock)
                hasTape = machine.HasTapeInDeck;
            using var dialog = new OpenFileDialog
            {
                Title = shortcutAction switch
                {
                    TapeFileAction.StartNewGame => "Load Tape",
                    TapeFileAction.MountStopped => StoppedTapeActionLabel(hasTape),
                    _ => "Choose Tape"
                },
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
                ApplyTapeFileAction(action, dialog.FileName);
                SuppressSpectrumHostInputForMilliseconds(PostLoadInputSuppressionMilliseconds);
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

        private void ApplyTapeFileAction(TapeFileAction action, string path)
        {
            if (action == TapeFileAction.MountStopped)
            {
                MountStoppedTape(path);
                tapeTransportOverlay.SetObservedState(Tape.TapeTransportState.Stopped, markerStop: false);
                tapeTransportOverlay.Clear();
                UpdateTapeTransportMenuItem();
                UpdateWindowTitle();
                return;
            }

            LoadTapeForNewGame(path);
            SetLoadedMediaDisplayName(path);
            ShowMountedTapeTransportFeedback();
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

        private void MountStoppedTape(string path)
        {
            string extension = Path.GetExtension(path);
            lock (machineLock)
            {
                if (extension.Equals(".tzx", StringComparison.OrdinalIgnoreCase))
                    Tape.TzxLoader.Mount(machine, path, startStopped: true);
                else if (extension.Equals(".tap", StringComparison.OrdinalIgnoreCase))
                    Tape.TapLoader.Mount(machine, path, startStopped: true);
                else
                    throw new InvalidOperationException("Insert or change tape requires a .tap or .tzx image.");
            }
        }

        private void LoadSnapshotOrRecordingFromDialog()
        {
            using var dialog = new OpenFileDialog
            {
                Title = "Load Snapshot or RZX Recording",
                Filter = "Snapshots and recordings (*.sna;*.z80;*.szx;*.rzx)|*.sna;*.z80;*.szx;*.rzx" +
                    "|Snapshots (*.sna;*.z80;*.szx)|*.sna;*.z80;*.szx|Recordings (*.rzx)|*.rzx|All files (*.*)|*.*",
                CheckFileExists = true,
                Multiselect = false
            };
            using IDisposable pauseLease = AcquireEmulationPauseLease();

            if (dialog.ShowDialog(this) != DialogResult.OK)
                return;
            try
            {
                LoadSnapshotOrRecording(dialog.FileName);
                SuppressSpectrumHostInputForMilliseconds(PostLoadInputSuppressionMilliseconds);
                PresentCurrentMachineFrame(frameClock.ElapsedTicks, force: true);
                screenBox.Focus();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    $"Failed to open snapshot or recording:\n{ex.Message}",
                    "Snapshot or Recording Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void LoadSnapshotOrRecording(string path)
        {
            ExecuteWithEmulationPaused(() =>
            {
                ClearHostAndSpectrumInputState();
                lock (machineLock)
                {
                    SpectrumMachine newMachine = CreateConfiguredMachine();
                    switch (Path.GetExtension(path).ToLowerInvariant())
                    {
                        case ".sna": SnapshotLoader.Load(newMachine, path, selectedMachineModel); break;
                        case ".z80": Z80SnapshotLoader.Load(newMachine, path, selectedMachineModel); break;
                        case ".szx": SzxSnapshotLoader.Load(newMachine, path); break;
                        case ".rzx": RzxLoader.Load(newMachine, path); break;
                        default: throw new InvalidOperationException("Select a .sna, .z80 or .szx snapshot, or an .rzx recording.");
                    }
                    machine = newMachine;
                }
                ResetFrameScheduler();
                RecreateAudioPipeline();
                lock (machineLock)
                    machine.ClearDebugHistory();
                SetLoadedMediaDisplayName(path);
            });
        }
    }
}
