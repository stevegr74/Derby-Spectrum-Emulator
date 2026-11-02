namespace Derby
{
    public sealed class SpectrumRomSet
    {
        private SpectrumRomSet(
            string name,
            SpectrumMachineModel model,
            SpectrumMemoryPagingModel pagingModel,
            params string[] romPaths)
        {
            Name = name;
            Model = model;
            PagingModel = pagingModel;
            RomPaths = Array.AsReadOnly(romPaths);
        }

        public string Name { get; }
        public SpectrumMachineModel Model { get; }
        public SpectrumMemoryPagingModel PagingModel { get; }
        public IReadOnlyList<string> RomPaths { get; }
        public string Rom0Path => RomPaths[0];
        public string Rom1Path => RomPaths[1];

        public static SpectrumRomSet Bundled128K(string romFolder) =>
            Create128K(
                "Sinclair ZX Spectrum 128K (bundled)",
                Path.Combine(romFolder, "128-0.rom"),
                Path.Combine(romFolder, "128-1.rom"));

        public static SpectrumRomSet Bundled48K(string romFolder) =>
            Create48K(
                "Sinclair ZX Spectrum 48K (bundled)",
                Path.Combine(romFolder, "48.rom"));

        public static SpectrumRomSet BundledPlus2(string romFolder) =>
            Create128K(
                "Sinclair ZX Spectrum +2 Grey (bundled)",
                Path.Combine(romFolder, "plus2-0.rom"),
                Path.Combine(romFolder, "plus2-1.rom"));

        public static SpectrumRomSet BundledPlus2A(string romFolder) =>
            CreatePlus2A(
                "Sinclair ZX Spectrum +2A (bundled)",
                Path.Combine(romFolder, "plus3-0.rom"),
                Path.Combine(romFolder, "plus3-1.rom"),
                Path.Combine(romFolder, "plus3-2.rom"),
                Path.Combine(romFolder, "plus3-3.rom"));

        public static SpectrumRomSet Create48K(string name, string romPath)
        {
            ValidateName(name);
            ValidateRomFile(romPath);
            return new SpectrumRomSet(
                name,
                SpectrumMachineModel.Spectrum48K,
                SpectrumMemoryPagingModel.Fixed48K,
                romPath,
                romPath);
        }

        public static SpectrumRomSet Create128K(string name, string rom0Path, string rom1Path)
        {
            ValidateName(name);
            ValidateRomFile(rom0Path);
            ValidateRomFile(rom1Path);
            return new SpectrumRomSet(
                name,
                SpectrumMachineModel.Spectrum128K,
                SpectrumMemoryPagingModel.Standard128K,
                rom0Path,
                rom1Path);
        }

        public static SpectrumRomSet CreatePlus2A(
            string name,
            string rom0Path,
            string rom1Path,
            string rom2Path,
            string rom3Path)
        {
            ValidateName(name);
            string[] paths = [rom0Path, rom1Path, rom2Path, rom3Path];
            foreach (string path in paths)
                ValidateRomFile(path);
            return new SpectrumRomSet(
                name,
                SpectrumMachineModel.Spectrum128K,
                SpectrumMemoryPagingModel.Plus2APlus3,
                paths);
        }

        internal byte[][] Load()
        {
            var banks = new byte[RomPaths.Count][];
            for (int bank = 0; bank < banks.Length; bank++)
            {
                banks[bank] = File.ReadAllBytes(RomPaths[bank]);
                if (banks[bank].Length != 16384)
                    throw new InvalidOperationException("ROM files must be exactly 16KB each.");
            }
            return banks;
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
