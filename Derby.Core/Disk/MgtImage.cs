namespace Derby.Disk;

public sealed class MgtImage
{
    public const int Tracks = 80;
    public const int Sides = 2;
    public const int SectorsPerTrack = 10;
    public const int SectorSize = 512;
    public const int ImageSize = Tracks * Sides * SectorsPerTrack * SectorSize;

    private readonly byte[] bytes;

    private MgtImage(byte[] bytes) => this.bytes = bytes;

    public static MgtImage Load(string path)
    {
        using FileStream stream = File.OpenRead(path);
        ValidateSize(stream.Length);
        byte[] bytes = new byte[ImageSize];
        stream.ReadExactly(bytes);
        return new MgtImage(bytes);
    }

    public static MgtImage Parse(ReadOnlySpan<byte> bytes)
    {
        ValidateSize(bytes.Length);
        return new MgtImage(bytes.ToArray());
    }

    public ReadOnlySpan<byte> GetSector(int track, int side, int sector)
    {
        if ((uint)track >= Tracks || (uint)side >= Sides || sector is < 1 or > SectorsPerTrack)
            return [];

        // MGT alternates sides per track. IMG stores complete sides instead.
        int offset = ((track * Sides + side) * SectorsPerTrack + sector - 1) * SectorSize;
        return bytes.AsSpan(offset, SectorSize);
    }

    private static void ValidateSize(long length)
    {
        if (length != ImageSize)
            throw new InvalidDataException("Expected a double-sided, 80-track MGT disk image (819,200 bytes).");
    }
}
