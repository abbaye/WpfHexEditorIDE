//////////////////////////////////////////////
// Apache 2.0  - 2026
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using System;
using System.Linq;
using System.Text;

namespace WpfHexEditor.BinaryAnalysis.Models.ExportImport
{
    /// <summary>
    /// Represents an Intel HEX record
    /// Format: :LLAAAATT[DD...]CC
    /// LL = Byte count, AAAA = Address, TT = Record type, DD = Data bytes, CC = Checksum
    /// </summary>
    public class IntelHexRecord
    {
        /// <summary>
        /// Number of data bytes in the record (0-255)
        /// </summary>
        public byte ByteCount { get; set; }

        /// <summary>
        /// Address field (16-bit)
        /// </summary>
        public ushort Address { get; set; }

        /// <summary>
        /// Record type
        /// </summary>
        public IntelHexRecordType RecordType { get; set; }

        /// <summary>
        /// Data bytes (length = ByteCount)
        /// </summary>
        public byte[] Data { get; set; }

        /// <summary>
        /// Checksum byte (calculated or parsed)
        /// </summary>
        public byte Checksum { get; set; }

        /// <summary>
        /// Constructor
        /// </summary>
        public IntelHexRecord()
        {
            Data = Array.Empty<byte>();
        }

        /// <summary>
        /// Constructor with parameters
        /// </summary>
        public IntelHexRecord(ushort address, IntelHexRecordType recordType, byte[] data)
        {
            Address = address;
            RecordType = recordType;
            Data = data ?? Array.Empty<byte>();
            ByteCount = (byte)Data.Length;
            Checksum = CalculateChecksum();
        }

        /// <summary>
        /// Calculate checksum for this record
        /// Checksum = Two's complement of sum of all bytes (count, address, type, data)
        /// </summary>
        public byte CalculateChecksum()
        {
            int sum = ByteCount;
            sum += (Address >> 8) & 0xFF;  // High byte of address
            sum += Address & 0xFF;          // Low byte of address
            sum += (byte)RecordType;

            foreach (var b in Data)
                sum += b;

            return (byte)(0x100 - (sum & 0xFF));
        }

        /// <summary>
        /// Validate checksum
        /// </summary>
        public bool ValidateChecksum()
        {
            return Checksum == CalculateChecksum();
        }

        /// <summary>
        /// Convert record to Intel HEX string format
        /// </summary>
        public string ToHexString()
        {
            var sb = new StringBuilder();

            sb.Append(':');
            sb.AppendFormat("{0:X2}", ByteCount);
            sb.AppendFormat("{0:X4}", Address);
            sb.AppendFormat("{0:X2}", (byte)RecordType);

            foreach (var b in Data)
                sb.AppendFormat("{0:X2}", b);

            sb.AppendFormat("{0:X2}", CalculateChecksum());

            return sb.ToString();
        }

        /// <summary>
        /// Parse Intel HEX record from string
        /// </summary>
        public static IntelHexRecord Parse(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                throw new ArgumentException("Line cannot be empty");

            line = line.Trim();

            if (!line.StartsWith(":"))
                throw new FormatException("Intel HEX record must start with ':'");

            if (line.Length < 11) // Minimum: :LLAAAATTCC
                throw new FormatException("Intel HEX record too short");

            try
            {
                var record = new IntelHexRecord();

                // Parse byte count
                record.ByteCount = Convert.ToByte(line.Substring(1, 2), 16);

                // Parse address
                record.Address = Convert.ToUInt16(line.Substring(3, 4), 16);

                // Parse record type
                record.RecordType = (IntelHexRecordType)Convert.ToByte(line.Substring(7, 2), 16);

                // Parse data bytes
                record.Data = new byte[record.ByteCount];
                for (int i = 0; i < record.ByteCount; i++)
                {
                    record.Data[i] = Convert.ToByte(line.Substring(9 + i * 2, 2), 16);
                }

                // Parse checksum
                int checksumPos = 9 + record.ByteCount * 2;
                record.Checksum = Convert.ToByte(line.Substring(checksumPos, 2), 16);

                // Validate checksum
                if (!record.ValidateChecksum())
                    throw new FormatException($"Invalid checksum: expected {record.CalculateChecksum():X2}, got {record.Checksum:X2}");

                return record;
            }
            catch (Exception ex) when (ex is not FormatException)
            {
                throw new FormatException($"Failed to parse Intel HEX record: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Try parse Intel HEX record from string
        /// </summary>
        public static bool TryParse(string line, out IntelHexRecord record)
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
            return $"IntelHEX [{RecordType}] Addr:0x{Address:X4} Data:{ByteCount} bytes";
        }
    }
}
