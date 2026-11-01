namespace Derby
{
    public sealed class SpectrumRomSet
    {
        private SpectrumRomSet(string name, SpectrumMachineModel model, string rom0Path, string rom1Path)
        {
            Name = name;
            Model = model;
            Rom0Path = rom0Path;
            Rom1Path = rom1Path;
        }

        public string Name { get; }
        public SpectrumMachineModel Model { get; }
        public string Rom0Path { get; }
        public string Rom1Path { get; }

        public static SpectrumRomSet Bundled128K(string romFolder) =>
            Create128K(
                "Sinclair ZX Spectrum 128K (bundled)",
                Path.Combine(romFolder, "128-0.rom"),
                Path.Combine(romFolder, "128-1.rom"));

        public static SpectrumRomSet Bundled48K(string romFolder) =>
            Create48K(
                "Sinclair ZX Spectrum 48K (bundled)",
                Path.Combine(romFolder, "128-1.rom"));

        public static SpectrumRomSet Create48K(string name, string romPath)
        {
            ValidateName(name);
            ValidateRomFile(romPath);
            return new SpectrumRomSet(name, SpectrumMachineModel.Spectrum48K, romPath, romPath);
        }

        public static SpectrumRomSet Create128K(string name, string rom0Path, string rom1Path)
        {
            ValidateName(name);
            ValidateRomFile(rom0Path);
            ValidateRomFile(rom1Path);
            return new SpectrumRomSet(name, SpectrumMachineModel.Spectrum128K, rom0Path, rom1Path);
        }

        internal (byte[] Rom0, byte[] Rom1) Load()
        {
            byte[] rom0 = File.ReadAllBytes(Rom0Path);
            byte[] rom1 = File.ReadAllBytes(Rom1Path);
            if (rom0.Length != 16384 || rom1.Length != 16384)
                throw new InvalidOperationException("ROM files must be exactly 16KB each.");
            return (rom0, rom1);
        }

        private static void ValidateName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("ROM set name must be provided.", nameof(name));
        }

        private static void ValidateRomFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("ROM file path must be provided.", nameof(path));
            if (!File.Exists(path))
                throw new FileNotFoundException("ROM file was not found.", path);
            if (new FileInfo(path).Length != 16384)
                throw new InvalidDataException($"ROM file '{Path.GetFileName(path)}' must be exactly 16KB.");
        }
    }
}
