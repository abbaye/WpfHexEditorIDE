//////////////////////////////////////////////
// Apache 2.0  - 2026
// Data Inspector Service - Multi-format byte interpretation
// Author : Claude Sonnet 4.5
// Contributors: Derek Tremblay (derektremblay666@gmail.com)
//////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using WpfHexEditor.BinaryAnalysis.Models.DataInspector;

namespace WpfHexEditor.BinaryAnalysis.Services
{
    /// <summary>
    /// Service for interpreting byte arrays in multiple formats
    /// Supports integers, floats, dates, network addresses, GUIDs, colors, etc.
    /// </summary>
    public class DataInspectorService
    {
        #region Constants

        private static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        private static readonly DateTime FileTimeEpoch = new DateTime(1601, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        private static readonly DateTime DosEpoch = new DateTime(1980, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        #endregion

        #region Public Methods

        /// <summary>
        /// Interpret byte array in all supported formats
        /// </summary>
        public List<InspectorValue> InterpretBytes(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
                return new List<InspectorValue>();

            var results = new List<InspectorValue>();

            // Integer interpretations (1, 2, 4, 8 bytes)
            results.AddRange(InterpretIntegers(bytes));

            // Float interpretations (4, 8 bytes)
            results.AddRange(InterpretFloats(bytes));

            // Date/Time interpretations (4, 8 bytes)
            results.AddRange(InterpretDateTime(bytes));

            // Network interpretations (4, 6, 16 bytes)
            results.AddRange(InterpretNetwork(bytes));

            // GUID interpretation (16 bytes)
            results.AddRange(InterpretGuid(bytes));

            // Color interpretations (3, 4 bytes)
            results.AddRange(InterpretColors(bytes));

            // Binary/Octal/Hex (any length, but show first bytes)
            results.AddRange(InterpretBasicFormats(bytes));

            // Bit representation (1 byte)
            results.AddRange(InterpretBits(bytes));

            return results;
        }

        #endregion

        #region Integer Interpretations

        private List<InspectorValue> InterpretIntegers(byte[] bytes)
        {
            var results = new List<InspectorValue>();

            // Int8 / UInt8 (1 byte)
            if (bytes.Length >= 1)
            {
                results.Add(new InspectorValue
                {
                    Category = "Integer",
                    Format = "Int8 (signed)",
                    Value = ((sbyte)bytes[0]).ToString(),
                    HexValue = bytes[0].ToString("X2"),
                    IsValid = true
                });

                results.Add(new InspectorValue
                {
                    Category = "Integer",
                    Format = "UInt8 (unsigned)",
                    Value = bytes[0].ToString(),
                    HexValue = bytes[0].ToString("X2"),
                    IsValid = true
                });
            }

            // Int16 / UInt16 (2 bytes)
            if (bytes.Length >= 2)
            {
                // Little Endian
                var int16LE = BitConverter.ToInt16(bytes, 0);
                var uint16LE = BitConverter.ToUInt16(bytes, 0);

                results.Add(new InspectorValue
                {
                    Category = "Integer",
                    Format = "Int16 LE (signed)",
                    Value = int16LE.ToString(),
                    HexValue = BitConverter.ToString(bytes, 0, 2).Replace("-", " "),
                    IsValid = true
                });

                results.Add(new InspectorValue
                {
                    Category = "Integer",
                    Format = "UInt16 LE (unsigned)",
                    Value = uint16LE.ToString(),
                    HexValue = BitConverter.ToString(bytes, 0, 2).Replace("-", " "),
                    IsValid = true
                });

                // Big Endian
                var bytesReversed = new byte[2];
                Array.Copy(bytes, bytesReversed, 2);
                Array.Reverse(bytesReversed);
                var int16BE = BitConverter.ToInt16(bytesReversed, 0);
                var uint16BE = BitConverter.ToUInt16(bytesReversed, 0);

                results.Add(new InspectorValue
                {
                    Category = "Integer",
                    Format = "Int16 BE (signed)",
                    Value = int16BE.ToString(),
                    HexValue = BitConverter.ToString(bytesReversed, 0, 2).Replace("-", " "),
                    IsValid = true
                });

                results.Add(new InspectorValue
                {
                    Category = "Integer",
                    Format = "UInt16 BE (unsigned)",
                    Value = uint16BE.ToString(),
                    HexValue = BitConverter.ToString(bytesReversed, 0, 2).Replace("-", " "),
                    IsValid = true
                });
            }

            // Int32 / UInt32 (4 bytes)
            if (bytes.Length >= 4)
            {
                // Little Endian
                var int32LE = BitConverter.ToInt32(bytes, 0);
                var uint32LE = BitConverter.ToUInt32(bytes, 0);

                results.Add(new InspectorValue
                {
                    Category = "Integer",
                    Format = "Int32 LE (signed)",
                    Value = int32LE.ToString("N0"),
                    HexValue = BitConverter.ToString(bytes, 0, 4).Replace("-", " "),
                    IsValid = true
                });

                results.Add(new InspectorValue
                {
                    Category = "Integer",
                    Format = "UInt32 LE (unsigned)",
                    Value = uint32LE.ToString("N0"),
                    HexValue = BitConverter.ToString(bytes, 0, 4).Replace("-", " "),
                    IsValid = true
                });

                // Big Endian
                var bytesReversed = new byte[4];
                Array.Copy(bytes, bytesReversed, 4);
                Array.Reverse(bytesReversed);
                var int32BE = BitConverter.ToInt32(bytesReversed, 0);
                var uint32BE = BitConverter.ToUInt32(bytesReversed, 0);

                results.Add(new InspectorValue
                {
                    Category = "Integer",
                    Format = "Int32 BE (signed)",
                    Value = int32BE.ToString("N0"),
                    HexValue = BitConverter.ToString(bytesReversed, 0, 4).Replace("-", " "),
                    IsValid = true
                });

                results.Add(new InspectorValue
                {
                    Category = "Integer",
                    Format = "UInt32 BE (unsigned)",
                    Value = uint32BE.ToString("N0"),
                    HexValue = BitConverter.ToString(bytesReversed, 0, 4).Replace("-", " "),
                    IsValid = true
                });
            }

            // Int64 / UInt64 (8 bytes)
            if (bytes.Length >= 8)
            {
                // Little Endian
                var int64LE = BitConverter.ToInt64(bytes, 0);
                var uint64LE = BitConverter.ToUInt64(bytes, 0);

                results.Add(new InspectorValue
                {
                    Category = "Integer",
                    Format = "Int64 LE (signed)",
                    Value = int64LE.ToString("N0"),
                    HexValue = BitConverter.ToString(bytes, 0, 8).Replace("-", " "),
                    IsValid = true
                });

                results.Add(new InspectorValue
                {
                    Category = "Integer",
                    Format = "UInt64 LE (unsigned)",
                    Value = uint64LE.ToString("N0"),
                    HexValue = BitConverter.ToString(bytes, 0, 8).Replace("-", " "),
                    IsValid = true
                });

                // Big Endian
                var bytesReversed = new byte[8];
                Array.Copy(bytes, bytesReversed, 8);
                Array.Reverse(bytesReversed);
                var int64BE = BitConverter.ToInt64(bytesReversed, 0);
                var uint64BE = BitConverter.ToUInt64(bytesReversed, 0);

                results.Add(new InspectorValue
                {
                    Category = "Integer",
                    Format = "Int64 BE (signed)",
                    Value = int64BE.ToString("N0"),
                    HexValue = BitConverter.ToString(bytesReversed, 0, 8).Replace("-", " "),
                    IsValid = true
                });

                results.Add(new InspectorValue
                {
                    Category = "Integer",
                    Format = "UInt64 BE (unsigned)",
                    Value = uint64BE.ToString("N0"),
                    HexValue = BitConverter.ToString(bytesReversed, 0, 8).Replace("-", " "),
                    IsValid = true
                });
            }

            return results;
        }

        #endregion

        #region Float Interpretations

        private List<InspectorValue> InterpretFloats(byte[] bytes)
        {
            var results = new List<InspectorValue>();

            // Float (4 bytes)
            if (bytes.Length >= 4)
            {
                try
                {
                    var floatLE = BitConverter.ToSingle(bytes, 0);
                    if (!float.IsNaN(floatLE) && !float.IsInfinity(floatLE))
                    {
                        results.Add(new InspectorValue
                        {
                            Category = "Float",
                            Format = "Float32 LE",
                            Value = floatLE.ToString("G9"),
                            HexValue = BitConverter.ToString(bytes, 0, 4).Replace("-", " "),
                            IsValid = true
                        });
                    }

                    // Big Endian
                    var bytesReversed = new byte[4];
                    Array.Copy(bytes, bytesReversed, 4);
                    Array.Reverse(bytesReversed);
                    var floatBE = BitConverter.ToSingle(bytesReversed, 0);
                    if (!float.IsNaN(floatBE) && !float.IsInfinity(floatBE))
                    {
                        results.Add(new InspectorValue
                        {
                            Category = "Float",
                            Format = "Float32 BE",
                            Value = floatBE.ToString("G9"),
                            HexValue = BitConverter.ToString(bytesReversed, 0, 4).Replace("-", " "),
                            IsValid = true
                        });
                    }
                }
                catch { }
            }

            // Double (8 bytes)
            if (bytes.Length >= 8)
            {
                try
                {
                    var doubleLE = BitConverter.ToDouble(bytes, 0);
                    if (!double.IsNaN(doubleLE) && !double.IsInfinity(doubleLE))
                    {
                        results.Add(new InspectorValue
                        {
                            Category = "Float",
                            Format = "Float64 LE (double)",
                            Value = doubleLE.ToString("G17"),
                            HexValue = BitConverter.ToString(bytes, 0, 8).Replace("-", " "),
                            IsValid = true
                        });
                    }

                    // Big Endian
                    var bytesReversed = new byte[8];
                    Array.Copy(bytes, bytesReversed, 8);
                    Array.Reverse(bytesReversed);
                    var doubleBE = BitConverter.ToDouble(bytesReversed, 0);
                    if (!double.IsNaN(doubleBE) && !double.IsInfinity(doubleBE))
                    {
                        results.Add(new InspectorValue
                        {
                            Category = "Float",
                            Format = "Float64 BE (double)",
                            Value = doubleBE.ToString("G17"),
                            HexValue = BitConverter.ToString(bytesReversed, 0, 8).Replace("-", " "),
                            IsValid = true
                        });
                    }
                }
                catch { }
            }

            return results;
        }

        #endregion

        #region Date/Time Interpretations

        private List<InspectorValue> InterpretDateTime(byte[] bytes)
        {
            var results = new List<InspectorValue>();

            // Unix Timestamp (4 bytes - seconds since 1970)
            if (bytes.Length >= 4)
            {
                try
                {
                    var unixTimestamp = BitConverter.ToUInt32(bytes, 0);
                    var date = UnixEpoch.AddSeconds(unixTimestamp);
                    if (date.Year >= 1970 && date.Year <= 2100)
                    {
                        results.Add(new InspectorValue
                        {
                            Category = "Date/Time",
                            Format = "Unix Timestamp (32-bit)",
                            Value = date.ToString("yyyy-MM-dd HH:mm:ss UTC"),
                            HexValue = BitConverter.ToString(bytes, 0, 4).Replace("-", " "),
                            IsValid = true
                        });
                    }
                }
                catch { }
            }

            // Unix Timestamp (8 bytes - milliseconds since 1970)
            if (bytes.Length >= 8)
            {
                try
                {
                    var unixTimestampMs = BitConverter.ToInt64(bytes, 0);
                    if (unixTimestampMs > 0 && unixTimestampMs < 253402300799999) // Valid range
                    {
                        var date = UnixEpoch.AddMilliseconds(unixTimestampMs);
                        if (date.Year >= 1970 && date.Year <= 2100)
                        {
                            results.Add(new InspectorValue
                            {
                                Category = "Date/Time",
                                Format = "Unix Timestamp (64-bit ms)",
                                Value = date.ToString("yyyy-MM-dd HH:mm:ss.fff UTC"),
                                HexValue = BitConverter.ToString(bytes, 0, 8).Replace("-", " "),
                                IsValid = true
                            });
                        }
                    }
                }
                catch { }

                // Windows FILETIME (8 bytes - 100-nanosecond intervals since 1601)
                try
                {
                    var fileTime = BitConverter.ToInt64(bytes, 0);
                    if (fileTime > 0 && fileTime < 2650467743999999999) // Valid range
                    {
                        var date = FileTimeEpoch.AddTicks(fileTime);
                        if (date.Year >= 1601 && date.Year <= 2100)
                        {
                            results.Add(new InspectorValue
                            {
                                Category = "Date/Time",
                                Format = "Windows FILETIME",
                                Value = date.ToString("yyyy-MM-dd HH:mm:ss.fffffff UTC"),
                                HexValue = BitConverter.ToString(bytes, 0, 8).Replace("-", " "),
                                IsValid = true
                            });
                        }
                    }
                }
                catch { }

                // .NET DateTime Ticks (8 bytes)
                try
                {
                    var ticks = BitConverter.ToInt64(bytes, 0);
                    if (ticks >= DateTime.MinValue.Ticks && ticks <= DateTime.MaxValue.Ticks)
                    {
                        var date = new DateTime(ticks);
                        results.Add(new InspectorValue
                        {
                            Category = "Date/Time",
                            Format = ".NET DateTime Ticks",
                            Value = date.ToString("yyyy-MM-dd HH:mm:ss.fffffff"),
                            HexValue = BitConverter.ToString(bytes, 0, 8).Replace("-", " "),
                            IsValid = true
                        });
                    }
                }
                catch { }
            }

            return results;
        }

        #endregion

        #region Network Interpretations

        private List<InspectorValue> InterpretNetwork(byte[] bytes)
        {
            var results = new List<InspectorValue>();

            // IPv4 Address (4 bytes)
            if (bytes.Length >= 4)
            {
                try
                {
                    var ipv4 = new IPAddress(bytes.Take(4).ToArray());
                    results.Add(new InspectorValue
                    {
                        Category = "Network",
                        Format = "IPv4 Address",
                        Value = ipv4.ToString(),
                        HexValue = BitConverter.ToString(bytes, 0, 4).Replace("-", " "),
                        IsValid = true
                    });

                    // Port (first 2 bytes as port number)
                    var port = BitConverter.ToUInt16(bytes, 0);
                    results.Add(new InspectorValue
                    {
                        Category = "Network",
                        Format = "Port Number (LE)",
                        Value = port.ToString(),
                        HexValue = BitConverter.ToString(bytes, 0, 2).Replace("-", " "),
                        IsValid = true
                    });

                    var portBE = (ushort)((bytes[0] << 8) | bytes[1]);
                    results.Add(new InspectorValue
                    {
                        Category = "Network",
                        Format = "Port Number (BE)",
                        Value = portBE.ToString(),
                        HexValue = BitConverter.ToString(bytes, 0, 2).Replace("-", " "),
                        IsValid = true
                    });
                }
                catch { }
            }

            // MAC Address (6 bytes)
            if (bytes.Length >= 6)
            {
                try
                {
                    var mac = string.Join(":", bytes.Take(6).Select(b => b.ToString("X2")));
                    results.Add(new InspectorValue
                    {
                        Category = "Network",
                        Format = "MAC Address",
                        Value = mac,
                        HexValue = BitConverter.ToString(bytes, 0, 6).Replace("-", " "),
                        IsValid = true
                    });
                }
                catch { }
            }

            // IPv6 Address (16 bytes)
            if (bytes.Length >= 16)
            {
                try
                {
                    var ipv6 = new IPAddress(bytes.Take(16).ToArray());
                    results.Add(new InspectorValue
                    {
                        Category = "Network",
                        Format = "IPv6 Address",
                        Value = ipv6.ToString(),
                        HexValue = BitConverter.ToString(bytes, 0, 16).Replace("-", " "),
                        IsValid = true
                    });
                }
                catch { }
            }

            return results;
        }

        #endregion

        #region GUID Interpretation

        private List<InspectorValue> InterpretGuid(byte[] bytes)
        {
            var results = new List<InspectorValue>();

            if (bytes.Length >= 16)
            {
                try
                {
                    var guid = new Guid(bytes.Take(16).ToArray());
                    results.Add(new InspectorValue
                    {
                        Category = "GUID",
                        Format = "GUID/UUID",
                        Value = guid.ToString(),
                        HexValue = BitConverter.ToString(bytes, 0, 16).Replace("-", " "),
                        IsValid = true
                    });

                    results.Add(new InspectorValue
                    {
                        Category = "GUID",
                        Format = "GUID (braces)",
                        Value = guid.ToString("B"),
                        HexValue = BitConverter.ToString(bytes, 0, 16).Replace("-", " "),
                        IsValid = true
                    });
                }
                catch { }
            }

            return results;
        }

        #endregion

        #region Color Interpretations

        private List<InspectorValue> InterpretColors(byte[] bytes)
        {
            var results = new List<InspectorValue>();

            // RGB (3 bytes)
            if (bytes.Length >= 3)
            {
                results.Add(new InspectorValue
                {
                    Category = "Color",
                    Format = "RGB",
                    Value = $"R={bytes[0]} G={bytes[1]} B={bytes[2]}",
                    HexValue = $"#{bytes[0]:X2}{bytes[1]:X2}{bytes[2]:X2}",
                    IsValid = true
                });

                // BGR (reversed)
                results.Add(new InspectorValue
                {
                    Category = "Color",
                    Format = "BGR",
                    Value = $"B={bytes[0]} G={bytes[1]} R={bytes[2]}",
                    HexValue = $"#{bytes[2]:X2}{bytes[1]:X2}{bytes[0]:X2}",
                    IsValid = true
                });
            }

            // RGBA (4 bytes)
            if (bytes.Length >= 4)
            {
                results.Add(new InspectorValue
                {
                    Category = "Color",
                    Format = "RGBA",
                    Value = $"R={bytes[0]} G={bytes[1]} B={bytes[2]} A={bytes[3]}",
                    HexValue = $"#{bytes[0]:X2}{bytes[1]:X2}{bytes[2]:X2}{bytes[3]:X2}",
                    IsValid = true
                });

                // ARGB
                results.Add(new InspectorValue
                {
                    Category = "Color",
                    Format = "ARGB",
                    Value = $"A={bytes[0]} R={bytes[1]} G={bytes[2]} B={bytes[3]}",
                    HexValue = $"#{bytes[0]:X2}{bytes[1]:X2}{bytes[2]:X2}{bytes[3]:X2}",
                    IsValid = true
                });
            }

            return results;
        }

        #endregion

        #region Basic Format Interpretations

        private List<InspectorValue> InterpretBasicFormats(byte[] bytes)
        {
            var results = new List<InspectorValue>();
            var maxBytes = Math.Min(bytes.Length, 8); // Show up to 8 bytes

            // Hexadecimal
            var hexValue = BitConverter.ToString(bytes, 0, maxBytes).Replace("-", " ");
            results.Add(new InspectorValue
            {
                Category = "Basic",
                Format = "Hexadecimal",
                Value = hexValue,
                HexValue = hexValue,
                IsValid = true
            });

            // Binary
            var binaryValue = string.Join(" ", bytes.Take(maxBytes).Select(b => Convert.ToString(b, 2).PadLeft(8, '0')));
            results.Add(new InspectorValue
            {
                Category = "Basic",
                Format = "Binary",
                Value = binaryValue,
                HexValue = hexValue,
                IsValid = true
            });

            // Octal
            var octalValue = string.Join(" ", bytes.Take(maxBytes).Select(b => Convert.ToString(b, 8).PadLeft(3, '0')));
            results.Add(new InspectorValue
            {
                Category = "Basic",
                Format = "Octal",
                Value = octalValue,
                HexValue = hexValue,
                IsValid = true
            });

            // ASCII (printable characters)
            var ascii = new StringBuilder();
            foreach (var b in bytes.Take(maxBytes))
            {
                ascii.Append(b >= 32 && b < 127 ? (char)b : '.');
            }
            results.Add(new InspectorValue
            {
                Category = "Basic",
                Format = "ASCII",
                Value = ascii.ToString(),
                HexValue = hexValue,
                IsValid = true
            });

            return results;
        }

        #endregion

        #region Bit Interpretation

        private List<InspectorValue> InterpretBits(byte[] bytes)
        {
            var results = new List<InspectorValue>();

            if (bytes.Length >= 1)
            {
                var byteValue = bytes[0];
                var bits = new StringBuilder();
                for (int i = 7; i >= 0; i--)
                {
                    var bit = (byteValue >> i) & 1;
                    bits.Append($"Bit{i}={bit} ");
                }

                results.Add(new InspectorValue
                {
                    Category = "Bits",
                    Format = "Bit Pattern (MSB first)",
                    Value = bits.ToString().Trim(),
                    HexValue = byteValue.ToString("X2"),
                    IsValid = true
                });
            }

            return results;
        }

        #endregion
    }
}
