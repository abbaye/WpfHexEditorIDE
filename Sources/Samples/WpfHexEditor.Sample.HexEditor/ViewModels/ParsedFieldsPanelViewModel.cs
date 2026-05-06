// WpfHexEditor.Sample.HexEditor — ParsedFieldsPanelViewModel.cs

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using WpfHexEditor.Core.Interfaces;
using WpfHexEditor.Core.ViewModels;

namespace WpfHexEditor.Sample.HexEditor.ViewModels
{
    public class ParsedFieldsPanelViewModel : INotifyPropertyChanged
    {
        private IParsedFieldsPanel? _panel;
        private string _filterText = string.Empty;
        private ParsedFieldViewModel? _selectedField;
        private string _statusText = "No file loaded";
        private string _formatBadge = string.Empty;

        public event PropertyChangedEventHandler? PropertyChanged;

        public ObservableCollection<ParsedFieldViewModel> Fields { get; } = new();

        public ParsedFieldViewModel? SelectedField
        {
            get => _selectedField;
            set { _selectedField = value; OnPropertyChanged(); }
        }

        public string FilterText
        {
            get => _filterText;
            set { _filterText = value; OnPropertyChanged(); ApplyFilter(); }
        }

        public string StatusText
        {
            get => _statusText;
            set { _statusText = value; OnPropertyChanged(); }
        }

        public string FormatBadge
        {
            get => _formatBadge;
            set { _formatBadge = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasFormat)); }
        }

        public bool HasFormat => !string.IsNullOrEmpty(_formatBadge);
        public bool IsEmpty   => Fields.Count == 0;
        public bool IsNotEmpty => Fields.Count > 0;

        public ICommand ClearFilterCommand { get; }
        public ICommand RefreshCommand     { get; }

        public ParsedFieldsPanelViewModel()
        {
            ClearFilterCommand = new RelayCommand(() => FilterText = string.Empty);
            RefreshCommand     = new RelayCommand(Refresh);
        }

        public void AttachPanel(IParsedFieldsPanel panel)
        {
            if (_panel != null)
            {
                _panel.FieldSelected     -= OnFieldSelected;
                _panel.RefreshRequested  -= OnRefreshRequested;
            }

            _panel = panel;
            _panel.FieldSelected    += OnFieldSelected;
            _panel.RefreshRequested += OnRefreshRequested;

            Refresh();
        }

        public void OnFileClosed()
        {
            Fields.Clear();
            FormatBadge = string.Empty;
            StatusText  = "No file loaded";
            NotifyCountChanged();
        }

        private void OnFieldSelected(object? sender, ParsedFieldViewModel field)
        {
            _selectedField = field;
            OnPropertyChanged(nameof(SelectedField));
        }

        private void OnRefreshRequested(object? sender, EventArgs e) => Refresh();

        private void Refresh()
        {
            if (_panel == null) return;

            Fields.Clear();
            var allFields = _panel.ParsedFields;
            var filtered = string.IsNullOrWhiteSpace(_filterText)
                ? allFields
                : allFields.Where(f => f.Name.Contains(_filterText, StringComparison.OrdinalIgnoreCase)
                                    || f.FormattedValue.Contains(_filterText, StringComparison.OrdinalIgnoreCase));

            foreach (var f in filtered)
                Fields.Add(f);

            FormatBadge = _panel.FormatInfo?.Name ?? string.Empty;
            StatusText  = Fields.Count > 0 ? $"{Fields.Count} field(s)" : "No fields parsed";
            NotifyCountChanged();
        }

        private void ApplyFilter() => Refresh();

        private void NotifyCountChanged()
        {
            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(IsNotEmpty));
        }

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
