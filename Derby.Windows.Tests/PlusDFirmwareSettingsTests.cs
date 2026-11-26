using Xunit;

namespace Derby.Windows.Tests;

public sealed class PlusDFirmwareSettingsTests
{
    [Fact]
    public void RememberedFirmwareCanBeReloadedUntilItIsMissingOrInvalid()
    {
        DirectoryInfo folder = Directory.CreateTempSubdirectory();
        try
        {
            string romPath = Path.Combine(folder.FullName, "test-plusd.rom");
            string settingsPath = Path.Combine(folder.FullName, "settings", "plusd-rom-path.txt");
            var settings = new PlusDFirmwareSettings(settingsPath);
            byte[] firmware = new byte[8192];
            firmware[0] = 0xF3;

            Assert.Null(settings.TryLoad());
            File.WriteAllBytes(romPath, firmware);
            settings.Remember(romPath);

            var nextSession = new PlusDFirmwareSettings(settingsPath);
            Assert.Equal(firmware, nextSession.TryLoad());
            File.WriteAllBytes(romPath, new byte[16384]);
            Assert.Throws<InvalidDataException>(() => PlusDFirmwareSettings.ReadFirmware(romPath));
            Assert.Null(nextSession.TryLoad());
            File.Delete(romPath);
            Assert.Null(nextSession.TryLoad());
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }
}
