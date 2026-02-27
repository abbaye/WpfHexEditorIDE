//////////////////////////////////////////////
// Apache 2.0  - 2026
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using WpfHexEditor.BinaryAnalysis.Models.ExportImport;

namespace WpfHexEditor.BinaryAnalysis.Services
{
    /// <summary>
    /// Service for exporting and importing Intel HEX format files
    /// Supports reading and writing Intel HEX format with 16-byte data records
    /// </summary>
    public class IntelHexService
    {
        /// <summary>
        /// Maximum bytes per data record (default: 16)
        /// </summary>
        public int MaxBytesPerRecord { get; set; } = 16;

        /// <summary>
        /// Default start address (default: 0x0000)
        /// </summary>
        public uint DefaultStartAddress { get; set; } = 0x0000;

        /// <summary>
        /// Export binary data to Intel HEX format file
        /// </summary>
        /// <param name="data">Binary data to export</param>
        /// <param name="filePath">Output file path</param>
        /// <param name="baseAddress">Base address for data (default: 0)</param>
        public void ExportToFile(byte[] data, string filePath, uint baseAddress = 0)
        {
            if (data == null || data.Length == 0)
                throw new ArgumentException("Data cannot be null or empty");

            var hexLines = ExportToHex(data, baseAddress);
            File.WriteAllLines(filePath, hexLines, Encoding.ASCII);
        }

        /// <summary>
        /// Export binary data to Intel HEX format strings
        /// </summary>
        /// <param name="data">Binary data to export</param>
        /// <param name="baseAddress">Base address for data (default: 0)</param>
        /// <returns>List of Intel HEX record strings</returns>
        public List<string> ExportToHex(byte[] data, uint baseAddress = 0)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            var lines = new List<string>();
            uint currentAddress = baseAddress;
            int offset = 0;

            // Handle extended addressing if base address > 0xFFFF
            if (baseAddress > 0xFFFF)
            {
                // Write Extended Linear Address record
                ushort upperAddress = (ushort)((baseAddress >> 16) & 0xFFFF);
                var elaRecord = new IntelHexRecord(0, IntelHexRecordType.ExtendedLinearAddress,
                    new byte[] { (byte)(upperAddress >> 8), (byte)(upperAddress & 0xFF) });
                lines.Add(elaRecord.ToHexString());

                currentAddress = baseAddress & 0xFFFF;
            }

            // Write data records
            while (offset < data.Length)
            {
                int bytesToWrite = Math.Min(MaxBytesPerRecord, data.Length - offset);
                var recordData = new byte[bytesToWrite];
                Array.Copy(data, offset, recordData, 0, bytesToWrite);

                // Check if we need a new Extended Linear Address record
                if ((currentAddress + bytesToWrite) > 0x10000)
                {
                    // Write remaining bytes in current 64KB segment
                    int remainingInSegment = 0x10000 - (int)(currentAddress & 0xFFFF);
                    if (remainingInSegment > 0 && remainingInSegment < bytesToWrite)
                    {
                        var partialData = new byte[remainingInSegment];
                        Array.Copy(recordData, 0, partialData, 0, remainingInSegment);

                        var record = new IntelHexRecord((ushort)(currentAddress & 0xFFFF),
                            IntelHexRecordType.Data, partialData);
                        lines.Add(record.ToHexString());

                        offset += remainingInSegment;
                        currentAddress += (uint)remainingInSegment;
                        continue;
                    }

                    // New segment
                    ushort upperAddress = (ushort)((currentAddress >> 16) & 0xFFFF);
                    var elaRecord = new IntelHexRecord(0, IntelHexRecordType.ExtendedLinearAddress,
                        new byte[] { (byte)(upperAddress >> 8), (byte)(upperAddress & 0xFF) });
                    lines.Add(elaRecord.ToHexString());
                }

                var dataRecord = new IntelHexRecord((ushort)(currentAddress & 0xFFFF),
                    IntelHexRecordType.Data, recordData);
                lines.Add(dataRecord.ToHexString());

                offset += bytesToWrite;
                currentAddress += (uint)bytesToWrite;
            }

            // Write End of File record
            var eofRecord = new IntelHexRecord(0, IntelHexRecordType.EndOfFile, Array.Empty<byte>());
            lines.Add(eofRecord.ToHexString());

            return lines;
        }

        /// <summary>
        /// Import Intel HEX format file to binary data
        /// </summary>
        /// <param name="filePath">Input file path</param>
        /// <returns>Import result with binary data</returns>
        public IntelHexImportResult ImportFromFile(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("File not found", filePath);

            var lines = File.ReadAllLines(filePath, Encoding.ASCII);
            return ImportFromHex(lines);
        }

        /// <summary>
        /// Import Intel HEX format from strings
        /// </summary>
        /// <param name="hexLines">Intel HEX record strings</param>
        /// <returns>Import result with binary data</returns>
        public IntelHexImportResult ImportFromHex(string[] hexLines)
        {
            var result = new IntelHexImportResult();

            try
            {
                var dataSegments = new Dictionary<uint, byte[]>();
                uint currentSegmentBase = 0;
                uint minAddress = uint.MaxValue;
                uint maxAddress = 0;

                foreach (var line in hexLines)
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    // Parse record
                    if (!IntelHexRecord.TryParse(line, out var record))
                    {
                        result.Warnings.Add($"Failed to parse line: {line}");
                        continue;
                    }

                    switch (record.RecordType)
                    {
                        case IntelHexRecordType.Data:
                            // Calculate absolute address
                            uint absoluteAddress = currentSegmentBase + record.Address;

                            // Store data
                            if (!dataSegments.ContainsKey(absoluteAddress))
                            {
                                dataSegments[absoluteAddress] = new byte[record.ByteCount];
                            }
                            Array.Copy(record.Data, dataSegments[absoluteAddress], record.ByteCount);

                            // Track address range
                            minAddress = Math.Min(minAddress, absoluteAddress);
                            maxAddress = Math.Max(maxAddress, absoluteAddress + (uint)record.ByteCount - 1);
                            break;

                        case IntelHexRecordType.EndOfFile:
                            // End of file reached
                            break;

                        case IntelHexRecordType.ExtendedLinearAddress:
                            // Update segment base (upper 16 bits of address)
                            currentSegmentBase = ((uint)record.Data[0] << 24) | ((uint)record.Data[1] << 16);
                            break;

                        case IntelHexRecordType.ExtendedSegmentAddress:
                            // Update segment base (segment address * 16)
                            currentSegmentBase = (((uint)record.Data[0] << 8) | record.Data[1]) << 4;
                            break;

                        case IntelHexRecordType.StartLinearAddress:
                            // Start address (32-bit)
                            result.StartAddress = ((uint)record.Data[0] << 24) | ((uint)record.Data[1] << 16) |
                                                 ((uint)record.Data[2] << 8) | record.Data[3];
                            break;

                        case IntelHexRecordType.StartSegmentAddress:
                            // Start address (CS:IP format)
                            result.StartAddress = (((uint)record.Data[0] << 8) | record.Data[1]) << 4;
                            break;
                    }
                }

                if (dataSegments.Count == 0)
                {
                    result.Success = false;
                    result.ErrorMessage = "No data records found";
                    return result;
                }

                // Assemble continuous binary data
                uint dataLength = maxAddress - minAddress + 1;
                result.Data = new byte[dataLength];
                result.BaseAddress = minAddress;

                foreach (var segment in dataSegments)
                {
                    uint offsetInResult = segment.Key - minAddress;
                    Array.Copy(segment.Value, 0, result.Data, offsetInResult, segment.Value.Length);
                }

                result.Success = true;
                result.RecordCount = hexLines.Length;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = $"Import failed: {ex.Message}";
            }

            return result;
        }
    }

    /// <summary>
    /// Result of Intel HEX import operation
    /// </summary>
    public class IntelHexImportResult
    {
        /// <summary>
        /// Whether import succeeded
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Error message if import failed
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Imported binary data
        /// </summary>
        public byte[] Data { get; set; }

        /// <summary>
        /// Base address where data starts
        /// </summary>
        public uint BaseAddress { get; set; }

        /// <summary>
        /// Start address from Start Address records
        /// </summary>
        public uint? StartAddress { get; set; }

        /// <summary>
        /// Number of records processed
        /// </summary>
        public int RecordCount { get; set; }

        /// <summary>
        /// Warnings encountered during import
        /// </summary>
        public List<string> Warnings { get; set; } = new List<string>();

        public override string ToString()
        {
            if (Success)
                return $"✓ Imported {Data?.Length ?? 0} bytes from {RecordCount} records (base: 0x{BaseAddress:X})";
            else
                return $"✗ Failed: {ErrorMessage}";
        }
    }
}
