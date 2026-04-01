//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Structure Overlay - Field Model
// Author : Claude Sonnet 4.5
// Contributors: Derek Tremblay (derektremblay666@gmail.com), Claude Sonnet 4.6
//////////////////////////////////////////////

using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace WpfHexEditor.Core.Models.StructureOverlay
{
    /// <summary>
    /// Represents a single field in a structure overlay
    /// </summary>
    public class OverlayField : INotifyPropertyChanged
    {
        private string _name;
        private string _type;
        private long _offset;
        private int _length;
        private string _value;
        private string _description;
        private Color _color;
        private bool _isHighlighted;
        private bool _isSelected;

        /// <summary>
        /// Field name
        /// </summary>
        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Field type (uint32, string, etc.)
        /// </summary>
        public string Type
        {
            get => _type;
            set { _type = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Offset in file (in bytes)
        /// </summary>
        public long Offset
        {
            get => _offset;
            set { _offset = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Length in bytes
        /// </summary>
        public int Length
        {
            get => _length;
            set { _length = value; OnPropertyChanged(); }
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
        /// Field description
        /// </summary>
        public string Description
        {
            get => _description;
            set { _description = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Overlay color for this field
        /// </summary>
        public Color Color
        {
            get => _color;
            set { _color = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Whether this field is currently highlighted
        /// </summary>
        public bool IsHighlighted
        {
            get => _isHighlighted;
            set { _isHighlighted = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Whether this field is currently selected
        /// </summary>
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
