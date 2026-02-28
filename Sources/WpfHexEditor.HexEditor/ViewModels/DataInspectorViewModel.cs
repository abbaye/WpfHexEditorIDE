//////////////////////////////////////////////
// Apache 2.0  - 2026
// Data Inspector ViewModel
// Author : Claude Sonnet 4.5
// Contributors: Derek Tremblay (derektremblay666@gmail.com)
//////////////////////////////////////////////

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using WpfHexEditor.BinaryAnalysis.Models.DataInspector;
using WpfHexEditor.BinaryAnalysis.Services;

namespace WpfHexEditor.HexEditor.ViewModels
{
    /// <summary>
    /// ViewModel for the Data Inspector panel
    /// </summary>
    public class DataInspectorViewModel : INotifyPropertyChanged
    {
        private readonly DataInspectorService _service;
        private ObservableCollection<InspectorValue> _values;
        private string _selectedCategory;
        private bool _showAllFormats;
        private byte[] _currentBytes;

        public DataInspectorViewModel()
        {
            _service = new DataInspectorService();
            _values = new ObservableCollection<InspectorValue>();
            _showAllFormats = true; // Show all formats by default
            _selectedCategory = "All";
        }

        /// <summary>
        /// Collection of interpreted values
        /// </summary>
        public ObservableCollection<InspectorValue> Values
        {
            get => _values;
            set
            {
                _values = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Selected category filter ("All", "Integer", "Float", etc.)
        /// </summary>
        public string SelectedCategory
        {
            get => _selectedCategory;
            set
            {
                _selectedCategory = value;
                OnPropertyChanged();
                ApplyFilter();
            }
        }

        /// <summary>
        /// Whether to show all format interpretations or just valid ones
        /// </summary>
        public bool ShowAllFormats
        {
            get => _showAllFormats;
            set
            {
                _showAllFormats = value;
                OnPropertyChanged();
                ApplyFilter();
            }
        }

        /// <summary>
        /// Update the inspector with new byte data
        /// </summary>
        public void UpdateBytes(byte[] bytes)
        {
            _currentBytes = bytes;

            if (bytes == null || bytes.Length == 0)
            {
                Values.Clear();
                return;
            }

            // Interpret bytes in all formats
            var interpretations = _service.InterpretBytes(bytes);

            // Apply filter
            var filtered = ApplyFilterToList(interpretations);

            // Update observable collection
            Values.Clear();
            foreach (var value in filtered)
            {
                Values.Add(value);
            }
        }

        /// <summary>
        /// Apply current filters to the values list
        /// </summary>
        private void ApplyFilter()
        {
            if (_currentBytes == null)
                return;

            UpdateBytes(_currentBytes);
        }

        /// <summary>
        /// Apply filters to a list of inspector values
        /// </summary>
        private System.Collections.Generic.List<InspectorValue> ApplyFilterToList(System.Collections.Generic.List<InspectorValue> values)
        {
            var filtered = values;

            // Filter by category
            if (!string.IsNullOrEmpty(SelectedCategory) && SelectedCategory != "All")
            {
                filtered = filtered.Where(v => v.Category == SelectedCategory).ToList();
            }

            // Filter by validity
            if (!ShowAllFormats)
            {
                filtered = filtered.Where(v => v.IsValid).ToList();
            }

            return filtered;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
