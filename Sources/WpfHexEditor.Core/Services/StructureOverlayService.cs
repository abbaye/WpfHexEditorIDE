//////////////////////////////////////////////
// Apache 2.0  - 2026
// Structure Overlay Service - Converts format definitions to overlays
// Author : Claude Sonnet 4.5
// Contributors: Derek Tremblay (derektremblay666@gmail.com)
//////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using Newtonsoft.Json.Linq;
using WpfHexaEditor.Core;
using WpfHexaEditor.Core.FormatDetection;
using WpfHexaEditor.Models.StructureOverlay;

namespace WpfHexaEditor.Services
{
    /// <summary>
    /// Service for creating structure overlays from format definitions
    /// Integrates with the existing format detection system
    /// </summary>
    public class StructureOverlayService
    {
        private readonly FieldValueReader _fieldReader;
        private readonly Random _colorGenerator;

        // Predefined color palette for field highlighting
        private static readonly Color[] FieldColors = new[]
        {
            Color.FromArgb(128, 74, 144, 226),   // Blue
            Color.FromArgb(128, 126, 211, 33),   // Green
            Color.FromArgb(128, 245, 166, 35),   // Orange
            Color.FromArgb(128, 189, 16, 224),   // Purple
            Color.FromArgb(128, 80, 227, 194),   // Cyan
            Color.FromArgb(128, 255, 107, 107),  // Red
            Color.FromArgb(128, 255, 195, 0),    // Yellow
            Color.FromArgb(128, 144, 19, 254),   // Violet
        };

        public StructureOverlayService()
        {
            _fieldReader = new FieldValueReader();
            _colorGenerator = new Random();
        }

        #region Public Methods

        /// <summary>
        /// Create structure overlay from a format definition
        /// </summary>
        /// <param name="formatDefinition">JSON format definition</param>
        /// <param name="fileBytes">File bytes to read values from</param>
        /// <returns>Structure overlay or null if failed</returns>
        public OverlayStructure CreateOverlayFromFormat(JObject formatDefinition, byte[] fileBytes)
        {
            if (formatDefinition == null || fileBytes == null)
                return null;

            try
            {
                var overlay = new OverlayStructure
                {
                    Name = formatDefinition["formatName"]?.ToString() ?? "Unknown",
                    FormatType = formatDefinition["category"]?.ToString() ?? "Custom",
                    Description = formatDefinition["description"]?.ToString() ?? "",
                    StartOffset = 0,
                    TotalLength = 0
                };

                // Parse blocks and fields
                var blocks = formatDefinition["blocks"] as JArray;
                if (blocks == null || blocks.Count == 0)
                    return null;

                long currentOffset = 0;
                int colorIndex = 0;

                foreach (var block in blocks)
                {
                    var blockObj = block as JObject;
                    if (blockObj == null) continue;

                    var blockType = blockObj["type"]?.ToString();

                    // Only process "field" and "signature" blocks for now
                    if (blockType == "field")
                    {
                        var fields = blockObj["fields"] as JArray;
                        if (fields != null)
                        {
                            foreach (var field in fields)
                            {
                                var fieldObj = field as JObject;
                                if (fieldObj == null) continue;

                                var overlayField = ParseField(fieldObj, fileBytes, ref currentOffset, ref colorIndex);
                                if (overlayField != null)
                                {
                                    overlay.Fields.Add(overlayField);
                                }
                            }
                        }
                    }
                    else if (blockType == "signature")
                    {
                        // Treat signature as a special field
                        var signatureField = new OverlayField
                        {
                            Name = "Signature",
                            Type = "bytes",
                            Offset = currentOffset,
                            Length = 0,
                            Color = FieldColors[colorIndex++ % FieldColors.Length],
                            Description = "File signature/magic number"
                        };

                        var expected = blockObj["expected"]?.ToString();
                        if (!string.IsNullOrEmpty(expected))
                        {
                            // Parse hex signature
                            var signatureBytes = ParseHexSignature(expected);
                            signatureField.Length = signatureBytes.Length;
                            signatureField.Value = BitConverter.ToString(signatureBytes).Replace("-", " ");

                            overlay.Fields.Add(signatureField);
                            currentOffset += signatureField.Length;
                        }
                    }
                }

                // Calculate total length
                if (overlay.Fields.Count > 0)
                {
                    var lastField = overlay.Fields.OrderByDescending(f => f.Offset + f.Length).FirstOrDefault();
                    if (lastField != null)
                    {
                        overlay.TotalLength = (int)(lastField.Offset + lastField.Length - overlay.StartOffset);
                    }
                }

                return overlay;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Create custom overlay from user-defined fields
        /// </summary>
        public OverlayStructure CreateCustomOverlay(string name, List<(string name, string type, int length)> fields, long startOffset = 0)
        {
            var overlay = new OverlayStructure
            {
                Name = name,
                FormatType = "Custom",
                StartOffset = startOffset,
                IsVisible = true
            };

            long currentOffset = startOffset;
            int colorIndex = 0;

            foreach (var (fieldName, fieldType, fieldLength) in fields)
            {
                overlay.Fields.Add(new OverlayField
                {
                    Name = fieldName,
                    Type = fieldType,
                    Offset = currentOffset,
                    Length = fieldLength,
                    Color = FieldColors[colorIndex++ % FieldColors.Length],
                    Value = ""
                });

                currentOffset += fieldLength;
            }

            overlay.TotalLength = (int)(currentOffset - startOffset);

            return overlay;
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Parse a single field from format definition
        /// </summary>
        private OverlayField ParseField(JObject fieldDef, byte[] fileBytes, ref long currentOffset, ref int colorIndex)
        {
            var fieldName = fieldDef["name"]?.ToString();
            var fieldType = fieldDef["type"]?.ToString();
            var fieldLength = GetFieldLength(fieldDef, fieldType);

            if (string.IsNullOrEmpty(fieldName) || string.IsNullOrEmpty(fieldType) || fieldLength <= 0)
                return null;

            var field = new OverlayField
            {
                Name = fieldName,
                Type = fieldType,
                Offset = currentOffset,
                Length = fieldLength,
                Color = FieldColors[colorIndex++ % FieldColors.Length],
                Description = fieldDef["description"]?.ToString() ?? ""
            };

            // Read value from file
            if (currentOffset + fieldLength <= fileBytes.Length)
            {
                var valueBytes = new byte[fieldLength];
                Array.Copy(fileBytes, currentOffset, valueBytes, 0, fieldLength);

                field.Value = FormatFieldValue(valueBytes, fieldType);
            }
            else
            {
                field.Value = "(out of bounds)";
            }

            currentOffset += fieldLength;

            return field;
        }

        /// <summary>
        /// Get field length from definition
        /// </summary>
        private int GetFieldLength(JObject fieldDef, string fieldType)
        {
            // Check explicit length
            if (fieldDef.ContainsKey("length"))
            {
                var lengthToken = fieldDef["length"];
                if (lengthToken.Type == JTokenType.Integer)
                {
                    return lengthToken.Value<int>();
                }
            }

            // Infer from type
            switch (fieldType)
            {
                case "uint8":
                case "int8":
                    return 1;
                case "uint16":
                case "int16":
                    return 2;
                case "uint32":
                case "int32":
                case "float":
                    return 4;
                case "uint64":
                case "int64":
                case "double":
                    return 8;
                default:
                    return 0; // Unknown
            }
        }

        /// <summary>
        /// Format field value as string
        /// </summary>
        private string FormatFieldValue(byte[] bytes, string fieldType)
        {
            try
            {
                switch (fieldType)
                {
                    case "uint8":
                        return bytes[0].ToString();
                    case "int8":
                        return ((sbyte)bytes[0]).ToString();
                    case "uint16":
                        return BitConverter.ToUInt16(bytes, 0).ToString();
                    case "int16":
                        return BitConverter.ToInt16(bytes, 0).ToString();
                    case "uint32":
                        return BitConverter.ToUInt32(bytes, 0).ToString("N0");
                    case "int32":
                        return BitConverter.ToInt32(bytes, 0).ToString("N0");
                    case "uint64":
                        return BitConverter.ToUInt64(bytes, 0).ToString("N0");
                    case "int64":
                        return BitConverter.ToInt64(bytes, 0).ToString("N0");
                    case "float":
                        return BitConverter.ToSingle(bytes, 0).ToString("G");
                    case "double":
                        return BitConverter.ToDouble(bytes, 0).ToString("G");
                    case "string":
                    case "ascii":
                    case "utf8":
                        return System.Text.Encoding.UTF8.GetString(bytes).TrimEnd('\0');
                    default:
                        return BitConverter.ToString(bytes).Replace("-", " ");
                }
            }
            catch
            {
                return BitConverter.ToString(bytes).Replace("-", " ");
            }
        }

        /// <summary>
        /// Parse hex signature string to byte array
        /// </summary>
        private byte[] ParseHexSignature(string signature)
        {
            // Remove common prefixes and spaces
            signature = signature.Replace("0x", "").Replace(" ", "").Replace("-", "");

            var bytes = new List<byte>();
            for (int i = 0; i < signature.Length; i += 2)
            {
                if (i + 1 < signature.Length)
                {
                    var hexByte = signature.Substring(i, 2);
                    if (byte.TryParse(hexByte, System.Globalization.NumberStyles.HexNumber, null, out byte b))
                    {
                        bytes.Add(b);
                    }
                }
            }

            return bytes.ToArray();
        }

        #endregion
    }
}
