//////////////////////////////////////////////
// Apache 2.0  - 2026
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using System;
using System.Text;

namespace WpfHexaEditor.Models.ExportImport
{
    /// <summary>
    /// Represents a Motorola S-Record
    /// Format: STLLAAAA[DD...]CC
    /// S = Type (S0-S9), T = Type digit, LL = Byte count, AAAA = Address, DD = Data, CC = Checksum
    /// </summary>
    public class SRecord
    {
        /// <summary>
        /// Record type (S0-S9)
        /// </summary>
        public SRecordType RecordType { get; set; }

        /// <summary>
        /// Byte count (address + data + checksum bytes)
        /// </summary>
        public byte ByteCount { get; set; }

        /// <summary>
        /// Address field (16, 24, or 32 bits depending on type)
        /// </summary>
        public uint Address { get; set; }

        /// <summary>
        /// Data bytes
        /// </summary>
        public byte[] Data { get; set; }

        /// <summary>
        /// Checksum byte
        /// </summary>
        public byte Checksum { get; set; }

        /// <summary>
        /// Constructor
        /// </summary>
        public SRecord()
        {
            Data = Array.Empty<byte>();
        }

        /// <summary>
        /// Constructor with parameters
        /// </summary>
        public SRecord(SRecordType recordType, uint address, byte[] data)
        {
            RecordType = recordType;
            Address = address;
            Data = data ?? Array.Empty<byte>();

            // Calculate byte count (address bytes + data bytes + checksum byte)
            int addressBytes = GetAddressLength(recordType);
            ByteCount = (byte)(addressBytes + Data.Length + 1);

            Checksum = CalculateChecksum();
        }

        /// <summary>
        /// Get address field length in bytes for record type
        /// </summary>
        public static int GetAddressLength(SRecordType recordType)
        {
            return recordType switch
            {
                SRecordType.S0_Header => 2,
                SRecordType.S1_Data16 => 2,
                SRecordType.S2_Data24 => 3,
                SRecordType.S3_Data32 => 4,
                SRecordType.S5_Count16 => 2,
                SRecordType.S6_Count24 => 3,
                SRecordType.S7_Start32 => 4,
                SRecordType.S8_Start24 => 3,
                SRecordType.S9_Start16 => 2,
                _ => 2
            };
        }

        /// <summary>
        /// Calculate checksum for this record
        /// Checksum = One's complement of sum of (count + address + data bytes)
        /// </summary>
        public byte CalculateChecksum()
        {
            int sum = ByteCount;

            // Add address bytes
            int addressLength = GetAddressLength(RecordType);
            for (int i = addressLength - 1; i >= 0; i--)
            {
                sum += (byte)((Address >> (i * 8)) & 0xFF);
            }

            // Add data bytes
            foreach (var b in Data)
                sum += b;

            return (byte)(~sum & 0xFF);
        }

        /// <summary>
        /// Validate checksum
        /// </summary>
        public bool ValidateChecksum()
        {
            return Checksum == CalculateChecksum();
        }

        /// <summary>
        /// Convert record to S-Record string format
        /// </summary>
        public string ToSRecordString()
        {
            var sb = new StringBuilder();

            sb.Append('S');
            sb.Append((int)RecordType);
            sb.AppendFormat("{0:X2}", ByteCount);

            // Write address
            int addressLength = GetAddressLength(RecordType);
            for (int i = addressLength - 1; i >= 0; i--)
            {
                sb.AppendFormat("{0:X2}", (byte)((Address >> (i * 8)) & 0xFF));
            }

            // Write data
            foreach (var b in Data)
                sb.AppendFormat("{0:X2}", b);

            // Write checksum
            sb.AppendFormat("{0:X2}", CalculateChecksum());

            return sb.ToString();
        }

        /// <summary>
        /// Parse S-Record from string
        /// </summary>
        public static SRecord Parse(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                throw new ArgumentException("Line cannot be empty");

            line = line.Trim().ToUpperInvariant();

            if (!line.StartsWith("S"))
                throw new FormatException("S-Record must start with 'S'");

            if (line.Length < 8) // Minimum: S0LLAACC
                throw new FormatException("S-Record too short");

            try
            {
                var record = new SRecord();

                // Parse record type
                if (!int.TryParse(line.Substring(1, 1), out int typeValue) || typeValue > 9)
                    throw new FormatException($"Invalid record type: S{line[1]}");

                record.RecordType = (SRecordType)typeValue;

                // Parse byte count
                record.ByteCount = Convert.ToByte(line.Substring(2, 2), 16);

                // Parse address
                int addressLength = GetAddressLength(record.RecordType);
                uint address = 0;
                for (int i = 0; i < addressLength; i++)
                {
                    address = (address << 8) | Convert.ToByte(line.Substring(4 + i * 2, 2), 16);
                }
                record.Address = address;

                // Parse data bytes
                int dataLength = record.ByteCount - addressLength - 1; // Subtract address and checksum
                if (dataLength < 0)
                    throw new FormatException("Invalid byte count");

                record.Data = new byte[dataLength];
                int dataStart = 4 + addressLength * 2;
                for (int i = 0; i < dataLength; i++)
                {
                    record.Data[i] = Convert.ToByte(line.Substring(dataStart + i * 2, 2), 16);
                }

                // Parse checksum
                int checksumPos = dataStart + dataLength * 2;
                record.Checksum = Convert.ToByte(line.Substring(checksumPos, 2), 16);

                // Validate checksum
                if (!record.ValidateChecksum())
                    throw new FormatException($"Invalid checksum: expected {record.CalculateChecksum():X2}, got {record.Checksum:X2}");

                return record;
            }
            catch (Exception ex) when (ex is not FormatException)
            {
                throw new FormatException($"Failed to parse S-Record: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Try parse S-Record from string
        /// </summary>
        public static bool TryParse(string line, out SRecord record)
        {
            try
            {
                record = Parse(line);
                return true;
            }
            catch
            {
                record = null;
                return false;
            }
        }

        public override string ToString()
        {
            return $"S-Record [S{(int)RecordType}] Addr:0x{Address:X} Data:{Data.Length} bytes";
        }
    }
}
