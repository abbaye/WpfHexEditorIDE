//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5, Claude Sonnet 4.6
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
    /// Service for exporting and importing Motorola S-Record format files
    /// Supports S19 (16-bit), S28 (24-bit), and S37 (32-bit) formats
    /// </summary>
    public class SRecordService
    {
        /// <summary>
        /// Maximum bytes per data record (default: 32)
        /// </summary>
        public int MaxBytesPerRecord { get; set; } = 32;

        /// <summary>
        /// Header text for S0 record (default: empty)
        /// </summary>
        public string HeaderText { get; set; } = "WPFHexEditor";

        /// <summary>
        /// Export binary data to S-Record format file
        /// </summary>
        /// <param name="data">Binary data to export</param>
        /// <param name="filePath">Output file path</param>
        /// <param name="baseAddress">Base address for data</param>
        /// <param name="use32BitAddress">If true, use S3/S7 records (32-bit); otherwise use S1/S9 (16-bit)</param>
        public void ExportToFile(byte[] data, string filePath, uint baseAddress = 0, bool use32BitAddress = false)
        {
            if (data == null || data.Length == 0)
                throw new ArgumentException("Data cannot be null or empty");

            var srecLines = ExportToSRecord(data, baseAddress, use32BitAddress);
            File.WriteAllLines(filePath, srecLines, Encoding.ASCII);
        }

        /// <summary>
        /// Export binary data to S-Record format strings
        /// </summary>
        /// <param name="data">Binary data to export</param>
        /// <param name="baseAddress">Base address for data</param>
        /// <param name="use32BitAddress">If true, use S3/S7 records (32-bit); otherwise use S1/S9 (16-bit)</param>
        /// <returns>List of S-Record strings</returns>
        public List<string> ExportToSRecord(byte[] data, uint baseAddress = 0, bool use32BitAddress = false)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            var lines = new List<string>();

            // Determine record types based on address range
            SRecordType dataRecordType;
            SRecordType termRecordType;

            if (use32BitAddress || baseAddress + data.Length > 0xFFFFFF)
            {
                dataRecordType = SRecordType.S3_Data32;
                termRecordType = SRecordType.S7_Start32;
            }
            else if (baseAddress + data.Length > 0xFFFF)
            {
                dataRecordType = SRecordType.S2_Data24;
                termRecordType = SRecordType.S8_Start24;
            }
            else
            {
                dataRecordType = SRecordType.S1_Data16;
                termRecordType = SRecordType.S9_Start16;
            }

            // Write S0 header record
            var headerBytes = Encoding.ASCII.GetBytes(HeaderText);
            var s0Record = new SRecord(SRecordType.S0_Header, 0, headerBytes);
            lines.Add(s0Record.ToSRecordString());

            // Write data records
            int recordCount = 0;
            uint currentAddress = baseAddress;
            int offset = 0;

            while (offset < data.Length)
            {
                int bytesToWrite = Math.Min(MaxBytesPerRecord, data.Length - offset);
                var recordData = new byte[bytesToWrite];
                Array.Copy(data, offset, recordData, 0, bytesToWrite);

                var record = new SRecord(dataRecordType, currentAddress, recordData);
                lines.Add(record.ToSRecordString());

                offset += bytesToWrite;
                currentAddress += (uint)bytesToWrite;
                recordCount++;
            }

            // Write S5/S6 count record (optional, for record count verification)
            if (recordCount <= 0xFFFF)
            {
                var s5Record = new SRecord(SRecordType.S5_Count16, (uint)recordCount, Array.Empty<byte>());
                lines.Add(s5Record.ToSRecordString());
            }
            else if (recordCount <= 0xFFFFFF)
            {
                var s6Record = new SRecord(SRecordType.S6_Count24, (uint)recordCount, Array.Empty<byte>());
                lines.Add(s6Record.ToSRecordString());
            }

            // Write termination record (start address = base address)
            var termRecord = new SRecord(termRecordType, baseAddress, Array.Empty<byte>());
            lines.Add(termRecord.ToSRecordString());

            return lines;
        }

        /// <summary>
        /// Import S-Record format file to binary data
        /// </summary>
        /// <param name="filePath">Input file path</param>
        /// <returns>Import result with binary data</returns>
        public SRecordImportResult ImportFromFile(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("File not found", filePath);

            var lines = File.ReadAllLines(filePath, Encoding.ASCII);
            return ImportFromSRecord(lines);
        }

        /// <summary>
        /// Import S-Record format from strings
        /// </summary>
        /// <param name="srecLines">S-Record strings</param>
        /// <returns>Import result with binary data</returns>
        public SRecordImportResult ImportFromSRecord(string[] srecLines)
        {
            var result = new SRecordImportResult();

            try
            {
                var dataSegments = new Dictionary<uint, byte[]>();
                uint minAddress = uint.MaxValue;
                uint maxAddress = 0;
                int dataRecordCount = 0;

                foreach (var line in srecLines)
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    // Parse record
                    if (!SRecord.TryParse(line, out var record))
                    {
                        result.Warnings.Add($"Failed to parse line: {line}");
                        continue;
                    }

                    switch (record.RecordType)
                    {
                        case SRecordType.S0_Header:
                            // Decode header text
                            if (record.Data.Length > 0)
                            {
                                result.HeaderText = Encoding.ASCII.GetString(record.Data);
                            }
                            break;

                        case SRecordType.S1_Data16:
                        case SRecordType.S2_Data24:
                        case SRecordType.S3_Data32:
                            // Store data
                            if (!dataSegments.ContainsKey(record.Address))
                            {
                                dataSegments[record.Address] = new byte[record.Data.Length];
                            }
                            Array.Copy(record.Data, dataSegments[record.Address], record.Data.Length);

                            // Track address range
                            minAddress = Math.Min(minAddress, record.Address);
                            maxAddress = Math.Max(maxAddress, record.Address + (uint)record.Data.Length - 1);
                            dataRecordCount++;
                            break;

                        case SRecordType.S5_Count16:
                        case SRecordType.S6_Count24:
                            // Verify record count
                            uint expectedCount = record.Address; // Count is stored in address field
                            if (expectedCount != dataRecordCount)
                            {
                                result.Warnings.Add($"Record count mismatch: expected {expectedCount}, got {dataRecordCount}");
                            }
                            break;

                        case SRecordType.S7_Start32:
                        case SRecordType.S8_Start24:
                        case SRecordType.S9_Start16:
                            // Start address
                            result.StartAddress = record.Address;
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
                result.RecordCount = srecLines.Length;
                result.DataRecordCount = dataRecordCount;
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
    /// Result of S-Record import operation
    /// </summary>
    public class SRecordImportResult
    {
        /// <summary>
        /// Whether import succeeded
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Error message if import failed
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Imported binary data
        /// </summary>
        public byte[]? Data { get; set; }

        /// <summary>
        /// Base address where data starts
        /// </summary>
        public uint BaseAddress { get; set; }

        /// <summary>
        /// Start address from termination records
        /// </summary>
        public uint? StartAddress { get; set; }

        /// <summary>
        /// Header text from S0 record
        /// </summary>
        public string? HeaderText { get; set; }

        /// <summary>
        /// Total number of records processed
        /// </summary>
        public int RecordCount { get; set; }

        /// <summary>
        /// Number of data records (S1/S2/S3)
        /// </summary>
        public int DataRecordCount { get; set; }

        /// <summary>
        /// Warnings encountered during import
        /// </summary>
        public List<string> Warnings { get; set; } = new List<string>();

        public override string ToString()
        {
            if (Success)
                return $"✓ Imported {Data?.Length ?? 0} bytes from {DataRecordCount} data records (base: 0x{BaseAddress:X})";
            else
                return $"✗ Failed: {ErrorMessage}";
        }
    }
}
