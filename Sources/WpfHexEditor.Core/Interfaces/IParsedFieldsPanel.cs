//////////////////////////////////////////////
// Apache 2.0  - 2026
// Interface IParsedFieldsPanel + supporting types
// Decouples HexEditor Core from the concrete WindowPanels implementation
//////////////////////////////////////////////

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using WpfHexaEditor.Core.FormatDetection;
using WpfHexaEditor.ViewModels;

namespace WpfHexaEditor.Interfaces
{
    /// <summary>
    /// Contract for a panel that displays parsed format fields.
    /// HexEditor references this interface so that Core never depends on WindowPanels.
    /// </summary>
    public interface IParsedFieldsPanel
    {
        ObservableCollection<ParsedFieldViewModel> ParsedFields { get; }
        FormatInfo FormatInfo { get; set; }
        long TotalFileSize { get; set; }

        event EventHandler<ParsedFieldViewModel> FieldSelected;
        event EventHandler RefreshRequested;
        event EventHandler<string> FormatterChanged;
        event EventHandler<FieldEditedEventArgs> FieldValueEdited;
        event EventHandler<FormatCandidateSelectedEventArgs> FormatCandidateSelected;

        void RefreshView();
        void Clear();
        void SetEnrichedFormat(WpfHexaEditor.Core.FormatDetection.FormatDefinition format);
    }

    /// <summary>
    /// Format detection info shown in the panel header.
    /// Moved to Core so that HexEditor can populate it directly.
    /// </summary>
    public class FormatInfo : INotifyPropertyChanged
    {
        private bool _isDetected;
        private string _name;
        private string _description;
        private string _category;
        private ObservableCollection<FormatCandidateItem> _candidates;
        private FormatCandidateItem _selectedCandidate;
        private bool _isUpdatingSelection;
        private FormatReferences _references;

        public bool IsDetected
        {
            get => _isDetected;
            set { _isDetected = value; OnPropertyChanged(); }
        }

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public string Description
        {
            get => _description;
            set { _description = value; OnPropertyChanged(); }
        }

        public string Category
        {
            get => _category;
            set { _category = value; OnPropertyChanged(); }
        }

        public ObservableCollection<FormatCandidateItem> Candidates
        {
            get => _candidates;
            set
            {
                _candidates = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasMultipleCandidates));
            }
        }

        public FormatCandidateItem SelectedCandidate
        {
            get => _selectedCandidate;
            set
            {
                if (_isUpdatingSelection) return;
                _selectedCandidate = value;
                OnPropertyChanged();
            }
        }

        public bool HasMultipleCandidates => _candidates?.Count > 1;

        public void SetSelectedCandidateSilently(FormatCandidateItem item)
        {
            _isUpdatingSelection = true;
            _selectedCandidate = item;
            OnPropertyChanged(nameof(SelectedCandidate));
            _isUpdatingSelection = false;
        }

        public FormatReferences References
        {
            get => _references;
            set
            {
                _references = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasReferences));
            }
        }

        public bool HasReferences => References != null &&
            (References.Specifications?.Count > 0 || References.WebLinks?.Count > 0);

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>Lightweight wrapper for displaying a format candidate in a ComboBox.</summary>
    public class FormatCandidateItem
    {
        public string DisplayName { get; set; }
        public FormatMatchCandidate Candidate { get; set; }
        public override string ToString() => DisplayName;
    }

    /// <summary>Event args when the user selects a different format candidate.</summary>
    public class FormatCandidateSelectedEventArgs : EventArgs
    {
        public FormatMatchCandidate Candidate { get; }
        public FormatCandidateSelectedEventArgs(FormatMatchCandidate candidate) => Candidate = candidate;
    }

    /// <summary>Event args when the user edits a field value in the panel.</summary>
    public class FieldEditedEventArgs : EventArgs
    {
        public ParsedFieldViewModel Field { get; }
        public byte[] NewBytes { get; }
        public FieldEditedEventArgs(ParsedFieldViewModel field, byte[] newBytes)
        {
            Field = field;
            NewBytes = newBytes;
        }
    }
}
