//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using WpfHexEditor.SDK.Contracts.Services;

namespace WpfHexEditor.App.BinaryAnalysis.Services;

/// <summary>
/// Stateless cipher transforms applied to a byte range from the active hex file.
/// All methods are pure — no I/O, no side effects.
/// </summary>
public static class CipherDecoderService
{
    public const int MaxPreviewBytes = 4096;

    // -------------------------------------------------------------------------
    // XOR

    public static byte[] XorSingleKey(ReadOnlySpan<byte> data, byte key)
    {
        var result = new byte[data.Length];
        for (int i = 0; i < data.Length; i++)
            result[i] = (byte)(data[i] ^ key);
        return result;
    }

    public static byte[] XorRollingKey(ReadOnlySpan<byte> data, byte[] key)
    {
        if (key.Length == 0) return data.ToArray();
        var result = new byte[data.Length];
        for (int i = 0; i < data.Length; i++)
            result[i] = (byte)(data[i] ^ key[i % key.Length]);
        return result;
    }

    /// <summary>
    /// Auto-detects the most likely single-byte XOR key using chi-squared
    /// frequency analysis against English text distribution.
    /// </summary>
    public static byte DetectXorKey(ReadOnlySpan<byte> data)
        => RankXorKeys(data)[0].Key;

    /// <summary>Returns all 256 XOR keys ranked by chi-squared score (best first).</summary>
    public static (byte Key, double Score)[] RankXorKeys(ReadOnlySpan<byte> data)
    {
        var ranked = new (byte Key, double Score)[256];
        for (int k = 0; k < 256; k++)
            ranked[k] = ((byte)k, ScoreEnglish(data, (byte)k));
        Array.Sort(ranked, (a, b) => a.Score.CompareTo(b.Score));
        return ranked;
    }

    // -------------------------------------------------------------------------
    // ROT

    public static byte[] RotAlpha(ReadOnlySpan<byte> data, int shift)
    {
        shift = ((shift % 26) + 26) % 26;
        var result = new byte[data.Length];
        for (int i = 0; i < data.Length; i++)
        {
            byte b = data[i];
            result[i] = b switch
            {
                >= (byte)'A' and <= (byte)'Z' => (byte)('A' + (b - 'A' + shift) % 26),
                >= (byte)'a' and <= (byte)'z' => (byte)('a' + (b - 'a' + shift) % 26),
                _ => b
            };
        }
        return result;
    }

    // ROT-13 is ROT(13); ROT-47 covers printable ASCII
    public static byte[] Rot47(ReadOnlySpan<byte> data)
    {
        var result = new byte[data.Length];
        for (int i = 0; i < data.Length; i++)
        {
            byte b = data[i];
            result[i] = (b >= 33 && b <= 126) ? (byte)(33 + (b - 33 + 47) % 94) : b;
        }
        return result;
    }

    // -------------------------------------------------------------------------
    // Null-XOR entropy check

    /// <summary>Returns true if the decoded bytes look like printable text (heuristic).</summary>
    public static bool LooksLikeText(ReadOnlySpan<byte> decoded)
    {
        if (decoded.IsEmpty) return false;
        int printable = 0;
        foreach (byte b in decoded)
            if ((b >= 32 && b <= 126) || b == 9 || b == 10 || b == 13)
                printable++;
        return (double)printable / decoded.Length >= 0.75;
    }

    // -------------------------------------------------------------------------
    // Private helpers

    // Expected English letter frequency table (a–z)
    private static readonly double[] _enFreq =
    [
        0.08167, 0.01492, 0.02782, 0.04253, 0.12702, 0.02228, 0.02015,
        0.06094, 0.06966, 0.00153, 0.00772, 0.04025, 0.02406, 0.06749,
        0.07507, 0.01929, 0.00095, 0.05987, 0.06327, 0.09056, 0.02758,
        0.00978, 0.02360, 0.00150, 0.01974, 0.00074
    ];

    private static double ScoreEnglish(ReadOnlySpan<byte> data, byte key)
    {
        Span<int> freq = stackalloc int[26];
        int letters = 0;
        foreach (byte b in data)
        {
            byte d = (byte)(b ^ key);
            if (d >= 'a' && d <= 'z')      { freq[d - 'a']++; letters++; }
            else if (d >= 'A' && d <= 'Z') { freq[d - 'A']++; letters++; }
        }
        if (letters == 0) return double.MaxValue;

        double chi = 0;
        for (int i = 0; i < 26; i++)
        {
            double observed = (double)freq[i] / letters;
            double expected = _enFreq[i];
            double diff     = observed - expected;
            chi += diff * diff / (expected + 1e-9);
        }
        return chi;
    }
}
