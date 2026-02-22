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

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
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
                IndentLevel = indentLevel
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
    }
}
