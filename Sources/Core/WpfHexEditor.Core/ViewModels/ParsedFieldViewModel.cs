//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5, Claude Sonnet 4.6
//////////////////////////////////////////////

using System.ComponentModel;
using System.Runtime.CompilerServices;
using WpfHexEditor.Core.FormatDetection;
using WpfHexEditor.Core.ViewModels;

namespace WpfHexEditor.Core.ViewModels
{
    /// <summary>
    /// ViewModel representing a single parsed field from a format definition
    /// Displays field name, offset, length, value, and formatting information
    /// </summary>
    public class ParsedFieldViewModel : ViewModelBase
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
        private bool _isBookmarked;
        private bool _isSearchMatch;

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
                OnPropertyChanged(nameof(ActiveFormattedValue));
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
                OnPropertyChanged(nameof(ValidationIcon));
                OnPropertyChanged(nameof(ValidationColor));
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
        /// Validation status icon
        /// </summary>
        public string ValidationIcon =>
            !IsValid && !string.IsNullOrEmpty(ValidationMessage) ? "âš ï¸" :
            IsValid ? "âœ…" : "";

        /// <summary>
        /// Validation status color
        /// </summary>
        public string ValidationColor =>
            !IsValid && !string.IsNullOrEmpty(ValidationMessage) ? "#FF6B6B" :
            IsValid ? "#7ED321" : "#CCCCCC";

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
        /// Whether this field is bookmarked/favorited by the user
        /// </summary>
        public bool IsBookmarked
        {
            get => _isBookmarked;
            set
            {
                if (_isBookmarked != value)
                {
                    _isBookmarked = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Whether this field matches the current search query
        /// </summary>
        public bool IsSearchMatch
        {
            get => _isSearchMatch;
            set
            {
                if (_isSearchMatch != value)
                {
                    _isSearchMatch = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _groupName;

        /// <summary>
        /// Group name for section headers (C3): Signature, Header Fields, Data Fields
        /// </summary>
        public string GroupName
        {
            get => _groupName;
            set
            {
                if (_groupName != value)
                {
                    _groupName = value;
                    OnPropertyChanged();
                }
            }
        }

        // ── Tree hierarchy (C1) ────────────────────────────────────────────────
        private bool _isGroup;
        private bool _isExpanded = true;
        private System.Collections.ObjectModel.ObservableCollection<ParsedFieldViewModel> _children;

        /// <summary>Whether this node is a group container (repeating block, nested struct group).</summary>
        public bool IsGroup
        {
            get => _isGroup;
            set
            {
                if (_isGroup != value)
                {
                    _isGroup = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(GroupLabel));
                    OnPropertyChanged(nameof(FieldIcon));
                }
            }
        }

        /// <summary>Whether the tree node is expanded (C1).</summary>
        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded != value)
                {
                    _isExpanded = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Children for repeating/nested groups — null when no children so the TreeView
        /// does not render an expand toggle on leaf nodes.
        /// </summary>
        public System.Collections.ObjectModel.ObservableCollection<ParsedFieldViewModel>? ChildItems
            => _children?.Count > 0 ? _children : null;

        /// <summary>Whether this node has at least one child.</summary>
        public bool HasChildren => _children?.Count > 0;

        /// <summary>Display label: "Name [N entries]" for groups, plain Name otherwise.</summary>
        public string GroupLabel => IsGroup
            ? $"{Name ?? string.Empty} [{_children?.Count ?? 0} {(_children?.Count == 1 ? "entry" : "entries")}]"
            : (Name ?? string.Empty);

        /// <summary>Adds a child to this group node, refreshing computed tree properties.</summary>
        public void AddChild(ParsedFieldViewModel child)
        {
            _children ??= new System.Collections.ObjectModel.ObservableCollection<ParsedFieldViewModel>();
            _children.Add(child);
            OnPropertyChanged(nameof(ChildItems));
            OnPropertyChanged(nameof(HasChildren));
            OnPropertyChanged(nameof(GroupLabel));
        }

        // ── End tree hierarchy ──────────────────────────────────────────────────

        private FieldDisplayMode _displayMode = FieldDisplayMode.Auto;

        /// <summary>
        /// Per-field display mode for toggling Hex/Dec/Bin (C7)
        /// </summary>
        public FieldDisplayMode DisplayMode
        {
            get => _displayMode;
            set
            {
                if (_displayMode != value)
                {
                    _displayMode = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ActiveFormattedValue));
                }
            }
        }

        /// <summary>
        /// Value formatted according to the current DisplayMode (C7)
        /// </summary>
        public string ActiveFormattedValue => DisplayMode switch
        {
            FieldDisplayMode.Hex => GetValueAsHex(),
            FieldDisplayMode.Decimal => GetValueAsDecimal(),
            FieldDisplayMode.Binary => GetValueAsBinary(),
            _ => FormattedValue
        };

        /// <summary>
        /// Whether this field has a numeric type (for showing H/D/B toggle)
        /// </summary>
        public bool IsNumericType => ValueType?.ToLowerInvariant() switch
        {
            "uint8" or "byte" or "int8" or "sbyte" => true,
            "uint16" or "ushort" or "int16" or "short" => true,
            "uint32" or "uint" or "int32" or "int" => true,
            "uint64" or "ulong" or "int64" or "long" => true,
            "float" or "double" => true,
            _ => false
        };

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
        /// For metadata fields, shows "Metadata (computed value)"
        /// </summary>
        public string RangeDisplay
        {
            get
            {
                // Special handling for metadata fields
                if (ValueType == "metadata" || Offset < 0)
                {
                    return "Metadata (computed value)";
                }
                return $"{OffsetHex}-0x{EndOffset:X8} ({Length} byte{(Length != 1 ? "s" : "")})";
            }
        }

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
                    tooltip.AppendLine($"âš  {ValidationMessage}");
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
                    "signature"   => "\uE8A4",  // Bookmark
                    "field"       => GetIconForValueType(valueType),
                    "conditional" => "\uE752",  // Branch / decision
                    "loop"        => "\uE72C",  // Sync (circular arrows)
                    "action"      => "\uE756",  // Go / forward
                    "bitfield"    => "\uE71D",  // Flag bits
                    "metadata"    => "\uE946",  // Info / computed
                    _             => GetIconForValueType(valueType)
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
                return "\uE8EF";  // NumberSymbol (fallback)

            return valueType.ToLowerInvariant() switch
            {
                "string" or "ascii" or "utf8" or "utf16"                  => "\uE8AB",  // Font / text
                "uint8"  or "byte"  or "int8"  or "sbyte"                 => "\uE8EF",  // NumberSymbol
                "uint16" or "ushort" or "int16" or "short"                => "\uE8EF",
                "uint32" or "uint"   or "int32" or "int"                  => "\uE8EF",
                "uint64" or "ulong"  or "int64" or "long"                 => "\uE8EF",
                "float"  or "double"                                      => "\uE8EF",
                "bytes"                                                   => "\uE7C3",  // Page / binary
                "bitfield"                                                => "\uE71D",  // Flags / bitfield
                "metadata" or "computed"                                  => "\uE946",  // Info / computed
                _                                                         => "\uE8EF"
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

        /// <summary>
        /// Get value as hex string
        /// </summary>
        public string GetValueAsHex()
        {
            if (RawValue == null)
                return "N/A";

            if (RawValue is byte[] bytes && bytes.Length > 0)
                return System.BitConverter.ToString(bytes).Replace("-", " ");

            // Convert numeric types to hex
            return ValueType?.ToLowerInvariant() switch
            {
                "uint8" or "byte" when RawValue is byte b => $"0x{b:X2}",
                "int8" or "sbyte" when RawValue is sbyte sb => $"0x{(byte)sb:X2}",
                "uint16" or "ushort" when RawValue is ushort us => $"0x{us:X4}",
                "int16" or "short" when RawValue is short s => $"0x{(ushort)s:X4}",
                "uint32" or "uint" when RawValue is uint ui => $"0x{ui:X8}",
                "int32" or "int" when RawValue is int i => $"0x{(uint)i:X8}",
                "uint64" or "ulong" when RawValue is ulong ul => $"0x{ul:X16}",
                "int64" or "long" when RawValue is long l => $"0x{(ulong)l:X16}",
                _ => "N/A"
            };
        }

        /// <summary>
        /// Get value as decimal string
        /// </summary>
        public string GetValueAsDecimal()
        {
            if (RawValue == null)
                return "N/A";

            return ValueType?.ToLowerInvariant() switch
            {
                "uint8" or "byte" when RawValue is byte b => b.ToString(),
                "int8" or "sbyte" when RawValue is sbyte sb => sb.ToString(),
                "uint16" or "ushort" when RawValue is ushort us => us.ToString(),
                "int16" or "short" when RawValue is short s => s.ToString(),
                "uint32" or "uint" when RawValue is uint ui => ui.ToString(),
                "int32" or "int" when RawValue is int i => i.ToString(),
                "uint64" or "ulong" when RawValue is ulong ul => ul.ToString(),
                "int64" or "long" when RawValue is long l => l.ToString(),
                "float" when RawValue is float f => f.ToString("F6"),
                "double" when RawValue is double d => d.ToString("F6"),
                _ => RawValue.ToString()
            };
        }

        /// <summary>
        /// Get value as binary string
        /// </summary>
        public string GetValueAsBinary()
        {
            if (RawValue == null)
                return "N/A";

            if (RawValue is byte[] bytes)
            {
                var binary = new System.Text.StringBuilder();
                foreach (var b in bytes)
                {
                    if (binary.Length > 0)
                        binary.Append(' ');
                    binary.Append(System.Convert.ToString(b, 2).PadLeft(8, '0'));
                }
                return binary.ToString();
            }

            // Convert numeric types to binary
            byte[] numBytes = ValueType?.ToLowerInvariant() switch
            {
                "uint8" or "byte" when RawValue is byte b => new[] { b },
                "int8" or "sbyte" when RawValue is sbyte sb => new[] { (byte)sb },
                "uint16" or "ushort" when RawValue is ushort us => System.BitConverter.GetBytes(us),
                "int16" or "short" when RawValue is short s => System.BitConverter.GetBytes(s),
                "uint32" or "uint" when RawValue is uint ui => System.BitConverter.GetBytes(ui),
                "int32" or "int" when RawValue is int i => System.BitConverter.GetBytes(i),
                "uint64" or "ulong" when RawValue is ulong ul => System.BitConverter.GetBytes(ul),
                "int64" or "long" when RawValue is long l => System.BitConverter.GetBytes(l),
                _ => null
            };

            if (numBytes != null)
            {
                var binary = new System.Text.StringBuilder();
                foreach (var b in numBytes)
                {
                    if (binary.Length > 0)
                        binary.Append(' ');
                    binary.Append(System.Convert.ToString(b, 2).PadLeft(8, '0'));
                }
                return binary.ToString();
            }

            return "N/A";
        }
    }

    /// <summary>
    /// Display mode for per-field value formatting (C7)
    /// </summary>
    public enum FieldDisplayMode
    {
        Auto,
        Hex,
        Decimal,
        Binary
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
