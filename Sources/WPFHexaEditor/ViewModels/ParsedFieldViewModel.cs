//////////////////////////////////////////////
// Apache 2.0  - 2026
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using System.ComponentModel;
using System.Runtime.CompilerServices;
using WpfHexaEditor.Core.FormatDetection;

namespace WpfHexaEditor.ViewModels
{
    /// <summary>
    /// ViewModel representing a single parsed field from a format definition
    /// Displays field name, offset, length, value, and formatting information
    /// </summary>
    public class ParsedFieldViewModel : INotifyPropertyChanged
    {
        private string _name;
        private long _offset;
        private int _length;
        private object _rawValue;
        private string _formattedValue;
        private string _valueType;
        private string _description;
        private string _color;
        private bool _isSelected;
        private bool _isValid;
        private string _validationMessage;
        private int _indentLevel;
        private string _fieldIcon;
        private bool _isEditable;
        private string _editableValue;

        /// <summary>
        /// Name of the field (e.g., "Image Width", "File Signature")
        /// </summary>
        public string Name
        {
            get => _name;
            set
            {
                _name = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Absolute offset where this field starts in the file
        /// </summary>
        public long Offset
        {
            get => _offset;
            set
            {
                _offset = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(OffsetHex));
                OnPropertyChanged(nameof(OffsetDisplay));
            }
        }

        /// <summary>
        /// Length of the field in bytes
        /// </summary>
        public int Length
        {
            get => _length;
            set
            {
                _length = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(EndOffset));
                OnPropertyChanged(nameof(RangeDisplay));
            }
        }

        /// <summary>
        /// Raw value read from the file (before formatting)
        /// </summary>
        public object RawValue
        {
            get => _rawValue;
            set
            {
                _rawValue = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Formatted value for display (hex, decimal, string, etc.)
        /// </summary>
        public string FormattedValue
        {
            get => _formattedValue;
            set
            {
                _formattedValue = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Type of value (uint8, uint16, uint32, int16, int32, string, etc.)
        /// </summary>
        public string ValueType
        {
            get => _valueType;
            set
            {
                _valueType = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Description of this field
        /// </summary>
        public string Description
        {
            get => _description;
            set
            {
                _description = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Color for highlighting this field (hex format: #RRGGBB)
        /// </summary>
        public string Color
        {
            get => _color;
            set
            {
                _color = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Whether this field is currently selected
        /// </summary>
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Whether this field's value is valid
        /// </summary>
        public bool IsValid
        {
            get => _isValid;
            set
            {
                _isValid = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Validation message if field is invalid
        /// </summary>
        public string ValidationMessage
        {
            get => _validationMessage;
            set
            {
                _validationMessage = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Indentation level for visual hierarchy (0 = root, 1+ = nested)
        /// </summary>
        public int IndentLevel
        {
            get => _indentLevel;
            set
            {
                _indentLevel = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IndentMargin));
            }
        }

        /// <summary>
        /// Icon/emoji for field type visualization
        /// </summary>
        public string FieldIcon
        {
            get => _fieldIcon;
            set
            {
                _fieldIcon = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Whether this field can be edited by the user
        /// Signature blocks are typically not editable
        /// </summary>
        public bool IsEditable
        {
            get => _isEditable;
            set
            {
                _isEditable = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Editable string representation of the value
        /// Used for two-way binding in edit mode
        /// </summary>
        public string EditableValue
        {
            get => _editableValue ?? FormattedValue;
            set
            {
                if (_editableValue != value)
                {
                    _editableValue = value;
                    OnPropertyChanged();
                    OnValueEdited();
                }
            }
        }

        /// <summary>
        /// Margin for indentation (calculated from IndentLevel)
        /// </summary>
        public System.Windows.Thickness IndentMargin => new System.Windows.Thickness(IndentLevel * 16, 0, 0, 0);

        /// <summary>
        /// Offset formatted as hexadecimal (e.g., "0x0010")
        /// </summary>
        public string OffsetHex => $"0x{Offset:X8}";

        /// <summary>
        /// Display string for offset (e.g., "0x0010 (16)")
        /// </summary>
        public string OffsetDisplay => $"{OffsetHex} ({Offset})";

        /// <summary>
        /// End offset of this field
        /// </summary>
        public long EndOffset => Offset + Length;

        /// <summary>
        /// Display string for byte range (e.g., "0x10-0x13 (4 bytes)")
        /// </summary>
        public string RangeDisplay => $"{OffsetHex}-0x{EndOffset:X8} ({Length} byte{(Length != 1 ? "s" : "")})";

        /// <summary>
        /// Comprehensive tooltip text with all field details
        /// </summary>
        public string DetailedTooltip
        {
            get
            {
                var tooltip = new System.Text.StringBuilder();
                tooltip.AppendLine($"Field: {Name}");
                tooltip.AppendLine($"Type: {ValueType}");
                tooltip.AppendLine($"Range: {RangeDisplay}");

                if (!string.IsNullOrEmpty(Description))
                {
                    tooltip.AppendLine($"Description: {Description}");
                }

                if (RawValue != null)
                {
                    tooltip.AppendLine();
                    tooltip.AppendLine("Values:");

                    // Show value in multiple formats
                    if (RawValue is byte b)
                    {
                        tooltip.AppendLine($"  Hex: 0x{b:X2}");
                        tooltip.AppendLine($"  Dec: {b}");
                        tooltip.AppendLine($"  Bin: {System.Convert.ToString(b, 2).PadLeft(8, '0')}");
                    }
                    else if (RawValue is ushort us)
                    {
                        tooltip.AppendLine($"  Hex: 0x{us:X4}");
                        tooltip.AppendLine($"  Dec: {us}");
                    }
                    else if (RawValue is uint ui)
                    {
                        tooltip.AppendLine($"  Hex: 0x{ui:X8}");
                        tooltip.AppendLine($"  Dec: {ui}");
                    }
                    else if (RawValue is ulong ul)
                    {
                        tooltip.AppendLine($"  Hex: 0x{ul:X16}");
                        tooltip.AppendLine($"  Dec: {ul}");
                    }
                    else if (RawValue is short s)
                    {
                        tooltip.AppendLine($"  Hex: 0x{s:X4}");
                        tooltip.AppendLine($"  Dec: {s}");
                    }
                    else if (RawValue is int i)
                    {
                        tooltip.AppendLine($"  Hex: 0x{i:X8}");
                        tooltip.AppendLine($"  Dec: {i}");
                    }
                    else if (RawValue is long l)
                    {
                        tooltip.AppendLine($"  Hex: 0x{l:X16}");
                        tooltip.AppendLine($"  Dec: {l}");
                    }
                    else if (RawValue is string str)
                    {
                        tooltip.AppendLine($"  String: {str}");
                    }
                    else if (RawValue is float f)
                    {
                        tooltip.AppendLine($"  Float: {f}");
                        tooltip.AppendLine($"  Hex: 0x{System.BitConverter.ToUInt32(System.BitConverter.GetBytes(f), 0):X8}");
                    }
                    else if (RawValue is double d)
                    {
                        tooltip.AppendLine($"  Double: {d}");
                        tooltip.AppendLine($"  Hex: 0x{System.BitConverter.ToUInt64(System.BitConverter.GetBytes(d), 0):X16}");
                    }
                    else if (RawValue is byte[] bytes)
                    {
                        var hex = System.BitConverter.ToString(bytes).Replace("-", " ");
                        tooltip.AppendLine($"  Hex: {hex}");
                    }
                }

                if (!IsValid && !string.IsNullOrEmpty(ValidationMessage))
                {
                    tooltip.AppendLine();
                    tooltip.AppendLine($"⚠ {ValidationMessage}");
                }

                return tooltip.ToString().TrimEnd();
            }
        }

        /// <summary>
        /// The block definition this field was parsed from
        /// </summary>
        public BlockDefinition BlockDefinition { get; set; }

        /// <summary>
        /// Event raised when the user edits a value
        /// </summary>
        public event System.EventHandler<FieldValueEditedEventArgs> ValueEdited;

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected virtual void OnValueEdited()
        {
            ValueEdited?.Invoke(this, new FieldValueEditedEventArgs(this, EditableValue));
        }

        /// <summary>
        /// Create a ParsedFieldViewModel from a BlockDefinition and file data
        /// </summary>
        public static ParsedFieldViewModel FromBlockDefinition(BlockDefinition block, long offset, int length)
        {
            return FromBlockDefinition(block, offset, length, 0);
        }

        /// <summary>
        /// Create a ParsedFieldViewModel with specified indentation level
        /// </summary>
        public static ParsedFieldViewModel FromBlockDefinition(BlockDefinition block, long offset, int length, int indentLevel)
        {
            var field = new ParsedFieldViewModel
            {
                Name = block.Name ?? "Unnamed Field",
                Offset = offset,
                Length = length,
                ValueType = block.ValueType ?? "bytes",
                Description = block.Description ?? string.Empty,
                Color = block.Color ?? "#CCCCCC",
                BlockDefinition = block,
                IsValid = true,
                IndentLevel = indentLevel,
                IsEditable = !string.Equals(block.Type, "signature", System.StringComparison.OrdinalIgnoreCase)
            };

            // Assign icon based on type
            field.FieldIcon = GetIconForType(block.Type, block.ValueType);

            return field;
        }

        /// <summary>
        /// Get appropriate icon for field type
        /// </summary>
        private static string GetIconForType(string blockType, string valueType)
        {
            // Block type icons
            if (!string.IsNullOrEmpty(blockType))
            {
                return blockType.ToLowerInvariant() switch
                {
                    "signature" => "🔖",
                    "field" => GetIconForValueType(valueType),
                    "conditional" => "🔀",
                    "loop" => "🔁",
                    "action" => "⚡",
                    _ => "📄"
                };
            }

            return GetIconForValueType(valueType);
        }

        /// <summary>
        /// Get icon for value type
        /// </summary>
        private static string GetIconForValueType(string valueType)
        {
            if (string.IsNullOrEmpty(valueType))
                return "📄";

            return valueType.ToLowerInvariant() switch
            {
                "string" or "ascii" or "utf8" or "utf16" => "📝",
                "uint8" or "byte" or "int8" or "sbyte" => "🔢",
                "uint16" or "ushort" or "int16" or "short" => "🔢",
                "uint32" or "uint" or "int32" or "int" => "🔢",
                "uint64" or "ulong" or "int64" or "long" => "🔢",
                "float" or "double" => "📊",
                "bytes" => "📦",
                _ => "📄"
            };
        }

        /// <summary>
        /// Try to parse the edited value string to bytes based on the field's value type
        /// Returns null if parsing fails
        /// </summary>
        public byte[] TryParseEditedValue(string editedValue)
        {
            if (string.IsNullOrEmpty(editedValue) || string.IsNullOrEmpty(ValueType))
                return null;

            try
            {
                return ValueType.ToLowerInvariant() switch
                {
                    "uint8" or "byte" => new[] { ParseByte(editedValue) },
                    "int8" or "sbyte" => new[] { (byte)ParseSByte(editedValue) },
                    "uint16" or "ushort" => System.BitConverter.GetBytes(ParseUInt16(editedValue)),
                    "int16" or "short" => System.BitConverter.GetBytes(ParseInt16(editedValue)),
                    "uint32" or "uint" => System.BitConverter.GetBytes(ParseUInt32(editedValue)),
                    "int32" or "int" => System.BitConverter.GetBytes(ParseInt32(editedValue)),
                    "uint64" or "ulong" => System.BitConverter.GetBytes(ParseUInt64(editedValue)),
                    "int64" or "long" => System.BitConverter.GetBytes(ParseInt64(editedValue)),
                    "float" => System.BitConverter.GetBytes(float.Parse(editedValue)),
                    "double" => System.BitConverter.GetBytes(double.Parse(editedValue)),
                    "string" or "ascii" => System.Text.Encoding.ASCII.GetBytes(editedValue),
                    "utf8" => System.Text.Encoding.UTF8.GetBytes(editedValue),
                    "utf16" => System.Text.Encoding.Unicode.GetBytes(editedValue),
                    "bytes" => ParseHexBytes(editedValue),
                    _ => null
                };
            }
            catch
            {
                return null;
            }
        }

        private static byte ParseByte(string value)
        {
            value = value.Trim();
            if (value.StartsWith("0x", System.StringComparison.OrdinalIgnoreCase))
                return System.Convert.ToByte(value.Substring(2), 16);
            return byte.Parse(value);
        }

        private static sbyte ParseSByte(string value)
        {
            value = value.Trim();
            if (value.StartsWith("0x", System.StringComparison.OrdinalIgnoreCase))
                return (sbyte)System.Convert.ToSByte(value.Substring(2), 16);
            return sbyte.Parse(value);
        }

        private static ushort ParseUInt16(string value)
        {
            value = value.Trim();
            if (value.StartsWith("0x", System.StringComparison.OrdinalIgnoreCase))
                return System.Convert.ToUInt16(value.Substring(2), 16);
            return ushort.Parse(value);
        }

        private static short ParseInt16(string value)
        {
            value = value.Trim();
            if (value.StartsWith("0x", System.StringComparison.OrdinalIgnoreCase))
                return (short)System.Convert.ToInt16(value.Substring(2), 16);
            return short.Parse(value);
        }

        private static uint ParseUInt32(string value)
        {
            value = value.Trim();
            if (value.StartsWith("0x", System.StringComparison.OrdinalIgnoreCase))
                return System.Convert.ToUInt32(value.Substring(2), 16);
            return uint.Parse(value);
        }

        private static int ParseInt32(string value)
        {
            value = value.Trim();
            if (value.StartsWith("0x", System.StringComparison.OrdinalIgnoreCase))
                return System.Convert.ToInt32(value.Substring(2), 16);
            return int.Parse(value);
        }

        private static ulong ParseUInt64(string value)
        {
            value = value.Trim();
            if (value.StartsWith("0x", System.StringComparison.OrdinalIgnoreCase))
                return System.Convert.ToUInt64(value.Substring(2), 16);
            return ulong.Parse(value);
        }

        private static long ParseInt64(string value)
        {
            value = value.Trim();
            if (value.StartsWith("0x", System.StringComparison.OrdinalIgnoreCase))
                return System.Convert.ToInt64(value.Substring(2), 16);
            return long.Parse(value);
        }

        private static byte[] ParseHexBytes(string value)
        {
            value = value.Replace(" ", "").Replace("-", "").Replace("0x", "");
            if (value.Length % 2 != 0)
                throw new System.FormatException("Hex string must have even number of characters");

            var bytes = new byte[value.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = System.Convert.ToByte(value.Substring(i * 2, 2), 16);
            }
            return bytes;
        }
    }

    /// <summary>
    /// Event args for when a field value is edited
    /// </summary>
    public class FieldValueEditedEventArgs : System.EventArgs
    {
        public ParsedFieldViewModel Field { get; }
        public string NewValue { get; }

        public FieldValueEditedEventArgs(ParsedFieldViewModel field, string newValue)
        {
            Field = field;
            NewValue = newValue;
        }
    }
}
