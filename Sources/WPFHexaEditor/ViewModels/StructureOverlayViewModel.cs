//////////////////////////////////////////////
// Apache 2.0  - 2026
// Structure Overlay ViewModel
// Author : Claude Sonnet 4.5
// Contributors: Derek Tremblay (derektremblay666@gmail.com)
//////////////////////////////////////////////

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using WpfHexaEditor.Models.StructureOverlay;

namespace WpfHexaEditor.ViewModels
{
    /// <summary>
    /// ViewModel for the Structure Overlay panel
    /// </summary>
    public class StructureOverlayViewModel : INotifyPropertyChanged
    {
        private ObservableCollection<OverlayStructure> _structures;
        private OverlayStructure _selectedStructure;
        private OverlayField _selectedField;

        public StructureOverlayViewModel()
        {
            _structures = new ObservableCollection<OverlayStructure>();
        }

        /// <summary>
        /// Collection of overlay structures
        /// </summary>
        public ObservableCollection<OverlayStructure> Structures
        {
            get => _structures;
            set
            {
                _structures = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Currently selected structure
        /// </summary>
        public OverlayStructure SelectedStructure
        {
            get => _selectedStructure;
            set
            {
                _selectedStructure = value;
                OnPropertyChanged();
                OnStructureSelected?.Invoke(this, value);
            }
        }

        /// <summary>
        /// Currently selected field
        /// </summary>
        public OverlayField SelectedField
        {
            get => _selectedField;
            set
            {
                _selectedField = value;
                OnPropertyChanged();
                OnFieldSelected?.Invoke(this, value);
            }
        }

        /// <summary>
        /// Add a structure overlay
        /// </summary>
        public void AddStructure(OverlayStructure structure)
        {
            if (structure != null)
            {
                Structures.Add(structure);
            }
        }

        /// <summary>
        /// Remove a structure overlay
        /// </summary>
        public void RemoveStructure(OverlayStructure structure)
        {
            if (structure != null)
            {
                Structures.Remove(structure);
            }
        }

        /// <summary>
        /// Clear all structure overlays
        /// </summary>
        public void ClearAll()
        {
            Structures.Clear();
            SelectedStructure = null;
            SelectedField = null;
        }

        /// <summary>
        /// Toggle visibility of a structure
        /// </summary>
        public void ToggleStructureVisibility(OverlayStructure structure)
        {
            if (structure != null)
            {
                structure.IsVisible = !structure.IsVisible;
            }
        }

        /// <summary>
        /// Event fired when a structure is selected
        /// </summary>
        public event EventHandler<OverlayStructure> OnStructureSelected;

        /// <summary>
        /// Event fired when a field is selected
        /// </summary>
        public event EventHandler<OverlayField> OnFieldSelected;

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
