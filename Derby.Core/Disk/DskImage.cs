using System.Buffers.Binary;

namespace Derby.Disk;

public sealed class DskImage
{
    private readonly Dictionary<(int Cylinder, int Head), Sector[]> tracks = new();
    internal sealed record Sector(byte C, byte H, byte R, byte N, byte St1, byte St2, byte[] Data);

    private DskImage() { }

    public static DskImage Load(string path)
    {
        using var stream = File.OpenRead(path);
        if (stream.Length > 16 * 1024 * 1024)
            throw new InvalidDataException("The disk image exceeds the supported size.");
        byte[] data = new byte[(int)stream.Length];
        stream.ReadExactly(data);
        return Parse(data);
    }

    public static DskImage Parse(ReadOnlySpan<byte> data)
    {
        bool extended = data.StartsWith("EXTENDED CPC DSK File\r\nDisk-Info\r\n"u8);
        if (data.Length < 256 || (!extended && !data.StartsWith("MV - CPCEMU Disk-File\r\nDisk-Info\r\n"u8)))
            throw new InvalidDataException("Not a standard or Extended CPC DSK image.");

        int cylinders = data[48], heads = data[49];
        if (cylinders == 0 || heads is not (1 or 2) || cylinders * heads > 204)
            throw new InvalidDataException("Unsupported DSK track geometry.");

        var image = new DskImage();
        int offset = 256;
        for (int slot = 0; slot < cylinders * heads; slot++)
        {
            int size = extended ? data[52 + slot] * 256 : BinaryPrimitives.ReadUInt16LittleEndian(data[50..]);
            if (size == 0 && extended)
                continue;
            if (size < 256 || size > data.Length - offset)
                throw new InvalidDataException("Truncated or invalid DSK track.");

            ReadOnlySpan<byte> track = data.Slice(offset, size);
            offset += size;
            int count = track[21];
            if (!track.StartsWith("Track-Info\r\n"u8) || count > 29 || (!extended && track[20] > 7))
                throw new InvalidDataException("Invalid DSK track header.");

            var sectors = new Sector[count];
            int position = 256;
            for (int i = 0; i < count; i++)
            {
                ReadOnlySpan<byte> id = track.Slice(24 + 8 * i, 8);
                int length = extended ? BinaryPrimitives.ReadUInt16LittleEndian(id[6..]) : 128 << track[20];
                if (length > size - position)
                    throw new InvalidDataException("Truncated DSK sector data.");
                sectors[i] = new Sector(id[0], id[1], id[2], id[3], id[4], id[5], track.Slice(position, length).ToArray());
                position += length;
            }
            image.tracks.Add((slot / heads, slot % heads), sectors);
        }
        return image;
    }

    internal Sector[] GetTrack(int cylinder, int head) =>
        tracks.TryGetValue((cylinder, head), out Sector[]? sectors) ? sectors : [];
}
