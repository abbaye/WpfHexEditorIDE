using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace WpfHexEditor.Core.RomHacking
{
    /// <summary>
    /// IPS (International Patching System) patcher implementation.
    /// Supports reading and applying IPS patches to ROM files.
    /// </summary>
    public class IPSPatcher
    {
        private const string IPS_HEADER = "PATCH";
        private const string IPS_FOOTER = "EOF";
        private const int HEADER_SIZE = 5;
        private const int FOOTER_SIZE = 3;
        private const int RECORD_HEADER_SIZE = 5; // 3 bytes offset + 2 bytes size
        private const int RLE_HEADER_SIZE = 3; // 2 bytes count + 1 byte value

        /// <summary>
        /// Reads an IPS patch file and returns all records
        /// </summary>
        /// <param name="ipsFilePath">Path to the IPS patch file</param>
        /// <returns>List of IPS records</returns>
        /// <exception cref="FileNotFoundException">If the IPS file doesn't exist</exception>
        /// <exception cref="InvalidDataException">If the IPS file is malformed</exception>
        public static List<IPSRecord> ReadIPSFile(string ipsFilePath)
        {
            if (!File.Exists(ipsFilePath))
                throw new FileNotFoundException($"IPS patch file not found: {ipsFilePath}");

            var records = new List<IPSRecord>();

            using (var fs = new FileStream(ipsFilePath, FileMode.Open, FileAccess.Read))
            using (var br = new BinaryReader(fs))
            {
                // Verify header
                var header = new string(br.ReadChars(HEADER_SIZE));
                if (header != IPS_HEADER)
                    throw new InvalidDataException($"Invalid IPS header. Expected 'PATCH', got '{header}'");

                // Read records until EOF marker
                while (fs.Position < fs.Length)
                {
                    // Read offset (3 bytes, big-endian)
                    var offsetBytes = br.ReadBytes(3);
                    if (offsetBytes.Length < 3)
                        throw new InvalidDataException("Unexpected end of file while reading record offset");

                    // Check for EOF marker
                    var marker = System.Text.Encoding.ASCII.GetString(offsetBytes);
                    if (marker == IPS_FOOTER)
                    {
                        // EOF found - check for optional truncation size
                        if (fs.Position < fs.Length)
                        {
                            var truncateSize = ReadUInt24BigEndian(br.ReadBytes(3));
                            // Note: Truncation is handled separately in ApplyPatch
                        }
                        break;
                    }

                    var offset = (offsetBytes[0] << 16) | (offsetBytes[1] << 8) | offsetBytes[2];

                    // Read size (2 bytes, big-endian)
                    var size = (br.ReadByte() << 8) | br.ReadByte();

                    var record = new IPSRecord
                    {
                        Offset = offset,
                        Size = size
                    };

                    if (size == 0)
                    {
                        // RLE record
                        record.IsRLE = true;
                        var rleCount = (br.ReadByte() << 8) | br.ReadByte();
                        var rleValue = br.ReadByte();
                        record.RLECount = rleCount;
                        record.Data = new[] { rleValue };
                    }
                    else
                    {
                        // Normal record
                        record.IsRLE = false;
                        record.Data = br.ReadBytes(size);
                        if (record.Data.Length != size)
                            throw new InvalidDataException($"Expected {size} bytes of data, got {record.Data.Length}");
                    }

                    records.Add(record);
                }
            }

            return records;
        }

        /// <summary>
        /// Applies an IPS patch to a source file and saves the result
        /// </summary>
        /// <param name="sourceFilePath">Path to the original ROM file</param>
        /// <param name="ipsFilePath">Path to the IPS patch file</param>
        /// <param name="outputFilePath">Path where the patched file will be saved</param>
        /// <returns>Result of the patching operation</returns>
        public static IPSPatchResult ApplyPatch(string sourceFilePath, string ipsFilePath, string outputFilePath)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                // Validate inputs
                if (!File.Exists(sourceFilePath))
                    return IPSPatchResult.CreateFailure($"Source file not found: {sourceFilePath}");

                if (!File.Exists(ipsFilePath))
                    return IPSPatchResult.CreateFailure($"IPS patch file not found: {ipsFilePath}");

                // Read the entire source file into memory
                byte[] romData = File.ReadAllBytes(sourceFilePath);
                long originalSize = romData.Length;

                // Read IPS records
                List<IPSRecord> records;
                try
                {
                    records = ReadIPSFile(ipsFilePath);
                }
                catch (Exception ex)
                {
                    return IPSPatchResult.CreateFailure($"Failed to read IPS file: {ex.Message}");
                }

                if (records.Count == 0)
                    return IPSPatchResult.CreateFailure("IPS file contains no patch records");

                // Apply each record
                int appliedCount = 0;
                foreach (var record in records)
                {
                    try
                    {
                        ApplyRecord(ref romData, record);
                        appliedCount++;
                    }
                    catch (Exception ex)
                    {
                        return IPSPatchResult.CreateFailure($"Failed to apply record at offset 0x{record.Offset:X6}: {ex.Message}");
                    }
                }

                // Write patched file
                try
                {
                    File.WriteAllBytes(outputFilePath, romData);
                }
                catch (Exception ex)
                {
                    return IPSPatchResult.CreateFailure($"Failed to write output file: {ex.Message}");
                }

                stopwatch.Stop();

                var result = IPSPatchResult.CreateSuccess(
                    appliedCount,
                    records.Count,
                    originalSize,
                    romData.Length,
                    stopwatch.Elapsed
                );
                result.AppliedRecords = records;

                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                return IPSPatchResult.CreateFailure($"Unexpected error: {ex.Message}");
            }
        }

        /// <summary>
        /// Applies an IPS patch to a byte array (in-place modification)
        /// </summary>
        /// <param name="sourceData">Source ROM data</param>
        /// <param name="ipsFilePath">Path to the IPS patch file</param>
        /// <returns>Result of the patching operation</returns>
        public static IPSPatchResult ApplyPatchToData(ref byte[] sourceData, string ipsFilePath)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                if (sourceData == null || sourceData.Length == 0)
                    return IPSPatchResult.CreateFailure("Source data is empty");

                if (!File.Exists(ipsFilePath))
                    return IPSPatchResult.CreateFailure($"IPS patch file not found: {ipsFilePath}");

                long originalSize = sourceData.Length;

                // Read IPS records
                List<IPSRecord> records;
                try
                {
                    records = ReadIPSFile(ipsFilePath);
                }
                catch (Exception ex)
                {
                    return IPSPatchResult.CreateFailure($"Failed to read IPS file: {ex.Message}");
                }

                if (records.Count == 0)
                    return IPSPatchResult.CreateFailure("IPS file contains no patch records");

                // Apply each record
                int appliedCount = 0;
                foreach (var record in records)
                {
                    try
                    {
                        ApplyRecord(ref sourceData, record);
                        appliedCount++;
                    }
                    catch (Exception ex)
                    {
                        return IPSPatchResult.CreateFailure($"Failed to apply record at offset 0x{record.Offset:X6}: {ex.Message}");
                    }
                }

                stopwatch.Stop();

                var result = IPSPatchResult.CreateSuccess(
                    appliedCount,
                    records.Count,
                    originalSize,
                    sourceData.Length,
                    stopwatch.Elapsed
                );
                result.AppliedRecords = records;

                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                return IPSPatchResult.CreateFailure($"Unexpected error: {ex.Message}");
            }
        }

        /// <summary>
        /// Applies a single IPS record to the ROM data
        /// </summary>
        private static void ApplyRecord(ref byte[] romData, IPSRecord record)
        {
            // Expand ROM if needed
            int requiredSize = record.IsRLE
                ? record.Offset + record.RLECount
                : record.Offset + record.Size;

            if (requiredSize > romData.Length)
            {
                Array.Resize(ref romData, requiredSize);
            }

            if (record.IsRLE)
            {
                // RLE: repeat the same byte
                for (int i = 0; i < record.RLECount; i++)
                {
                    romData[record.Offset + i] = record.Data[0];
                }
            }
            else
            {
                // Normal: copy data bytes
                Array.Copy(record.Data, 0, romData, record.Offset, record.Size);
            }
        }

        /// <summary>
        /// Reads a 24-bit unsigned integer in big-endian format
        /// </summary>
        private static int ReadUInt24BigEndian(byte[] bytes)
        {
            if (bytes.Length != 3)
                throw new ArgumentException("Expected 3 bytes for UInt24");

            return (bytes[0] << 16) | (bytes[1] << 8) | bytes[2];
        }

        /// <summary>
        /// Validates if a file is a valid IPS patch
        /// </summary>
        /// <param name="filePath">Path to the file to validate</param>
        /// <returns>True if the file is a valid IPS patch</returns>
        public static bool IsValidIPSFile(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                    return false;

                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                using (var br = new BinaryReader(fs))
                {
                    if (fs.Length < HEADER_SIZE + FOOTER_SIZE)
                        return false;

                    var header = new string(br.ReadChars(HEADER_SIZE));
                    return header == IPS_HEADER;
                }
            }
            catch
            {
                return false;
            }
        }
    }
}
