using Xunit;

namespace Derby.Windows.Tests;

public sealed class PlusDFirmwareSettingsTests
{
    [Fact]
    public void CancellingBrowseOrResetDoesNotAuthoriseAttachmentOrRememberFirmware()
    {
        DirectoryInfo folder = Directory.CreateTempSubdirectory();
        try
        {
            string settingsPath = Path.Combine(folder.FullName, "plusd-rom-path.txt");
            string otherRom = Path.Combine(folder.FullName, "other.rom");
            byte[] firmware = new byte[8192];
            File.WriteAllBytes(otherRom, firmware);
            var settings = new PlusDFirmwareSettings(settingsPath);

            Assert.Null(settings.ResolveForAttachment(folder.FullName, () => null,
                () => throw new InvalidOperationException("Reset confirmation must not run after cancelling Browse.")));
            Assert.Null(settings.ResolveForAttachment(folder.FullName, () => otherRom, () => false));
            Assert.False(File.Exists(settingsPath));

            Assert.Equal(firmware, settings.ResolveForAttachment(folder.FullName, () => otherRom, () => true));
            Assert.Equal(otherRom, File.ReadAllText(settingsPath));
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }

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

            Assert.Null(settings.TryLoad(folder.FullName));
            File.WriteAllBytes(romPath, firmware);
            settings.Remember(romPath);

            var nextSession = new PlusDFirmwareSettings(settingsPath);
            Assert.Equal(firmware, nextSession.TryLoad(folder.FullName));
            File.WriteAllBytes(romPath, new byte[16384]);
            Assert.Throws<InvalidDataException>(() => PlusDFirmwareSettings.ReadFirmware(romPath));
            Assert.Null(nextSession.TryLoad(folder.FullName));
            File.Delete(romPath);
            Assert.Null(nextSession.TryLoad(folder.FullName));
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }

    [Fact]
    public void LocalFirmwareTakesPriorityAndInvalidLocalFirmwareFallsBack()
    {
        DirectoryInfo folder = Directory.CreateTempSubdirectory();
        try
        {
            string localPath = Path.Combine(folder.FullName, "plusd.rom");
            string rememberedPath = Path.Combine(folder.FullName, "other.rom");
            string settingsPath = Path.Combine(folder.FullName, "plusd-rom-path.txt");
            var settings = new PlusDFirmwareSettings(settingsPath);
            byte[] localFirmware = new byte[8192];
            byte[] rememberedFirmware = new byte[8192];
            rememberedFirmware[0] = 0xF3;

            File.WriteAllBytes(localPath, localFirmware);
            Assert.Equal(localFirmware, settings.TryLoad(folder.FullName));
            Assert.False(File.Exists(settingsPath));

            File.WriteAllBytes(rememberedPath, rememberedFirmware);
            settings.Remember(rememberedPath);
            Assert.Equal(localFirmware, settings.TryLoad(folder.FullName));
            File.WriteAllBytes(localPath, new byte[8191]);
            Assert.Equal(rememberedFirmware, settings.TryLoad(folder.FullName));
            Assert.Equal(rememberedPath, File.ReadAllText(settingsPath));
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }
}
