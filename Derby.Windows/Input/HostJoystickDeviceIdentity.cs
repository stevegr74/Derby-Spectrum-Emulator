namespace Derby
{
    public static class HostJoystickDeviceIdentity
    {
        private static readonly IReadOnlyDictionary<uint, string> KnownProductNames =
            new Dictionary<uint, string>
            {
                [HardwareKey(0x054C, 0x09CC)] = "Sony DualShock 4",
                [HardwareKey(0x0738, 0x2217)] = "Speedlink Competition Pro"
            };

        public static string CreateProfileId(HostJoystickDevice device)
        {
            ArgumentNullException.ThrowIfNull(device);
            string hardware = device.HardwareVendorId != 0 || device.HardwareProductId != 0
                ? $"{device.HardwareVendorId:X4}:{device.HardwareProductId:X4}"
                : Normalize(device.DisplayName);
            return $"{hardware}:{device.AxisCount}:{device.ButtonCount}:{device.SwitchCount}";
        }

        public static IReadOnlyDictionary<string, string> CreateDisplayLabels(
            IReadOnlyList<HostJoystickDevice> devices)
        {
            ArgumentNullException.ThrowIfNull(devices);
            var labels = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (IGrouping<string, HostJoystickDevice> group in devices
                         .GroupBy(CreateBaseLabel, StringComparer.CurrentCultureIgnoreCase))
            {
                HostJoystickDevice[] matches = group
                    .OrderBy(device => device.ProfileId, StringComparer.Ordinal)
                    .ThenBy(device => device.Id, StringComparer.Ordinal)
                    .ToArray();
                for (int index = 0; index < matches.Length; index++)
                {
                    string suffix = matches.Length > 1 ? $" #{index + 1}" : string.Empty;
                    labels[matches[index].Id] = group.Key + suffix;
                }
            }

            return labels;
        }

        public static string DescribeHardware(HostJoystickDevice device)
        {
            ArgumentNullException.ThrowIfNull(device);
            string identity = device.HardwareVendorId != 0 || device.HardwareProductId != 0
                ? $"VID {device.HardwareVendorId:X4} / PID {device.HardwareProductId:X4}"
                : "Hardware identity unavailable";
            return $"{identity}  |  {device.AxisCount} axes  |  {device.SwitchCount} hats  |  {device.ButtonCount} buttons";
        }

        private static string CreateBaseLabel(HostJoystickDevice device)
        {
            string displayName = string.IsNullOrWhiteSpace(device.DisplayName)
                ? "USB game controller"
                : device.DisplayName.Trim();
            if (!IsGenericName(displayName))
                return displayName;

            if (KnownProductNames.TryGetValue(
                    HardwareKey(device.HardwareVendorId, device.HardwareProductId),
                    out string? productName))
                return productName;

            return device.HardwareVendorId != 0 || device.HardwareProductId != 0
                ? $"Game controller [{device.HardwareVendorId:X4}:{device.HardwareProductId:X4}]"
                : "USB game controller";
        }

        private static uint HardwareKey(ushort vendorId, ushort productId) =>
            ((uint)vendorId << 16) | productId;

        private static bool IsGenericName(string value) =>
            value.Equals("HID-compliant game controller", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("USB game controller", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("Game controller", StringComparison.OrdinalIgnoreCase);

        private static string Normalize(string value)
        {
            Span<char> buffer = stackalloc char[value.Length];
            int length = 0;
            foreach (char character in value)
            {
                if (!char.IsLetterOrDigit(character))
                    continue;
                buffer[length++] = char.ToUpperInvariant(character);
            }

            return length == 0 ? "UNKNOWN" : new string(buffer[..length]);
        }
    }

    public static class HostJoystickDeviceResolver
    {
        public static HostJoystickDevice? Resolve(
            HostJoystickChannelSettings channel,
            IReadOnlyList<HostJoystickDevice> devices,
            ISet<string> claimedDeviceIds,
            string? previousAutomaticDeviceId = null,
            string? previousAutomaticProfileId = null)
        {
            ArgumentNullException.ThrowIfNull(channel);
            ArgumentNullException.ThrowIfNull(devices);
            ArgumentNullException.ThrowIfNull(claimedDeviceIds);

            if (channel.PreferredDeviceId != null || channel.PreferredDeviceProfileId != null)
            {
                HostJoystickDevice? exact = devices.FirstOrDefault(device =>
                    !claimedDeviceIds.Contains(device.Id) &&
                    MatchesPreference(device, channel.PreferredDeviceId));
                if (exact != null)
                    return exact;

                return devices.FirstOrDefault(device =>
                    !claimedDeviceIds.Contains(device.Id) &&
                    string.Equals(
                        device.ProfileId,
                        channel.PreferredDeviceProfileId,
                        StringComparison.Ordinal));
            }

            HostJoystickDevice? previous = devices.FirstOrDefault(device =>
                !claimedDeviceIds.Contains(device.Id) &&
                string.Equals(device.Id, previousAutomaticDeviceId, StringComparison.Ordinal));
            if (previous != null)
                return previous;

            previous = devices.FirstOrDefault(device =>
                !claimedDeviceIds.Contains(device.Id) &&
                string.Equals(device.ProfileId, previousAutomaticProfileId, StringComparison.Ordinal));
            return previous ?? devices.FirstOrDefault(device => !claimedDeviceIds.Contains(device.Id));
        }

        public static bool MatchesPreference(HostJoystickDevice device, string? preferredDeviceId) =>
            preferredDeviceId != null &&
            (string.Equals(device.Id, preferredDeviceId, StringComparison.Ordinal) ||
             string.Equals(device.LegacyId, preferredDeviceId, StringComparison.Ordinal));
    }

    public static class HostJoystickIdentification
    {
        private const double AxisMovementThreshold = 0.18;

        public static bool HasMeaningfulChange(
            HostJoystickReading baseline,
            HostJoystickReading current)
        {
            ArgumentNullException.ThrowIfNull(baseline);
            ArgumentNullException.ThrowIfNull(current);
            if (!string.Equals(baseline.DeviceId, current.DeviceId, StringComparison.Ordinal))
                return false;

            int buttonCount = Math.Min(baseline.Buttons.Count, current.Buttons.Count);
            for (int index = 0; index < buttonCount; index++)
            {
                if (!baseline.Buttons[index] && current.Buttons[index])
                    return true;
            }

            int switchCount = Math.Min(baseline.Switches.Count, current.Switches.Count);
            for (int index = 0; index < switchCount; index++)
            {
                if (baseline.Switches[index] != current.Switches[index])
                    return true;
            }

            int axisCount = Math.Min(baseline.Axes.Count, current.Axes.Count);
            for (int index = 0; index < axisCount; index++)
            {
                if (Math.Abs(baseline.Axes[index] - current.Axes[index]) >= AxisMovementThreshold)
                    return true;
            }

            return false;
        }
    }
}
