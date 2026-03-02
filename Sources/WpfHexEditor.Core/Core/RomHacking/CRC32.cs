//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.Core.RomHacking
{
    /// <summary>
    /// CRC-32 (ISO 3309 / ITU-T V.42) implementation using the standard polynomial 0xEDB88320.
    /// Net48-compatible — does not depend on System.IO.Hashing.
    /// </summary>
    public static class CRC32
    {
        private static readonly uint[] _table = BuildTable();

        private static uint[] BuildTable()
        {
            var table = new uint[256];
            for (uint i = 0; i < 256; i++)
            {
                uint crc = i;
                for (int j = 0; j < 8; j++)
                    crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
                table[i] = crc;
            }
            return table;
        }

        public static uint Compute(byte[] data)
            => Compute(data, 0, data.Length);

        public static uint Compute(byte[] data, int offset, int count)
        {
            uint crc = 0xFFFFFFFF;
            int end = offset + count;
            for (int i = offset; i < end; i++)
                crc = (crc >> 8) ^ _table[(crc ^ data[i]) & 0xFF];
            return crc ^ 0xFFFFFFFF;
        }
    }
}
