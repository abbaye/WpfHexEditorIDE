// ==========================================================
// Project: WpfHexEditor.Core
// File: IPSRecord.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-06
// Description:
//     Represents a single IPS patch record containing offset, size, RLE flag, and data.
//     Supports both normal data records and RLE (Run-Length Encoding) records where
//     a single byte value is repeated a given number of times.
//
// Architecture Notes:
//     Pure data model — no WPF dependencies, no logic.
//     Consumed by IPSPatcher for reading and creating IPS patch files.
//
// ==========================================================

using System;

namespace WpfHexEditor.Core.RomHacking
{
    /// <summary>
    /// Represents a single IPS patch record
    /// </summary>
    public class IPSRecord
    {
        /// <summary>
        /// Offset in the target file where patch should be applied (24-bit big-endian)
        /// </summary>
        public int Offset { get; set; }

        /// <summary>
        /// Size of the patch data (0 = RLE record)
        /// </summary>
        public int Size { get; set; }

        /// <summary>
        /// True if this is an RLE (Run-Length Encoding) record
        /// </summary>
        public bool IsRLE { get; set; }

        /// <summary>
        /// For RLE records: number of times to repeat the byte value
        /// </summary>
        public int RLECount { get; set; }

        /// <summary>
        /// For RLE records: the byte value to repeat
        /// For normal records: the patch data bytes
        /// </summary>
        public byte[] Data { get; set; }

        /// <summary>
        /// Creates a new IPSRecord
        /// </summary>
        public IPSRecord()
        {
            Data = Array.Empty<byte>();
        }

        /// <summary>
        /// Returns a string representation of this record
        /// </summary>
        public override string ToString()
        {
            if (IsRLE)
            {
                return $"IPS Record @ 0x{Offset:X6}: RLE {RLECount}x byte 0x{Data[0]:X2}";
            }
            return $"IPS Record @ 0x{Offset:X6}: {Size} bytes";
        }
    }
}
