namespace Derby.Disk;

// Read-only, polled non-DMA subset of the uPD765 used by the +3.
internal sealed class Plus3DiskController
{
    private readonly byte[] command = new byte[9];
    private readonly Queue<(byte Status, byte Cylinder)> interrupts = new();
    private readonly Queue<byte> result = new();
    private readonly Queue<ReadOnlyMemory<byte>> transfer = new();
    private int commandLength;
    private int expectedLength;
    private int byteIndex;
    private byte cylinder;
    private int sectorCursor;

    internal DskImage? Image { get; private set; }
    internal bool MotorOn { get; set; }
    internal ulong? LastAccess { get; private set; }
    internal byte Status => transfer.Count != 0 ? (byte)0xF0 :
        result.Count != 0 ? (byte)0xD0 : commandLength != 0 ? (byte)0x90 : (byte)0x80;

    internal void Reset()
    {
        ClearTransfer();
        cylinder = 0;
        MotorOn = false;
        interrupts.Clear();
    }

    private void ClearTransfer()
    {
        commandLength = 0;
        byteIndex = 0;
        sectorCursor = 0;
        result.Clear();
        transfer.Clear();
        LastAccess = null;
    }

    internal void SetDisk(DskImage? image)
    {
        ClearTransfer();
        interrupts.Clear();
        Image = image;
    }

    internal byte ReadData(ulong now)
    {
        if (transfer.TryPeek(out ReadOnlyMemory<byte> sector))
        {
            LastAccess = now;
            byte value = sector.Span[byteIndex++];
            if (byteIndex == sector.Length)
            {
                byteIndex = 0;
                transfer.Dequeue();
            }
            return value;
        }
        return result.TryDequeue(out byte response) ? response : (byte)0xFF;
    }

    internal void WriteData(byte value, ulong now)
    {
        if (result.Count != 0 || transfer.Count != 0)
            return;
        if (commandLength == 0)
        {
            expectedLength = (value & 0x1F) switch
            {
                0x03 or 0x0F => 3,
                0x04 or 0x07 or 0x0A => 2,
                0x05 or 0x06 or 0x09 or 0x0C => 9,
                0x0D => 6,
                _ => 1
            };
        }
        command[commandLength++] = value;
        if (commandLength == expectedLength)
        {
            Execute(now);
            commandLength = 0;
        }
    }

    private void Execute(ulong now)
    {
        byte unit = (byte)(command[1] & 7);
        bool driveA = (unit & 3) == 0;
        bool ready = driveA && MotorOn && Image != null;
        switch (command[0] & 0x1F)
        {
            case 0x03: // Specify.
                return;
            case 0x04: // Sense drive status.
                result.Enqueue((byte)(unit | (driveA && cylinder == 0 ? 0x10 : 0) | (ready ? 0x60 : 0)));
                return;
            case 0x07: // Recalibrate.
            case 0x0F: // Seek.
                if (driveA)
                {
                    cylinder = (command[0] & 0x1F) == 0x07 ? (byte)0 : command[2];
                    sectorCursor = 0;
                    if (ready)
                        LastAccess = now;
                }
                if (interrupts.Count == 4)
                    interrupts.Dequeue();
                interrupts.Enqueue(((byte)(unit | (driveA ? 0x20 : 0x68)), driveA ? cylinder : (byte)0));
                return;
            case 0x08: // Sense interrupt status.
                if (interrupts.TryDequeue(out var interrupt))
                {
                    result.Enqueue(interrupt.Status);
                    result.Enqueue(interrupt.Cylinder);
                }
                else
                    result.Enqueue(0x80);
                return;
            case 0x0A: // Read ID.
                DskImage.Sector[] ids = ready ? Image!.GetTrack(cylinder, unit >> 2) : [];
                if (ids.Length == 0)
                    ReturnSectorResult((byte)(unit | (ready ? 0x40 : 0x48)), 0x01, 0, cylinder, (byte)(unit >> 2), 0, 0);
                else
                {
                    LastAccess = now;
                    DskImage.Sector id = ids[sectorCursor++ % ids.Length];
                    ReturnSectorResult(unit, 0, 0, id.C, id.H, id.R, id.N);
                }
                return;
            case 0x06: // Read data.
            case 0x0C: // Read deleted data.
                ReadSectors(unit, ready, now);
                return;
            case 0x05: // Write data.
            case 0x09: // Write deleted data.
            case 0x0D: // Format track.
                ReturnSectorResult((byte)(unit | 0x40), 0x02, 0, cylinder,
                    (byte)(unit >> 2), 0, 0);
                return;
            default:
                result.Enqueue(0x80);
                return;
        }
    }

    private void ReadSectors(byte unit, bool ready, ulong now)
    {
        byte c = command[2], h = command[3], r = command[4], n = command[5];
        byte st0 = unit, st1 = 0, st2 = 0;
        if (!ready)
        {
            ReturnSectorResult((byte)(unit | 0x48), 0x04, 0, c, h, r, n);
            return;
        }
        LastAccess = now;
        DskImage.Sector[] sectors = Image!.GetTrack(cylinder, unit >> 2);
        int length = n == 0 ? Math.Min(128, (int)command[8]) : n <= 7 ? 128 << n : 0;
        if (length == 0 && n == 0)
            length = 128;
        int opcode = command[0] & 0x1F;
        bool skipDeleted = (command[0] & 0x20) != 0;
        for (int id = r; id <= command[6]; id++)
        {
            r = (byte)id;
            DskImage.Sector? sector = Array.Find(sectors, s => s.C == c && s.H == h && s.R == r && s.N == n);
            if (sector == null || length == 0 || sector.Data.Length < length)
            {
                st1 = 0x04;
                break;
            }

            st1 = sector.St1;
            st2 = sector.St2;
            bool wrongMark = ((st2 & 0x40) != 0) != (opcode == 0x0C);
            if (wrongMark && skipDeleted)
                continue;
            st2 = (byte)((st2 & ~0x40) | (wrongMark ? 0x40 : 0));
            transfer.Enqueue(sector.Data.AsMemory(0, length));
            if (st1 != 0 || st2 != 0)
                break;
        }
        if (st1 == 0 && st2 == 0)
            st1 = transfer.Count == 0 ? (byte)0x04 : (byte)0x80;
        st0 |= 0x40;
        ReturnSectorResult(st0, st1, st2, c, h, r, n);
    }

    private void ReturnSectorResult(byte st0, byte st1, byte st2, byte c, byte h, byte r, byte n)
    {
        foreach (byte value in new[] { st0, st1, st2, c, h, r, n })
            result.Enqueue(value);
    }
}
