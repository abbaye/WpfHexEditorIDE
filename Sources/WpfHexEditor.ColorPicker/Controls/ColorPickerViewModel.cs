//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using WpfHexEditor.ColorPicker.Helpers;

namespace WpfHexEditor.ColorPicker.Controls
{
    /// <summary>
    /// ViewModel for ColorPicker component that manages color state and conversions between RGB/HSV/Hex.
    /// </summary>
    public class ColorPickerViewModel : INotifyPropertyChanged
    {
        private bool _isUpdating; // Prevents circular updates
        private byte _red;
        private byte _green;
        private byte _blue;
        private byte _alpha;
        private double _hue;
        private double _saturation;
        private double _value;
        private string? _hexColor;
        private bool _isHexValid;
        private Color _selectedColor;

        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Initializes a new instance with default color (Blue).
        /// </summary>
        public ColorPickerViewModel()
        {
            SetColor(Color.FromRgb(64, 64, 255)); // Default blue
        }

        #region RGB Properties (0-255)

        /// <summary>
        /// Red component (0-255)
        /// </summary>
        public byte Red
        {
            get => _red;
            set
            {
                if (_red != value)
                {
                    _red = value;
                    OnPropertyChanged();
                    if (!_isUpdating)
                        UpdateColorFromRgb();
                }
            }
        }

        /// <summary>
        /// Green component (0-255)
        /// </summary>
        public byte Green
        {
            get => _green;
            set
            {
                if (_green != value)
                {
                    _green = value;
                    OnPropertyChanged();
                    if (!_isUpdating)
                        UpdateColorFromRgb();
                }
            }
        }

        /// <summary>
        /// Blue component (0-255)
        /// </summary>
        public byte Blue
        {
            get => _blue;
            set
            {
                if (_blue != value)
                {
                    _blue = value;
                    OnPropertyChanged();
                    if (!_isUpdating)
                        UpdateColorFromRgb();
                }
            }
        }

        /// <summary>
        /// Alpha/Transparency component (0-255, where 0=transparent, 255=opaque)
        /// </summary>
        public byte Alpha
        {
            get => _alpha;
            set
            {
                if (_alpha != value)
                {
                    _alpha = value;
                    OnPropertyChanged();
                    if (!_isUpdating)
                        UpdateColorFromRgb();
                }
            }
        }

        #endregion

        #region HSV Properties (H: 0-360Â°, S/V: 0-1)

        /// <summary>
        /// Hue (0-360Â°)
        /// </summary>
        public double Hue
        {
            get => _hue;
            set
            {
                if (Math.Abs(_hue - value) > 0.01)
                {
                    _hue = value;
                    OnPropertyChanged();
                    if (!_isUpdating)
                        UpdateColorFromHsv();
                }
            }
        }

        /// <summary>
        /// Saturation (0-1)
        /// </summary>
        public double Saturation
        {
            get => _saturation;
            set
            {
                if (Math.Abs(_saturation - value) > 0.01)
                {
                    _saturation = value;
                    OnPropertyChanged();
                    if (!_isUpdating)
                        UpdateColorFromHsv();
                }
            }
        }

        /// <summary>
        /// Value/Brightness (0-1)
        /// </summary>
        public double Value
        {
            get => _value;
            set
            {
                if (Math.Abs(_value - value) > 0.01)
                {
                    _value = value;
                    OnPropertyChanged();
                    if (!_isUpdating)
                        UpdateColorFromHsv();
                }
            }
        }

        #endregion

        #region Hex and Validation

        /// <summary>
        /// Hex color string (#AARRGGBB or #RRGGBB)
        /// </summary>
        public string? HexColor
        {
            get => _hexColor;
            set
            {
                if (_hexColor != value)
                {
                    _hexColor = value;
                    OnPropertyChanged();
                    if (!_isUpdating)
                        ValidateAndApplyHex();
                }
            }
        }

        /// <summary>
        /// True if current HexColor is valid
        /// </summary>
        public bool IsHexValid
        {
            get => _isHexValid;
            private set
            {
                if (_isHexValid != value)
                {
                    _isHexValid = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ValidationIcon));
                    OnPropertyChanged(nameof(ValidationColor));
                }
            }
        }

        /// <summary>
        /// Validation icon (âœ“ or âœ—)
        /// </summary>
        public string ValidationIcon => IsHexValid ? "âœ“" : "âœ—";

        /// <summary>
        /// Validation color (Green or Red)
        /// </summary>
        public Color ValidationColor => IsHexValid
            ? Color.FromRgb(76, 175, 80)   // Green
            : Color.FromRgb(244, 67, 54);  // Red

        #endregion

        #region Selected Color

        /// <summary>
        /// The current selected color (ARGB). This is the single source of truth.
        /// </summary>
        public Color SelectedColor
        {
            get => _selectedColor;
            private set
            {
                if (_selectedColor != value)
                {
                    _selectedColor = value;
                    OnPropertyChanged();
                }
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Sets the color from an external source. Updates all properties (RGB, HSV, Hex).
        /// </summary>
        /// <param name="color">Color to set</param>
        public void SetColor(Color color)
        {
            _isUpdating = true;
            try
            {
                // Update RGB
                _red = color.R;
                _green = color.G;
                _blue = color.B;
                _alpha = color.A;

                // Update HSV
                (_hue, _saturation, _value) = ColorSpaceConverter.RgbToHsv(color.R, color.G, color.B);

                // Update Hex
                _hexColor = $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
                _isHexValid = true;

                // Update selected color
                _selectedColor = color;

                // Notify all properties changed
                OnPropertyChanged(nameof(Red));
                OnPropertyChanged(nameof(Green));
                OnPropertyChanged(nameof(Blue));
                OnPropertyChanged(nameof(Alpha));
                OnPropertyChanged(nameof(Hue));
                OnPropertyChanged(nameof(Saturation));
                OnPropertyChanged(nameof(Value));
                OnPropertyChanged(nameof(HexColor));
                OnPropertyChanged(nameof(IsHexValid));
                OnPropertyChanged(nameof(ValidationIcon));
                OnPropertyChanged(nameof(ValidationColor));
                OnPropertyChanged(nameof(SelectedColor));
            }
            finally
            {
                _isUpdating = false;
            }
        }

        #endregion

        #region Private Update Methods

        /// <summary>
        /// Updates color from RGB changes. Recalculates HSV and Hex.
        /// </summary>
        private void UpdateColorFromRgb()
        {
            _isUpdating = true;
            try
            {
                // Create new color
                _selectedColor = Color.FromArgb(_alpha, _red, _green, _blue);

                // Update HSV
                (_hue, _saturation, _value) = ColorSpaceConverter.RgbToHsv(_red, _green, _blue);

                // Update Hex
                _hexColor = $"#{_alpha:X2}{_red:X2}{_green:X2}{_blue:X2}";
                _isHexValid = true;

                // Notify changes
                OnPropertyChanged(nameof(Hue));
                OnPropertyChanged(nameof(Saturation));
                OnPropertyChanged(nameof(Value));
                OnPropertyChanged(nameof(HexColor));
                OnPropertyChanged(nameof(IsHexValid));
                OnPropertyChanged(nameof(ValidationIcon));
                OnPropertyChanged(nameof(ValidationColor));
                OnPropertyChanged(nameof(SelectedColor));
            }
            finally
            {
                _isUpdating = false;
            }
        }

        /// <summary>
        /// Updates color from HSV changes. Recalculates RGB and Hex.
        /// </summary>
        private void UpdateColorFromHsv()
        {
            _isUpdating = true;
            try
            {
                // Convert HSV to RGB
                (_red, _green, _blue) = ColorSpaceConverter.HsvToRgb(_hue, _saturation, _value);

                // Create new color
                _selectedColor = Color.FromArgb(_alpha, _red, _green, _blue);

                // Update Hex
                _hexColor = $"#{_alpha:X2}{_red:X2}{_green:X2}{_blue:X2}";
                _isHexValid = true;

                // Notify changes
                OnPropertyChanged(nameof(Red));
                OnPropertyChanged(nameof(Green));
                OnPropertyChanged(nameof(Blue));
                OnPropertyChanged(nameof(HexColor));
                OnPropertyChanged(nameof(IsHexValid));
                OnPropertyChanged(nameof(ValidationIcon));
                OnPropertyChanged(nameof(ValidationColor));
                OnPropertyChanged(nameof(SelectedColor));
            }
            finally
            {
                _isUpdating = false;
            }
        }

        /// <summary>
        /// Validates hex input and applies if valid.
        /// </summary>
        private void ValidateAndApplyHex()
        {
            if (TryParseHexColor(_hexColor, out Color color))
            {
                _isUpdating = true;
                try
                {
                    IsHexValid = true;

                    // Update RGB
                    _red = color.R;
                    _green = color.G;
                    _blue = color.B;
                    _alpha = color.A;

                    // Update HSV
                    (_hue, _saturation, _value) = ColorSpaceConverter.RgbToHsv(color.R, color.G, color.B);

                    // Update selected color
                    _selectedColor = color;

                    // Notify changes
                    OnPropertyChanged(nameof(Red));
                    OnPropertyChanged(nameof(Green));
                    OnPropertyChanged(nameof(Blue));
                    OnPropertyChanged(nameof(Alpha));
                    OnPropertyChanged(nameof(Hue));
                    OnPropertyChanged(nameof(Saturation));
                    OnPropertyChanged(nameof(Value));
                    OnPropertyChanged(nameof(SelectedColor));
                }
                finally
                {
                    _isUpdating = false;
                }
            }
            else
            {
                IsHexValid = false;
            }
        }

        /// <summary>
        /// Tries to parse a hex color string.
        /// Supports: #AARRGGBB, #RRGGBB, AARRGGBB, RRGGBB, #RGB
        /// </summary>
        private bool TryParseHexColor(string hex, out Color color)
        {
            color = Colors.Transparent;
            hex = hex?.Trim().Replace("#", "").ToUpper() ?? "";

            try
            {
                if (hex.Length == 8) // AARRGGBB
                {
                    byte a = Convert.ToByte(hex.Substring(0, 2), 16);
                    byte r = Convert.ToByte(hex.Substring(2, 2), 16);
                    byte g = Convert.ToByte(hex.Substring(4, 2), 16);
                    byte b = Convert.ToByte(hex.Substring(6, 2), 16);
                    color = Color.FromArgb(a, r, g, b);
                    return true;
                }
                else if (hex.Length == 6) // RRGGBB
                {
                    byte r = Convert.ToByte(hex.Substring(0, 2), 16);
                    byte g = Convert.ToByte(hex.Substring(2, 2), 16);
                    byte b = Convert.ToByte(hex.Substring(4, 2), 16);
                    color = Color.FromArgb(255, r, g, b);
                    return true;
                }
                else if (hex.Length == 3) // RGB (expand to RRGGBB)
                {
                    byte r = Convert.ToByte(new string(hex[0], 2), 16);
                    byte g = Convert.ToByte(new string(hex[1], 2), 16);
                    byte b = Convert.ToByte(new string(hex[2], 2), 16);
                    color = Color.FromArgb(255, r, g, b);
                    return true;
                }
            }
            catch
            {
                // Invalid hex format
            }

            return false;
        }

        #endregion

        #region INotifyPropertyChanged

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
}
