namespace Derby;

internal sealed class PlusDFirmwareSettings(string? settingsPath = null)
{
    private readonly string path = settingsPath ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Derby", "plusd-rom-path.txt");

    public byte[]? TryLoad()
    {
        try
        {
            return ReadFirmware(File.ReadAllText(path));
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    }

    public void Remember(string firmwarePath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, Path.GetFullPath(firmwarePath));
    }

    public static byte[] ReadFirmware(string path)
    {
        using FileStream stream = File.OpenRead(path);
        if (stream.Length != 0x2000)
            throw new InvalidDataException("The +D ROM must be exactly 8 KB.");
        byte[] firmware = new byte[0x2000];
        stream.ReadExactly(firmware);
        return firmware;
    }
}
