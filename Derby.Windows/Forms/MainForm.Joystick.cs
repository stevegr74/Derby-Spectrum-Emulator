using Derby.Input;

namespace Derby
{
    public partial class MainForm
    {
        private string? automaticController1DeviceId;
        private string? automaticController1ProfileId;
        private string? automaticController2DeviceId;
        private string? automaticController2ProfileId;

        private void InitializeJoystickMenu()
        {
            joystickMenuItem.DropDownOpening += (_, _) => RebuildJoystickMenu();
            RebuildJoystickMenu();
        }

        private void RebuildJoystickMenu()
        {
            joystickMenuItem.DropDownItems.Clear();
            HostJoystickSettings settings = GetJoystickSettingsSnapshot();
            IReadOnlyList<HostJoystickDevice> devices = joystickSource.GetDevices();
            polledJoystickDevices = devices;
            nextJoystickDeviceRefreshTicks = frameClock.ElapsedTicks + System.Diagnostics.Stopwatch.Frequency;

            var claimed = new HashSet<string>(StringComparer.Ordinal);
            HostJoystickDevice? first = ResolveDevice(1, settings.Controller1Settings, devices, claimed);
            if (first != null)
                claimed.Add(first.Id);
            HostJoystickDevice? second = ResolveDevice(2, settings.Controller2Settings, devices, claimed);
            IReadOnlyDictionary<string, string> labels = HostJoystickDeviceIdentity.CreateDisplayLabels(devices);

            AddControllerMenu("Controller 1", 1, settings.Controller1Settings, devices, first, labels);
            AddControllerMenu("Controller 2", 2, settings.Controller2Settings, devices, second, labels);
            joystickMenuItem.DropDownItems.Add(new ToolStripSeparator());
            var configure = new ToolStripMenuItem("Configure both controllers...");
            configure.Click += (_, _) => ConfigureJoystick();
            joystickMenuItem.DropDownItems.Add(configure);
        }

        private void AddControllerMenu(
            string label,
            int controllerNumber,
            HostJoystickChannelSettings channel,
            IReadOnlyList<HostJoystickDevice> devices,
            HostJoystickDevice? resolvedDevice,
            IReadOnlyDictionary<string, string> labels)
        {
            var controllerMenu = new ToolStripMenuItem(label);
            var disabled = new ToolStripMenuItem("Off") { Checked = !channel.Enabled };
            disabled.Click += (_, _) => SelectJoystick(controllerNumber, enabled: false, preferredDeviceId: null);
            controllerMenu.DropDownItems.Add(disabled);

            string automaticLabel = resolvedDevice == null
                ? "Automatic (no controller available)"
                : $"Automatic -> {labels[resolvedDevice.Id]}";
            var automatic = new ToolStripMenuItem(automaticLabel)
            {
                Checked = channel.Enabled &&
                          channel.PreferredDeviceId == null &&
                          channel.PreferredDeviceProfileId == null
            };
            automatic.Click += (_, _) => SelectJoystick(
                controllerNumber,
                enabled: true,
                preferredDeviceId: null,
                preferredDeviceProfileId: null);
            controllerMenu.DropDownItems.Add(automatic);
            controllerMenu.DropDownItems.Add(new ToolStripSeparator());

            foreach (HostJoystickDevice device in devices)
            {
                var item = new ToolStripMenuItem(labels[device.Id])
                {
                    Checked = channel.Enabled &&
                              resolvedDevice?.Id == device.Id &&
                              (channel.PreferredDeviceId != null || channel.PreferredDeviceProfileId != null),
                    ToolTipText = HostJoystickDeviceIdentity.DescribeHardware(device)
                };
                string deviceId = device.Id;
                string profileId = device.ProfileId;
                item.Click += (_, _) => SelectJoystick(
                    controllerNumber,
                    enabled: true,
                    preferredDeviceId: deviceId,
                    preferredDeviceProfileId: profileId);
                controllerMenu.DropDownItems.Add(item);
            }

            if (devices.Count == 0)
                controllerMenu.DropDownItems.Add(new ToolStripMenuItem("No controllers detected") { Enabled = false });
            else if (channel.Enabled &&
                     (channel.PreferredDeviceId != null || channel.PreferredDeviceProfileId != null) &&
                     resolvedDevice == null)
                controllerMenu.DropDownItems.Add(new ToolStripMenuItem("Selected controller unavailable") { Enabled = false });

            controllerMenu.DropDownItems.Add(new ToolStripSeparator());
            controllerMenu.DropDownItems.Add(
                new ToolStripMenuItem($"Interface: {FormatJoystickInterface(channel.EffectiveInterface)}") { Enabled = false });
            joystickMenuItem.DropDownItems.Add(controllerMenu);
        }

        private void SelectJoystick(
            int controllerNumber,
            bool enabled,
            string? preferredDeviceId,
            string? preferredDeviceProfileId = null)
        {
            HostJoystickSettings current = GetJoystickSettingsSnapshot();
            HostJoystickChannelSettings channel = GetControllerSettings(current, controllerNumber) with
            {
                Enabled = enabled,
                PreferredDeviceId = preferredDeviceId,
                PreferredDeviceProfileId = preferredDeviceProfileId
            };
            HostJoystickChannelSettings other = GetControllerSettings(current, controllerNumber == 1 ? 2 : 1);
            if (channel.Enabled && other.Enabled && channel.EffectiveInterface == other.EffectiveInterface)
            {
                MessageBox.Show(
                    this,
                    "Controller 1 and Controller 2 must use different Spectrum interfaces. Use Configure both controllers to choose the mappings.",
                    "Joystick Configuration",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            if (channel.Enabled && other.Enabled &&
                PreferencesClaimSameDevice(channel, other, polledJoystickDevices))
            {
                MessageBox.Show(
                    this,
                    "The same USB controller cannot be assigned to both controller slots.",
                    "Joystick Configuration",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            HostJoystickSettings updated;
            lock (inputStateLock)
            {
                joystickSettings = SetControllerSettings(joystickSettings, controllerNumber, channel);
                pendingJoystickReset = true;
                updated = joystickSettings;
            }

            joystickSettingsStore.Save(updated);
            RebuildJoystickMenu();
            screenBox.Focus();
        }

        private void ConfigureJoystick()
        {
            using IDisposable pauseLease = AcquireEmulationPauseLease();
            HostJoystickSettings settings = GetJoystickSettingsSnapshot();
            IReadOnlyList<HostJoystickDevice> devices = joystickSource.GetDevices();
            using var dialog = new JoystickConfigurationForm(
                settings.Controller1Settings,
                settings.Controller2Settings,
                devices,
                joystickSource,
                settings.DeviceProfiles);
            if (dialog.ShowDialog(this) != DialogResult.OK)
                return;

            HostJoystickSettings updated;
            lock (inputStateLock)
            {
                joystickSettings = settings
                    .WithController1(dialog.Controller1Settings) with
                    {
                        Controller2 = dialog.Controller2Settings,
                        DeviceProfiles = dialog.DeviceProfiles
                    };
                pendingJoystickReset = true;
                updated = joystickSettings;
            }

            joystickSettingsStore.Save(updated);
            RebuildJoystickMenu();
            screenBox.Focus();
        }

        private HostJoystickSettings GetJoystickSettingsSnapshot()
        {
            lock (inputStateLock)
                return joystickSettings;
        }

        private void PollJoystick(SpectrumMachine activeMachine)
        {
            HostJoystickSettings settings;
            bool reset;
            lock (inputStateLock)
            {
                settings = joystickSettings;
                reset = pendingJoystickReset;
                pendingJoystickReset = false;
            }

            long now = frameClock.ElapsedTicks;
            if (!reset && now < nextJoystickPollTicks)
                return;
            nextJoystickPollTicks = now + (System.Diagnostics.Stopwatch.Frequency / 200);

            if (reset || now >= nextJoystickDeviceRefreshTicks)
            {
                polledJoystickDevices = joystickSource.GetDevices();
                nextJoystickDeviceRefreshTicks = now + System.Diagnostics.Stopwatch.Frequency;
            }
            IReadOnlyList<HostJoystickDevice> devices = polledJoystickDevices;
            var claimedDeviceIds = new HashSet<string>(StringComparer.Ordinal);
            var states = new Dictionary<SpectrumJoystickInterface, SpectrumJoystickState>();
            PollController(1, settings, settings.Controller1Settings, devices, claimedDeviceIds, states);
            PollController(2, settings, settings.Controller2Settings, devices, claimedDeviceIds, states);

            activeMachine.ClearJoystick();
            foreach ((SpectrumJoystickInterface joystickInterface, SpectrumJoystickState state) in states)
                activeMachine.SetJoystickState(joystickInterface, state);
        }

        private void PollController(
            int controllerNumber,
            HostJoystickSettings settings,
            HostJoystickChannelSettings channel,
            IReadOnlyList<HostJoystickDevice> devices,
            HashSet<string> claimedDeviceIds,
            Dictionary<SpectrumJoystickInterface, SpectrumJoystickState> states)
        {
            if (!channel.Enabled)
                return;

            HostJoystickDevice? device = ResolveDevice(controllerNumber, channel, devices, claimedDeviceIds);
            if (device == null)
                return;

            claimedDeviceIds.Add(device.Id);
            SpectrumJoystickInterface joystickInterface = channel.EffectiveInterface;
            HostJoystickChannelSettings effectiveChannel = channel with
            {
                Mapping = settings.GetMapping(device, channel)
            };
            SpectrumJoystickState state = joystickInputController.Poll(effectiveChannel, device.Id);
            if (joystickInterface != SpectrumJoystickInterface.None)
                states[joystickInterface] = states.GetValueOrDefault(joystickInterface) | state;
        }

        private HostJoystickDevice? ResolveDevice(
            int controllerNumber,
            HostJoystickChannelSettings channel,
            IReadOnlyList<HostJoystickDevice> devices,
            HashSet<string> claimedDeviceIds)
        {
            string? previousId = controllerNumber == 1
                ? automaticController1DeviceId
                : automaticController2DeviceId;
            string? previousProfileId = controllerNumber == 1
                ? automaticController1ProfileId
                : automaticController2ProfileId;
            HostJoystickDevice? resolved = HostJoystickDeviceResolver.Resolve(
                channel,
                devices,
                claimedDeviceIds,
                previousId,
                previousProfileId);

            bool automatic = channel.PreferredDeviceId == null && channel.PreferredDeviceProfileId == null;
            if (automatic && resolved != null)
            {
                if (controllerNumber == 1)
                {
                    automaticController1DeviceId = resolved.Id;
                    automaticController1ProfileId = resolved.ProfileId;
                }
                else
                {
                    automaticController2DeviceId = resolved.Id;
                    automaticController2ProfileId = resolved.ProfileId;
                }
            }

            return resolved;
        }

        private static bool DeviceMatchesPreference(HostJoystickDevice device, string? preferredDeviceId) =>
            HostJoystickDeviceResolver.MatchesPreference(device, preferredDeviceId);

        private static bool PreferencesClaimSameDevice(
            HostJoystickChannelSettings firstChannel,
            HostJoystickChannelSettings secondChannel,
            IReadOnlyList<HostJoystickDevice> devices)
        {
            string? firstPreference = firstChannel.PreferredDeviceId;
            string? secondPreference = secondChannel.PreferredDeviceId;
            if (firstPreference == null || secondPreference == null)
                return false;
            if (string.Equals(firstPreference, secondPreference, StringComparison.Ordinal))
                return true;

            HostJoystickDevice? first = devices.FirstOrDefault(device =>
                DeviceMatchesPreference(device, firstPreference));
            HostJoystickDevice? second = devices.FirstOrDefault(device =>
                DeviceMatchesPreference(device, secondPreference));
            return first != null && second != null && first.Id == second.Id;
        }

        private void ResetJoystickInputBridgeState()
        {
            lock (inputStateLock)
                pendingJoystickReset = true;
            nextJoystickPollTicks = 0;
            nextJoystickDeviceRefreshTicks = 0;
        }

        private static HostJoystickChannelSettings GetControllerSettings(
            HostJoystickSettings settings,
            int controllerNumber) =>
            controllerNumber == 1 ? settings.Controller1Settings : settings.Controller2Settings;

        private static HostJoystickSettings SetControllerSettings(
            HostJoystickSettings settings,
            int controllerNumber,
            HostJoystickChannelSettings channel) =>
            controllerNumber == 1
                ? settings.WithController1(channel)
                : settings with { Controller2 = channel };

        private static string FormatJoystickInterface(SpectrumJoystickInterface joystickInterface) => joystickInterface switch
        {
            SpectrumJoystickInterface.Kempston => "Kempston",
            SpectrumJoystickInterface.Cursor => "Cursor (keys 5, 8, 6, 7, 0)",
            SpectrumJoystickInterface.SinclairKeys1To5 => "Sinclair 1 (keys 1-5; Interface II socket 2)",
            SpectrumJoystickInterface.SinclairKeys6To0 => "Sinclair 2 (keys 6-0; Interface II socket 1)",
            _ => "None"
        };
    }
}
