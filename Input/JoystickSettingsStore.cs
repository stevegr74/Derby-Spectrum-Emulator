using System.Text.Json;

namespace Derby
{
    public sealed class JoystickSettingsStore
    {
        private readonly string path;
        private readonly string? legacyPath;

        public JoystickSettingsStore(string? path = null, string? legacyPath = null)
        {
            string localData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            this.path = path ?? Path.Combine(localData, "Derby", "joystick-settings.json");
            this.legacyPath = legacyPath ?? (path == null
                ? Path.Combine(localData, "Spectrum128kEmulator", "joystick-settings.json")
                : null);
        }

        public HostJoystickSettings Load()
        {
            try
            {
                string? sourcePath = File.Exists(path)
                    ? path
                    : legacyPath != null && File.Exists(legacyPath) ? legacyPath : null;
                if (sourcePath == null)
                    return new HostJoystickSettings();
                HostJoystickSettings settings =
                    JsonSerializer.Deserialize<HostJoystickSettings>(File.ReadAllText(sourcePath)) ??
                    new HostJoystickSettings();
                if (sourcePath == legacyPath)
                    Save(settings);
                return settings;
            }
            catch
            {
                return new HostJoystickSettings();
            }
        }

        public void Save(HostJoystickSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);
            string? directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);
            File.WriteAllText(path, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
        }
    }
}
