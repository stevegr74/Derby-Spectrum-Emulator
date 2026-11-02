using System;
using System.Security.Cryptography;

namespace Derby
{
    public sealed class SpectrumRomProfile
    {
        private const string BundledRom0Sha256 = "3BA308F23B9471D13D9BA30C23030059A9CE5D4B317B85B86274B132651D1425";
        private const string BundledRom1Sha256 = "8D93C3342321E9D1E51D60AFCD7D15F6A7AFD978C231B43435A7C0757C60B9A3";
        private const string Original48RomSha256 = "D55DAA439B673B0E3F5897F99AC37ECB45F974D1862B4DADB85DEC34AF99CB42";
        private const string Plus2Rom0Sha256 = "51DC881D1D3E8242972390224E7CFD8650987D9A620E086ACA9DFDF6988FCBF1";
        private const string Plus2Rom1Sha256 = "AE6D95F1E6D16C3D1C947B77E6087952F3204B7930C6CADAE3769289A1F5A1B9";
        private static readonly string[] Plus3RomSha256 =
        [
            "841D54545C743A7202FB9B068534ED906A08CC657A19955AE20A8DE5AF8AB185",
            "6D6CCAB28F255B01C8CD89CEAB9E7B60247F4610471118092F290F6C7768B7E3",
            "C5590212A269117657CE611ACBAC582E41E9DB87DC78DB480DA2615E413B2DA6",
            "8EC5CB9FD7EC24A041800314BB5A2C99BE02F7E50745194EB649ED6D9085BD1F"
        ];

        private SpectrumRomProfile(string name, bool supportsAddressBasedTapeServices)
        {
            Name = name;
            SupportsAddressBasedTapeServices = supportsAddressBasedTapeServices;
        }

        public string Name { get; }
        public bool SupportsAddressBasedTapeServices { get; }

        internal static SpectrumRomProfile SyntheticTest { get; } =
            new SpectrumRomProfile("Synthetic test ROMs", supportsAddressBasedTapeServices: true);

        internal ushort TapeReturnAddress => 0x053F;
        internal ushort LoadBytesTrapAddress => 0x056B;
        internal ushort LoadBytesSyncLoopAddress => 0x0574;
        internal ushort UsrReturnAddress => 0x2D2B;
        internal ushort EndCalcLiteralAddress => 0x2758;
        internal ushort BasicResumeExecutionLoopAddress => 0x1555;
        internal ushort TapeAutoStartExecutionLoopAddress => 0x12A2;
        internal ushort MainExecutionReportAddress => 0x1303;
        internal ushort BasicLineNewAddress => 0x1B9E;
        internal ushort KeyboardInputLoopAddress => 0x15E7;

        internal bool IsMountedLoadReturnAddress(ushort address)
        {
            return IsInRange(address, 0x15F7, 0x15FA) ||
                   IsInRange(address, 0x0E51, 0x0E5C) ||
                   IsInRange(address, 0x15FD, 0x1600) ||
                   IsInRange(address, 0x10A8, 0x10B4) ||
                   IsInRange(address, 0x1615, 0x16E4) ||
                   IsInRange(address, 0x3631, 0x3634);
        }

        internal bool IsTapeInputRoutinePortRead(ushort nextInstructionAddress)
        {
            if (!SupportsAddressBasedTapeServices)
                return false;

            // IN A,(FE) leaves PC immediately after its operand. These are the
            // two EAR reads in the bundled Sinclair ROM's LD-BYTES routine.
            return nextInstructionAddress == 0x0564 ||
                   nextInstructionAddress == 0x05F3;
        }

        internal static SpectrumRomProfile Detect(
            byte[] rom0,
            byte[] rom1,
            SpectrumMachineModel model = SpectrumMachineModel.Spectrum128K)
        {
            ArgumentNullException.ThrowIfNull(rom0);
            ArgumentNullException.ThrowIfNull(rom1);

            // Blank ROMs are inert fixtures used throughout the unit suite. They opt
            // into the bundled layout without pretending that an arbitrary ROM does.
            if (IsZeroFilled(rom0) && IsZeroFilled(rom1))
                return SyntheticTest;

            string rom0Hash = Convert.ToHexString(SHA256.HashData(rom0));
            string rom1Hash = Convert.ToHexString(SHA256.HashData(rom1));
            if (model == SpectrumMachineModel.Spectrum48K &&
                rom0Hash == rom1Hash &&
                (rom0Hash == Original48RomSha256 || rom0Hash == BundledRom1Sha256))
            {
                return new SpectrumRomProfile("Sinclair ZX Spectrum 48K", supportsAddressBasedTapeServices: true);
            }

            if (rom0Hash == BundledRom0Sha256 && rom1Hash == BundledRom1Sha256)
                return new SpectrumRomProfile("Sinclair ZX Spectrum 128K", supportsAddressBasedTapeServices: true);

            if (rom0Hash == Plus2Rom0Sha256 && rom1Hash == Plus2Rom1Sha256)
                return new SpectrumRomProfile("Sinclair ZX Spectrum +2 Grey", supportsAddressBasedTapeServices: false);

            string description = model == SpectrumMachineModel.Spectrum48K
                ? "Unknown 48K ROM"
                : "Unknown 128K ROM pair";
            return new SpectrumRomProfile(description, supportsAddressBasedTapeServices: false);
        }

        internal static SpectrumRomProfile DetectPlus2A(IReadOnlyList<byte[]> romBanks)
        {
            ArgumentNullException.ThrowIfNull(romBanks);
            if (romBanks.Count != 4)
                throw new ArgumentException("A +2A/+3 ROM set must contain four banks.", nameof(romBanks));

            bool bundled = true;
            for (int bank = 0; bank < romBanks.Count; bank++)
            {
                string hash = Convert.ToHexString(SHA256.HashData(romBanks[bank]));
                bundled &= hash == Plus3RomSha256[bank];
            }

            return new SpectrumRomProfile(
                bundled ? "Sinclair ZX Spectrum +2A/+3" : "Unknown +2A/+3 ROM set",
                supportsAddressBasedTapeServices: false);
        }

        private static bool IsInRange(ushort value, ushort minimum, ushort maximum) =>
            value >= minimum && value <= maximum;

        private static bool IsZeroFilled(byte[] data)
        {
            foreach (byte value in data)
            {
                if (value != 0)
                    return false;
            }

            return true;
        }

    }
}
