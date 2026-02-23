//////////////////////////////////////////////
// Apache 2.0  - 2026
// Binary Templates - Template Structure Model
// Author : Claude Sonnet 4.5
// Contributors: Derek Tremblay (derektremblay666@gmail.com)
//////////////////////////////////////////////

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WpfHexaEditor.Models.BinaryTemplates
{
    /// <summary>
    /// Represents a binary template structure (similar to 010 Editor templates)
    /// </summary>
    public class TemplateStructure : INotifyPropertyChanged
    {
        private string _name;
        private string _description;
        private ObservableCollection<TemplateField> _fields;
        private string _script;

        public TemplateStructure()
        {
            _fields = new ObservableCollection<TemplateField>();
        }

        /// <summary>
        /// Template name
        /// </summary>
        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Template description
        /// </summary>
        public string Description
        {
            get => _description;
            set { _description = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Collection of fields
        /// </summary>
        public ObservableCollection<TemplateField> Fields
        {
            get => _fields;
            set { _fields = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Template script (C-like syntax)
        /// </summary>
        public string Script
        {
            get => _script;
            set { _script = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// Represents a field in a binary template
    /// </summary>
    public class TemplateField : INotifyPropertyChanged
    {
        private string _name;
        private string _type;
        private string _arraySize;
        private string _condition;
        private string _comment;

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public string Type
        {
            get => _type;
            set { _type = value; OnPropertyChanged(); }
        }

        public string ArraySize
        {
            get => _arraySize;
            set { _arraySize = value; OnPropertyChanged(); }
        }

        public string Condition
        {
            get => _condition;
            set { _condition = value; OnPropertyChanged(); }
        }

        public string Comment
        {
            get => _comment;
            set { _comment = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
