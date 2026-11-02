using System.Drawing.Drawing2D;

namespace Derby
{
    public partial class MainForm
    {
        private void OpenQuickStateManager()
        {
            using IDisposable pauseLease = AcquireEmulationPauseLease();
            using var form = new QuickStateManagerForm(
                index => quickStateSlots[index],
                SaveQuickStateSlot,
                RestoreQuickStateSlot,
                DeleteQuickStateSlot);
            form.ShowDialog(this);
            PresentCurrentMachineFrame(frameClock.ElapsedTicks, force: true);
            screenBox.Focus();
        }

        private QuickStateSlot SaveQuickStateSlot(int index)
        {
            ValidateQuickStateSlotIndex(index);
            Spectrum128Machine.QuickState state;
            byte[] persistentState;
            lock (machineLock)
            {
                state = machine.CaptureQuickState();
                persistentState = machine.CapturePersistentState();
                presentationState.Publish(machine);
            }

            if (!presentationState.TryCapture(out EmulationPresentationSnapshot presentation))
                throw new InvalidOperationException("The current Spectrum frame is unavailable.");

            var slot = new QuickStateSlot(
                state,
                persistentState,
                selectedRomSet,
                loadedMediaDisplayName,
                loadedMediaIdentity,
                CreateQuickStateThumbnail(presentation));
            quickStateSlots[index]?.Dispose();
            quickStateSlots[index] = slot;
            PersistQuickStateArchive();
            ShowQuickStateOverlay($"QUICK STATE {index + 1} SAVED");
            return slot;
        }

        private bool RestoreQuickStateSlot(int index)
        {
            ValidateQuickStateSlotIndex(index);
            QuickStateSlot? slot = quickStateSlots[index];
            if (slot == null)
                return false;

            selectedMachineModel = slot.State?.MachineModel ?? selectedMachineModel;
            if (!SameRomSet(selectedRomSet, slot.RomSet))
            {
                selectedRomSet = slot.RomSet;
                lock (machineLock)
                    machine = CreateConfiguredMachine();
            }
            loadedMediaDisplayName = slot.MediaDisplayName;
            loadedMediaIdentity = slot.MediaIdentity;
            pokeApplications.Clear();
            lock (machineLock)
            {
                if (slot.State != null)
                    machine.RestoreQuickState(slot.State);
                else
                    machine.RestorePersistentState(slot.PersistentState);
                selectedMachineModel = machine.MachineModel;
                presentationState.Publish(machine);
            }
            ClearHostAndSpectrumInputState();
            ResetFrameScheduler();
            RecreateAudioPipeline();

            UpdateMachineModelMenuItems();
            ShowMountedTapeTransportFeedback();
            ShowQuickStateOverlay($"QUICK STATE {index + 1} RESTORED");
            SuppressSpectrumHostInputForMilliseconds(PostLoadInputSuppressionMilliseconds);
            return true;
        }

        private bool DeleteQuickStateSlot(int index)
        {
            ValidateQuickStateSlotIndex(index);
            QuickStateSlot? slot = quickStateSlots[index];
            if (slot == null)
                return false;

            quickStateSlots[index] = null;
            slot.Dispose();
            PersistQuickStateArchive();
            ShowQuickStateOverlay($"QUICK STATE {index + 1} DELETED");
            return true;
        }

        private static Bitmap CreateQuickStateThumbnail(EmulationPresentationSnapshot snapshot)
        {
            var frameBuffer = new SpectrumFrameBuffer();
            if (snapshot.BorderFrame == null)
                frameBuffer.Render(snapshot.ScreenBankData, snapshot.BorderColor, snapshot.FlashPhase);
            else if (snapshot.UlaFrame != null)
                frameBuffer.Render(snapshot.UlaFrame, snapshot.FlashPhase);
            else
                frameBuffer.Render(snapshot.ScreenBankData, snapshot.BorderFrame, snapshot.FlashPhase);

            using var native = new Bitmap(
                SpectrumFrameBuffer.Width,
                SpectrumFrameBuffer.Height,
                System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            SpectrumRenderer.RenderToBitmap(native, frameBuffer);
            var thumbnail = new Bitmap(160, 120, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using Graphics graphics = Graphics.FromImage(thumbnail);
            graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
            graphics.PixelOffsetMode = PixelOffsetMode.Half;
            graphics.DrawImage(native, new Rectangle(0, 0, thumbnail.Width, thumbnail.Height));
            return thumbnail;
        }

        private static void ValidateQuickStateSlotIndex(int index)
        {
            if ((uint)index >= QuickStateSlotCount)
                throw new ArgumentOutOfRangeException(nameof(index));
        }

        private void LoadQuickStateArchive()
        {
            if (quickStateArchiveLoaded)
                return;

            QuickStateSlot?[] loaded = quickStateArchiveStore.Load(
                QuickStateSlotCount,
                ResolveBundledRomSet);
            for (int index = 0; index < quickStateSlots.Length; index++)
            {
                quickStateSlots[index]?.Dispose();
                quickStateSlots[index] = loaded[index];
            }
            quickStateArchiveLoaded = true;
        }

        private SpectrumRomSet? ResolveBundledRomSet(string name) =>
            new[] { bundled128RomSet, bundled48RomSet, bundledPlus2RomSet, bundledPlus2ARomSet }
                .FirstOrDefault(candidate => string.Equals(candidate.Name, name, StringComparison.Ordinal));

        private void PersistQuickStateArchive()
        {
            if (!quickStateArchiveLoaded)
                return;

            try
            {
                quickStateArchiveStore.Save(quickStateSlots);
            }
            catch (Exception exception)
            {
                MessageBox.Show(
                    this,
                    $"The Quick State remains available for this session, but DERBY could not update '{quickStateArchiveStore.Path}'.\n\n{exception.Message}",
                    "Quick State Archive",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }
    }
}
