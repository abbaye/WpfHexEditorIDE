//////////////////////////////////////////////
// Apache 2.0  - 2026
// Data Inspector - Value Model
// Author : Claude Sonnet 4.5
// Contributors: Derek Tremblay (derektremblay666@gmail.com)
//////////////////////////////////////////////

using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WpfHexaEditor.Models.DataInspector
{
    /// <summary>
    /// Represents a single interpreted value in the Data Inspector
    /// </summary>
    public class InspectorValue : INotifyPropertyChanged
    {
        private string _category;
        private string _format;
        private string _value;
        private string _hexValue;
        private bool _isValid;

        /// <summary>
        /// Category of the value (Integer, Float, Date/Time, Network, etc.)
        /// </summary>
        public string Category
        {
            get => _category;
            set { _category = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Format description (e.g., "Int32 LE", "Float64", "Unix Timestamp")
        /// </summary>
        public string Format
        {
            get => _format;
            set { _format = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Interpreted value as string
        /// </summary>
        public string Value
        {
            get => _value;
            set { _value = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Hexadecimal representation of raw bytes
        /// </summary>
        public string HexValue
        {
            get => _hexValue;
            set { _hexValue = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Whether this interpretation is valid for the current data
        /// </summary>
        public bool IsValid
        {
            get => _isValid;
            set { _isValid = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
