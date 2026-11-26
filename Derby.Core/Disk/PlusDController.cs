namespace Derby.Disk;

// Read-only, polled WD1772 subset. The drive uses a synthetic index and
// byte-ready cadence rather than a magnetic track or mechanical drive model.
internal sealed class PlusDController
{
    private const byte MotorOn = 0x80;
    private const byte WriteProtect = 0x40;
    private const byte RecordNotFound = 0x10;
    private const byte TrackZero = 0x04;
    private const byte DataRequest = 0x02;
    private const byte Busy = 0x01;
    private const int BytePeriodsPerRevolution = 6250;
    private const int BytePeriodsPerIndexPulse = 125;

    private MgtImage? disk;
    private byte driveControl;
    private byte track;
    private byte sector;
    private byte data;
    private byte status;
    private int head;
    private int stepDirection = 1;
    private int readOffset = -1;
    private int transferSide;
    private int transferSector;
    private bool multiple;
    private bool typeOneStatus;
    private ulong readyAt;

    public void SetDisk(MgtImage? image)
    {
        disk = image;
        CancelCommand();
    }

    public void Reset()
    {
        driveControl = track = sector = data = 0;
        head = 0;
        stepDirection = 1;
        CancelCommand();
    }

    public void SelectDrive(byte value)
    {
        if (((driveControl ^ value) & 0x83) != 0 && readOffset >= 0)
            CancelCommand();
        driveControl = value;
    }

    public byte ReadRegister(int register, ulong now, int byteTime)
    {
        bool ready = readOffset >= 0 && now >= readyAt;
        switch (register)
        {
            case 0:
                bool index = typeOneStatus && (status & MotorOn) != 0 && disk != null &&
                    (driveControl & 1) != 0 && now % (ulong)(byteTime * BytePeriodsPerRevolution) <
                    (ulong)(byteTime * BytePeriodsPerIndexPulse);
                return (byte)(status | (readOffset >= 0 ? Busy : 0) | (ready || index ? DataRequest : 0));
            case 1:
                return track;
            case 2:
                return sector;
            default:
                if (!ready)
                    return data;

                data = disk!.GetSector(head, transferSide, transferSector)[readOffset++];
                readyAt = now + (ulong)byteTime;
                if (readOffset == MgtImage.SectorSize)
                {
                    readOffset = -1;
                    if (multiple)
                    {
                        sector = (byte)++transferSector;
                        if (transferSector <= MgtImage.SectorsPerTrack)
                            readOffset = 0;
                        else
                            status |= RecordNotFound;
                    }
                }
                return data;
        }
    }

    public void WriteRegister(int register, byte value, ulong now, int byteTime)
    {
        switch (register)
        {
            case 0:
                StartCommand(value, now, byteTime);
                break;
            case 1:
                if (readOffset < 0)
                    track = value;
                break;
            case 2:
                if (readOffset < 0)
                    sector = value;
                break;
            case 3:
                data = value;
                break;
        }
    }

    private void StartCommand(byte command, ulong now, int byteTime)
    {
        int operation = command >> 4;
        if (operation == 0xD)
        {
            CancelCommand();
            typeOneStatus = true;
            status = (byte)(MotorOn | (head == 0 ? TrackZero : 0) | WriteProtect);
            return;
        }
        if (readOffset >= 0)
            return;
        status = MotorOn;
        typeOneStatus = operation < 8;

        if (operation < 8)
        {
            if (operation is 0 or 1)
            {
                head = operation == 0 ? 0 : data;
                track = (byte)head;
            }
            else
            {
                if (operation >= 4)
                    stepDirection = operation < 6 ? 1 : -1;
                head = Math.Clamp(head + stepDirection, 0, 255);
                if ((operation & 1) != 0)
                    track = (byte)head;
            }
            status |= 0x20 | WriteProtect;
            if (head == 0)
                status |= TrackZero;
            return;
        }

        if (operation is 8 or 9)
        {
            transferSide = (driveControl >> 7) & 1;
            transferSector = sector;
            if ((driveControl & 1) == 0 || disk == null || track != head ||
                disk.GetSector(head, transferSide, transferSector).IsEmpty)
            {
                status |= RecordNotFound;
                return;
            }
            readOffset = 0;
            multiple = operation == 9;
            readyAt = now + (ulong)byteTime;
            return;
        }

        status |= operation is 0xA or 0xB or 0xF ? WriteProtect : RecordNotFound;
    }

    private void CancelCommand()
    {
        readOffset = -1;
        multiple = false;
        typeOneStatus = false;
        status = 0;
        readyAt = 0;
    }
}
