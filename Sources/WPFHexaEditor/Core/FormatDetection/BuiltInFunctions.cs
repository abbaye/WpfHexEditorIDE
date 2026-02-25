using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using WpfHexaEditor.Core.Bytes;

namespace WpfHexaEditor.Core.FormatDetection
{
    /// <summary>
    /// Built-in functions for format detection scripts.
    /// These functions can be called from format definition JSON files.
    /// </summary>
    public class BuiltInFunctions
    {
        private readonly byte[] _data;
        private readonly Dictionary<string, object> _variables;
        private readonly ByteProvider _byteProvider;

        public BuiltInFunctions(byte[] data, Dictionary<string, object> variables, ByteProvider byteProvider = null)
        {
            _data = data ?? throw new ArgumentNullException(nameof(data));
            _variables = variables ?? new Dictionary<string, object>();
            _byteProvider = byteProvider; // Optional - for reading beyond _data buffer
        }

        /// <summary>
        /// Detects Byte Order Mark (BOM) at the beginning of the file.
        /// Sets variables: encoding, bomDetected, bomSize
        /// </summary>
        public void DetectBOM()
        {
            if (_data.Length >= 3 &&
                _data[0] == 0xEF && _data[1] == 0xBB && _data[2] == 0xBF)
            {
                _variables["encoding"] = "UTF-8";
                _variables["bomDetected"] = true;
                _variables["bomSize"] = 3;
                return;
            }

            if (_data.Length >= 2 &&
                _data[0] == 0xFF && _data[1] == 0xFE)
            {
                if (_data.Length >= 4 && _data[2] == 0x00 && _data[3] == 0x00)
                {
                    _variables["encoding"] = "UTF-32LE";
                    _variables["bomDetected"] = true;
                    _variables["bomSize"] = 4;
                }
                else
                {
                    _variables["encoding"] = "UTF-16LE";
                    _variables["bomDetected"] = true;
                    _variables["bomSize"] = 2;
                }
                return;
            }

            if (_data.Length >= 2 &&
                _data[0] == 0xFE && _data[1] == 0xFF)
            {
                _variables["encoding"] = "UTF-16BE";
                _variables["bomDetected"] = true;
                _variables["bomSize"] = 2;
                return;
            }

            if (_data.Length >= 4 &&
                _data[0] == 0x00 && _data[1] == 0x00 &&
                _data[2] == 0xFE && _data[3] == 0xFF)
            {
                _variables["encoding"] = "UTF-32BE";
                _variables["bomDetected"] = true;
                _variables["bomSize"] = 4;
                return;
            }

            // No BOM detected
            _variables["bomDetected"] = false;
            _variables["bomSize"] = 0;
        }

        /// <summary>
        /// Counts the number of lines in the file (LF count).
        /// Sets variable: lineCount
        /// </summary>
        public void CountLines()
        {
            int count = 0;
            for (int i = 0; i < _data.Length; i++)
            {
                if (_data[i] == 0x0A) // LF
                    count++;
            }
            _variables["lineCount"] = count;
        }

        /// <summary>
        /// Detects the predominant line ending style in the file.
        /// Analyzes first 8192 bytes (or entire file if smaller).
        /// Sets variables: lineEnding, lfCount, crlfCount, crCount
        /// </summary>
        public void DetectLineEnding()
        {
            int sampleSize = Math.Min(8192, _data.Length);
            int lf = 0;
            int crlf = 0;
            int cr = 0;

            for (int i = 0; i < sampleSize; i++)
            {
                if (_data[i] == 0x0D) // CR
                {
                    if (i + 1 < sampleSize && _data[i + 1] == 0x0A) // CRLF
                    {
                        crlf++;
                        i++; // Skip the LF
                    }
                    else
                    {
                        cr++;
                    }
                }
                else if (_data[i] == 0x0A) // LF (standalone)
                {
                    lf++;
                }
            }

            _variables["lfCount"] = lf;
            _variables["crlfCount"] = crlf;
            _variables["crCount"] = cr;

            // Determine predominant style
            if (crlf > lf && crlf > cr)
                _variables["lineEnding"] = "CRLF";
            else if (lf > cr)
                _variables["lineEnding"] = "LF";
            else if (cr > 0)
                _variables["lineEnding"] = "CR";
            else
                _variables["lineEnding"] = "None";
        }

        /// <summary>
        /// Returns the minimum of two numbers.
        /// </summary>
        public long Min(long a, long b)
        {
            return Math.Min(a, b);
        }

        /// <summary>
        /// Returns the maximum of two numbers.
        /// </summary>
        public long Max(long a, long b)
        {
            return Math.Max(a, b);
        }

        /// <summary>
        /// Extracts a substring from the data as UTF-8 text.
        /// </summary>
        public string Substring(long offset, long length)
        {
            if (offset < 0 || offset >= _data.Length)
                return string.Empty;

            long actualLength = Math.Min(length, _data.Length - offset);
            if (actualLength <= 0)
                return string.Empty;

            try
            {
                return Encoding.UTF8.GetString(_data, (int)offset, (int)actualLength);
            }
            catch
            {
                return string.Empty;
            }
        }

        #region Phase 1 - Core Functions (Critical Priority)

        /// <summary>
        /// Calculates CRC-32 checksum for a section of data.
        /// Sets variable: calculatedCRC32
        /// </summary>
        /// <param name="offset">Starting offset in file</param>
        /// <param name="length">Number of bytes to process</param>
        public void CalculateCRC32(long offset, long length)
        {
            if (offset < 0 || offset >= _data.Length || length <= 0)
            {
                _variables["calculatedCRC32"] = 0u;
                return;
            }

            long actualLength = Math.Min(length, _data.Length - offset);
            if (actualLength <= 0)
            {
                _variables["calculatedCRC32"] = 0u;
                return;
            }

            try
            {
                uint crc = 0xFFFFFFFF;
                long endOffset = offset + actualLength;

                for (long i = offset; i < endOffset; i++)
                {
                    crc = (crc >> 8) ^ Crc32Table[(crc ^ _data[i]) & 0xFF];
                }

                _variables["calculatedCRC32"] = crc ^ 0xFFFFFFFF;
            }
            catch
            {
                _variables["calculatedCRC32"] = 0u;
            }
        }

        /// <summary>
        /// Validates CRC-32 checksum against expected value.
        /// Sets variables: crc32Valid, crc32Mismatch
        /// </summary>
        /// <param name="offset">Starting offset in file</param>
        /// <param name="length">Number of bytes to process</param>
        /// <param name="expectedCRC">Expected CRC-32 value</param>
        public void ValidateCRC32(long offset, long length, uint expectedCRC)
        {
            CalculateCRC32(offset, length);

            if (_variables.TryGetValue("calculatedCRC32", out var calculated))
            {
                uint calculatedCRC = Convert.ToUInt32(calculated);
                bool isValid = calculatedCRC == expectedCRC;

                _variables["crc32Valid"] = isValid;
                _variables["crc32Mismatch"] = !isValid;
                _variables["expectedCRC32"] = expectedCRC;
            }
            else
            {
                _variables["crc32Valid"] = false;
                _variables["crc32Mismatch"] = true;
            }
        }

        /// <summary>
        /// Multiplies two numbers (for calculations like width × height).
        /// Sets variable: calculatedSize
        /// </summary>
        /// <param name="a">First number</param>
        /// <param name="b">Second number</param>
        /// <returns>Product of a and b</returns>
        public long Multiply(long a, long b)
        {
            try
            {
                long result = checked(a * b); // Check for overflow
                _variables["calculatedSize"] = result;
                return result;
            }
            catch (OverflowException)
            {
                _variables["calculatedSize"] = long.MaxValue;
                return long.MaxValue;
            }
        }

        /// <summary>
        /// Reads a 32-bit unsigned integer in little-endian format.
        /// Sets variable: readValue
        /// </summary>
        /// <param name="offset">Offset where to read the value</param>
        /// <returns>The uint32 value</returns>
        public uint ReadUInt32LE(long offset)
        {
            if (offset < 0 || offset + 4 > _data.Length)
            {
                _variables["readValue"] = 0u;
                return 0u;
            }

            try
            {
                uint value = (uint)(_data[offset] |
                                   (_data[offset + 1] << 8) |
                                   (_data[offset + 2] << 16) |
                                   (_data[offset + 3] << 24));

                _variables["readValue"] = value;
                return value;
            }
            catch
            {
                _variables["readValue"] = 0u;
                return 0u;
            }
        }

        /// <summary>
        /// Reads a 32-bit unsigned integer in big-endian format.
        /// Sets variable: readValue
        /// </summary>
        /// <param name="offset">Offset where to read the value</param>
        /// <returns>The uint32 value</returns>
        public uint ReadUInt32BE(long offset)
        {
            if (offset < 0 || offset + 4 > _data.Length)
            {
                _variables["readValue"] = 0u;
                return 0u;
            }

            try
            {
                uint value = (uint)((_data[offset] << 24) |
                                   (_data[offset + 1] << 16) |
                                   (_data[offset + 2] << 8) |
                                    _data[offset + 3]);

                _variables["readValue"] = value;
                return value;
            }
            catch
            {
                _variables["readValue"] = 0u;
                return 0u;
            }
        }

        /// <summary>
        /// Reads an 8-bit unsigned integer.
        /// Sets variable: readValue
        /// </summary>
        /// <param name="offset">Offset where to read the value</param>
        /// <returns>The uint8 value</returns>
        public byte ReadUInt8(long offset)
        {
            if (offset < 0 || offset >= _data.Length)
            {
                _variables["readValue"] = (byte)0;
                return 0;
            }

            try
            {
                byte value = _data[offset];
                _variables["readValue"] = value;
                return value;
            }
            catch
            {
                _variables["readValue"] = (byte)0;
                return 0;
            }
        }

        /// <summary>
        /// Reads a 16-bit unsigned integer in little-endian format.
        /// Sets variable: readValue
        /// </summary>
        /// <param name="offset">Offset where to read the value</param>
        /// <returns>The uint16 value</returns>
        public ushort ReadUInt16LE(long offset)
        {
            if (offset < 0 || offset + 2 > _data.Length)
            {
                _variables["readValue"] = (ushort)0;
                return 0;
            }

            try
            {
                ushort value = (ushort)(_data[offset] |
                                       (_data[offset + 1] << 8));

                _variables["readValue"] = value;
                return value;
            }
            catch
            {
                _variables["readValue"] = (ushort)0;
                return 0;
            }
        }

        /// <summary>
        /// Reads a 16-bit unsigned integer in big-endian format.
        /// Sets variable: readValue
        /// </summary>
        /// <param name="offset">Offset where to read the value</param>
        /// <returns>The uint16 value</returns>
        public ushort ReadUInt16BE(long offset)
        {
            if (offset < 0 || offset + 2 > _data.Length)
            {
                _variables["readValue"] = (ushort)0;
                return 0;
            }

            try
            {
                ushort value = (ushort)((_data[offset] << 8) |
                                        _data[offset + 1]);

                _variables["readValue"] = value;
                return value;
            }
            catch
            {
                _variables["readValue"] = (ushort)0;
                return 0;
            }
        }

        /// <summary>
        /// Reads a 24-bit unsigned integer in little-endian format.
        /// Sets variable: readValue
        /// </summary>
        /// <param name="offset">Offset where to read the value</param>
        /// <returns>The uint24 value as uint32</returns>
        public uint ReadUInt24LE(long offset)
        {
            if (offset < 0 || offset + 3 > _data.Length)
            {
                _variables["readValue"] = 0u;
                return 0u;
            }

            try
            {
                uint value = (uint)(_data[offset] |
                                   (_data[offset + 1] << 8) |
                                   (_data[offset + 2] << 16));

                _variables["readValue"] = value;
                return value;
            }
            catch
            {
                _variables["readValue"] = 0u;
                return 0u;
            }
        }

        /// <summary>
        /// Reads a 64-bit unsigned integer in little-endian format.
        /// Sets variable: readValue
        /// </summary>
        /// <param name="offset">Offset where to read the value</param>
        /// <returns>The uint64 value</returns>
        public ulong ReadUInt64LE(long offset)
        {
            if (offset < 0 || offset + 8 > _data.Length)
            {
                _variables["readValue"] = 0UL;
                return 0UL;
            }

            try
            {
                ulong value = (ulong)_data[offset] |
                             ((ulong)_data[offset + 1] << 8) |
                             ((ulong)_data[offset + 2] << 16) |
                             ((ulong)_data[offset + 3] << 24) |
                             ((ulong)_data[offset + 4] << 32) |
                             ((ulong)_data[offset + 5] << 40) |
                             ((ulong)_data[offset + 6] << 48) |
                             ((ulong)_data[offset + 7] << 56);

                _variables["readValue"] = value;
                return value;
            }
            catch
            {
                _variables["readValue"] = 0UL;
                return 0UL;
            }
        }

        /// <summary>
        /// Reads a 64-bit unsigned integer in big-endian format.
        /// Sets variable: readValue
        /// </summary>
        /// <param name="offset">Offset where to read the value</param>
        /// <returns>The uint64 value</returns>
        public ulong ReadUInt64BE(long offset)
        {
            if (offset < 0 || offset + 8 > _data.Length)
            {
                _variables["readValue"] = 0UL;
                return 0UL;
            }

            try
            {
                ulong value = ((ulong)_data[offset] << 56) |
                             ((ulong)_data[offset + 1] << 48) |
                             ((ulong)_data[offset + 2] << 40) |
                             ((ulong)_data[offset + 3] << 32) |
                             ((ulong)_data[offset + 4] << 24) |
                             ((ulong)_data[offset + 5] << 16) |
                             ((ulong)_data[offset + 6] << 8) |
                             (ulong)_data[offset + 7];

                _variables["readValue"] = value;
                return value;
            }
            catch
            {
                _variables["readValue"] = 0UL;
                return 0UL;
            }
        }

        /// <summary>
        /// Specialized function to read PNG IHDR dimensions and calculate stats.
        /// Sets variables: imageWidth, imageHeight, totalPixels
        /// </summary>
        public void ReadPNGDimensions()
        {
            // PNG IHDR structure: width at offset 16, height at offset 20 (both big-endian uint32)
            if (_data.Length < 24)
            {
                _variables["imageWidth"] = 0u;
                _variables["imageHeight"] = 0u;
                _variables["totalPixels"] = 0L;
                return;
            }

            try
            {
                uint width = ReadUInt32BE(16);
                uint height = ReadUInt32BE(20);

                _variables["imageWidth"] = width;
                _variables["imageHeight"] = height;
                _variables["totalPixels"] = (long)width * height;
            }
            catch
            {
                _variables["imageWidth"] = 0u;
                _variables["imageHeight"] = 0u;
                _variables["totalPixels"] = 0L;
            }
        }

        /// <summary>
        /// Specialized function to validate PNG IHDR chunk CRC.
        /// Reads stored CRC from offset 29 and calculates CRC over IHDR type + data (offset 12-28).
        /// Sets variables: ihdrCRC, calculatedCRC32, crc32Valid
        /// </summary>
        public void ValidatePNGIHDR()
        {
            if (_data.Length < 33)
            {
                _variables["crc32Valid"] = false;
                return;
            }

            try
            {
                // Read stored CRC at offset 29 (big-endian)
                uint storedCRC = ReadUInt32BE(29);
                _variables["ihdrCRC"] = storedCRC;

                // Calculate CRC over IHDR chunk type + data (offset 12 to 28 = 17 bytes)
                CalculateCRC32(12, 17);

                // Compare
                if (_variables.TryGetValue("calculatedCRC32", out var calculated))
                {
                    uint calculatedCRC = Convert.ToUInt32(calculated);
                    bool isValid = calculatedCRC == storedCRC;

                    _variables["crc32Valid"] = isValid;
                    _variables["crc32Mismatch"] = !isValid;
                }
            }
            catch
            {
                _variables["crc32Valid"] = false;
            }
        }

        /// <summary>
        /// Calculates MD5 hash of a data section.
        /// Sets variable: md5Hash (hexadecimal string)
        /// </summary>
        /// <param name="offset">Starting offset</param>
        /// <param name="length">Number of bytes to hash</param>
        public void CalculateMD5(long offset, long length)
        {
            if (offset < 0 || offset >= _data.Length || length <= 0)
            {
                _variables["md5Hash"] = string.Empty;
                return;
            }

            try
            {
                long actualLength = Math.Min(length, _data.Length - offset);
                byte[] buffer = new byte[actualLength];
                Array.Copy(_data, offset, buffer, 0, actualLength);

#pragma warning disable CA5351 // Do not use broken cryptographic algorithms (MD5 used for file validation, not security)
                using (var md5 = MD5.Create())
                {
                    byte[] hash = md5.ComputeHash(buffer);
                    string hashString = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                    _variables["md5Hash"] = hashString;
                }
#pragma warning restore CA5351
            }
            catch
            {
                _variables["md5Hash"] = string.Empty;
            }
        }

        /// <summary>
        /// Calculates SHA-1 hash of a data section.
        /// Sets variable: sha1Hash (hexadecimal string)
        /// </summary>
        /// <param name="offset">Starting offset</param>
        /// <param name="length">Number of bytes to hash</param>
        public void CalculateSHA1(long offset, long length)
        {
            if (offset < 0 || offset >= _data.Length || length <= 0)
            {
                _variables["sha1Hash"] = string.Empty;
                return;
            }

            try
            {
                long actualLength = Math.Min(length, _data.Length - offset);
                byte[] buffer = new byte[actualLength];
                Array.Copy(_data, offset, buffer, 0, actualLength);

#pragma warning disable CA5350 // Do not use weak cryptographic algorithms (SHA1 used for file validation, not security)
                using (var sha1 = SHA1.Create())
                {
                    byte[] hash = sha1.ComputeHash(buffer);
                    string hashString = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                    _variables["sha1Hash"] = hashString;
                }
#pragma warning restore CA5350
            }
            catch
            {
                _variables["sha1Hash"] = string.Empty;
            }
        }

        /// <summary>
        /// Calculates SHA-256 hash of a data section.
        /// Sets variable: sha256Hash (hexadecimal string)
        /// </summary>
        /// <param name="offset">Starting offset</param>
        /// <param name="length">Number of bytes to hash</param>
        public void CalculateSHA256(long offset, long length)
        {
            if (offset < 0 || offset >= _data.Length || length <= 0)
            {
                _variables["sha256Hash"] = string.Empty;
                return;
            }

            try
            {
                long actualLength = Math.Min(length, _data.Length - offset);
                byte[] buffer = new byte[actualLength];
                Array.Copy(_data, offset, buffer, 0, actualLength);

                using (var sha256 = SHA256.Create())
                {
                    byte[] hash = sha256.ComputeHash(buffer);
                    string hashString = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                    _variables["sha256Hash"] = hashString;
                }
            }
            catch
            {
                _variables["sha256Hash"] = string.Empty;
            }
        }

        /// <summary>
        /// Divides two integers with protection against division by zero.
        /// Sets variable: calculatedValue
        /// </summary>
        /// <param name="numerator">The numerator</param>
        /// <param name="denominator">The denominator</param>
        /// <returns>The result of division</returns>
        public long Divide(long numerator, long denominator)
        {
            if (denominator == 0)
            {
                _variables["calculatedValue"] = 0L;
                return 0L;
            }

            try
            {
                long result = numerator / denominator;
                _variables["calculatedValue"] = result;
                return result;
            }
            catch
            {
                _variables["calculatedValue"] = 0L;
                return 0L;
            }
        }

        /// <summary>
        /// Performs bitwise AND operation.
        /// Sets variable: extractedFlags
        /// </summary>
        /// <param name="a">First value</param>
        /// <param name="b">Second value</param>
        /// <returns>Result of a AND b</returns>
        public long BitwiseAND(long a, long b)
        {
            long result = a & b;
            _variables["extractedFlags"] = result;
            return result;
        }

        /// <summary>
        /// Performs bitwise OR operation.
        /// </summary>
        /// <param name="a">First value</param>
        /// <param name="b">Second value</param>
        /// <returns>Result of a OR b</returns>
        public long BitwiseOR(long a, long b)
        {
            long result = a | b;
            _variables["combinedFlags"] = result;
            return result;
        }

        /// <summary>
        /// Performs right bit shift operation.
        /// Sets variable: shiftedValue
        /// </summary>
        /// <param name="value">Value to shift</param>
        /// <param name="bits">Number of bits to shift right</param>
        /// <returns>Shifted value</returns>
        public long BitShiftRight(long value, int bits)
        {
            if (bits < 0 || bits > 63)
            {
                _variables["shiftedValue"] = value;
                return value;
            }

            long result = value >> bits;
            _variables["shiftedValue"] = result;
            return result;
        }

        /// <summary>
        /// Extracts null-terminated ASCII string from data.
        /// Sets variables: extractedString, stringLength
        /// </summary>
        /// <param name="offset">Starting offset</param>
        /// <param name="maxLength">Maximum length to read</param>
        /// <returns>Extracted string</returns>
        public string ExtractASCIIString(long offset, long maxLength)
        {
            if (offset < 0 || offset >= _data.Length || maxLength <= 0)
            {
                _variables["extractedString"] = string.Empty;
                _variables["stringLength"] = 0;
                return string.Empty;
            }

            try
            {
                long actualMaxLength = Math.Min(maxLength, _data.Length - offset);
                var sb = new StringBuilder();
                long length = 0;

                for (long i = 0; i < actualMaxLength; i++)
                {
                    byte b = _data[offset + i];
                    if (b == 0) // Null terminator
                        break;

                    if (b >= 32 && b <= 126) // Printable ASCII
                    {
                        sb.Append((char)b);
                    }
                    else
                    {
                        sb.Append('?'); // Replace non-printable with ?
                    }

                    length++;
                }

                string result = sb.ToString();
                _variables["extractedString"] = result;
                _variables["stringLength"] = (int)length;
                return result;
            }
            catch
            {
                _variables["extractedString"] = string.Empty;
                _variables["stringLength"] = 0;
                return string.Empty;
            }
        }

        /// <summary>
        /// Extracts null-terminated UTF-8 string from data.
        /// Sets variable: extractedString
        /// </summary>
        /// <param name="offset">Starting offset</param>
        /// <param name="maxLength">Maximum length to read</param>
        /// <returns>Extracted string</returns>
        public string ExtractUTF8String(long offset, long maxLength)
        {
            if (offset < 0 || offset >= _data.Length || maxLength <= 0)
            {
                _variables["extractedString"] = string.Empty;
                _variables["stringLength"] = 0;
                return string.Empty;
            }

            try
            {
                long actualMaxLength = Math.Min(maxLength, _data.Length - offset);
                long endOffset = offset + actualMaxLength;

                // Find null terminator
                long nullPos = -1;
                for (long i = offset; i < endOffset; i++)
                {
                    if (_data[i] == 0)
                    {
                        nullPos = i;
                        break;
                    }
                }

                long length = nullPos >= 0 ? nullPos - offset : actualMaxLength;
                if (length <= 0)
                {
                    _variables["extractedString"] = string.Empty;
                    _variables["stringLength"] = 0;
                    return string.Empty;
                }

                byte[] buffer = new byte[length];
                Array.Copy(_data, offset, buffer, 0, length);

                string result = Encoding.UTF8.GetString(buffer);
                _variables["extractedString"] = result;
                _variables["stringLength"] = (int)length;
                return result;
            }
            catch
            {
                _variables["extractedString"] = string.Empty;
                _variables["stringLength"] = 0;
                return string.Empty;
            }
        }

        /// <summary>
        /// Finds first occurrence of a byte sequence.
        /// Sets variables: foundOffset, patternFound
        /// </summary>
        /// <param name="pattern">Byte pattern to search for</param>
        /// <param name="startOffset">Offset to start searching from</param>
        /// <returns>Offset where pattern was found, or -1 if not found</returns>
        public long FindByteSequence(byte[] pattern, long startOffset)
        {
            if (pattern == null || pattern.Length == 0 || startOffset < 0 || startOffset >= _data.Length)
            {
                _variables["foundOffset"] = -1L;
                _variables["patternFound"] = false;
                return -1L;
            }

            try
            {
                long searchLimit = _data.Length - pattern.Length + 1;

                for (long i = startOffset; i < searchLimit; i++)
                {
                    bool found = true;
                    for (int j = 0; j < pattern.Length; j++)
                    {
                        if (_data[i + j] != pattern[j])
                        {
                            found = false;
                            break;
                        }
                    }

                    if (found)
                    {
                        _variables["foundOffset"] = i;
                        _variables["patternFound"] = true;
                        return i;
                    }
                }

                // Not found
                _variables["foundOffset"] = -1L;
                _variables["patternFound"] = false;
                return -1L;
            }
            catch
            {
                _variables["foundOffset"] = -1L;
                _variables["patternFound"] = false;
                return -1L;
            }
        }

        #endregion

        #region Phase 2 - Specialized Functions (Media, Archives, Images)

        /// <summary>
        /// Finds and parses MP3 frame header automatically.
        /// Skips ID3v2 tag if present and finds first MP3 frame sync word.
        /// Sets variables: mp3Version, mp3Layer, bitrate, sampleRate, channelMode, frameOffset
        /// </summary>
        public void ParseMP3Header()
        {
            try
            {
                long searchStart = 0;

                // Check for ID3v2 tag and skip it
                if (_data.Length >= 10 &&
                    _data[0] == 0x49 && _data[1] == 0x44 && _data[2] == 0x33) // "ID3"
                {
                    // ID3v2 size is a syncsafe integer at offset 6-9
                    int size = (_data[6] << 21) | (_data[7] << 14) | (_data[8] << 7) | _data[9];
                    searchStart = 10 + size; // 10-byte header + tag size
                }

                // Find first MP3 frame sync word (11 bits set: 0xFF Ex)
                long frameOffset = -1;
                for (long i = searchStart; i < _data.Length - 4; i++)
                {
                    if (_data[i] == 0xFF && (_data[i + 1] & 0xE0) == 0xE0)
                    {
                        frameOffset = i;
                        break;
                    }
                }

                if (frameOffset < 0)
                {
                    _variables["mp3Version"] = "No Frame";
                    _variables["mp3Layer"] = "Unknown";
                    _variables["bitrate"] = 0;
                    _variables["sampleRate"] = 0;
                    _variables["channelMode"] = "Unknown";
                    _variables["frameOffset"] = -1L;
                    return;
                }

                _variables["frameOffset"] = frameOffset;

                // Parse frame header at found offset
                ParseMP3FrameHeader(frameOffset);
            }
            catch
            {
                _variables["mp3Version"] = "Error";
                _variables["mp3Layer"] = "Error";
                _variables["bitrate"] = 0;
                _variables["sampleRate"] = 0;
                _variables["channelMode"] = "Error";
                _variables["frameOffset"] = -1L;
            }
        }

        /// <summary>
        /// Parses MP3 frame header (4 bytes).
        /// Sets variables: mp3Version, mp3Layer, bitrate, sampleRate, channelMode
        /// </summary>
        /// <param name="offset">Offset of MP3 frame header</param>
        public void ParseMP3FrameHeader(long offset)
        {
            if (offset < 0 || offset + 4 > _data.Length)
            {
                _variables["mp3Version"] = "Unknown";
                _variables["mp3Layer"] = "Unknown";
                _variables["bitrate"] = 0;
                _variables["sampleRate"] = 0;
                _variables["channelMode"] = "Unknown";
                return;
            }

            try
            {
                // MP3 frame header is 4 bytes (32 bits)
                uint header = (uint)((_data[offset] << 24) |
                                    (_data[offset + 1] << 16) |
                                    (_data[offset + 2] << 8) |
                                     _data[offset + 3]);

                // Check sync word (11 bits all set to 1)
                if ((header & 0xFFE00000) != 0xFFE00000)
                {
                    _variables["mp3Version"] = "Invalid";
                    return;
                }

                // MPEG version (2 bits)
                int versionBits = (int)((header >> 19) & 0x3);
                string version = versionBits switch
                {
                    0 => "MPEG 2.5",
                    2 => "MPEG 2",
                    3 => "MPEG 1",
                    _ => "Reserved"
                };
                _variables["mp3Version"] = version;

                // Layer (2 bits)
                int layerBits = (int)((header >> 17) & 0x3);
                string layer = layerBits switch
                {
                    1 => "Layer III",
                    2 => "Layer II",
                    3 => "Layer I",
                    _ => "Reserved"
                };
                _variables["mp3Layer"] = layer;

                // Bitrate index (4 bits)
                int bitrateIndex = (int)((header >> 12) & 0xF);

                // Bitrate table for MPEG 1 Layer III (most common)
                int[] bitrateTable = { 0, 32, 40, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320, 0 };
                int bitrate = bitrateIndex < bitrateTable.Length ? bitrateTable[bitrateIndex] : 0;
                _variables["bitrate"] = bitrate;

                // Sample rate index (2 bits)
                int sampleRateIndex = (int)((header >> 10) & 0x3);
                int[] sampleRateTable = { 44100, 48000, 32000, 0 }; // For MPEG 1
                int sampleRate = sampleRateIndex < sampleRateTable.Length ? sampleRateTable[sampleRateIndex] : 0;
                _variables["sampleRate"] = sampleRate;

                // Channel mode (2 bits)
                int channelModeBits = (int)((header >> 6) & 0x3);
                string channelMode = channelModeBits switch
                {
                    0 => "Stereo",
                    1 => "Joint Stereo",
                    2 => "Dual Channel",
                    3 => "Mono",
                    _ => "Unknown"
                };
                _variables["channelMode"] = channelMode;
            }
            catch
            {
                _variables["mp3Version"] = "Error";
                _variables["mp3Layer"] = "Error";
                _variables["bitrate"] = 0;
                _variables["sampleRate"] = 0;
                _variables["channelMode"] = "Error";
            }
        }

        /// <summary>
        /// Calculates MP3 duration from frame count and sample rate.
        /// Sets variables: durationSeconds, durationFormatted
        /// </summary>
        /// <param name="totalFrames">Total number of MP3 frames</param>
        /// <param name="sampleRate">Sample rate in Hz</param>
        public void CalculateMP3Duration(long totalFrames, long sampleRate)
        {
            if (sampleRate == 0 || totalFrames == 0)
            {
                _variables["durationSeconds"] = 0.0;
                _variables["durationFormatted"] = "00:00";
                return;
            }

            try
            {
                // Each MP3 frame contains 1152 samples for Layer III
                const int samplesPerFrame = 1152;
                double totalSamples = totalFrames * samplesPerFrame;
                double durationSeconds = totalSamples / sampleRate;

                _variables["durationSeconds"] = durationSeconds;

                // Format as MM:SS
                int minutes = (int)(durationSeconds / 60);
                int seconds = (int)(durationSeconds % 60);
                _variables["durationFormatted"] = $"{minutes:D2}:{seconds:D2}";
            }
            catch
            {
                _variables["durationSeconds"] = 0.0;
                _variables["durationFormatted"] = "00:00";
            }
        }

        /// <summary>
        /// Parses WAV format chunk (fmt).
        /// Sets variables: wavFormat, channels, sampleRate, byteRate, bitDepth
        /// </summary>
        /// <param name="offset">Offset of WAV format chunk data (after chunk header)</param>
        public void ParseWAVFormatChunk(long offset)
        {
            if (offset < 0 || offset + 16 > _data.Length)
            {
                _variables["wavFormat"] = "Unknown";
                _variables["channels"] = 0;
                _variables["sampleRate"] = 0;
                _variables["byteRate"] = 0;
                _variables["bitDepth"] = 0;
                return;
            }

            try
            {
                // WAV format chunk (little-endian)
                ushort audioFormat = (ushort)(_data[offset] | (_data[offset + 1] << 8));
                ushort channels = (ushort)(_data[offset + 2] | (_data[offset + 3] << 8));
                uint sampleRate = (uint)(_data[offset + 4] | (_data[offset + 5] << 8) |
                                        (_data[offset + 6] << 16) | (_data[offset + 7] << 24));
                uint byteRate = (uint)(_data[offset + 8] | (_data[offset + 9] << 8) |
                                      (_data[offset + 10] << 16) | (_data[offset + 11] << 24));
                ushort blockAlign = (ushort)(_data[offset + 12] | (_data[offset + 13] << 8));
                ushort bitsPerSample = (ushort)(_data[offset + 14] | (_data[offset + 15] << 8));

                string formatName = audioFormat switch
                {
                    1 => "PCM",
                    3 => "IEEE Float",
                    6 => "A-law",
                    7 => "μ-law",
                    65534 => "Extensible",
                    _ => $"Format {audioFormat}"
                };

                _variables["wavFormat"] = formatName;
                _variables["channels"] = (int)channels;
                _variables["sampleRate"] = (int)sampleRate;
                _variables["byteRate"] = (int)byteRate;
                _variables["bitDepth"] = (int)bitsPerSample;
            }
            catch
            {
                _variables["wavFormat"] = "Error";
                _variables["channels"] = 0;
                _variables["sampleRate"] = 0;
                _variables["byteRate"] = 0;
                _variables["bitDepth"] = 0;
            }
        }

        /// <summary>
        /// Parses ZIP archive by finding and parsing the End of Central Directory (EOCD) record.
        /// Supports both ZIP32 and ZIP64 formats.
        /// Sets variables: zipFileCount, zipCentralDirSize, zipCommentLength, zipComment, eocdOffset, isZip64
        /// </summary>
        public void ParseZIPArchive()
        {
            try
            {
                byte[] searchBuffer;
                long fileLength;
                long bufferStartOffset;

                if (_byteProvider != null && _byteProvider.IsOpen)
                {
                    fileLength = _byteProvider.VirtualLength;
                    int searchSize = (int)Math.Min(65579, fileLength);
                    bufferStartOffset = fileLength - searchSize;
                    searchBuffer = _byteProvider.GetBytes(bufferStartOffset, searchSize);
                }
                else
                {
                    fileLength = _data.Length;
                    searchBuffer = _data;
                    bufferStartOffset = 0;
                }

                long eocdOffsetInBuffer = -1;
                bool isZip64 = false;

                for (long i = searchBuffer.Length - 22; i >= 0; i--)
                {
                    if (i + 4 <= searchBuffer.Length &&
                        searchBuffer[i] == 0x50 && searchBuffer[i + 1] == 0x4B &&
                        searchBuffer[i + 2] == 0x05 && searchBuffer[i + 3] == 0x06)
                    {
                        eocdOffsetInBuffer = i;
                        break;
                    }
                }

                if (eocdOffsetInBuffer < 0)
                {
                    for (long i = searchBuffer.Length - 20; i >= 0; i--)
                    {
                        if (i + 4 <= searchBuffer.Length &&
                            searchBuffer[i] == 0x50 && searchBuffer[i + 1] == 0x4B &&
                            searchBuffer[i + 2] == 0x06 && searchBuffer[i + 3] == 0x07)
                        {
                            if (i + 16 <= searchBuffer.Length)
                            {
                                long eocd64Offset = searchBuffer[i + 8] | ((long)searchBuffer[i + 9] << 8) |
                                                   ((long)searchBuffer[i + 10] << 16) | ((long)searchBuffer[i + 11] << 24) |
                                                   ((long)searchBuffer[i + 12] << 32) | ((long)searchBuffer[i + 13] << 40) |
                                                   ((long)searchBuffer[i + 14] << 48) | ((long)searchBuffer[i + 15] << 56);

                                long eocd64RelativeOffset = eocd64Offset - bufferStartOffset;
                                if (eocd64RelativeOffset >= 0 && eocd64RelativeOffset + 56 <= searchBuffer.Length)
                                {
                                    if (searchBuffer[eocd64RelativeOffset] == 0x50 && searchBuffer[eocd64RelativeOffset + 1] == 0x4B &&
                                        searchBuffer[eocd64RelativeOffset + 2] == 0x06 && searchBuffer[eocd64RelativeOffset + 3] == 0x06)
                                    {
                                        eocdOffsetInBuffer = eocd64RelativeOffset;
                                        isZip64 = true;
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }

                long eocdOffset = eocdOffsetInBuffer >= 0 ? bufferStartOffset + eocdOffsetInBuffer : -1;

                if (eocdOffsetInBuffer < 0 || eocdOffsetInBuffer + (isZip64 ? 56 : 22) > searchBuffer.Length)
                {
                    _variables["zipFileCount"] = 0;
                    _variables["zipCentralDirSize"] = 0L;
                    _variables["zipCommentLength"] = 0;
                    _variables["eocdOffset"] = -1L;
                    _variables["zipComment"] = string.Empty;
                    _variables["isZip64"] = false;
                    return;
                }

                _variables["eocdOffset"] = eocdOffset;
                _variables["isZip64"] = isZip64;

                long offset = eocdOffsetInBuffer;
                long totalEntries;
                long centralDirSize;
                int commentLength = 0;

                if (isZip64)
                {
                    offset += 24;
                    long entriesOnDisk = searchBuffer[offset] | ((long)searchBuffer[offset + 1] << 8) |
                                        ((long)searchBuffer[offset + 2] << 16) | ((long)searchBuffer[offset + 3] << 24) |
                                        ((long)searchBuffer[offset + 4] << 32) | ((long)searchBuffer[offset + 5] << 40) |
                                        ((long)searchBuffer[offset + 6] << 48) | ((long)searchBuffer[offset + 7] << 56);
                    offset += 8;

                    totalEntries = searchBuffer[offset] | ((long)searchBuffer[offset + 1] << 8) |
                                  ((long)searchBuffer[offset + 2] << 16) | ((long)searchBuffer[offset + 3] << 24) |
                                  ((long)searchBuffer[offset + 4] << 32) | ((long)searchBuffer[offset + 5] << 40) |
                                  ((long)searchBuffer[offset + 6] << 48) | ((long)searchBuffer[offset + 7] << 56);
                    offset += 8;

                    centralDirSize = searchBuffer[offset] | ((long)searchBuffer[offset + 1] << 8) |
                                    ((long)searchBuffer[offset + 2] << 16) | ((long)searchBuffer[offset + 3] << 24) |
                                    ((long)searchBuffer[offset + 4] << 32) | ((long)searchBuffer[offset + 5] << 40) |
                                    ((long)searchBuffer[offset + 6] << 48) | ((long)searchBuffer[offset + 7] << 56);

                    _variables["zipEntriesOnDisk"] = entriesOnDisk;
                }
                else
                {
                    offset += 8;
                    ushort entriesOnDisk = (ushort)(searchBuffer[offset] | (searchBuffer[offset + 1] << 8));
                    offset += 2;
                    totalEntries = (ushort)(searchBuffer[offset] | (searchBuffer[offset + 1] << 8));
                    offset += 2;
                    centralDirSize = (uint)(searchBuffer[offset] | (searchBuffer[offset + 1] << 8) |
                                          (searchBuffer[offset + 2] << 16) | (searchBuffer[offset + 3] << 24));
                    offset += 4;
                    offset += 4;
                    commentLength = (ushort)(searchBuffer[offset] | (searchBuffer[offset + 1] << 8));
                    _variables["zipEntriesOnDisk"] = (int)entriesOnDisk;
                }

                _variables["zipFileCount"] = totalEntries;
                _variables["zipCentralDirSize"] = centralDirSize;
                _variables["zipCommentLength"] = commentLength;

                if (!isZip64 && commentLength > 0 && eocdOffsetInBuffer + 22 + commentLength <= searchBuffer.Length)
                {
                    byte[] commentBytes = new byte[commentLength];
                    Array.Copy(searchBuffer, eocdOffsetInBuffer + 22, commentBytes, 0, commentLength);
                    string comment = Encoding.UTF8.GetString(commentBytes);
                    _variables["zipComment"] = comment;
                }
                else
                {
                    _variables["zipComment"] = string.Empty;
                }
            }
            catch (Exception ex)
            {
                _variables["zipFileCount"] = 0;
                _variables["zipCentralDirSize"] = 0L;
                _variables["zipCommentLength"] = 0;
                _variables["eocdOffset"] = -1L;
                _variables["zipComment"] = $"Error: {ex.Message}";
            }
        }


        /// <summary>
        /// Calculates compression ratio as percentage.
        /// Sets variable: compressionRatio
        /// </summary>
        /// <param name="compressedSize">Compressed size in bytes</param>
        /// <param name="uncompressedSize">Uncompressed size in bytes</param>
        public void CalculateCompressionRatio(long compressedSize, long uncompressedSize)
        {
            if (uncompressedSize == 0)
            {
                _variables["compressionRatio"] = 0.0;
                return;
            }

            try
            {
                double ratio = (1.0 - ((double)compressedSize / uncompressedSize)) * 100.0;
                _variables["compressionRatio"] = Math.Round(ratio, 2);
            }
            catch
            {
                _variables["compressionRatio"] = 0.0;
            }
        }

        /// <summary>
        /// Finds and parses JPEG Start Of Frame (SOF) marker to extract dimensions.
        /// Searches for SOF0 (0xFFC0) or SOF2 (0xFFC2) markers automatically.
        /// Sets variables: jpegWidth, jpegHeight, jpegComponents, jpegPrecision, sofOffset
        /// </summary>
        public void ParseJPEGDimensions()
        {
            try
            {
                // Search for SOF markers (baseline: FF C0, progressive: FF C2)
                long sofOffset = -1;

                // Search for SOF0 (baseline) first
                for (long i = 0; i < _data.Length - 10; i++)
                {
                    if (_data[i] == 0xFF && (_data[i + 1] == 0xC0 || _data[i + 1] == 0xC2))
                    {
                        sofOffset = i;
                        _variables["jpegProgressive"] = _data[i + 1] == 0xC2;
                        break;
                    }
                }

                if (sofOffset < 0)
                {
                    _variables["jpegWidth"] = 0;
                    _variables["jpegHeight"] = 0;
                    _variables["jpegComponents"] = 0;
                    _variables["jpegPrecision"] = 0;
                    _variables["sofOffset"] = -1L;
                    return;
                }

                _variables["sofOffset"] = sofOffset;

                // Parse SOF marker (skip FF Cx marker bytes)
                long dataOffset = sofOffset + 2;

                // SOF structure: length(2) + precision(1) + height(2) + width(2) + components(1)
                if (dataOffset + 8 > _data.Length)
                {
                    _variables["jpegWidth"] = 0;
                    _variables["jpegHeight"] = 0;
                    return;
                }

                // Skip length field (2 bytes)
                byte precision = _data[dataOffset + 2];
                ushort height = (ushort)((_data[dataOffset + 3] << 8) | _data[dataOffset + 4]);
                ushort width = (ushort)((_data[dataOffset + 5] << 8) | _data[dataOffset + 6]);
                byte components = _data[dataOffset + 7];

                _variables["jpegWidth"] = (int)width;
                _variables["jpegHeight"] = (int)height;
                _variables["jpegComponents"] = (int)components;
                _variables["jpegPrecision"] = (int)precision;
            }
            catch
            {
                _variables["jpegWidth"] = 0;
                _variables["jpegHeight"] = 0;
                _variables["jpegComponents"] = 0;
                _variables["jpegPrecision"] = 0;
                _variables["sofOffset"] = -1L;
            }
        }

        /// <summary>
        /// Parses JPEG Start Of Frame (SOF) marker to extract dimensions.
        /// Sets variables: jpegWidth, jpegHeight, jpegComponents, jpegPrecision
        /// </summary>
        /// <param name="offset">Offset of SOF marker data (after FF C0/C2)</param>
        public void ParseJPEGSOFMarker(long offset)
        {
            if (offset < 0 || offset + 8 > _data.Length)
            {
                _variables["jpegWidth"] = 0;
                _variables["jpegHeight"] = 0;
                _variables["jpegComponents"] = 0;
                _variables["jpegPrecision"] = 0;
                return;
            }

            try
            {
                // SOF marker structure (big-endian):
                // 2 bytes: length
                // 1 byte: precision (bits per sample)
                // 2 bytes: height
                // 2 bytes: width
                // 1 byte: number of components

                // Skip length (2 bytes)
                byte precision = _data[offset + 2];
                ushort height = (ushort)((_data[offset + 3] << 8) | _data[offset + 4]);
                ushort width = (ushort)((_data[offset + 5] << 8) | _data[offset + 6]);
                byte components = _data[offset + 7];

                _variables["jpegWidth"] = (int)width;
                _variables["jpegHeight"] = (int)height;
                _variables["jpegComponents"] = (int)components;
                _variables["jpegPrecision"] = (int)precision;
            }
            catch
            {
                _variables["jpegWidth"] = 0;
                _variables["jpegHeight"] = 0;
                _variables["jpegComponents"] = 0;
                _variables["jpegPrecision"] = 0;
            }
        }

        #endregion

        #region Phase 3 - Utility & Interpretation Functions

        /// <summary>
        /// Computes a value from an arithmetic expression using existing variables.
        /// Expression supports: variable names, +, -, *, / operators, and literal numbers.
        /// Sets the result in the specified target variable.
        /// Example: ComputeFromVariables("imageWidth * imageHeight", "totalPixels")
        /// </summary>
        public void ComputeFromVariables(string expression, string targetVariable)
        {
            if (string.IsNullOrWhiteSpace(expression) || string.IsNullOrWhiteSpace(targetVariable))
                return;

            try
            {
                // Use the proper recursive descent parser with operator precedence
                long result = ExpressionEvaluator.EvaluateStatic(expression, _variables);
                _variables[targetVariable] = result;
            }
            catch
            {
                _variables[targetVariable] = 0L;
            }
        }

        /// <summary>
        /// Formats a byte count into human-readable size string.
        /// Sets variables: formattedSize
        /// </summary>
        public void FormatFileSize(long bytes)
        {
            string[] units = { "B", "KB", "MB", "GB", "TB" };
            double size = bytes;
            int unitIndex = 0;

            while (size >= 1024 && unitIndex < units.Length - 1)
            {
                size /= 1024;
                unitIndex++;
            }

            _variables["formattedSize"] = unitIndex == 0
                ? $"{(long)size} {units[unitIndex]}"
                : $"{size:F1} {units[unitIndex]}";
        }

        /// <summary>
        /// Formats seconds into human-readable duration string (MM:SS or HH:MM:SS).
        /// Sets variables: formattedDuration
        /// </summary>
        public void FormatDuration(double seconds)
        {
            if (seconds <= 0)
            {
                _variables["formattedDuration"] = "0:00";
                return;
            }

            var ts = TimeSpan.FromSeconds(seconds);
            _variables["formattedDuration"] = ts.TotalHours >= 1
                ? $"{(int)ts.TotalHours}:{ts.Minutes:D2}:{ts.Seconds:D2}"
                : $"{(int)ts.TotalMinutes}:{ts.Seconds:D2}";
        }

        /// <summary>
        /// Interprets a color type code into a human-readable name.
        /// Supports PNG, BMP, TIFF, and generic color type codes.
        /// Sets variables: colorTypeName
        /// </summary>
        public void InterpretColorType(long colorType, string formatHint)
        {
            string name;
            string hint = (formatHint ?? "").ToLowerInvariant();

            if (hint.Contains("png"))
            {
                name = colorType switch
                {
                    0 => "Grayscale",
                    2 => "RGB",
                    3 => "Indexed (Palette)",
                    4 => "Grayscale + Alpha",
                    6 => "RGBA",
                    _ => $"Unknown ({colorType})"
                };
            }
            else if (hint.Contains("bmp"))
            {
                name = colorType switch
                {
                    0 => "Uncompressed RGB",
                    1 => "RLE 8-bit",
                    2 => "RLE 4-bit",
                    3 => "Bitfields",
                    4 => "JPEG",
                    5 => "PNG",
                    _ => $"Unknown ({colorType})"
                };
            }
            else
            {
                // Generic interpretation
                name = colorType switch
                {
                    0 => "Grayscale",
                    1 => "Indexed",
                    2 => "RGB",
                    3 => "Indexed (Palette)",
                    4 => "Grayscale + Alpha",
                    6 => "RGBA",
                    _ => $"Type {colorType}"
                };
            }

            _variables["colorTypeName"] = name;
        }

        /// <summary>
        /// Interprets a compression method code into a human-readable name.
        /// Sets variables: compressionName
        /// </summary>
        public void InterpretCompressionMethod(long method, string formatHint)
        {
            string name;
            string hint = (formatHint ?? "").ToLowerInvariant();

            if (hint.Contains("zip") || hint.Contains("pk"))
            {
                name = method switch
                {
                    0 => "Stored (no compression)",
                    1 => "Shrunk",
                    6 => "Imploded",
                    8 => "Deflated",
                    9 => "Deflate64",
                    12 => "BZIP2",
                    14 => "LZMA",
                    93 => "Zstandard",
                    95 => "XZ",
                    98 => "PPMd",
                    _ => $"Method {method}"
                };
            }
            else if (hint.Contains("png"))
            {
                name = method == 0 ? "Deflate" : $"Unknown ({method})";
            }
            else if (hint.Contains("tiff"))
            {
                name = method switch
                {
                    1 => "Uncompressed",
                    2 => "CCITT Group 3",
                    3 => "CCITT T.4",
                    4 => "CCITT T.6",
                    5 => "LZW",
                    6 => "Old-style JPEG",
                    7 => "JPEG",
                    8 => "Deflate",
                    32773 => "PackBits",
                    _ => $"Method {method}"
                };
            }
            else
            {
                name = method switch
                {
                    0 => "None / Stored",
                    1 => "Shrunk",
                    8 => "Deflate",
                    _ => $"Method {method}"
                };
            }

            _variables["compressionName"] = name;
        }

        /// <summary>
        /// Validates that a variable value falls within an expected range.
        /// Sets variables: fieldValid, fieldValidationMsg
        /// </summary>
        public void ValidateFieldRange(string varName, long min, long max)
        {
            if (!_variables.TryGetValue(varName, out var val))
            {
                _variables["fieldValid"] = false;
                _variables["fieldValidationMsg"] = $"Variable '{varName}' not found";
                return;
            }

            try
            {
                long value = Convert.ToInt64(val);
                bool valid = value >= min && value <= max;
                _variables["fieldValid"] = valid;
                _variables["fieldValidationMsg"] = valid
                    ? $"{varName} = {value} (valid)"
                    : $"{varName} = {value} (expected {min}-{max})";
            }
            catch
            {
                _variables["fieldValid"] = false;
                _variables["fieldValidationMsg"] = $"Cannot validate '{varName}'";
            }
        }

        /// <summary>
        /// Validates that bytes at a given offset match an expected hex signature.
        /// Sets variables: signatureValid
        /// </summary>
        public void ValidateSignatureMatch(long offset, string expectedHex)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(expectedHex))
                {
                    _variables["signatureValid"] = false;
                    return;
                }

                // Parse hex string to bytes
                string hex = expectedHex.Replace(" ", "").Replace("0x", "");
                int byteCount = hex.Length / 2;

                if (offset < 0 || offset + byteCount > _data.Length)
                {
                    _variables["signatureValid"] = false;
                    return;
                }

                bool match = true;
                for (int i = 0; i < byteCount; i++)
                {
                    byte expected = Convert.ToByte(hex.Substring(i * 2, 2), 16);
                    if (_data[offset + i] != expected)
                    {
                        match = false;
                        break;
                    }
                }

                _variables["signatureValid"] = match;
            }
            catch
            {
                _variables["signatureValid"] = false;
            }
        }

        #endregion

        #region Phase 4 - Additional Specialized Parsers

        /// <summary>
        /// Parses GIF header to extract dimensions, version, and animation info.
        /// Sets variables: gifWidth, gifHeight, gifVersion, gifColorTableSize, gifBackgroundColor
        /// </summary>
        public void ParseGIFHeader()
        {
            try
            {
                if (_data.Length < 13)
                {
                    SetGIFDefaults();
                    return;
                }

                // Bytes 0-2: "GIF", Bytes 3-5: version ("87a" or "89a")
                string version = System.Text.Encoding.ASCII.GetString(_data, 3, 3);
                _variables["gifVersion"] = "GIF" + version;

                // Bytes 6-7: width (little-endian)
                _variables["gifWidth"] = (int)(_data[6] | (_data[7] << 8));
                // Bytes 8-9: height (little-endian)
                _variables["gifHeight"] = (int)(_data[8] | (_data[9] << 8));

                // Byte 10: packed field
                byte packed = _data[10];
                bool hasGlobalTable = (packed & 0x80) != 0;
                int colorTableSize = hasGlobalTable ? (1 << ((packed & 0x07) + 1)) : 0;
                _variables["gifColorTableSize"] = colorTableSize;
                _variables["gifBackgroundColor"] = (int)_data[11];
            }
            catch
            {
                SetGIFDefaults();
            }
        }

        private void SetGIFDefaults()
        {
            _variables["gifWidth"] = 0;
            _variables["gifHeight"] = 0;
            _variables["gifVersion"] = "Unknown";
            _variables["gifColorTableSize"] = 0;
            _variables["gifBackgroundColor"] = 0;
        }

        /// <summary>
        /// Parses BMP BITMAPINFOHEADER at the given offset.
        /// Sets variables: bmpWidth, bmpHeight, bmpBitCount, bmpCompression, bmpImageSize, bmpCompressionName
        /// </summary>
        public void ParseBMPInfoHeader(long offset)
        {
            try
            {
                if (offset < 0 || offset + 40 > _data.Length)
                {
                    SetBMPDefaults();
                    return;
                }

                long o = offset;
                // Skip header size (4 bytes)
                int width = _data[o + 4] | (_data[o + 5] << 8) | (_data[o + 6] << 16) | (_data[o + 7] << 24);
                int height = _data[o + 8] | (_data[o + 9] << 8) | (_data[o + 10] << 16) | (_data[o + 11] << 24);
                int bitCount = _data[o + 14] | (_data[o + 15] << 8);
                int compression = _data[o + 16] | (_data[o + 17] << 8) | (_data[o + 18] << 16) | (_data[o + 19] << 24);
                int imageSize = _data[o + 20] | (_data[o + 21] << 8) | (_data[o + 22] << 16) | (_data[o + 23] << 24);

                _variables["bmpWidth"] = Math.Abs(width);
                _variables["bmpHeight"] = Math.Abs(height);
                _variables["bmpBitCount"] = bitCount;
                _variables["bmpCompression"] = compression;
                _variables["bmpImageSize"] = imageSize;
                _variables["bmpCompressionName"] = compression switch
                {
                    0 => "RGB (uncompressed)",
                    1 => "RLE 8-bit",
                    2 => "RLE 4-bit",
                    3 => "Bitfields",
                    4 => "JPEG",
                    5 => "PNG",
                    _ => $"Unknown ({compression})"
                };
            }
            catch
            {
                SetBMPDefaults();
            }
        }

        private void SetBMPDefaults()
        {
            _variables["bmpWidth"] = 0;
            _variables["bmpHeight"] = 0;
            _variables["bmpBitCount"] = 0;
            _variables["bmpCompression"] = 0;
            _variables["bmpImageSize"] = 0;
            _variables["bmpCompressionName"] = "Unknown";
        }

        /// <summary>
        /// Parses FLAC STREAMINFO metadata block (mandatory first block after fLaC signature).
        /// Sets variables: flacSampleRate, flacChannels, flacBitsPerSample, flacTotalSamples, flacDuration
        /// </summary>
        public void ParseFLACStreamInfo()
        {
            try
            {
                // fLaC signature at 0-3, STREAMINFO block header at 4-7, STREAMINFO data at 8-41
                if (_data.Length < 42)
                {
                    SetFLACDefaults();
                    return;
                }

                long o = 8; // STREAMINFO data starts at byte 8

                // Bytes 10-13 (from STREAMINFO start): sample rate (20 bits), channels (3 bits), bps (5 bits)
                // Offset from file start = 8 + 10 = 18
                uint val = (uint)((_data[o + 10] << 24) | (_data[o + 11] << 16) | (_data[o + 12] << 8) | _data[o + 13]);
                int sampleRate = (int)(val >> 12);
                int channels = (int)((val >> 9) & 0x07) + 1;
                int bitsPerSample = (int)((val >> 4) & 0x1F) + 1;

                // Total samples: 36 bits spanning bytes 13-17 from STREAMINFO start
                long totalSamples = ((long)(_data[o + 13] & 0x0F) << 32) |
                                    ((long)_data[o + 14] << 24) |
                                    ((long)_data[o + 15] << 16) |
                                    ((long)_data[o + 16] << 8) |
                                    _data[o + 17];

                _variables["flacSampleRate"] = sampleRate;
                _variables["flacChannels"] = channels;
                _variables["flacBitsPerSample"] = bitsPerSample;
                _variables["flacTotalSamples"] = totalSamples;

                if (sampleRate > 0 && totalSamples > 0)
                {
                    double duration = (double)totalSamples / sampleRate;
                    _variables["flacDuration"] = duration;
                    FormatDuration(duration);
                }
                else
                {
                    _variables["flacDuration"] = 0.0;
                }
            }
            catch
            {
                SetFLACDefaults();
            }
        }

        private void SetFLACDefaults()
        {
            _variables["flacSampleRate"] = 0;
            _variables["flacChannels"] = 0;
            _variables["flacBitsPerSample"] = 0;
            _variables["flacTotalSamples"] = 0L;
            _variables["flacDuration"] = 0.0;
        }

        /// <summary>
        /// Parses OGG page header at the given offset.
        /// Sets variables: oggVersion, oggHeaderType, oggGranulePosition, oggSerialNumber, oggPageSequence
        /// </summary>
        public void ParseOGGPageHeader(long offset)
        {
            try
            {
                if (offset < 0 || offset + 27 > _data.Length)
                {
                    SetOGGDefaults();
                    return;
                }

                long o = offset;
                _variables["oggVersion"] = (int)_data[o + 4];
                _variables["oggHeaderType"] = (int)_data[o + 5];

                // Granule position: 8 bytes little-endian at offset+6
                long granule = 0;
                for (int i = 0; i < 8; i++)
                    granule |= (long)_data[o + 6 + i] << (i * 8);
                _variables["oggGranulePosition"] = granule;

                // Serial number: 4 bytes LE at offset+14
                _variables["oggSerialNumber"] = (int)(_data[o + 14] | (_data[o + 15] << 8) |
                    (_data[o + 16] << 16) | (_data[o + 17] << 24));

                // Page sequence: 4 bytes LE at offset+18
                _variables["oggPageSequence"] = (int)(_data[o + 18] | (_data[o + 19] << 8) |
                    (_data[o + 20] << 16) | (_data[o + 21] << 24));
            }
            catch
            {
                SetOGGDefaults();
            }
        }

        private void SetOGGDefaults()
        {
            _variables["oggVersion"] = 0;
            _variables["oggHeaderType"] = 0;
            _variables["oggGranulePosition"] = 0L;
            _variables["oggSerialNumber"] = 0;
            _variables["oggPageSequence"] = 0;
        }

        /// <summary>
        /// Parses PE Optional Header to extract key executable metadata.
        /// Expects offset pointing to the PE signature (typically at the offset stored at 0x3C).
        /// Sets variables: peSubsystem, peSubsystemName, peEntryPoint, peImageBase, peSectionCount, peIs64Bit, peMachine, peMachineName
        /// </summary>
        public void ParsePEOptionalHeader(long peOffset)
        {
            try
            {
                if (peOffset < 0 || peOffset + 24 > _data.Length)
                {
                    SetPEDefaults();
                    return;
                }

                long o = peOffset;

                // COFF header starts at PE+4
                int machine = _data[o + 4] | (_data[o + 5] << 8);
                int sectionCount = _data[o + 6] | (_data[o + 7] << 8);

                _variables["peMachine"] = machine;
                _variables["peMachineName"] = machine switch
                {
                    0x014C => "x86 (i386)",
                    0x0200 => "IA-64 (Itanium)",
                    0x8664 => "x64 (AMD64)",
                    0xAA64 => "ARM64",
                    0x01C0 => "ARM",
                    0x01C4 => "ARM Thumb-2",
                    _ => $"Unknown (0x{machine:X4})"
                };
                _variables["peSectionCount"] = sectionCount;

                // Optional header starts at PE+24
                long optBase = o + 24;
                if (optBase + 2 > _data.Length) { SetPEDefaults(); return; }

                int magic = _data[optBase] | (_data[optBase + 1] << 8);
                bool is64 = magic == 0x020B; // PE32+ = 64-bit
                _variables["peIs64Bit"] = is64;

                // Entry point at optBase+16 (4 bytes LE)
                if (optBase + 20 <= _data.Length)
                {
                    uint entryPoint = (uint)(_data[optBase + 16] | (_data[optBase + 17] << 8) |
                        (_data[optBase + 18] << 16) | (_data[optBase + 19] << 24));
                    _variables["peEntryPoint"] = (long)entryPoint;
                }

                // ImageBase: at optBase+28 (4 bytes PE32) or optBase+24 (8 bytes PE32+)
                if (is64 && optBase + 32 <= _data.Length)
                {
                    long imageBase = 0;
                    for (int i = 0; i < 8; i++)
                        imageBase |= (long)_data[optBase + 24 + i] << (i * 8);
                    _variables["peImageBase"] = imageBase;
                }
                else if (optBase + 32 <= _data.Length)
                {
                    uint imageBase = (uint)(_data[optBase + 28] | (_data[optBase + 29] << 8) |
                        (_data[optBase + 30] << 16) | (_data[optBase + 31] << 24));
                    _variables["peImageBase"] = (long)imageBase;
                }

                // Subsystem: at optBase+68 (PE32) or optBase+68 (PE32+) - 2 bytes
                long subsysOffset = optBase + 68;
                if (subsysOffset + 2 <= _data.Length)
                {
                    int subsystem = _data[subsysOffset] | (_data[subsysOffset + 1] << 8);
                    _variables["peSubsystem"] = subsystem;
                    _variables["peSubsystemName"] = subsystem switch
                    {
                        1 => "Native",
                        2 => "Windows GUI",
                        3 => "Windows Console",
                        7 => "POSIX Console",
                        9 => "Windows CE GUI",
                        10 => "EFI Application",
                        11 => "EFI Boot Service Driver",
                        12 => "EFI Runtime Driver",
                        14 => "Xbox",
                        _ => $"Unknown ({subsystem})"
                    };
                }
            }
            catch
            {
                SetPEDefaults();
            }
        }

        private void SetPEDefaults()
        {
            _variables["peSubsystem"] = 0;
            _variables["peSubsystemName"] = "Unknown";
            _variables["peEntryPoint"] = 0L;
            _variables["peImageBase"] = 0L;
            _variables["peSectionCount"] = 0;
            _variables["peIs64Bit"] = false;
            _variables["peMachine"] = 0;
            _variables["peMachineName"] = "Unknown";
        }

        /// <summary>
        /// Parses ELF header to extract architecture, ABI, type, and entry point.
        /// Sets variables: elfClass, elfArch, elfABI, elfType, elfTypeName, elfEntryPoint, elfIs64Bit
        /// </summary>
        public void ParseELFHeader()
        {
            try
            {
                if (_data.Length < 64)
                {
                    SetELFDefaults();
                    return;
                }

                // ELF identification
                int elfClass = _data[4]; // 1=32-bit, 2=64-bit
                bool is64 = elfClass == 2;
                _variables["elfClass"] = elfClass;
                _variables["elfIs64Bit"] = is64;

                int abi = _data[7];
                _variables["elfABI"] = abi switch
                {
                    0 => "System V",
                    1 => "HP-UX",
                    2 => "NetBSD",
                    3 => "Linux",
                    6 => "Solaris",
                    9 => "FreeBSD",
                    12 => "ARM EABI",
                    97 => "ARM",
                    _ => $"ABI {abi}"
                };

                // Type: 2 bytes at offset 16
                int type = _data[16] | (_data[17] << 8);
                _variables["elfType"] = type;
                _variables["elfTypeName"] = type switch
                {
                    1 => "Relocatable",
                    2 => "Executable",
                    3 => "Shared Object",
                    4 => "Core Dump",
                    _ => $"Type {type}"
                };

                // Machine: 2 bytes at offset 18
                int machine = _data[18] | (_data[19] << 8);
                _variables["elfArch"] = machine switch
                {
                    3 => "x86",
                    8 => "MIPS",
                    20 => "PowerPC",
                    40 => "ARM",
                    62 => "x86-64",
                    183 => "AArch64 (ARM64)",
                    243 => "RISC-V",
                    _ => $"Machine {machine}"
                };

                // Entry point
                if (is64 && _data.Length >= 32)
                {
                    long entry = 0;
                    for (int i = 0; i < 8; i++)
                        entry |= (long)_data[24 + i] << (i * 8);
                    _variables["elfEntryPoint"] = entry;
                }
                else if (_data.Length >= 28)
                {
                    uint entry = (uint)(_data[24] | (_data[25] << 8) | (_data[26] << 16) | (_data[27] << 24));
                    _variables["elfEntryPoint"] = (long)entry;
                }
            }
            catch
            {
                SetELFDefaults();
            }
        }

        private void SetELFDefaults()
        {
            _variables["elfClass"] = 0;
            _variables["elfArch"] = "Unknown";
            _variables["elfABI"] = "Unknown";
            _variables["elfType"] = 0;
            _variables["elfTypeName"] = "Unknown";
            _variables["elfEntryPoint"] = 0L;
            _variables["elfIs64Bit"] = false;
        }

        /// <summary>
        /// Parses TIFF IFD (Image File Directory) at the given offset.
        /// Sets variables: tiffWidth, tiffHeight, tiffBitsPerSample, tiffCompression, tiffCompressionName, tiffPhotometric
        /// </summary>
        public void ParseTIFFIFD(long ifdOffset)
        {
            try
            {
                if (ifdOffset < 0 || ifdOffset + 2 > _data.Length)
                {
                    SetTIFFDefaults();
                    return;
                }

                // Determine endianness from TIFF header
                bool bigEndian = _data[0] == 0x4D && _data[1] == 0x4D; // "MM"

                int entryCount = bigEndian
                    ? (_data[ifdOffset] << 8) | _data[ifdOffset + 1]
                    : _data[ifdOffset] | (_data[ifdOffset + 1] << 8);

                if (entryCount <= 0 || entryCount > 500) { SetTIFFDefaults(); return; }

                int width = 0, height = 0, bps = 0, compression = 1, photometric = 0;

                for (int i = 0; i < entryCount && ifdOffset + 2 + (i + 1) * 12 <= _data.Length; i++)
                {
                    long entryBase = ifdOffset + 2 + i * 12;
                    int tag = bigEndian
                        ? (_data[entryBase] << 8) | _data[entryBase + 1]
                        : _data[entryBase] | (_data[entryBase + 1] << 8);

                    // Value at offset+8 (for SHORT/LONG inline values)
                    int val = bigEndian
                        ? (_data[entryBase + 8] << 8) | _data[entryBase + 9]
                        : _data[entryBase + 8] | (_data[entryBase + 9] << 8);

                    int valLong = bigEndian
                        ? (_data[entryBase + 8] << 24) | (_data[entryBase + 9] << 16) |
                          (_data[entryBase + 10] << 8) | _data[entryBase + 11]
                        : _data[entryBase + 8] | (_data[entryBase + 9] << 8) |
                          (_data[entryBase + 10] << 16) | (_data[entryBase + 11] << 24);

                    switch (tag)
                    {
                        case 256: width = valLong; break;      // ImageWidth
                        case 257: height = valLong; break;     // ImageLength
                        case 258: bps = val; break;            // BitsPerSample
                        case 259: compression = val; break;    // Compression
                        case 262: photometric = val; break;    // PhotometricInterpretation
                    }
                }

                _variables["tiffWidth"] = width;
                _variables["tiffHeight"] = height;
                _variables["tiffBitsPerSample"] = bps;
                _variables["tiffCompression"] = compression;
                _variables["tiffCompressionName"] = compression switch
                {
                    1 => "Uncompressed",
                    2 => "CCITT Group 3",
                    5 => "LZW",
                    6 => "Old JPEG",
                    7 => "JPEG",
                    8 => "Deflate",
                    32773 => "PackBits",
                    _ => $"Method {compression}"
                };
                _variables["tiffPhotometric"] = photometric;
            }
            catch
            {
                SetTIFFDefaults();
            }
        }

        private void SetTIFFDefaults()
        {
            _variables["tiffWidth"] = 0;
            _variables["tiffHeight"] = 0;
            _variables["tiffBitsPerSample"] = 0;
            _variables["tiffCompression"] = 0;
            _variables["tiffCompressionName"] = "Unknown";
            _variables["tiffPhotometric"] = 0;
        }

        /// <summary>
        /// Parses PDF to find version and attempt page count extraction.
        /// Sets variables: pdfVersion, pdfPageCount, pdfEncrypted, pdfLinearized
        /// </summary>
        public void ParsePDFTrailer()
        {
            try
            {
                _variables["pdfVersion"] = "Unknown";
                _variables["pdfPageCount"] = 0;
                _variables["pdfEncrypted"] = false;
                _variables["pdfLinearized"] = false;

                // PDF version from first line: %PDF-1.7
                if (_data.Length >= 8 && _data[0] == 0x25 && _data[1] == 0x50)
                {
                    string header = System.Text.Encoding.ASCII.GetString(_data, 0, Math.Min(20, _data.Length));
                    int idx = header.IndexOf("%PDF-");
                    if (idx >= 0 && idx + 8 <= header.Length)
                        _variables["pdfVersion"] = header.Substring(idx + 5, 3);
                }

                // Search for /Linearized and /Encrypt in first 4KB
                int searchLen = Math.Min(_data.Length, 4096);
                string content = System.Text.Encoding.ASCII.GetString(_data, 0, searchLen);

                if (content.Contains("/Linearized"))
                    _variables["pdfLinearized"] = true;
                if (content.Contains("/Encrypt"))
                    _variables["pdfEncrypted"] = true;

                // Try to find page count: /Count N in Pages dictionary
                int countIdx = content.IndexOf("/Count ");
                if (countIdx >= 0)
                {
                    string after = content.Substring(countIdx + 7, Math.Min(10, content.Length - countIdx - 7));
                    string numStr = "";
                    foreach (char c in after)
                    {
                        if (char.IsDigit(c)) numStr += c;
                        else if (numStr.Length > 0) break;
                    }
                    if (int.TryParse(numStr, out int pageCount))
                        _variables["pdfPageCount"] = pageCount;
                }
            }
            catch
            {
                _variables["pdfVersion"] = "Error";
                _variables["pdfPageCount"] = 0;
                _variables["pdfEncrypted"] = false;
                _variables["pdfLinearized"] = false;
            }
        }

        /// <summary>
        /// Parses SQLite database header (first 100 bytes).
        /// Sets variables: sqlitePageSize, sqlitePageCount, sqliteVersion, sqliteEncoding, sqliteVersionName
        /// </summary>
        public void ParseSQLiteHeader()
        {
            try
            {
                if (_data.Length < 100)
                {
                    SetSQLiteDefaults();
                    return;
                }

                // Page size: 2 bytes big-endian at offset 16
                int pageSize = (_data[16] << 8) | _data[17];
                if (pageSize == 1) pageSize = 65536; // Special case
                _variables["sqlitePageSize"] = pageSize;

                // Database size in pages: 4 bytes big-endian at offset 28
                int pageCount = (_data[28] << 24) | (_data[29] << 16) | (_data[30] << 8) | _data[31];
                _variables["sqlitePageCount"] = pageCount;

                // Text encoding: 4 bytes big-endian at offset 56
                int encoding = (_data[56] << 24) | (_data[57] << 16) | (_data[58] << 8) | _data[59];
                _variables["sqliteEncoding"] = encoding switch
                {
                    1 => "UTF-8",
                    2 => "UTF-16LE",
                    3 => "UTF-16BE",
                    _ => $"Unknown ({encoding})"
                };

                // SQLite version at offset 96: 4 bytes big-endian
                int version = (_data[96] << 24) | (_data[97] << 16) | (_data[98] << 8) | _data[99];
                _variables["sqliteVersion"] = version;
                _variables["sqliteVersionName"] = $"{version / 1000000}.{(version / 1000) % 1000}.{version % 1000}";
            }
            catch
            {
                SetSQLiteDefaults();
            }
        }

        private void SetSQLiteDefaults()
        {
            _variables["sqlitePageSize"] = 0;
            _variables["sqlitePageCount"] = 0;
            _variables["sqliteVersion"] = 0;
            _variables["sqliteEncoding"] = "Unknown";
            _variables["sqliteVersionName"] = "Unknown";
        }

        /// <summary>
        /// Parses EBML header for MKV/WebM files.
        /// Sets variables: mkvDocType, mkvDocTypeVersion, mkvEBMLVersion
        /// </summary>
        public void ParseMKVHeader()
        {
            try
            {
                _variables["mkvDocType"] = "Unknown";
                _variables["mkvDocTypeVersion"] = 0;
                _variables["mkvEBMLVersion"] = 0;

                if (_data.Length < 40) return;

                // Search for DocType string ("matroska" or "webm") within first 100 bytes
                int searchLen = Math.Min(_data.Length, 100);
                string content = System.Text.Encoding.ASCII.GetString(_data, 0, searchLen);

                if (content.Contains("matroska"))
                    _variables["mkvDocType"] = "Matroska (MKV)";
                else if (content.Contains("webm"))
                    _variables["mkvDocType"] = "WebM";

                // EBML version is typically at a fixed early offset
                // EBML element ID: 0x1A45DFA3, version element ID: 0x4286
                // Simple heuristic: look for version byte after EBML header
                if (_data.Length > 5)
                    _variables["mkvEBMLVersion"] = (int)_data[5] <= 4 ? (int)_data[5] : 1;
            }
            catch
            {
                _variables["mkvDocType"] = "Unknown";
                _variables["mkvDocTypeVersion"] = 0;
                _variables["mkvEBMLVersion"] = 0;
            }
        }

        #endregion

        #region CRC32 Lookup Table

        /// <summary>
        /// CRC-32 lookup table for fast computation
        /// </summary>
        private static readonly uint[] Crc32Table = new uint[256]
        {
            0x00000000, 0x77073096, 0xEE0E612C, 0x990951BA, 0x076DC419, 0x706AF48F, 0xE963A535, 0x9E6495A3,
            0x0EDB8832, 0x79DCB8A4, 0xE0D5E91E, 0x97D2D988, 0x09B64C2B, 0x7EB17CBD, 0xE7B82D07, 0x90BF1D91,
            0x1DB71064, 0x6AB020F2, 0xF3B97148, 0x84BE41DE, 0x1ADAD47D, 0x6DDDE4EB, 0xF4D4B551, 0x83D385C7,
            0x136C9856, 0x646BA8C0, 0xFD62F97A, 0x8A65C9EC, 0x14015C4F, 0x63066CD9, 0xFA0F3D63, 0x8D080DF5,
            0x3B6E20C8, 0x4C69105E, 0xD56041E4, 0xA2677172, 0x3C03E4D1, 0x4B04D447, 0xD20D85FD, 0xA50AB56B,
            0x35B5A8FA, 0x42B2986C, 0xDBBBC9D6, 0xACBCF940, 0x32D86CE3, 0x45DF5C75, 0xDCD60DCF, 0xABD13D59,
            0x26D930AC, 0x51DE003A, 0xC8D75180, 0xBFD06116, 0x21B4F4B5, 0x56B3C423, 0xCFBA9599, 0xB8BDA50F,
            0x2802B89E, 0x5F058808, 0xC60CD9B2, 0xB10BE924, 0x2F6F7C87, 0x58684C11, 0xC1611DAB, 0xB6662D3D,
            0x76DC4190, 0x01DB7106, 0x98D220BC, 0xEFD5102A, 0x71B18589, 0x06B6B51F, 0x9FBFE4A5, 0xE8B8D433,
            0x7807C9A2, 0x0F00F934, 0x9609A88E, 0xE10E9818, 0x7F6A0DBB, 0x086D3D2D, 0x91646C97, 0xE6635C01,
            0x6B6B51F4, 0x1C6C6162, 0x856530D8, 0xF262004E, 0x6C0695ED, 0x1B01A57B, 0x8208F4C1, 0xF50FC457,
            0x65B0D9C6, 0x12B7E950, 0x8BBEB8EA, 0xFCB9887C, 0x62DD1DDF, 0x15DA2D49, 0x8CD37CF3, 0xFBD44C65,
            0x4DB26158, 0x3AB551CE, 0xA3BC0074, 0xD4BB30E2, 0x4ADFA541, 0x3DD895D7, 0xA4D1C46D, 0xD3D6F4FB,
            0x4369E96A, 0x346ED9FC, 0xAD678846, 0xDA60B8D0, 0x44042D73, 0x33031DE5, 0xAA0A4C5F, 0xDD0D7CC9,
            0x5005713C, 0x270241AA, 0xBE0B1010, 0xC90C2086, 0x5768B525, 0x206F85B3, 0xB966D409, 0xCE61E49F,
            0x5EDEF90E, 0x29D9C998, 0xB0D09822, 0xC7D7A8B4, 0x59B33D17, 0x2EB40D81, 0xB7BD5C3B, 0xC0BA6CAD,
            0xEDB88320, 0x9ABFB3B6, 0x03B6E20C, 0x74B1D29A, 0xEAD54739, 0x9DD277AF, 0x04DB2615, 0x73DC1683,
            0xE3630B12, 0x94643B84, 0x0D6D6A3E, 0x7A6A5AA8, 0xE40ECF0B, 0x9309FF9D, 0x0A00AE27, 0x7D079EB1,
            0xF00F9344, 0x8708A3D2, 0x1E01F268, 0x6906C2FE, 0xF762575D, 0x806567CB, 0x196C3671, 0x6E6B06E7,
            0xFED41B76, 0x89D32BE0, 0x10DA7A5A, 0x67DD4ACC, 0xF9B9DF6F, 0x8EBEEFF9, 0x17B7BE43, 0x60B08ED5,
            0xD6D6A3E8, 0xA1D1937E, 0x38D8C2C4, 0x4FDFF252, 0xD1BB67F1, 0xA6BC5767, 0x3FB506DD, 0x48B2364B,
            0xD80D2BDA, 0xAF0A1B4C, 0x36034AF6, 0x41047A60, 0xDF60EFC3, 0xA867DF55, 0x316E8EEF, 0x4669BE79,
            0xCB61B38C, 0xBC66831A, 0x256FD2A0, 0x5268E236, 0xCC0C7795, 0xBB0B4703, 0x220216B9, 0x5505262F,
            0xC5BA3BBE, 0xB2BD0B28, 0x2BB45A92, 0x5CB36A04, 0xC2D7FFA7, 0xB5D0CF31, 0x2CD99E8B, 0x5BDEAE1D,
            0x9B64C2B0, 0xEC63F226, 0x756AA39C, 0x026D930A, 0x9C0906A9, 0xEB0E363F, 0x72076785, 0x05005713,
            0x95BF4A82, 0xE2B87A14, 0x7BB12BAE, 0x0CB61B38, 0x92D28E9B, 0xE5D5BE0D, 0x7CDCEFB7, 0x0BDBDF21,
            0x86D3D2D4, 0xF1D4E242, 0x68DDB3F8, 0x1FDA836E, 0x81BE16CD, 0xF6B9265B, 0x6FB077E1, 0x18B74777,
            0x88085AE6, 0xFF0F6A70, 0x66063BCA, 0x11010B5C, 0x8F659EFF, 0xF862AE69, 0x616BFFD3, 0x166CCF45,
            0xA00AE278, 0xD70DD2EE, 0x4E048354, 0x3903B3C2, 0xA7672661, 0xD06016F7, 0x4969474D, 0x3E6E77DB,
            0xAED16A4A, 0xD9D65ADC, 0x40DF0B66, 0x37D83BF0, 0xA9BCAE53, 0xDEBB9EC5, 0x47B2CF7F, 0x30B5FFE9,
            0xBDBDF21C, 0xCABAC28A, 0x53B39330, 0x24B4A3A6, 0xBAD03605, 0xCDD70693, 0x54DE5729, 0x23D967BF,
            0xB3667A2E, 0xC4614AB8, 0x5D681B02, 0x2A6F2B94, 0xB40BBE37, 0xC30C8EA1, 0x5A05DF1B, 0x2D02EF8D
        };

        #endregion

        /// <summary>
        /// Executes a built-in function by name with optional arguments.
        /// Returns the result or null if function doesn't return a value.
        /// </summary>
        public object Execute(string functionName, params object[] args)
        {
            switch (functionName.ToLowerInvariant())
            {
                case "detectbom":
                    DetectBOM();
                    return null;

                case "countlines":
                    CountLines();
                    return null;

                case "detectlineending":
                    DetectLineEnding();
                    return null;

                case "min":
                    if (args.Length >= 2)
                    {
                        long a = Convert.ToInt64(args[0]);
                        long b = Convert.ToInt64(args[1]);
                        return Min(a, b);
                    }
                    return 0L;

                case "max":
                    if (args.Length >= 2)
                    {
                        long a = Convert.ToInt64(args[0]);
                        long b = Convert.ToInt64(args[1]);
                        return Max(a, b);
                    }
                    return 0L;

                case "substring":
                    if (args.Length >= 2)
                    {
                        long offset = Convert.ToInt64(args[0]);
                        long length = Convert.ToInt64(args[1]);
                        return Substring(offset, length);
                    }
                    return string.Empty;

                // Phase 1 - Core Functions
                case "calculatecrc32":
                    if (args.Length >= 2)
                    {
                        long offset = Convert.ToInt64(args[0]);
                        long length = Convert.ToInt64(args[1]);
                        CalculateCRC32(offset, length);
                    }
                    return null;

                case "validatecrc32":
                    if (args.Length >= 3)
                    {
                        long offset = Convert.ToInt64(args[0]);
                        long length = Convert.ToInt64(args[1]);
                        uint expectedCRC = Convert.ToUInt32(args[2]);
                        ValidateCRC32(offset, length, expectedCRC);
                    }
                    return null;

                case "multiply":
                    if (args.Length >= 2)
                    {
                        long a = Convert.ToInt64(args[0]);
                        long b = Convert.ToInt64(args[1]);
                        return Multiply(a, b);
                    }
                    return 0L;

                case "readuint32le":
                    if (args.Length >= 1)
                    {
                        long offset = Convert.ToInt64(args[0]);
                        return ReadUInt32LE(offset);
                    }
                    return 0u;

                case "readuint32be":
                    if (args.Length >= 1)
                    {
                        long offset = Convert.ToInt64(args[0]);
                        return ReadUInt32BE(offset);
                    }
                    return 0u;

                case "readuint8":
                    if (args.Length >= 1)
                    {
                        long offset = Convert.ToInt64(args[0]);
                        return ReadUInt8(offset);
                    }
                    return (byte)0;

                case "readuint16le":
                    if (args.Length >= 1)
                    {
                        long offset = Convert.ToInt64(args[0]);
                        return ReadUInt16LE(offset);
                    }
                    return (ushort)0;

                case "readuint16be":
                    if (args.Length >= 1)
                    {
                        long offset = Convert.ToInt64(args[0]);
                        return ReadUInt16BE(offset);
                    }
                    return (ushort)0;

                case "readuint24le":
                    if (args.Length >= 1)
                    {
                        long offset = Convert.ToInt64(args[0]);
                        return ReadUInt24LE(offset);
                    }
                    return 0u;

                case "readuint64le":
                    if (args.Length >= 1)
                    {
                        long offset = Convert.ToInt64(args[0]);
                        return ReadUInt64LE(offset);
                    }
                    return 0UL;

                case "readuint64be":
                    if (args.Length >= 1)
                    {
                        long offset = Convert.ToInt64(args[0]);
                        return ReadUInt64BE(offset);
                    }
                    return 0UL;

                // Specialized PNG functions
                case "readpngdimensions":
                    ReadPNGDimensions();
                    return null;

                case "validatepngihdr":
                    ValidatePNGIHDR();
                    return null;

                // Phase 1 - Remaining Core Functions
                case "calculatemd5":
                    if (args.Length >= 2)
                    {
                        long offset = Convert.ToInt64(args[0]);
                        long length = Convert.ToInt64(args[1]);
                        CalculateMD5(offset, length);
                    }
                    return null;

                case "calculatesha1":
                    if (args.Length >= 2)
                    {
                        long offset = Convert.ToInt64(args[0]);
                        long length = Convert.ToInt64(args[1]);
                        CalculateSHA1(offset, length);
                    }
                    return null;

                case "calculatesha256":
                    if (args.Length >= 2)
                    {
                        long offset = Convert.ToInt64(args[0]);
                        long length = Convert.ToInt64(args[1]);
                        CalculateSHA256(offset, length);
                    }
                    return null;

                case "divide":
                    if (args.Length >= 2)
                    {
                        long numerator = Convert.ToInt64(args[0]);
                        long denominator = Convert.ToInt64(args[1]);
                        return Divide(numerator, denominator);
                    }
                    return 0L;

                case "bitwiseand":
                    if (args.Length >= 2)
                    {
                        long a = Convert.ToInt64(args[0]);
                        long b = Convert.ToInt64(args[1]);
                        return BitwiseAND(a, b);
                    }
                    return 0L;

                case "bitwiseor":
                    if (args.Length >= 2)
                    {
                        long a = Convert.ToInt64(args[0]);
                        long b = Convert.ToInt64(args[1]);
                        return BitwiseOR(a, b);
                    }
                    return 0L;

                case "bitshiftright":
                    if (args.Length >= 2)
                    {
                        long value = Convert.ToInt64(args[0]);
                        int bits = Convert.ToInt32(args[1]);
                        return BitShiftRight(value, bits);
                    }
                    return 0L;

                case "extractasciistring":
                    if (args.Length >= 2)
                    {
                        long offset = Convert.ToInt64(args[0]);
                        long maxLength = Convert.ToInt64(args[1]);
                        return ExtractASCIIString(offset, maxLength);
                    }
                    return string.Empty;

                case "extractutf8string":
                    if (args.Length >= 2)
                    {
                        long offset = Convert.ToInt64(args[0]);
                        long maxLength = Convert.ToInt64(args[1]);
                        return ExtractUTF8String(offset, maxLength);
                    }
                    return string.Empty;

                case "findbytesequence":
                    if (args.Length >= 2 && args[0] is byte[] pattern)
                    {
                        long startOffset = Convert.ToInt64(args[1]);
                        return FindByteSequence(pattern, startOffset);
                    }
                    return -1L;

                // Phase 2 - Specialized Functions
                case "parsemp3header":
                    ParseMP3Header();
                    return null;

                case "parsemp3frameheader":
                    if (args.Length >= 1)
                    {
                        long offset = Convert.ToInt64(args[0]);
                        ParseMP3FrameHeader(offset);
                    }
                    return null;

                case "calculatemp3duration":
                    if (args.Length >= 2)
                    {
                        long totalFrames = Convert.ToInt64(args[0]);
                        long sampleRate = Convert.ToInt64(args[1]);
                        CalculateMP3Duration(totalFrames, sampleRate);
                    }
                    return null;

                case "parsewavformatchunk":
                    if (args.Length >= 1)
                    {
                        long offset = Convert.ToInt64(args[0]);
                        ParseWAVFormatChunk(offset);
                    }
                    return null;

                case "parseziparchive":
                    ParseZIPArchive();
                    return null;

                case "calculatecompressionratio":
                    if (args.Length >= 2)
                    {
                        long compressedSize = Convert.ToInt64(args[0]);
                        long uncompressedSize = Convert.ToInt64(args[1]);
                        CalculateCompressionRatio(compressedSize, uncompressedSize);
                    }
                    return null;

                case "parsejpegdimensions":
                    ParseJPEGDimensions();
                    return null;

                case "parsejpegsofmarker":
                    if (args.Length >= 1)
                    {
                        long offset = Convert.ToInt64(args[0]);
                        ParseJPEGSOFMarker(offset);
                    }
                    return null;

                // Phase 3 - Utility & Interpretation Functions
                case "computefromvariables":
                    if (args.Length >= 2)
                    {
                        string expression = Convert.ToString(args[0]);
                        string targetVar = Convert.ToString(args[1]);
                        ComputeFromVariables(expression, targetVar);
                    }
                    return null;

                case "formatfilesize":
                    if (args.Length >= 1)
                    {
                        long bytes = Convert.ToInt64(args[0]);
                        FormatFileSize(bytes);
                    }
                    return null;

                case "formatduration":
                    if (args.Length >= 1)
                    {
                        double seconds = Convert.ToDouble(args[0]);
                        FormatDuration(seconds);
                    }
                    return null;

                case "interpretcolortype":
                    if (args.Length >= 1)
                    {
                        long colorType = Convert.ToInt64(args[0]);
                        string hint = args.Length >= 2 ? Convert.ToString(args[1]) : "";
                        InterpretColorType(colorType, hint);
                    }
                    return null;

                case "interpretcompressionmethod":
                    if (args.Length >= 1)
                    {
                        long method = Convert.ToInt64(args[0]);
                        string hint = args.Length >= 2 ? Convert.ToString(args[1]) : "";
                        InterpretCompressionMethod(method, hint);
                    }
                    return null;

                case "validatefieldrange":
                    if (args.Length >= 3)
                    {
                        string varName = Convert.ToString(args[0]);
                        long min = Convert.ToInt64(args[1]);
                        long max = Convert.ToInt64(args[2]);
                        ValidateFieldRange(varName, min, max);
                    }
                    return null;

                case "validatesignaturematch":
                    if (args.Length >= 2)
                    {
                        long offset = Convert.ToInt64(args[0]);
                        string expectedHex = Convert.ToString(args[1]);
                        ValidateSignatureMatch(offset, expectedHex);
                    }
                    return null;

                // Phase 4 - Additional Specialized Parsers
                case "parsegifheader":
                    ParseGIFHeader();
                    return null;

                case "parsebmpinfoheader":
                    if (args.Length >= 1)
                    {
                        long offset = Convert.ToInt64(args[0]);
                        ParseBMPInfoHeader(offset);
                    }
                    return null;

                case "parseflacstreaminfo":
                    ParseFLACStreamInfo();
                    return null;

                case "parseoggpageheader":
                    if (args.Length >= 1)
                    {
                        long offset = Convert.ToInt64(args[0]);
                        ParseOGGPageHeader(offset);
                    }
                    return null;

                case "parsepeoptionalheader":
                    if (args.Length >= 1)
                    {
                        long peOffset = Convert.ToInt64(args[0]);
                        ParsePEOptionalHeader(peOffset);
                    }
                    return null;

                case "parseelfheader":
                    ParseELFHeader();
                    return null;

                case "parsetiffifd":
                    if (args.Length >= 1)
                    {
                        long ifdOffset = Convert.ToInt64(args[0]);
                        ParseTIFFIFD(ifdOffset);
                    }
                    return null;

                case "parsepdftrailer":
                    ParsePDFTrailer();
                    return null;

                case "parsesqliteheader":
                    ParseSQLiteHeader();
                    return null;

                case "parsemkvheader":
                    ParseMKVHeader();
                    return null;

                default:
                    return null;
            }
        }
    }
}
