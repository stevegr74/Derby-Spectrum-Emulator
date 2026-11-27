using Derby.Input;

namespace Derby
{
    public sealed class JoystickConfigurationForm : Form
    {
        private readonly ControllerEditor controller1;
        private readonly ControllerEditor controller2;
        private readonly IHostJoystickSource? source;
        private readonly IReadOnlyList<HostJoystickDevice> devices;
        private readonly Dictionary<string, HostJoystickDeviceProfile> deviceProfiles;
        private readonly IReadOnlyDictionary<string, string> deviceLabels;
        private readonly System.Windows.Forms.Timer previewTimer = new() { Interval = 75 };

        public JoystickConfigurationForm(
            HostJoystickChannelSettings controller1Settings,
            HostJoystickChannelSettings controller2Settings,
            IReadOnlyList<HostJoystickDevice> devices,
            IHostJoystickSource? source = null,
            IReadOnlyDictionary<string, HostJoystickDeviceProfile>? deviceProfiles = null)
        {
            ArgumentNullException.ThrowIfNull(controller1Settings);
            ArgumentNullException.ThrowIfNull(controller2Settings);
            ArgumentNullException.ThrowIfNull(devices);
            this.source = source;
            this.devices = devices;
            this.deviceProfiles = deviceProfiles?.ToDictionary(
                pair => pair.Key,
                pair => pair.Value,
                StringComparer.Ordinal) ?? new Dictionary<string, HostJoystickDeviceProfile>(StringComparer.Ordinal);
            deviceLabels = HostJoystickDeviceIdentity.CreateDisplayLabels(devices);

            Text = "Joystick Configuration";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(940, 485);
            EmulatorUiStyle.ApplyForm(this);
            FixedDialogWorkArea.Enable(this);

            var claimed = new HashSet<string>(StringComparer.Ordinal);
            HostJoystickDevice? firstDevice = HostJoystickDeviceResolver.Resolve(
                controller1Settings,
                devices,
                claimed);
            if (firstDevice != null)
                claimed.Add(firstDevice.Id);
            HostJoystickDevice? secondDevice = HostJoystickDeviceResolver.Resolve(
                controller2Settings,
                devices,
                claimed);

            controller1 = new ControllerEditor(
                "Controller 1",
                controller1Settings,
                devices,
                this.deviceProfiles,
                deviceLabels,
                firstDevice);
            controller2 = new ControllerEditor(
                "Controller 2",
                controller2Settings,
                devices,
                this.deviceProfiles,
                deviceLabels,
                secondDevice);

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(18, 16, 18, 14),
                ColumnCount = 1,
                RowCount = 4,
                BackColor = BackColor
            };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            root.Controls.Add(new Label
            {
                Text = "JOYSTICK CONFIGURATION",
                AutoSize = true,
                Font = EmulatorUiStyle.UiFont(13F, FontStyle.Bold),
                ForeColor = EmulatorUiStyle.Ink,
                Margin = new Padding(0, 0, 0, 3)
            }, 0, 0);
            root.Controls.Add(new Label
            {
                Text = "Assign up to two USB controllers. Automatic keeps its current controller when possible, then selects the first unclaimed device.",
                AutoSize = true,
                ForeColor = EmulatorUiStyle.Muted,
                Margin = new Padding(0, 0, 0, 12)
            }, 0, 1);

            var editors = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Margin = Padding.Empty
            };
            editors.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            editors.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            controller1.Group.Margin = new Padding(0, 0, 7, 0);
            controller2.Group.Margin = new Padding(7, 0, 0, 0);
            editors.Controls.Add(controller1.Group, 0, 0);
            editors.Controls.Add(controller2.Group, 1, 0);
            root.Controls.Add(editors, 0, 2);

            var buttons = new FlowLayoutPanel
            {
                AutoSize = true,
                FlowDirection = FlowDirection.RightToLeft,
                Dock = DockStyle.Fill,
                WrapContents = false,
                Margin = Padding.Empty
            };
            var save = new Button { Text = "Save mappings", AutoSize = true, Padding = new Padding(8, 2, 8, 2) };
            var cancel = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                AutoSize = true,
                Padding = new Padding(8, 2, 8, 2)
            };
            EmulatorUiStyle.StyleButton(save, primary: true);
            EmulatorUiStyle.StyleButton(cancel);
            save.Click += (_, _) => SaveIfValid();
            buttons.Controls.Add(save);
            buttons.Controls.Add(cancel);
            root.Controls.Add(buttons, 0, 3);

            AcceptButton = save;
            CancelButton = cancel;
            Controls.Add(root);

            previewTimer.Tick += (_, _) => UpdateLivePreviews();
            Shown += (_, _) =>
            {
                UpdateLivePreviews();
                previewTimer.Start();
            };
        }

        public HostJoystickChannelSettings Controller1Settings => controller1.Settings;
        public HostJoystickChannelSettings Controller2Settings => controller2.Settings;
        public Dictionary<string, HostJoystickDeviceProfile> DeviceProfiles
        {
            get
            {
                controller1.CommitDeviceProfile();
                controller2.CommitDeviceProfile();
                return new Dictionary<string, HostJoystickDeviceProfile>(deviceProfiles, StringComparer.Ordinal);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                previewTimer.Dispose();
            base.Dispose(disposing);
        }

        private void SaveIfValid()
        {
            controller1.CommitDeviceProfile();
            controller2.CommitDeviceProfile();
            HostJoystickChannelSettings first = Controller1Settings;
            HostJoystickChannelSettings second = Controller2Settings;
            if (first.Enabled && second.Enabled && first.EffectiveInterface == second.EffectiveInterface)
            {
                MessageBox.Show(
                    this,
                    "Controller 1 and Controller 2 must use different Spectrum interfaces.",
                    "Joystick Configuration",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            HostJoystickDevice? firstDevice = FindMatchingDevice(devices, first.PreferredDeviceId);
            HostJoystickDevice? secondDevice = FindMatchingDevice(devices, second.PreferredDeviceId);
            if (first.Enabled && second.Enabled && firstDevice != null && secondDevice != null &&
                firstDevice.Id == secondDevice.Id)
            {
                MessageBox.Show(
                    this,
                    "The same USB controller cannot be assigned to both controller slots.",
                    "Joystick Configuration",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            DialogResult = DialogResult.OK;
            Close();
        }

        private void UpdateLivePreviews()
        {
            var claimed = new HashSet<string>(StringComparer.Ordinal);
            string? firstDeviceId = ResolvePreviewDevice(controller1, claimed);
            if (firstDeviceId != null)
                claimed.Add(firstDeviceId);
            string? secondDeviceId = ResolvePreviewDevice(controller2, claimed);
            controller1.UpdateLivePreview(source, firstDeviceId);
            controller2.UpdateLivePreview(source, secondDeviceId);
        }

        private string? ResolvePreviewDevice(ControllerEditor editor, HashSet<string> claimed)
        {
            if (!editor.Enabled)
                return null;
            HostJoystickDevice? resolved = HostJoystickDeviceResolver.Resolve(
                editor.Settings,
                devices,
                claimed,
                editor.AutomaticDeviceId,
                editor.AutomaticDeviceProfileId);
            return resolved?.Id;
        }

        private static HostJoystickDevice? FindMatchingDevice(
            IReadOnlyList<HostJoystickDevice> devices,
            string? preferredDeviceId) =>
            preferredDeviceId == null
                ? null
                : devices.FirstOrDefault(device =>
                    string.Equals(device.Id, preferredDeviceId, StringComparison.Ordinal) ||
                    string.Equals(device.LegacyId, preferredDeviceId, StringComparison.Ordinal));

        private sealed class ControllerEditor
        {
            private readonly CheckBox enabled = new() { Text = "Enabled", AutoSize = true };
            private readonly ComboBox device = CreateSelector();
            private readonly Button identify = new() { Text = "Identify", AutoSize = false, Width = 76 };
            private readonly Label deviceDetails = new()
            {
                AutoSize = true,
                Font = EmulatorUiStyle.UiFont(7.5F),
                ForeColor = EmulatorUiStyle.Muted,
                Margin = new Padding(0, 3, 0, 0)
            };
            private readonly ComboBox emulatedInterface = CreateSelector();
            private readonly ComboBox horizontalAxis = CreateSelector();
            private readonly ComboBox verticalAxis = CreateSelector();
            private readonly ComboBox hatSwitch = CreateSelector();
            private readonly ComboBox fireButton = CreateSelector();
            private readonly TrackBar deadZone = new()
            {
                Minimum = 0,
                Maximum = 95,
                SmallChange = 5,
                LargeChange = 10,
                TickFrequency = 10,
                AutoSize = false,
                Height = 30,
                Dock = DockStyle.Fill
            };
            private readonly Label deadZoneValue = new()
            {
                AutoSize = false,
                Width = 48,
                TextAlign = ContentAlignment.MiddleRight
            };
            private readonly Label livePreview = new()
            {
                AutoSize = false,
                Height = 38,
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = EmulatorUiStyle.UiFont(9F, FontStyle.Bold),
                ForeColor = EmulatorUiStyle.Muted,
                BackColor = EmulatorUiStyle.Panel,
                Text = "NO CONTROLLER INPUT"
            };
            private readonly IReadOnlyList<HostJoystickDevice> devices;
            private readonly Dictionary<string, HostJoystickDeviceProfile> deviceProfiles;
            private readonly IReadOnlyDictionary<string, string> deviceLabels;
            private readonly HostJoystickMapping fallbackMapping;
            private readonly HostJoystickDevice? automaticDevice;
            private string? activeProfileId;
            private bool identifyArmed;
            private HostJoystickReading? identifyBaseline;
            private bool loadingMapping;

            public ControllerEditor(
                string title,
                HostJoystickChannelSettings settings,
                IReadOnlyList<HostJoystickDevice> devices,
                Dictionary<string, HostJoystickDeviceProfile> deviceProfiles,
                IReadOnlyDictionary<string, string> deviceLabels,
                HostJoystickDevice? automaticDevice)
            {
                this.devices = devices;
                this.deviceProfiles = deviceProfiles;
                this.deviceLabels = deviceLabels;
                this.automaticDevice = automaticDevice;
                fallbackMapping = settings.EffectiveMapping;
                Group = new GroupBox
                {
                    Text = title,
                    Dock = DockStyle.Fill,
                    Padding = new Padding(12, 8, 12, 10)
                };
                EmulatorUiStyle.StyleGroup(Group);

                enabled.Checked = settings.Enabled;
                PopulateDeviceSelector(
                    device,
                    devices,
                    settings.PreferredDeviceId,
                    settings.PreferredDeviceProfileId,
                    automaticDevice,
                    deviceLabels);
                PopulateInterfaceSelector(emulatedInterface, settings.EffectiveInterface);
                EmulatorUiStyle.StyleButton(identify);
                identify.Height = device.PreferredHeight;

                var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 9 };
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                layout.Controls.Add(enabled, 0, 0);
                layout.SetColumnSpan(enabled, 2);
                var devicePanel = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    AutoSize = true,
                    ColumnCount = 2,
                    RowCount = 2,
                    Margin = Padding.Empty
                };
                devicePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                devicePanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                devicePanel.Controls.Add(device, 0, 0);
                devicePanel.Controls.Add(identify, 1, 0);
                devicePanel.Controls.Add(deviceDetails, 0, 1);
                devicePanel.SetColumnSpan(deviceDetails, 2);
                AddRow(layout, 1, "USB device", devicePanel, alignLabelToTop: true);
                AddRow(layout, 2, "Spectrum interface", emulatedInterface);
                AddRow(layout, 3, "Left / right", horizontalAxis);
                AddRow(layout, 4, "Up / down", verticalAxis);
                AddRow(layout, 5, "D-pad / hat", hatSwitch);
                AddRow(layout, 6, "Fire", fireButton);

                var deadZonePanel = new TableLayoutPanel
                {
                    AutoSize = true,
                    ColumnCount = 2,
                    Margin = Padding.Empty
                };
                deadZonePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                deadZonePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 52));
                deadZonePanel.Controls.Add(deadZone, 0, 0);
                deadZonePanel.Controls.Add(deadZoneValue, 1, 0);
                AddRow(layout, 7, "Axis dead zone", deadZonePanel);
                layout.Controls.Add(livePreview, 0, 8);
                layout.SetColumnSpan(livePreview, 2);
                Group.Controls.Add(layout);

                LoadSelectedDeviceProfile();
                enabled.CheckedChanged += (_, _) => UpdateEnabledState();
                device.SelectedIndexChanged += (_, _) => ChangeSelectedDevice();
                identify.Click += (_, _) => BeginIdentify();
                deadZone.ValueChanged += (_, _) => UpdateDeadZoneLabel();
                UpdateEnabledState();
            }

            public GroupBox Group { get; }
            public bool Enabled => enabled.Checked;
            public string? AutomaticDeviceId => automaticDevice?.Id;
            public string? AutomaticDeviceProfileId => automaticDevice?.ProfileId;
            public string? PreferredDeviceId =>
                device.SelectedItem is DeviceChoice choice ? choice.Id : null;
            public string? PreferredDeviceProfileId =>
                device.SelectedItem is DeviceChoice choice ? choice.ProfileId : null;
            public HostJoystickChannelSettings Settings => new(
                enabled.Checked,
                PreferredDeviceId,
                ReadMapping(),
                GetSelectedInterface(),
                PreferredDeviceProfileId);

            public void CommitDeviceProfile()
            {
                if (loadingMapping || activeProfileId == null)
                    return;
                deviceProfiles[activeProfileId] = new HostJoystickDeviceProfile(
                    activeProfileId,
                    ReadMapping());
            }

            public void UpdateLivePreview(IHostJoystickSource? source, string? resolvedDeviceId)
            {
                if (!Enabled)
                {
                    SetPreview("OFF", active: false);
                    return;
                }
                if (source == null || resolvedDeviceId == null ||
                    !source.TryRead(resolvedDeviceId, out HostJoystickReading reading))
                {
                    SetPreview("NO CONTROLLER INPUT", active: false);
                    return;
                }

                if (identifyArmed)
                {
                    if (identifyBaseline == null ||
                        !string.Equals(identifyBaseline.DeviceId, reading.DeviceId, StringComparison.Ordinal))
                    {
                        identifyBaseline = reading;
                        SetPreview("MOVE A STICK OR PRESS A BUTTON", active: false);
                    }
                    else if (HostJoystickIdentification.HasMeaningfulChange(identifyBaseline, reading))
                    {
                        identifyArmed = false;
                        identifyBaseline = null;
                        string label = deviceLabels.GetValueOrDefault(resolvedDeviceId, "selected controller");
                        SetPreview($"IDENTIFIED: {label.ToUpperInvariant()}", active: true);
                    }
                    else
                    {
                        SetPreview("MOVE A STICK OR PRESS A BUTTON", active: false);
                    }
                    return;
                }

                SpectrumJoystickState state = HostJoystickMapper.Map(reading, Settings.EffectiveMapping);
                SetPreview($"{FormatInterface(Settings.EffectiveInterface)}  |  {DescribeState(state)}", state != SpectrumJoystickState.None);
            }

            private void SetPreview(string text, bool active)
            {
                livePreview.Text = text;
                livePreview.ForeColor = active ? EmulatorUiStyle.Accent : EmulatorUiStyle.Muted;
                livePreview.BackColor = active ? EmulatorUiStyle.AccentSoft : EmulatorUiStyle.Panel;
            }

            private void UpdateEnabledState()
            {
                foreach (Control control in new Control[]
                         { device, identify, emulatedInterface, horizontalAxis, verticalAxis, hatSwitch, fireButton, deadZone })
                    control.Enabled = enabled.Checked;
            }

            private void ChangeSelectedDevice()
            {
                CommitDeviceProfile();
                identifyArmed = false;
                identifyBaseline = null;
                LoadSelectedDeviceProfile();
            }

            private void LoadSelectedDeviceProfile()
            {
                HostJoystickDevice? selectedDevice = ResolveSelectedDevice();
                activeProfileId = selectedDevice?.ProfileId;
                HostJoystickMapping mapping = activeProfileId != null &&
                                              deviceProfiles.TryGetValue(activeProfileId, out HostJoystickDeviceProfile? profile)
                    ? profile.EffectiveMapping
                    : fallbackMapping;
                loadingMapping = true;
                PopulateMappingSelectors(mapping);
                deadZone.Value = (int)Math.Round(Math.Clamp(mapping.DeadZone, 0.0, 0.95) * 100.0);
                loadingMapping = false;
                UpdateDeadZoneLabel();
                deviceDetails.Text = selectedDevice == null
                    ? "No controller is currently available"
                    : HostJoystickDeviceIdentity.DescribeHardware(selectedDevice);
            }

            private HostJoystickDevice? ResolveSelectedDevice()
            {
                if (device.SelectedItem is not DeviceChoice choice)
                    return null;
                if (choice.Id == null)
                    return automaticDevice;
                return devices.FirstOrDefault(candidate => candidate.Id == choice.Id);
            }

            private void BeginIdentify()
            {
                identifyArmed = true;
                identifyBaseline = null;
                SetPreview("MOVE A STICK OR PRESS A BUTTON", active: false);
            }

            private void PopulateMappingSelectors(HostJoystickMapping mapping)
            {
                HostJoystickDevice? selectedDevice = ResolveSelectedDevice();
                PopulateSelector(horizontalAxis, "Axis", selectedDevice?.AxisCount ?? -1, mapping.HorizontalAxis, false);
                PopulateSelector(verticalAxis, "Axis", selectedDevice?.AxisCount ?? -1, mapping.VerticalAxis, false);
                PopulateSelector(hatSwitch, "Hat", selectedDevice?.SwitchCount ?? -1, mapping.HatSwitch, true);
                PopulateSelector(fireButton, "Button", selectedDevice?.ButtonCount ?? -1, mapping.FireButton, true);
            }

            private SpectrumJoystickInterface GetSelectedInterface() =>
                emulatedInterface.SelectedItem is InterfaceChoice choice
                    ? choice.Value
                    : SpectrumJoystickInterface.Kempston;

            private void UpdateDeadZoneLabel() => deadZoneValue.Text = $"{deadZone.Value}%";

            private HostJoystickMapping ReadMapping() => new(
                GetSelectedValue(horizontalAxis),
                GetSelectedValue(verticalAxis),
                GetSelectedValue(hatSwitch),
                GetSelectedValue(fireButton),
                deadZone.Value / 100.0);

        }

        private static ComboBox CreateSelector() => new()
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Dock = DockStyle.Fill,
            IntegralHeight = false,
            DropDownHeight = 180
        };

        private static void PopulateDeviceSelector(
            ComboBox selector,
            IReadOnlyList<HostJoystickDevice> devices,
            string? selectedValue,
            string? selectedProfileId,
            HostJoystickDevice? automaticDevice,
            IReadOnlyDictionary<string, string> labels)
        {
            string automaticLabel = automaticDevice == null
                ? "Automatic (no controller available)"
                : $"Auto: {labels[automaticDevice.Id]}";
            var choices = new List<DeviceChoice> { new(automaticLabel, null, null) };
            choices.AddRange(devices.Select(device => new DeviceChoice(
                labels[device.Id],
                device.Id,
                device.ProfileId)));
            HostJoystickDevice? selectedDevice = FindMatchingDevice(devices, selectedValue);
            selectedDevice ??= selectedProfileId == null
                ? null
                : devices.FirstOrDefault(device => device.ProfileId == selectedProfileId);
            if (selectedValue != null && selectedDevice == null)
                choices.Add(new DeviceChoice("Unavailable saved controller", selectedValue, selectedProfileId));
            selector.Items.AddRange(choices.Cast<object>().ToArray());
            string? normalizedSelection = selectedDevice?.Id ?? selectedValue;
            selector.SelectedIndex = Math.Max(0, choices.FindIndex(choice => choice.Id == normalizedSelection));
        }

        private static void PopulateInterfaceSelector(ComboBox selector, SpectrumJoystickInterface selectedValue)
        {
            InterfaceChoice[] choices =
            [
                new("Kempston", SpectrumJoystickInterface.Kempston),
                new("Cursor (keys 5, 8, 6, 7, 0)", SpectrumJoystickInterface.Cursor),
                new("Sinclair 1 (keys 1-5; Interface II socket 2)", SpectrumJoystickInterface.SinclairKeys1To5),
                new("Sinclair 2 (keys 6-0; Interface II socket 1)", SpectrumJoystickInterface.SinclairKeys6To0)
            ];
            selector.Items.AddRange(choices.Cast<object>().ToArray());
            int selectedIndex = Array.FindIndex(choices, choice => choice.Value == selectedValue);
            selector.SelectedIndex = selectedIndex >= 0 ? selectedIndex : 0;
        }

        private static string FormatInterface(SpectrumJoystickInterface joystickInterface) => joystickInterface switch
        {
            SpectrumJoystickInterface.Cursor => "CURSOR",
            SpectrumJoystickInterface.SinclairKeys1To5 => "SINCLAIR 1",
            SpectrumJoystickInterface.SinclairKeys6To0 => "SINCLAIR 2",
            _ => "KEMPSTON"
        };

        private static string DescribeState(SpectrumJoystickState state)
        {
            if (state == SpectrumJoystickState.None)
                return "CENTRED  |  FIRE RELEASED";
            var parts = new List<string>();
            if (state.HasFlag(SpectrumJoystickState.Left)) parts.Add("LEFT");
            if (state.HasFlag(SpectrumJoystickState.Right)) parts.Add("RIGHT");
            if (state.HasFlag(SpectrumJoystickState.Up)) parts.Add("UP");
            if (state.HasFlag(SpectrumJoystickState.Down)) parts.Add("DOWN");
            if (state.HasFlag(SpectrumJoystickState.Fire)) parts.Add("FIRE");
            return string.Join("  +  ", parts);
        }

        private static void PopulateSelector(
            ComboBox selector,
            string itemName,
            int availableCount,
            int selectedValue,
            bool allowUnassigned)
        {
            var choices = new List<MappingChoice>();
            if (allowUnassigned)
                choices.Add(new MappingChoice("Not assigned", -1));
            int count = availableCount >= 0 ? availableCount : Math.Max(8, selectedValue + 1);
            for (int index = 0; index < count; index++)
                choices.Add(new MappingChoice($"{itemName} {index + 1}", index));
            if (choices.Count == 0)
                choices.Add(new MappingChoice("Not available", -1));
            selector.Items.Clear();
            selector.Items.AddRange(choices.Cast<object>().ToArray());
            int selectedIndex = choices.FindIndex(choice => choice.Value == selectedValue);
            selector.SelectedIndex = selectedIndex >= 0 ? selectedIndex : 0;
        }

        private static int GetSelectedValue(ComboBox selector) =>
            selector.SelectedItem is MappingChoice choice ? choice.Value : -1;

        private static void AddRow(
            TableLayoutPanel layout,
            int row,
            string label,
            Control control,
            bool alignLabelToTop = false)
        {
            layout.Controls.Add(new Label
            {
                Text = label,
                AutoSize = true,
                Anchor = alignLabelToTop
                    ? AnchorStyles.Left | AnchorStyles.Top
                    : AnchorStyles.Left,
                Margin = alignLabelToTop
                    ? new Padding(0, 10, 0, 6)
                    : new Padding(0, 6, 0, 6)
            }, 0, row);
            control.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            control.Margin = new Padding(8, 4, 0, 4);
            layout.Controls.Add(control, 1, row);
        }

        private sealed record DeviceChoice(string Label, string? Id, string? ProfileId)
        {
            public override string ToString() => Label;
        }

        private sealed record MappingChoice(string Label, int Value)
        {
            public override string ToString() => Label;
        }

        private sealed record InterfaceChoice(string Label, SpectrumJoystickInterface Value)
        {
            public override string ToString() => Label;
        }
    }
}
