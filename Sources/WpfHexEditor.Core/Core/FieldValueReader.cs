//////////////////////////////////////////////
// Apache 2.0  - 2026
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using System;
using System.Linq;
using System.Text;

namespace WpfHexEditor.Core
{
    /// <summary>
    /// Utility class for reading typed values from byte arrays
    /// Supports various integer types, strings, and endianness
    /// </summary>
    public class FieldValueReader
    {
        /// <summary>
        /// Read a value from byte data based on type
        /// </summary>
        /// <param name="data">Source byte array</param>
        /// <param name="offset">Offset in the array</param>
        /// <param name="length">Number of bytes to read</param>
        /// <param name="valueType">Type to read (uint8, uint16, uint32, int16, int32, string, etc.)</param>
        /// <param name="bigEndian">Whether to use big-endian byte order (default: little-endian)</param>
        /// <returns>Parsed value or raw bytes if type is unknown</returns>
        public object ReadValue(byte[] data, int offset, int length, string valueType, bool bigEndian = false)
        {
            if (data == null || offset < 0 || offset + length > data.Length)
                return null;

            try
            {
                return valueType switch
                {
                    "uint8" or "byte" => data[offset],
                    "int8" or "sbyte" => (sbyte)data[offset],
                    "uint16" or "ushort" => ReadUInt16(data, offset, bigEndian),
                    "int16" or "short" => ReadInt16(data, offset, bigEndian),
                    "uint32" or "uint" => ReadUInt32(data, offset, bigEndian),
                    "int32" or "int" => ReadInt32(data, offset, bigEndian),
                    "uint64" or "ulong" => ReadUInt64(data, offset, bigEndian),
                    "int64" or "long" => ReadInt64(data, offset, bigEndian),
                    "float" => ReadFloat(data, offset, bigEndian),
                    "double" => ReadDouble(data, offset, bigEndian),
                    "string" or "ascii" => ReadString(data, offset, length, Encoding.ASCII),
                    "utf8" => ReadString(data, offset, length, Encoding.UTF8),
                    "utf16" => ReadString(data, offset, length, Encoding.Unicode),
                    "bytes" => ReadBytes(data, offset, length),
                    // New types for Phase 4
                    "guid" => ReadGuid(data, offset),
                    "timestamp" or "unix_timestamp" => ReadUnixTimestamp(data, offset, bigEndian),
                    "timestamp64" or "unix_timestamp64" => ReadUnixTimestamp64(data, offset, bigEndian),
                    "filetime" => ReadFileTime(data, offset, bigEndian),
                    "ipv4" => ReadIPv4(data, offset),
                    "ipv6" => ReadIPv6(data, offset),
                    "mac" or "mac_address" => ReadMacAddress(data, offset),
                    _ => ReadBytes(data, offset, length) // Default: return raw bytes
                };
            }
            catch (Exception)
            {
                // If reading fails, return raw bytes
                return ReadBytes(data, offset, length);
            }
        }

        #region Unsigned Integers

        private ushort ReadUInt16(byte[] data, int offset, bool bigEndian)
        {
            if (bigEndian)
                return (ushort)((data[offset] << 8) | data[offset + 1]);
            else
                return BitConverter.ToUInt16(data, offset);
        }

        private uint ReadUInt32(byte[] data, int offset, bool bigEndian)
        {
            if (bigEndian)
                return (uint)((data[offset] << 24) | (data[offset + 1] << 16) |
                             (data[offset + 2] << 8) | data[offset + 3]);
            else
                return BitConverter.ToUInt32(data, offset);
        }

        private ulong ReadUInt64(byte[] data, int offset, bool bigEndian)
        {
            if (bigEndian)
            {
                ulong value = 0;
                for (int i = 0; i < 8; i++)
                    value = (value << 8) | data[offset + i];
                return value;
            }
            else
                return BitConverter.ToUInt64(data, offset);
        }

        #endregion

        #region Signed Integers

        private short ReadInt16(byte[] data, int offset, bool bigEndian)
        {
            if (bigEndian)
                return (short)((data[offset] << 8) | data[offset + 1]);
            else
                return BitConverter.ToInt16(data, offset);
        }

        private int ReadInt32(byte[] data, int offset, bool bigEndian)
        {
            if (bigEndian)
                return (data[offset] << 24) | (data[offset + 1] << 16) |
                       (data[offset + 2] << 8) | data[offset + 3];
            else
                return BitConverter.ToInt32(data, offset);
        }

        private long ReadInt64(byte[] data, int offset, bool bigEndian)
        {
            if (bigEndian)
            {
                long value = 0;
                for (int i = 0; i < 8; i++)
                    value = (value << 8) | data[offset + i];
                return value;
            }
            else
                return BitConverter.ToInt64(data, offset);
        }

        #endregion

        #region Floating Point

        private float ReadFloat(byte[] data, int offset, bool bigEndian)
        {
            if (bigEndian)
            {
                // Reverse bytes for big-endian
                byte[] temp = new byte[4];
                temp[0] = data[offset + 3];
                temp[1] = data[offset + 2];
                temp[2] = data[offset + 1];
                temp[3] = data[offset];
                return BitConverter.ToSingle(temp, 0);
            }
            else
                return BitConverter.ToSingle(data, offset);
        }

        private double ReadDouble(byte[] data, int offset, bool bigEndian)
        {
            if (bigEndian)
            {
                // Reverse bytes for big-endian
                byte[] temp = new byte[8];
                for (int i = 0; i < 8; i++)
                    temp[i] = data[offset + 7 - i];
                return BitConverter.ToDouble(temp, 0);
            }
            else
                return BitConverter.ToDouble(data, offset);
        }

        #endregion

        #region Strings

        private string ReadString(byte[] data, int offset, int length, Encoding encoding)
        {
            if (length <= 0)
                return string.Empty;

            // Check for null terminator
            int actualLength = length;

            // For UTF-16 encodings, check for double-null (0x00 0x00)
            bool isUtf16 = encoding is UnicodeEncoding;

            if (isUtf16)
            {
                // UTF-16: check for 0x00 0x00 (null character in UTF-16)
                for (int i = 0; i < length - 1; i += 2)
                {
                    if (data[offset + i] == 0 && data[offset + i + 1] == 0)
                    {
                        actualLength = i;
                        break;
                    }
                }
            }
            else
            {
                // ASCII/UTF-8: check for single 0x00
                for (int i = 0; i < length; i++)
                {
                    if (data[offset + i] == 0)
                    {
                        actualLength = i;
                        break;
                    }
                }
            }

            if (actualLength == 0)
                return string.Empty;

            return encoding.GetString(data, offset, actualLength);
        }

        #endregion

        #region Bytes

        private byte[] ReadBytes(byte[] data, int offset, int length)
        {
            var result = new byte[length];
            Array.Copy(data, offset, result, 0, length);
            return result;
        }

        #endregion

        #region Extended String Reading

        /// <summary>
        /// Read null-terminated ASCII string
        /// </summary>
        /// <param name="data">Source byte array</param>
        /// <param name="offset">Offset in the array</param>
        /// <param name="maxLength">Maximum length to read</param>
        /// <returns>Null-terminated string</returns>
        public string ReadStringNullTerminated(byte[] data, int offset, int maxLength = 1024)
        {
            if (data == null || offset < 0 || offset >= data.Length)
                return string.Empty;

            int length = 0;
            while (offset + length < data.Length &&
                   length < maxLength &&
                   data[offset + length] != 0)
            {
                length++;
            }

            if (length == 0)
                return string.Empty;

            return Encoding.ASCII.GetString(data, offset, length);
        }

        /// <summary>
        /// Read null-terminated UTF-8 string
        /// </summary>
        public string ReadStringUTF8NullTerminated(byte[] data, int offset, int maxLength = 1024)
        {
            if (data == null || offset < 0 || offset >= data.Length)
                return string.Empty;

            int length = 0;
            while (offset + length < data.Length &&
                   length < maxLength &&
                   data[offset + length] != 0)
            {
                length++;
            }

            if (length == 0)
                return string.Empty;

            return Encoding.UTF8.GetString(data, offset, length);
        }

        #endregion

        #region Hex String Conversion

        /// <summary>
        /// Read bytes as hexadecimal string
        /// </summary>
        public string ReadBytesAsHex(byte[] data, int offset, int length)
        {
            var bytes = ReadBytes(data, offset, length);
            if (bytes == null || bytes.Length == 0)
                return string.Empty;

            return BitConverter.ToString(bytes).Replace("-", "");
        }

        #endregion

        #region Signature Checking (Static Methods)

        /// <summary>
        /// Check if signature matches at specified offset
        /// </summary>
        /// <param name="data">Byte array to check</param>
        /// <param name="offset">Offset in bytes</param>
        /// <param name="hexSignature">Hex string signature (e.g., "504B0304")</param>
        /// <returns>True if signature matches</returns>
        public static bool CheckSignature(byte[] data, int offset, string hexSignature)
        {
            if (string.IsNullOrWhiteSpace(hexSignature) || data == null)
                return false;

            var expectedBytes = HexStringToBytes(hexSignature);
            if (expectedBytes == null || offset < 0 || offset + expectedBytes.Length > data.Length)
                return false;

            for (int i = 0; i < expectedBytes.Length; i++)
            {
                if (data[offset + i] != expectedBytes[i])
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Find offset of signature in data
        /// </summary>
        /// <param name="data">Byte array to search</param>
        /// <param name="hexSignature">Hex string signature</param>
        /// <param name="startOffset">Start searching from this offset</param>
        /// <param name="maxOffset">Stop searching at this offset (0 = end of data)</param>
        /// <returns>Offset where signature found, or -1 if not found</returns>
        public static int FindSignature(byte[] data, string hexSignature, int startOffset = 0, int maxOffset = 0)
        {
            if (string.IsNullOrWhiteSpace(hexSignature) || data == null)
                return -1;

            var expectedBytes = HexStringToBytes(hexSignature);
            if (expectedBytes == null || expectedBytes.Length == 0)
                return -1;

            int searchEnd = maxOffset > 0 ? Math.Min(maxOffset, data.Length) : data.Length;

            for (int i = startOffset; i <= searchEnd - expectedBytes.Length; i++)
            {
                bool match = true;
                for (int j = 0; j < expectedBytes.Length; j++)
                {
                    if (data[i + j] != expectedBytes[j])
                    {
                        match = false;
                        break;
                    }
                }

                if (match)
                    return i;
            }

            return -1;
        }

        /// <summary>
        /// Convert hex string to byte array
        /// </summary>
        /// <param name="hex">Hex string (e.g., "504B0304")</param>
        /// <returns>Byte array or null if invalid</returns>
        private static byte[] HexStringToBytes(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex))
                return null;

            // Remove spaces and dashes
            hex = hex.Replace(" ", "").Replace("-", "");

            if (hex.Length % 2 != 0)
                return null;

            try
            {
                var bytes = new byte[hex.Length / 2];
                for (int i = 0; i < bytes.Length; i++)
                {
                    bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
                }
                return bytes;
            }
            catch
            {
                return null;
            }
        }

        #endregion

        #region New Data Types (Phase 4)

        /// <summary>
        /// Read a GUID (16 bytes)
        /// </summary>
        private Guid ReadGuid(byte[] data, int offset)
        {
            var guidBytes = new byte[16];
            Array.Copy(data, offset, guidBytes, 0, 16);
            return new Guid(guidBytes);
        }

        /// <summary>
        /// Read a Unix timestamp (32-bit, seconds since epoch)
        /// </summary>
        private DateTime ReadUnixTimestamp(byte[] data, int offset, bool bigEndian)
        {
            uint timestamp = ReadUInt32(data, offset, bigEndian);
            return DateTimeOffset.FromUnixTimeSeconds(timestamp).DateTime;
        }

        /// <summary>
        /// Read a Unix timestamp (64-bit, seconds since epoch)
        /// </summary>
        private DateTime ReadUnixTimestamp64(byte[] data, int offset, bool bigEndian)
        {
            long timestamp = ReadInt64(data, offset, bigEndian);
            return DateTimeOffset.FromUnixTimeSeconds(timestamp).DateTime;
        }

        /// <summary>
        /// Read a Windows FILETIME (64-bit, 100-nanosecond intervals since 1601-01-01)
        /// </summary>
        private DateTime ReadFileTime(byte[] data, int offset, bool bigEndian)
        {
            long fileTime = ReadInt64(data, offset, bigEndian);
            try
            {
                return DateTime.FromFileTimeUtc(fileTime);
            }
            catch
            {
                // Invalid FILETIME value
                return DateTime.MinValue;
            }
        }

        /// <summary>
        /// Read an IPv4 address (4 bytes)
        /// </summary>
        private System.Net.IPAddress ReadIPv4(byte[] data, int offset)
        {
            var ipBytes = new byte[4];
            Array.Copy(data, offset, ipBytes, 0, 4);
            return new System.Net.IPAddress(ipBytes);
        }

        /// <summary>
        /// Read an IPv6 address (16 bytes)
        /// </summary>
        private System.Net.IPAddress ReadIPv6(byte[] data, int offset)
        {
            var ipBytes = new byte[16];
            Array.Copy(data, offset, ipBytes, 0, 16);
            return new System.Net.IPAddress(ipBytes);
        }

        /// <summary>
        /// Read a MAC address (6 bytes)
        /// Returns formatted string like "01:23:45:67:89:AB"
        /// </summary>
        private string ReadMacAddress(byte[] data, int offset)
        {
            return string.Join(":", data.Skip(offset).Take(6).Select(b => b.ToString("X2")));
        }

        #endregion

        /// <summary>
        /// Detect if a field should use big-endian based on format hints
        /// Some formats like network protocols typically use big-endian
        /// </summary>
        public static bool ShouldUseBigEndian(string formatName)
        {
            if (string.IsNullOrEmpty(formatName))
                return false;

            var upper = formatName.ToUpperInvariant();

            // Network formats typically use big-endian (network byte order)
            // Use word boundaries to avoid false matches (e.g., ZIP containing "IP")
            if (upper.Contains("NETWORK") || upper.Contains("TCP") ||
                upper.Contains(" IP ") || upper.StartsWith("IP ") || upper.EndsWith(" IP") ||
                upper.Contains("PCAP"))
                return true;

            // Some image formats use big-endian
            if (upper.Contains("TIFF") && upper.Contains("MOTOROLA"))
                return true;

            // Java class files use big-endian
            if (upper.Contains("JAVA") || upper.Contains("CLASS"))
                return true;

            // Default: little-endian (x86, most modern formats)
            return false;
        }
    }
}
