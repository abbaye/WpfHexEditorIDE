//////////////////////////////////////////////
// Apache 2.0  - 2026
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using System;
using System.Text;

namespace WpfHexaEditor.Core
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
            for (int i = 0; i < length; i++)
            {
                if (data[offset + i] == 0)
                {
                    actualLength = i;
                    break;
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
            if (upper.Contains("NETWORK") || upper.Contains("TCP") || upper.Contains("IP") ||
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
