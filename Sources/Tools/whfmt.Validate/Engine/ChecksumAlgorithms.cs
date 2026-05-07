// ==========================================================
// Project: whfmt.Validate
// File: Engine/ChecksumAlgorithms.cs
// Description: Standalone checksum algorithms (CRC32, MD5, SHA-1, SHA-256, Adler32, Sum variants).
//              No WPF dependency — pure net8.0.
// ==========================================================

using System.Security.Cryptography;

namespace WhfmtValidate.Engine;

internal static class ChecksumAlgorithms
{
    private static readonly uint[] Crc32Table = BuildCrc32Table();

    private static uint[] BuildCrc32Table()
    {
        const uint poly = 0xEDB88320;
        var table = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            uint crc = i;
            for (int j = 8; j > 0; j--)
                crc = (crc & 1) == 1 ? (crc >> 1) ^ poly : crc >> 1;
            table[i] = crc;
        }
        return table;
    }

    internal static string? Calculate(byte[] data, string algorithm) =>
        algorithm.ToLowerInvariant() switch
        {
            "crc32"                    => Crc32(data),
            "crc16"                    => Crc16(data),
            "adler32"                  => Adler32(data),
            "md5"                      => Hash(data, MD5.Create()),
            "sha1"                     => Hash(data, SHA1.Create()),
            "sha256"                   => Hash(data, SHA256.Create()),
            "sum8"  or "checksum8"     => Sum8(data),
            "sum16" or "checksum16"    => Sum16(data),
            "sum32" or "checksum32"    => Sum32(data),
            _                          => null
        };

    private static string Crc32(byte[] data)
    {
        uint crc = 0xFFFFFFFF;
        foreach (var b in data)
            crc = (crc >> 8) ^ Crc32Table[(crc & 0xFF) ^ b];
        return (~crc).ToString("X8");
    }

    private static string Crc16(byte[] data)
    {
        ushort crc = 0xFFFF;
        foreach (var b in data)
        {
            crc ^= b;
            for (int i = 0; i < 8; i++)
                crc = (crc & 1) != 0 ? (ushort)((crc >> 1) ^ 0xA001) : (ushort)(crc >> 1);
        }
        return crc.ToString("X4");
    }

    private static string Adler32(byte[] data)
    {
        const uint mod = 65521;
        uint a = 1, b = 0;
        foreach (var byt in data) { a = (a + byt) % mod; b = (b + a) % mod; }
        return ((b << 16) | a).ToString("X8");
    }

    private static string Hash(byte[] data, HashAlgorithm alg)
    {
        using (alg)
            return BitConverter.ToString(alg.ComputeHash(data)).Replace("-", "");
    }

    private static string Sum8(byte[] data)  { byte s = 0;   foreach (var b in data) s += b;  return s.ToString("X2"); }
    private static string Sum16(byte[] data) { ushort s = 0; foreach (var b in data) s += b;  return s.ToString("X4"); }
    private static string Sum32(byte[] data) { uint s = 0;   foreach (var b in data) s += b;  return s.ToString("X8"); }
}
