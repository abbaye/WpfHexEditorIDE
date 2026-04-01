//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Structure Overlay - Structure Model
// Author : Claude Sonnet 4.5
// Contributors: Derek Tremblay (derektremblay666@gmail.com), Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WpfHexEditor.Core.Models.StructureOverlay
{
    /// <summary>
    /// Represents a complete structure overlay (e.g., PNG header, PE header, etc.)
    /// </summary>
    public class OverlayStructure : INotifyPropertyChanged
    {
        private string _name;
        private string _formatType;
        private long _startOffset;
        private int _totalLength;
        private string _description;
        private ObservableCollection<OverlayField> _fields;
        private bool _isVisible;
        private bool _isExpanded;

        public OverlayStructure()
        {
            _fields = new ObservableCollection<OverlayField>();
            _isVisible = true;
            _isExpanded = true;
        }

        /// <summary>
        /// Structure name (e.g., "PNG IHDR Chunk", "PE Header")
        /// </summary>
        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Format type (PNG, PE, JPEG, Custom, etc.)
        /// </summary>
        public string FormatType
        {
            get => _formatType;
            set { _formatType = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Start offset in file
        /// </summary>
        public long StartOffset
        {
            get => _startOffset;
            set { _startOffset = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Total length of structure in bytes
        /// </summary>
        public int TotalLength
        {
            get => _totalLength;
            set { _totalLength = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Structure description
        /// </summary>
        public string Description
        {
            get => _description;
            set { _description = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Collection of fields in this structure
        /// </summary>
        public ObservableCollection<OverlayField> Fields
        {
            get => _fields;
            set { _fields = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Whether this overlay is currently visible on the hex editor
        /// </summary>
        public bool IsVisible
        {
            get => _isVisible;
            set { _isVisible = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Whether this structure is expanded in the tree view
        /// </summary>
        public bool IsExpanded
        {
            get => _isExpanded;
            set { _isExpanded = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
