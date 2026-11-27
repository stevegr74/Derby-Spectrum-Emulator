namespace Derby
{
    public partial class MainForm
    {
        private OnScreenKeyboardForm? onScreenKeyboard;

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            // Bit 30 indicates that Windows is repeating a held key.
            if (keyData == Keys.F3 && (msg.LParam.ToInt64() & (1L << 30)) != 0)
                return true;

            if (keyData is Keys.Apps or (Keys.Shift | Keys.F10) &&
                !mouseCaptured && !displayContextMenu.Visible)
            {
                displayContextMenu.Show(screenBox, new Point(0, 0));
                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void InitializeKeyboard()
        {
            KeyPreview = true;
            PreviewKeyDown += MainForm_PreviewKeyDown;
            KeyDown += MainForm_KeyDown;
            KeyUp += MainForm_KeyUp;
            Deactivate += MainForm_Deactivate;
            screenBox.MouseClick += (_, _) => screenBox.Focus();
            screenBox.PreviewKeyDown += MainForm_PreviewKeyDown;
            Shown += (_, _) => screenBox.Focus();
            keyboardMessageFilter = new SpectrumKeyboardMessageFilter(
                () => HasKeyboardInputFocus() && !displayContextMenu.Visible,
                QueueSpectrumHostKeyEvent, TryConsumeMouseEscape);
            Application.AddMessageFilter(keyboardMessageFilter);
        }

        private void MainForm_Deactivate(object? sender, EventArgs e)
        {
            ReleaseSpectrumMouse();
            consumedMouseEscape = false;
            if (onScreenKeyboard is { IsDisposed: false } && IsHandleCreated && !Disposing)
            {
                // Wait for activation to settle when switching between the two windows.
                BeginInvoke(ResetKeyboardInputIfUnfocused);
                return;
            }
            LogInputDiagnostic("deactivate", null, "clearing input state");
            QueueSpectrumHostInputReset();
        }

        private bool HasKeyboardInputFocus() => ContainsFocus || onScreenKeyboard?.ContainsFocus == true;

        private void ResetKeyboardInputIfUnfocused()
        {
            if (IsDisposed || Disposing || HasKeyboardInputFocus())
                return;
            LogInputDiagnostic("deactivate", null, "clearing input state");
            onScreenKeyboard?.ClearInput();
            QueueSpectrumHostInputReset();
        }

        private void OpenOnScreenKeyboard()
        {
            if (onScreenKeyboard is { IsDisposed: false })
            {
                onScreenKeyboard.Activate();
                return;
            }

            string path = OnScreenKeyboardSettings.DefaultPath;
            OnScreenKeyboardSettings? saved = OnScreenKeyboardSettings.Load(path);
            var layout = saved?.Layout ?? (selectedMachineModel == SpectrumMachineModel.Spectrum48K
                ? OnScreenKeyboardLayout.Spectrum48K : OnScreenKeyboardLayout.Spectrum128K);
            var keyboard = new OnScreenKeyboardForm(layout, QueueOnScreenKeyEvent, QueueOnScreenKeyCancellation);
            onScreenKeyboard = keyboard;
            keyboard.Bounds = OnScreenKeyboardSettings.Place(
                saved?.Bounds, keyboard.Size, keyboard.MinimumSize, Bounds, Screen.FromControl(this).WorkingArea);
            keyboard.Deactivate += (_, _) =>
            {
                if (IsHandleCreated && !Disposing)
                    BeginInvoke(ResetKeyboardInputIfUnfocused);
            };
            keyboard.FormClosed += (_, _) =>
            {
                new OnScreenKeyboardSettings(keyboard.LayoutChoice, keyboard.Bounds).Save(path);
                onScreenKeyboard = null;
            };
            keyboard.Show(this);
        }

        private void MainForm_PreviewKeyDown(object? sender, PreviewKeyDownEventArgs e)
        {
            if (IsSpectrumMappedKey(e.KeyCode))
                e.IsInputKey = true;
        }

        private void MainForm_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.F4 && e.Alt)
                return;

            if (GetMainShortcutAction(e.KeyData) is { } action)
            {
                action();
                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }

            if (IsSpectrumMappedKey(e.KeyCode))
            {
                LogInputDiagnostic("keydown", e.KeyCode, "mapped event");
                QueueSpectrumHostKeyEvent(e.KeyCode, true);
                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }

            HandleKey(e.KeyCode, true);
            e.Handled = true;
            e.SuppressKeyPress = true;
        }

        private Action? GetMainShortcutAction(Keys keyData) => keyData switch
        {
            Keys.F1 => ToggleHelpWindow,
            Keys.F2 => ToggleStatusOverlay,
            Keys.F3 => CycleMachineProfile,
            Keys.F4 => () => SelectDisplayMode(SpectrumDisplayModes.Next(displayMode)),
            (Keys.Shift | Keys.F4) => CycleDisplayFilter,
            Keys.F5 => ToggleTapeTransport,
            (Keys.Shift | Keys.F5) => OpenTapeDeck,
            Keys.F6 => LoadTapeFromDialog,
            Keys.F7 => MountStoppedTapeFromDialog,
            Keys.F8 => OpenDiskDrive,
            Keys.F9 => LoadSnapshotOrRecordingFromDialog,
            Keys.F10 => OpenQuickStateManager,
            Keys.F11 => ToggleDisassemblerWindow,
            Keys.F12 => DumpMachineDebugState,
            (Keys.Shift | Keys.F12) => OpenDebugDumps,
            _ => null
        };

        private void MainForm_KeyUp(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.F4 && e.Alt)
                return;

            if (GetMainShortcutAction(e.KeyData) != null)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }

            if (IsSpectrumMappedKey(e.KeyCode))
            {
                LogInputDiagnostic("keyup", e.KeyCode, "mapped event");
                QueueSpectrumHostKeyEvent(e.KeyCode, false);
                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }

            HandleKey(e.KeyCode, false);
            e.Handled = true;
            e.SuppressKeyPress = true;
        }

        private void HandleKey(Keys key, bool pressed)
        {
            if (IsSpectrumMappedKey(key))
                QueueSpectrumHostKeyEvent(key, pressed);
        }

        private bool IsSpectrumMappedKey(Keys key)
        {
            return SpectrumKeyboardMapping.IsMappedKey(key);
        }

        private void ProcessPendingSpectrumKeyReleases()
        {
            keyboardInputController.Advance(
                machine.GetKeyboardRowScanCount,
                inputEmulatedTStates,
                machine.SetKey);
        }

        private void QueueSpectrumHostKeyEvent(Keys key, bool isDown)
        {
            if (frameClock.ElapsedTicks < Volatile.Read(ref suppressSpectrumHostInputUntilTicks))
            {
                LogInputDiagnostic("queue-suppressed", key, $"isDown={isDown}");
                return;
            }

            lock (inputStateLock)
            {
                pendingSpectrumHostKeyEvents.Enqueue(new SpectrumHostKeyEvent(key, isDown));
            }
        }

        private void QueueSpectrumHostInputReset()
        {
            lock (inputStateLock)
            {
                pendingSpectrumHostKeyEvents.Clear();
                pendingSpectrumHostKeyEvents.Enqueue(new SpectrumHostKeyEvent(Keys.None, false, Reset: true));
            }
        }

        private void QueueOnScreenKeyEvent(OnScreenKeyStroke stroke, bool isDown)
        {
            lock (inputStateLock)
                pendingSpectrumHostKeyEvents.Enqueue(new SpectrumHostKeyEvent(Keys.None, isDown, Stroke: stroke));
        }

        private void QueueOnScreenKeyCancellation()
        {
            lock (inputStateLock)
                pendingSpectrumHostKeyEvents.Enqueue(new SpectrumHostKeyEvent(Keys.None, false, CancelOnScreen: true));
        }

        private void SuppressSpectrumHostInputForMilliseconds(int milliseconds)
        {
            if (milliseconds <= 0)
                return;

            long durationTicks = milliseconds * System.Diagnostics.Stopwatch.Frequency / 1000;
            Volatile.Write(ref suppressSpectrumHostInputUntilTicks, frameClock.ElapsedTicks + durationTicks);
        }

        private void ResetInputBridgeState()
        {
            onScreenKeyboard?.ClearInput();
            lock (inputStateLock)
            {
                pendingSpectrumHostKeyEvents.Clear();
                keyboardInputController.Reset();
            }
        }

        private void DrainPendingSpectrumHostKeyEvents()
        {
            while (true)
            {
                SpectrumHostKeyEvent? keyEvent = null;
                lock (inputStateLock)
                {
                    if (pendingSpectrumHostKeyEvents.Count == 0)
                        break;

                    keyEvent = pendingSpectrumHostKeyEvents.Dequeue();
                }

                if (keyEvent.Value.Reset)
                {
                    keyboardInputController.Reset();
                    machine.ClearKeyboard();
                    continue;
                }

                if (keyEvent.Value.CancelOnScreen)
                {
                    keyboardInputController.CancelOnScreenKeys(machine.SetKey);
                    continue;
                }
                if (keyEvent.Value.Stroke is OnScreenKeyStroke stroke)
                {
                    keyboardInputController.HandleOnScreenKeyEvent(stroke, keyEvent.Value.IsDown,
                        machine.GetKeyboardRowScanCount, inputEmulatedTStates, (ulong)machine.FrameTStates, machine.SetKey);
                    continue;
                }
                UpdateSpectrumMappedKeyState(keyEvent.Value.Key, keyEvent.Value.IsDown);
            }
        }

        private bool UpdateSpectrumMappedKeyState(Keys key, bool isDown)
        {
            bool changed = keyboardInputController.HandleHostKeyEvent(
                key,
                isDown,
                machine.GetKeyboardRowScanCount,
                inputEmulatedTStates,
                (ulong)machine.FrameTStates,
                machine.SetKey);
            LogInputDiagnostic("update", key, $"isDown={isDown} changed={changed} {DescribeBridgeState(key)}");
            return changed;
        }
    }
}
