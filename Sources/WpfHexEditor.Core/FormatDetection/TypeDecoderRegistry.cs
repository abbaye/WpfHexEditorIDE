// ==========================================================
// Project: WpfHexEditor.Core
// File: TypeDecoderRegistry.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude (Anthropic)
// Created: 2026-03-25
// Description:
//     Registry of binary value-type decoders for .whfmt v2.0.
//     Decodes raw bytes into human-readable display strings for all
//     value types defined in the whfmt schema (19 types + legacy aliases).
//
// Architecture Notes:
//     Stateless singleton. Decoders are pure functions (byte[] → string).
//     Used by FormatScriptInterpreter when creating ParsedFieldResult.DisplayValue.
//     No WPF dependencies.
// ==========================================================

using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace WpfHexEditor.Core.FormatDetection
{
    /// <summary>
    /// Registry of binary value-type decoders for all whfmt v2.0 value types.
    /// </summary>
    public static class TypeDecoderRegistry
    {
        // Delegate: (data, offset, length, bigEndian) → display string
        private delegate string Decoder(byte[] data, long offset, int length, bool bigEndian);

        private static readonly Dictionary<string, Decoder> _decoders =
            new Dictionary<string, Decoder>(StringComparer.OrdinalIgnoreCase);

        static TypeDecoderRegistry()
        {
            // ── Integer types (already handled by interpreter; provided for completeness) ──
            Register("uint8",      (d, o, l, be) => ReadU8(d, o).ToString());
            Register("uint16",     (d, o, l, be) => ReadU16(d, o, be).ToString());
            Register("uint32",     (d, o, l, be) => ReadU32(d, o, be).ToString());
            Register("uint64",     (d, o, l, be) => ReadU64(d, o, be).ToString());
            Register("int8",       (d, o, l, be) => ((sbyte)ReadU8(d, o)).ToString());
            Register("int16",      (d, o, l, be) => ((short)ReadU16(d, o, be)).ToString());
            Register("int32",      (d, o, l, be) => ((int)ReadU32(d, o, be)).ToString());
            Register("int64",      (d, o, l, be) => ((long)ReadU64(d, o, be)).ToString());
            Register("byte",       (d, o, l, be) => ReadU8(d, o).ToString());
            Register("sbyte",      (d, o, l, be) => ((sbyte)ReadU8(d, o)).ToString());

            // ── String types ────────────────────────────────────────────────────────
            Register("ascii",      DecodeAscii);
            Register("string",     DecodeAscii);
            Register("ascii8",     DecodeAscii8);
            Register("utf8",       DecodeUtf8);
            Register("utf16le",    DecodeUtf16Le);
            Register("utf16be",    DecodeUtf16Be);

            // ── Floating-point ───────────────────────────────────────────────────────
            Register("float32",    DecodeFloat32);
            Register("float64",    DecodeFloat64);

            // ── GUID ─────────────────────────────────────────────────────────────────
            Register("guid",       DecodeGuid);

            // ── Date / Time ──────────────────────────────────────────────────────────
            Register("dosdate",    DecodeDosDate);
            Register("dostime",    DecodeDosTime);
            Register("filetime",   DecodeFileTime);
            Register("unixtime32", DecodeUnixTime32);
            Register("unixtime64", DecodeUnixTime64);

            // ── Network ──────────────────────────────────────────────────────────────
            Register("ipv4",       DecodeIpv4);
            Register("ipv6",       DecodeIpv6);

            // ── Hash digests (display as hex) ────────────────────────────────────────
            Register("md5",        DecodeHexDump);
            Register("sha1",       DecodeHexDump);
            Register("sha256",     DecodeHexDump);

            // ── Variable-length integer ───────────────────────────────────────────────
            Register("varint",     DecodeVarint);

            // ── Binary-coded decimal ─────────────────────────────────────────────────
            Register("bcd",        DecodeBcd);

            // ── Raw bytes ────────────────────────────────────────────────────────────
            Register("bytes",      DecodeHexDump);
            Register("raw",        DecodeHexDump);
        }

        private static void Register(string name, Decoder decoder) =>
            _decoders[name] = decoder;

        // ── Public API ────────────────────────────────────────────────────────────

        /// <summary>
        /// Decode bytes at <paramref name="offset"/> of <paramref name="length"/> using <paramref name="valueType"/>.
        /// Returns null if the type is not registered or the data is insufficient.
        /// </summary>
        public static string Decode(string valueType, byte[] data, long offset, int length, bool bigEndian = false)
        {
            if (string.IsNullOrWhiteSpace(valueType) || data == null)
                return null;
            if (offset < 0 || offset >= data.Length)
                return null;

            if (!_decoders.TryGetValue(valueType, out var decoder))
                return null;

            try
            {
                return decoder(data, offset, length, bigEndian);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>Returns true if the given value type has a registered decoder.</summary>
        public static bool IsKnown(string valueType) =>
            !string.IsNullOrWhiteSpace(valueType) && _decoders.ContainsKey(valueType);

        // ── Decoder implementations ───────────────────────────────────────────────

        private static string DecodeAscii(byte[] d, long o, int l, bool be)
        {
            int len = (int)Math.Min(l, d.Length - o);
            if (len <= 0) return string.Empty;
            var sb = new StringBuilder(len);
            for (int i = 0; i < len; i++)
            {
                byte b = d[o + i];
                if (b == 0) break;
                sb.Append(b >= 32 && b < 127 ? (char)b : '?');
            }
            return sb.ToString().TrimEnd();
        }

        private static string DecodeAscii8(byte[] d, long o, int l, bool be)
        {
            // Fixed 8-byte field, NUL-padded
            int len = (int)Math.Min(8, d.Length - o);
            if (len <= 0) return string.Empty;
            var sb = new StringBuilder(8);
            for (int i = 0; i < len; i++)
            {
                byte b = d[o + i];
                if (b == 0) break;
                sb.Append(b >= 32 && b < 127 ? (char)b : '?');
            }
            return sb.ToString().TrimEnd();
        }

        private static string DecodeUtf8(byte[] d, long o, int l, bool be)
        {
            int len = (int)Math.Min(l, d.Length - o);
            if (len <= 0) return string.Empty;
            // find NUL terminator
            int strLen = len;
            for (int i = 0; i < len; i++) { if (d[o + i] == 0) { strLen = i; break; } }
            var buf = new byte[strLen];
            Array.Copy(d, o, buf, 0, strLen);
            return Encoding.UTF8.GetString(buf).TrimEnd('\0');
        }

        private static string DecodeUtf16Le(byte[] d, long o, int l, bool be)
        {
            int len = (int)Math.Min(l, d.Length - o);
            // Align to 2 bytes
            len &= ~1;
            if (len <= 0) return string.Empty;
            // Find UTF-16 NUL (0x0000)
            int strLen = len;
            for (int i = 0; i + 1 < len; i += 2)
            {
                if (d[o + i] == 0 && d[o + i + 1] == 0) { strLen = i; break; }
            }
            var buf = new byte[strLen];
            Array.Copy(d, o, buf, 0, strLen);
            return Encoding.Unicode.GetString(buf).TrimEnd('\0');
        }

        private static string DecodeUtf16Be(byte[] d, long o, int l, bool be)
        {
            int len = (int)Math.Min(l, d.Length - o);
            len &= ~1;
            if (len <= 0) return string.Empty;
            int strLen = len;
            for (int i = 0; i + 1 < len; i += 2)
            {
                if (d[o + i] == 0 && d[o + i + 1] == 0) { strLen = i; break; }
            }
            var buf = new byte[strLen];
            Array.Copy(d, o, buf, 0, strLen);
            return Encoding.BigEndianUnicode.GetString(buf).TrimEnd('\0');
        }

        private static string DecodeFloat32(byte[] d, long o, int l, bool be)
        {
            if (o + 4 > d.Length) return "0.0";
            var buf = new byte[4];
            Array.Copy(d, o, buf, 0, 4);
            if (be ^ !BitConverter.IsLittleEndian) Array.Reverse(buf);
            float v = BitConverter.ToSingle(buf, 0);
            return float.IsNaN(v) || float.IsInfinity(v) ? v.ToString() : v.ToString("G7");
        }

        private static string DecodeFloat64(byte[] d, long o, int l, bool be)
        {
            if (o + 8 > d.Length) return "0.0";
            var buf = new byte[8];
            Array.Copy(d, o, buf, 0, 8);
            if (be ^ !BitConverter.IsLittleEndian) Array.Reverse(buf);
            double v = BitConverter.ToDouble(buf, 0);
            return double.IsNaN(v) || double.IsInfinity(v) ? v.ToString() : v.ToString("G15");
        }

        private static string DecodeGuid(byte[] d, long o, int l, bool be)
        {
            if (o + 16 > d.Length) return Guid.Empty.ToString("B");
            var buf = new byte[16];
            Array.Copy(d, o, buf, 0, 16);
            return new Guid(buf).ToString("B").ToUpperInvariant();
        }

        private static string DecodeDosDate(byte[] d, long o, int l, bool be)
        {
            if (o + 2 > d.Length) return "1980-01-01";
            ushort v = ReadU16(d, o, false);
            int day   = v & 0x1F;
            int month = (v >> 5) & 0x0F;
            int year  = ((v >> 9) & 0x7F) + 1980;
            if (month < 1 || month > 12 || day < 1 || day > 31) return $"Raw: 0x{v:X4}";
            return $"{year:D4}-{month:D2}-{day:D2}";
        }

        private static string DecodeDosTime(byte[] d, long o, int l, bool be)
        {
            if (o + 2 > d.Length) return "00:00:00";
            ushort v = ReadU16(d, o, false);
            int sec  = (v & 0x1F) * 2;
            int min  = (v >> 5) & 0x3F;
            int hour = (v >> 11) & 0x1F;
            return $"{hour:D2}:{min:D2}:{sec:D2}";
        }

        private static string DecodeFileTime(byte[] d, long o, int l, bool be)
        {
            if (o + 8 > d.Length) return "—";
            ulong raw = ReadU64(d, o, false);
            if (raw == 0) return "—";
            try
            {
                var dt = DateTime.FromFileTimeUtc((long)raw);
                return dt.ToString("yyyy-MM-dd HH:mm:ss UTC");
            }
            catch { return $"0x{raw:X16}"; }
        }

        private static string DecodeUnixTime32(byte[] d, long o, int l, bool be)
        {
            if (o + 4 > d.Length) return "—";
            uint raw = ReadU32(d, o, be);
            if (raw == 0) return "—";
            try
            {
                var dt = DateTimeOffset.FromUnixTimeSeconds(raw).UtcDateTime;
                return dt.ToString("yyyy-MM-dd HH:mm:ss UTC");
            }
            catch { return raw.ToString(); }
        }

        private static string DecodeUnixTime64(byte[] d, long o, int l, bool be)
        {
            if (o + 8 > d.Length) return "—";
            ulong raw = ReadU64(d, o, be);
            if (raw == 0) return "—";
            try
            {
                var dt = DateTimeOffset.FromUnixTimeSeconds((long)raw).UtcDateTime;
                return dt.ToString("yyyy-MM-dd HH:mm:ss UTC");
            }
            catch { return raw.ToString(); }
        }

        private static string DecodeIpv4(byte[] d, long o, int l, bool be)
        {
            if (o + 4 > d.Length) return "0.0.0.0";
            return $"{d[o]}.{d[o+1]}.{d[o+2]}.{d[o+3]}";
        }

        private static string DecodeIpv6(byte[] d, long o, int l, bool be)
        {
            if (o + 16 > d.Length) return "::";
            var buf = new byte[16];
            Array.Copy(d, o, buf, 0, 16);
            return new IPAddress(buf).ToString();
        }

        private static string DecodeHexDump(byte[] d, long o, int l, bool be)
        {
            int len = (int)Math.Min(l, d.Length - o);
            if (len <= 0) return string.Empty;
            // Show at most 32 bytes then "..."
            int show = Math.Min(len, 32);
            var sb = new StringBuilder(show * 3);
            for (int i = 0; i < show; i++)
            {
                if (i > 0) sb.Append(' ');
                sb.Append(d[o + i].ToString("X2"));
            }
            if (len > show) sb.Append(" …");
            return sb.ToString();
        }

        private static string DecodeVarint(byte[] d, long o, int l, bool be)
        {
            // LEB128 unsigned
            long result = 0;
            int shift = 0;
            long pos = o;
            while (pos < d.Length && shift < 64)
            {
                byte b = d[pos++];
                result |= (long)(b & 0x7F) << shift;
                if ((b & 0x80) == 0) break;
                shift += 7;
            }
            return result.ToString();
        }

        private static string DecodeBcd(byte[] d, long o, int l, bool be)
        {
            int len = (int)Math.Min(l, d.Length - o);
            if (len <= 0) return string.Empty;
            var sb = new StringBuilder(len * 2);
            for (int i = 0; i < len; i++)
            {
                byte b = d[o + i];
                sb.Append((char)('0' + (b >> 4)));
                sb.Append((char)('0' + (b & 0x0F)));
            }
            return sb.ToString().TrimStart('0').PadLeft(1, '0');
        }

        // ── Low-level read helpers ────────────────────────────────────────────────

        internal static byte ReadU8(byte[] d, long o) =>
            o >= 0 && o < d.Length ? d[o] : (byte)0;

        internal static ushort ReadU16(byte[] d, long o, bool be)
        {
            if (o + 2 > d.Length) return 0;
            return be
                ? (ushort)((d[o] << 8) | d[o + 1])
                : (ushort)(d[o] | (d[o + 1] << 8));
        }

        internal static uint ReadU32(byte[] d, long o, bool be)
        {
            if (o + 4 > d.Length) return 0;
            return be
                ? (uint)((d[o] << 24) | (d[o+1] << 16) | (d[o+2] << 8) | d[o+3])
                : (uint)(d[o] | (d[o+1] << 8) | (d[o+2] << 16) | (d[o+3] << 24));
        }

        internal static ulong ReadU64(byte[] d, long o, bool be)
        {
            if (o + 8 > d.Length) return 0;
            if (be)
                return ((ulong)d[o] << 56) | ((ulong)d[o+1] << 48) | ((ulong)d[o+2] << 40) |
                       ((ulong)d[o+3] << 32) | ((ulong)d[o+4] << 24) | ((ulong)d[o+5] << 16) |
                       ((ulong)d[o+6] << 8) | d[o+7];
            return (ulong)d[o] | ((ulong)d[o+1] << 8) | ((ulong)d[o+2] << 16) |
                   ((ulong)d[o+3] << 24) | ((ulong)d[o+4] << 32) | ((ulong)d[o+5] << 40) |
                   ((ulong)d[o+6] << 48) | ((ulong)d[o+7] << 56);
        }
    }
}
