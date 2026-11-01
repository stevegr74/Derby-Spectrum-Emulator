using System;

namespace Derby.Z80
{
    public sealed record Z80Instruction(
        ushort Address,
        byte[] Bytes,
        int Length,
        string Mnemonic,
        ushort? BranchTarget)
    {
        public string BytesText => string.Join(" ", Array.ConvertAll(Bytes, b => b.ToString("X2")));
    }
}
