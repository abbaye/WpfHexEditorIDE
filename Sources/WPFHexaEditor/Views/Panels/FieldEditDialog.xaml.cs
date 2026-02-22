//////////////////////////////////////////////
// Apache 2.0  - 2026
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using WpfHexaEditor.ViewModels;

namespace WpfHexaEditor.Views.Panels
{
    /// <summary>
    /// Dialog for editing a parsed field value
    /// </summary>
    public partial class FieldEditDialog : Window, INotifyPropertyChanged
    {
        private ParsedFieldViewModel _field;
        private string _editedValue;

        public FieldEditDialog(ParsedFieldViewModel field)
        {
            InitializeComponent();
            DataContext = this;

            _field = field;
            _editedValue = field.FormattedValue;

            OnPropertyChanged(nameof(FieldName));
            OnPropertyChanged(nameof(FieldType));
            OnPropertyChanged(nameof(FieldOffset));
            OnPropertyChanged(nameof(FieldLength));
            OnPropertyChanged(nameof(CurrentValue));
            OnPropertyChanged(nameof(EditedValue));
            OnPropertyChanged(nameof(FormatHint));

            ValueTextBox.Focus();
            ValueTextBox.SelectAll();
        }

        public string FieldName => _field.Name;
        public string FieldType => _field.ValueType;
        public string FieldOffset => _field.OffsetHex;
        public string FieldLength => $"{_field.Length} byte{(_field.Length != 1 ? "s" : "")}";
        public string CurrentValue => _field.FormattedValue;

        public string EditedValue
        {
            get => _editedValue;
            set
            {
                _editedValue = value;
                OnPropertyChanged();
            }
        }

        public string FormatHint
        {
            get
            {
                return _field.ValueType?.ToLowerInvariant() switch
                {
                    "uint8" or "byte" or "int8" or "sbyte" => "Format: decimal (0-255) or hex (0x00-0xFF)",
                    "uint16" or "ushort" or "int16" or "short" => "Format: decimal or hex (0x0000-0xFFFF)",
                    "uint32" or "uint" or "int32" or "int" => "Format: decimal or hex (0x00000000-0xFFFFFFFF)",
                    "uint64" or "ulong" or "int64" or "long" => "Format: decimal or hex (0x0000000000000000-0xFFFFFFFFFFFFFFFF)",
                    "float" => "Format: decimal with optional exponent (e.g., 3.14, 1.23e-4)",
                    "double" => "Format: decimal with optional exponent (e.g., 3.14159, 1.23e-10)",
                    "string" or "ascii" => "Format: plain text (ASCII characters)",
                    "utf8" => "Format: plain text (UTF-8 encoded)",
                    "utf16" => "Format: plain text (UTF-16 encoded)",
                    "bytes" => "Format: hex bytes (e.g., '48 65 6C 6C 6F' or '48656C6C6F')",
                    _ => "Enter a new value for this field"
                };
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
