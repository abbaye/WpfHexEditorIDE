// Polyfills for net8.0 APIs used by linked Generator files that are unavailable in netstandard2.0.
// Placed in namespace WhfmtCodeGen.Generator so they resolve before System.* in the same namespace.

namespace WhfmtCodeGen.Generator;

// Convert.FromHexString — available from .NET 5+ only.
// ParserGenerator.cs calls this unqualified so this local shadow resolves first.
internal static class Convert
{
    internal static byte[] FromHexString(string hex)
    {
        var bytes = new byte[hex.Length / 2];
        for (int i = 0; i < bytes.Length; i++)
            bytes[i] = System.Convert.ToByte(hex.Substring(i * 2, 2), 16);
        return bytes;
    }
}
