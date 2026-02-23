//////////////////////////////////////////////
// Apache 2.0  - 2026
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using System;
using System.Security.Cryptography;

namespace WpfHexaEditor.Core.FormatDetection
{
    /// <summary>
    /// Validates checksums and CRCs for file integrity verification
    /// Supports: CRC32, MD5, SHA1, SHA256, checksum8, checksum16, checksum32
    /// </summary>
    public class ChecksumValidator
    {
        /// <summary>
        /// Calculate and validate a checksum
        /// </summary>
        /// <param name="data">Data to checksum</param>
        /// <param name="algorithm">Algorithm: crc32, md5, sha1, sha256, sum8, sum16, sum32</param>
        /// <param name="expectedValue">Expected checksum value (hex string)</param>
        /// <returns>True if checksum matches</returns>
        public bool Validate(byte[] data, string algorithm, string expectedValue)
        {
            if (data == null || string.IsNullOrWhiteSpace(algorithm) || string.IsNullOrWhiteSpace(expectedValue))
                return false;

            try
            {
                var calculated = Calculate(data, algorithm);
                return string.Equals(calculated, expectedValue, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Calculate a checksum
        /// </summary>
        public string Calculate(byte[] data, string algorithm)
        {
            if (data == null || string.IsNullOrWhiteSpace(algorithm))
                return null;

            algorithm = algorithm.ToLowerInvariant();

            return algorithm switch
            {
                "crc32" => CalculateCRC32(data),
                "md5" => CalculateMD5(data),
                "sha1" => CalculateSHA1(data),
                "sha256" => CalculateSHA256(data),
                "sum8" or "checksum8" => CalculateSum8(data),
                "sum16" or "checksum16" => CalculateSum16(data),
                "sum32" or "checksum32" => CalculateSum32(data),
                _ => null
            };
        }

        #region Simple Checksums

        private string CalculateSum8(byte[] data)
        {
            byte sum = 0;
            foreach (var b in data)
                sum += b;
            return sum.ToString("X2");
        }

        private string CalculateSum16(byte[] data)
        {
            ushort sum = 0;
            foreach (var b in data)
                sum += b;
            return sum.ToString("X4");
        }

        private string CalculateSum32(byte[] data)
        {
            uint sum = 0;
            foreach (var b in data)
                sum += b;
            return sum.ToString("X8");
        }

        #endregion

        #region CRC32

        private static readonly uint[] Crc32Table = InitializeCRC32Table();

        private static uint[] InitializeCRC32Table()
        {
            const uint polynomial = 0xEDB88320;
            var table = new uint[256];

            for (uint i = 0; i < 256; i++)
            {
                uint crc = i;
                for (int j = 8; j > 0; j--)
                {
                    if ((crc & 1) == 1)
                        crc = (crc >> 1) ^ polynomial;
                    else
                        crc >>= 1;
                }
                table[i] = crc;
            }

            return table;
        }

        private string CalculateCRC32(byte[] data)
        {
            uint crc = 0xFFFFFFFF;

            foreach (var b in data)
            {
                byte index = (byte)((crc & 0xFF) ^ b);
                crc = (crc >> 8) ^ Crc32Table[index];
            }

            crc = ~crc;
            return crc.ToString("X8");
        }

        #endregion

        #region Cryptographic Hashes

        private string CalculateMD5(byte[] data)
        {
            using (var md5 = MD5.Create())
            {
                var hash = md5.ComputeHash(data);
                return BitConverter.ToString(hash).Replace("-", "");
            }
        }

        private string CalculateSHA1(byte[] data)
        {
            using (var sha1 = SHA1.Create())
            {
                var hash = sha1.ComputeHash(data);
                return BitConverter.ToString(hash).Replace("-", "");
            }
        }

        private string CalculateSHA256(byte[] data)
        {
            using (var sha256 = SHA256.Create())
            {
                var hash = sha256.ComputeHash(data);
                return BitConverter.ToString(hash).Replace("-", "");
            }
        }

        #endregion
    }
}
