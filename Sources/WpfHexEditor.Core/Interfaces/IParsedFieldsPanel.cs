//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Interface IParsedFieldsPanel + supporting types
// Decouples HexEditor Core from the concrete WindowPanels implementation
//////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using WpfHexEditor.Core.FormatDetection;
using WpfHexEditor.Core.ViewModels;

namespace WpfHexEditor.Core.Interfaces
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

        /// <summary>
        /// When true, programmatic candidate selection does not fire
        /// <see cref="FormatCandidateSelected"/>. Set by HexEditor during
        /// <c>ParseFieldsAsync</c> to avoid re-entrant RefreshParsedFields calls.
        /// </summary>
        bool SuppressFormatCandidateEvents { get; set; }

        event EventHandler<ParsedFieldViewModel> FieldSelected;
        event EventHandler RefreshRequested;
        event EventHandler<string> FormatterChanged;
        event EventHandler<FieldEditedEventArgs> FieldValueEdited;
        event EventHandler<FormatCandidateSelectedEventArgs> FormatCandidateSelected;

        void RefreshView();
        void Clear();
        void SetEnrichedFormat(WpfHexEditor.Core.FormatDetection.FormatDefinition? format);
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

        public bool HasReferences     => References != null &&
            (References.Specifications?.Count > 0 || References.WebLinks?.Count > 0);
        public bool HasSpecifications => References?.Specifications?.Count > 0;
        public bool HasWebLinks       => References?.WebLinks?.Count > 0;
        public bool HasBothReferences => HasSpecifications && HasWebLinks;

        public List<string>? Specifications => References?.Specifications;
        public List<string>? WebLinks       => References?.WebLinks;

        // ── Navigation bookmarks from whfmt navigation.bookmarks (C6) ──────────
        private ObservableCollection<FormatNavigationBookmark> _bookmarks;

        public ObservableCollection<FormatNavigationBookmark> Bookmarks
        {
            get => _bookmarks;
            set
            {
                _bookmarks = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasBookmarks));
            }
        }

        public bool HasBookmarks => _bookmarks?.Count > 0;

        // ── D3 — Forensic alerts from whfmt assertions ───────────────────────
        private List<AssertionResult> _forensicAlerts;

        /// <summary>
        /// Assertion results (failed or warning) collected during format detection.
        /// Displayed in the Forensic Alerts section of the ParsedFieldsPanel.
        /// </summary>
        public List<AssertionResult> ForensicAlerts
        {
            get => _forensicAlerts;
            set
            {
                _forensicAlerts = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasForensicAlerts));
                OnPropertyChanged(nameof(ForensicAlertCount));
            }
        }

        public bool HasForensicAlerts => _forensicAlerts?.Count > 0;
        public int ForensicAlertCount => _forensicAlerts?.Count ?? 0;

        // ── D4 — Inspector groups from whfmt inspector.groups ──────────────
        private List<InspectorGroupItem> _inspectorGroups;

        public List<InspectorGroupItem> InspectorGroups
        {
            get => _inspectorGroups;
            set
            {
                _inspectorGroups = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasInspectorGroups));
            }
        }

        public bool HasInspectorGroups => _inspectorGroups?.Count > 0;

        private string _inspectorBadge;
        public string InspectorBadge
        {
            get => _inspectorBadge;
            set { _inspectorBadge = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasInspectorBadge)); }
        }
        public bool HasInspectorBadge => !string.IsNullOrEmpty(_inspectorBadge);

        // ── D5 — Export templates from whfmt exportTemplates ───────────────
        private List<ExportTemplateItem> _exportTemplates;

        public List<ExportTemplateItem> ExportTemplates
        {
            get => _exportTemplates;
            set
            {
                _exportTemplates = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasExportTemplates));
            }
        }

        public bool HasExportTemplates => _exportTemplates?.Count > 0;

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// Lightweight wrapper for displaying a format candidate in a ComboBox.
    /// </summary>
    public class FormatCandidateItem
    {
        public string DisplayName { get; set; }
        public FormatMatchCandidate Candidate { get; set; }
        public override string ToString() => DisplayName;
    }

    /// <summary>
    /// Event args when the user selects a different format candidate.
    /// </summary>
    public class FormatCandidateSelectedEventArgs : EventArgs
    {
        public FormatMatchCandidate Candidate { get; }
        public FormatCandidateSelectedEventArgs(FormatMatchCandidate candidate) => Candidate = candidate;
    }

    /// <summary>
    /// A navigation bookmark from whfmt navigation.bookmarks (C6).
    /// </summary>
    public class FormatNavigationBookmark
    {
        public string Name        { get; set; }
        public long   Offset      { get; set; }
        public string Icon        { get; set; }
        public string Color       { get; set; }
        public string Description { get; set; }
        public override string ToString() => Name;
    }

    /// <summary>
    /// Event args when the user edits a field value in the panel.
    /// </summary>
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

    /// <summary>
    /// D4 — A collapsible group of fields for the Inspector section.
    /// Built from whfmt inspector.groups.
    /// </summary>
    public class InspectorGroupItem : INotifyPropertyChanged
    {
        private bool _isExpanded = true;

        public string Title      { get; set; }
        public string Icon       { get; set; }
        public bool   Highlight  { get; set; }
        public List<InspectorFieldItem> Fields { get; set; } = new();

        public bool IsExpanded
        {
            get => _isExpanded;
            set { _isExpanded = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsExpanded))); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }

    /// <summary>
    /// D4 — A single field within an inspector group.
    /// </summary>
    public class InspectorFieldItem
    {
        public string Name         { get; set; }
        public string DisplayValue { get; set; }
        public override string ToString() => $"{Name}: {DisplayValue}";
    }

    /// <summary>
    /// D5 — An export template available for the active format.
    /// Built from whfmt exportTemplates.
    /// </summary>
    public class ExportTemplateItem
    {
        public string Name   { get; set; }
        public string Format { get; set; }
        public string Icon   => Format switch
        {
            "json"         => "\uE943",
            "csv"          => "\uE8A5",
            "c-struct"     => "\uE943",
            "python-bytes" => "\uE943",
            "xml"          => "\uE943",
            _              => "\uE8A5"
        };
        public FormatDetection.ExportTemplate Source { get; set; }
        public override string ToString() => Name;
    }

    /// <summary>
    /// Event args for breadcrumb enrichment. The HexEditor fires this event
    /// so plugins (e.g. ParsedFieldsPlugin) can enrich the breadcrumb segments
    /// with richer data (GroupName-based hierarchy). Without any subscriber,
    /// the breadcrumb works standalone using CustomBackgroundBlock data.
    /// </summary>
    public class BreadcrumbEnrichEventArgs : EventArgs
    {
        /// <summary>Current cursor offset in the file.</summary>
        public long Offset { get; init; }

        /// <summary>
        /// Base segments built from CustomBackgroundBlock data.
        /// Subscribers can replace or enrich this list.
        /// </summary>
        public List<BreadcrumbSegmentData> Segments { get; set; } = new();

        /// <summary>Whether a subscriber has enriched the segments.</summary>
        public bool IsEnriched { get; set; }
    }

    /// <summary>
    /// Portable breadcrumb segment data (Core-level, no WPF dependency).
    /// </summary>
    public class BreadcrumbSegmentData
    {
        public string Name { get; set; } = "";
        public long Offset { get; set; }
        public int Length { get; set; }
        public bool IsGroup { get; set; }
        public bool IsFormat { get; set; }
        public string? Color { get; set; }
        public List<BreadcrumbSegmentData>? Siblings { get; set; }
    }
}
