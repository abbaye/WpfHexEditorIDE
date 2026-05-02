//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5, Claude Sonnet 4.6
//////////////////////////////////////////////

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using WpfHexEditor.Core.FormatDetection;
using WpfHexEditor.Core.Interfaces;
using WpfHexEditor.Core.ViewModels;
using WpfHexEditor.Plugins.ParsedFields.Dialogs;
using WpfHexEditor.Plugins.ParsedFields.Properties;
using WpfHexEditor.SDK.UI;

namespace WpfHexEditor.Plugins.ParsedFields.Views
{
    /// <summary>
    /// Panel for displaying parsed fields from format definitions
    /// Shows field names, values, offsets, and descriptions
    /// </summary>
    public partial class ParsedFieldsPanel : UserControl, INotifyPropertyChanged, IParsedFieldsPanel
    {
        private ObservableCollection<ParsedFieldViewModel> _parsedFields;
        private ObservableCollection<ParsedFieldViewModel> _filteredFields;
        private ObservableCollection<ParsedFieldViewModel> _metadataFields;
        private ObservableCollection<InsightChip> _insightChips;
        private ObservableCollection<ParsedFieldViewModel> _treeRootItems;
        private FormatInfo _formatInfo;
        private string _searchText;
        private bool _showBookmarksOnly;
        private string _searchResultText;
        private bool _hasSelection;
        private int _sortMode = 0; // 0=offset, 1=nameAZ, 2=nameZA, 3=type, 4=size
        private long _totalFileSize;
        private ToolbarOverflowManager _overflowManager = null!;
        private readonly EnrichedFormatViewModel _enrichedVm = new();
        private bool _suppressFilter; // set by BeginBulkUpdate/EndBulkUpdate

        // ── Source selector state ─────────────────────────────────────────
        private string? _pinnedFilePath;  // null = Auto mode
        private Func<IReadOnlyList<(string FilePath, string Title)>>? _openDocumentsProvider;

        public ParsedFieldsPanel()
        {
            InitializeComponent();
            DataContext = this;
            // Initialize collections FIRST to avoid NullReferenceException in ApplyFilter()
            FilteredFields    = new ObservableCollection<ParsedFieldViewModel>();
            MetadataFields    = new ObservableCollection<ParsedFieldViewModel>();
            InsightChips      = new ObservableCollection<InsightChip>();
            ParsedFields      = new ObservableCollection<ParsedFieldViewModel>();
            TreeRootItems     = new ObservableCollection<ParsedFieldViewModel>();

            // Set up CollectionView grouping for section headers (C3)
            var view = CollectionViewSource.GetDefaultView(FilteredFields);
            view?.GroupDescriptions.Add(new PropertyGroupDescription("GroupName"));

            // Subscribe to collection changes to automatically update filtered view.
            // Guard: skip during bulk field population (BeginBulkUpdate/EndBulkUpdate).
            ParsedFields.CollectionChanged += (s, e) => { if (!_suppressFilter) ApplyFilter(); };

            FormatInfo = new FormatInfo();

            Loaded += (_, _) =>
            {
                _overflowManager = new ToolbarOverflowManager(
                    toolbarContainer:      ToolbarBorder,
                    alwaysVisiblePanel:    ToolbarRightPanel,
                    overflowButton:        ToolbarOverflowButton,
                    overflowMenu:          OverflowContextMenu,
                    groupsInCollapseOrder: new FrameworkElement[]
                    {
                        TbgExport,   // [0] first to collapse
                        TbgActions,  // [1] second to collapse
                        TbgSource,   // [2] last to collapse (always useful)
                    },
                    leftFixedElements: new FrameworkElement[] { RefreshButton });
                Dispatcher.InvokeAsync(_overflowManager.CaptureNaturalWidths, DispatcherPriority.Loaded);
                RebuildExportTemplatesInMenu();
            };
        }

        /// <summary>
        /// ViewModel for the embedded "Enriched Format Metadata" expander section.
        /// Bound in XAML via RelativeSource; populated by SetEnrichedFormat().
        /// </summary>
        public EnrichedFormatViewModel EnrichedFormat => _enrichedVm;

        /// <summary>
        /// Collection of parsed fields to display
        /// </summary>
        public ObservableCollection<ParsedFieldViewModel> ParsedFields
        {
            get => _parsedFields;
            set
            {
                _parsedFields = value;
                OnPropertyChanged();
                ApplyFilter(); // Refilter when fields change
            }
        }

        /// <summary>
        /// Filtered collection for display
        /// </summary>
        public ObservableCollection<ParsedFieldViewModel> FilteredFields
        {
            get => _filteredFields;
            private set
            {
                _filteredFields = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Metadata fields separated for spotlight display (C1)
        /// </summary>
        public ObservableCollection<ParsedFieldViewModel> MetadataFields
        {
            get => _metadataFields;
            private set
            {
                _metadataFields = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Key insight chips for at-a-glance summary (C4)
        /// </summary>
        public ObservableCollection<InsightChip> InsightChips
        {
            get => _insightChips;
            private set
            {
                _insightChips = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Root items for the TreeView â€” built from FilteredFields with group hierarchy (C1).
        /// </summary>
        public ObservableCollection<ParsedFieldViewModel> TreeRootItems
        {
            get => _treeRootItems;
            private set
            {
                _treeRootItems = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Total file size in bytes for coverage bar calculation (C6)
        /// </summary>
        public long TotalFileSize
        {
            get => _totalFileSize;
            set
            {
                _totalFileSize = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Search/filter text
        /// </summary>
        public string SearchText
        {
            get => _searchText;
            set
            {
                _searchText = value;
                OnPropertyChanged();
                ApplyFilter();
            }
        }

        /// <summary>
        /// Search result text (e.g., "- 3 matches")
        /// </summary>
        public string SearchResultText
        {
            get => _searchResultText;
            private set
            {
                _searchResultText = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Indicates whether a field is currently selected
        /// </summary>
        public bool HasSelection
        {
            get => _hasSelection;
            private set
            {
                _hasSelection = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Information about the detected format
        /// </summary>
        public FormatInfo FormatInfo
        {
            get => _formatInfo;
            set
            {
                _formatInfo = value;
                OnPropertyChanged();
                RebuildExportTemplatesInMenu();
            }
        }

        /// <summary>
        /// Event fired when a field is selected
        /// </summary>
        public event EventHandler<ParsedFieldViewModel> FieldSelected;

        /// <summary>
        /// Event raised when the user clicks a navigation bookmark chip. (C6)
        /// The long value is the absolute byte offset to navigate to.
        /// </summary>
        public event EventHandler<long> NavigateToOffsetRequested;

        /// <summary>
        /// Event fired when the refresh button is clicked
        /// </summary>
        public event EventHandler RefreshRequested;

        /// <summary>
        /// Fired when the user selects a specific source document (filePath) or null for Auto mode.
        /// </summary>
        public event EventHandler<string?> SourceDocumentSelected;

        // GroupName used for enriched format fields injected into the main fields list
        private const string EnrichedGroupName = "Format Metadata";

        /// <summary>
        /// Injects enriched format metadata fields directly into ParsedFields so they appear
        /// as a "Format Metadata" group inside the main fields ListBox.
        /// </summary>
        public void SetEnrichedFormat(FormatDefinition? format)
        {
            _enrichedVm.CurrentFormat = format;
            RemoveEnrichedFields();
            if (format != null)
                InjectEnrichedFields();
        }

        /// <summary>
        /// Removes all injected enriched fields and clears the VM.
        /// </summary>
        public void ClearEnrichedFormat()
        {
            _enrichedVm.CurrentFormat = null;
            RemoveEnrichedFields();
        }

        /// <summary>
        /// Removes previously injected enriched fields from ParsedFields.
        /// </summary>
        private void RemoveEnrichedFields()
        {
            var toRemove = ParsedFields
                .Where(f => string.Equals(f.ValueType, "enriched",
                                          StringComparison.OrdinalIgnoreCase))
                .ToList();
            foreach (var f in toRemove)
                ParsedFields.Remove(f);
        }

        /// <summary>
        /// Creates flat ParsedFieldViewModel entries from EnrichedFormatViewModel and inserts
        /// them at the start of ParsedFields so they form a "Format Metadata" group.
        /// </summary>
        private void InjectEnrichedFields()
        {
            var fields = new System.Collections.Generic.List<ParsedFieldViewModel>();

            void Add(string name, string? value, string icon = "â„¹", string? desc = null)
            {
                if (string.IsNullOrWhiteSpace(value) || value == "N/A") return;
                fields.Add(new ParsedFieldViewModel
                {
                    Name           = name,
                    FormattedValue = value,
                    ValueType      = "enriched",
                    Offset         = -2,   // Distinct from metadata (-1)
                    Length         = 0,
                    FieldIcon      = icon,
                    GroupName      = EnrichedGroupName,
                    Description    = desc ?? string.Empty,
                    IsValid        = true
                });
            }

            Add("Category",      _enrichedVm.FormatCategory,          "ðŸ·");
            Add("MIME Types",    _enrichedVm.MimeTypesDisplay,         "ðŸ“„");
            Add("Extensions",    _enrichedVm.ExtensionsDisplay,        "ðŸ”–");
            Add("Software",      _enrichedVm.SoftwareDisplay,          "ðŸ’¾");
            Add("Use Cases",     _enrichedVm.UseCasesDisplay,          "ðŸ“‹");
            Add("Doc Level",     _enrichedVm.DocumentationLevel,       "ðŸ“š");
            Add("Quality",       _enrichedVm.CompletenessScoreDisplay, "â­");
            if (_enrichedVm.HasDetectionInfo)
                Add("Signature", _enrichedVm.SignatureHex,             "ðŸ”", "Magic bytes");
            if (_enrichedVm.HasRelatedFormats)
                Add("Related",   _enrichedVm.RelatedFormatsDisplay,    "ðŸ”—");
            if (_enrichedVm.HasTechnicalDetails)
                Add("Technical", _enrichedVm.TechnicalSummary,         "âš™");
            if (_enrichedVm.HasSpecifications)
                Add("Specs",      _enrichedVm.SpecificationsDisplay,                           "ðŸ“‘");
            if (_enrichedVm.HasWebLinks)
                Add("References", string.Join(" Â· ", _enrichedVm.WebLinks.Select(u =>
                {
                    if (Uri.TryCreate(u, UriKind.Absolute, out var uri)) return uri.Host;
                    return u.Length > 40 ? u[..37] + "â€¦" : u;
                })), "ðŸŒ");

            // Insert in reverse so they stay in declaration order at the top
            for (int i = fields.Count - 1; i >= 0; i--)
                ParsedFields.Insert(0, fields[i]);
        }

        private void OnEnrichedLinkNavigate(object sender,
            System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    { FileName = e.Uri.AbsoluteUri, UseShellExecute = true });
            }
            catch { /* silently ignore */ }
            e.Handled = true;
        }

        /// <summary>
        /// Event fired when the formatter selection changes
        /// </summary>
        public event EventHandler<string> FormatterChanged;

        /// <summary>
        /// Event fired when a field value is edited
        /// </summary>
        public event EventHandler<FieldEditedEventArgs> FieldValueEdited;

        /// <summary>
        /// Raised when user selects a different format candidate from the dropdown
        /// </summary>
        public event EventHandler<FormatCandidateSelectedEventArgs> FormatCandidateSelected;

        /// <summary>
        /// When true, <see cref="FormatCandidateCombo_SelectionChanged"/> does not fire
        /// <see cref="FormatCandidateSelected"/>. Set by the HexEditor during programmatic
        /// candidate selection to avoid re-entrant RefreshParsedFields calls.
        /// </summary>
        public bool SuppressFormatCandidateEvents { get; set; }

        private void FormatCandidateCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SuppressFormatCandidateEvents) return;
            if (FormatInfo?.SelectedCandidate?.Candidate != null && e.AddedItems.Count > 0)
            {
                FormatCandidateSelected?.Invoke(this,
                    new FormatCandidateSelectedEventArgs(FormatInfo.SelectedCandidate.Candidate));
            }
        }

        private void FieldsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            HasSelection = FieldsListBox.SelectedItem != null;

            if (FieldsListBox.SelectedItem is ParsedFieldViewModel field)
            {
                FieldSelected?.Invoke(this, field);
            }
        }

        /// <summary>
        /// TreeView selection handler.  Syncs the hidden FieldsListBox.SelectedItem so all
        /// existing context-menu click handlers continue to work without changes. (C1)
        /// </summary>
        private void FieldsTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is ParsedFieldViewModel field && !field.IsGroup)
            {
                // Sync hidden ListBox â†’ all context menu handlers keep working unchanged
                FieldsListBox.SelectedItem = field;
                HasSelection = true;
                FieldSelected?.Invoke(this, field);
            }
            else
            {
                FieldsListBox.SelectedItem = null;
                HasSelection = e.NewValue != null;
            }
        }

        private void FieldsTreeView_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (FieldsTreeView.SelectedItem is not ParsedFieldViewModel field || field.IsGroup)
                return;

            switch (e.Key)
            {
                case System.Windows.Input.Key.Enter:
                    EditField(field);
                    e.Handled = true;
                    break;
                case System.Windows.Input.Key.C when
                    (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) != 0:
                    System.Windows.Clipboard.SetText(field.ActiveFormattedValue ?? string.Empty);
                    e.Handled = true;
                    break;
            }
        }

        private void FieldsListBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (FieldsListBox.SelectedItem is not ParsedFieldViewModel field)
                return;

            switch (e.Key)
            {
                case System.Windows.Input.Key.Enter:
                    // Enter key opens edit dialog
                    EditField(field);
                    e.Handled = true;
                    break;

                case System.Windows.Input.Key.C when e.KeyboardDevice.Modifiers == System.Windows.Input.ModifierKeys.Control:
                    // Ctrl+C copies field value
                    CopyFieldValue(field);
                    e.Handled = true;
                    break;

                case System.Windows.Input.Key.Space:
                    // Space toggles bookmark
                    field.IsBookmarked = !field.IsBookmarked;
                    e.Handled = true;
                    break;

                case System.Windows.Input.Key.Up when e.KeyboardDevice.Modifiers == System.Windows.Input.ModifierKeys.Control:
                    // Ctrl+Up navigates to previous field
                    NavigateToPreviousField();
                    e.Handled = true;
                    break;

                case System.Windows.Input.Key.Down when e.KeyboardDevice.Modifiers == System.Windows.Input.ModifierKeys.Control:
                    // Ctrl+Down navigates to next field
                    NavigateToNextField();
                    e.Handled = true;
                    break;

                case System.Windows.Input.Key.Home:
                    // Home key jumps to first field
                    NavigateToFirstField();
                    e.Handled = true;
                    break;

                case System.Windows.Input.Key.End:
                    // End key jumps to last field
                    NavigateToLastField();
                    e.Handled = true;
                    break;
            }
        }

        /// <summary>
        /// Navigate to the previous field in the list
        /// </summary>
        private void NavigateToPreviousField()
        {
            var currentIndex = FieldsListBox.SelectedIndex;
            if (currentIndex > 0)
            {
                FieldsListBox.SelectedIndex = currentIndex - 1;
                FieldsListBox.ScrollIntoView(FieldsListBox.SelectedItem);
            }
        }

        /// <summary>
        /// Navigate to the next field in the list
        /// </summary>
        private void NavigateToNextField()
        {
            var currentIndex = FieldsListBox.SelectedIndex;
            if (currentIndex < FilteredFields.Count - 1)
            {
                FieldsListBox.SelectedIndex = currentIndex + 1;
                FieldsListBox.ScrollIntoView(FieldsListBox.SelectedItem);
            }
        }

        /// <summary>
        /// Navigate to the first field in the list
        /// </summary>
        private void NavigateToFirstField()
        {
            if (FilteredFields.Count > 0)
            {
                FieldsListBox.SelectedIndex = 0;
                FieldsListBox.ScrollIntoView(FieldsListBox.SelectedItem);
            }
        }

        /// <summary>
        /// Navigate to the last field in the list
        /// </summary>
        private void NavigateToLastField()
        {
            if (FilteredFields.Count > 0)
            {
                FieldsListBox.SelectedIndex = FilteredFields.Count - 1;
                FieldsListBox.ScrollIntoView(FieldsListBox.SelectedItem);
            }
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshRequested?.Invoke(this, EventArgs.Empty);
        }

        // ── Source selector ───────────────────────────────────────────────

        /// <summary>
        /// Registers the callback the plugin uses to supply the current open-document list.
        /// Called once during plugin initialization.
        /// </summary>
        public void SetOpenDocumentsProvider(Func<IReadOnlyList<(string FilePath, string Title)>> provider)
            => _openDocumentsProvider = provider;

        /// <summary>
        /// Notifies the panel that a pinned document was closed — revert to Auto.
        /// Called by the plugin when DocumentUnregistered fires for the pinned path.
        /// </summary>
        public void NotifyPinnedDocumentClosed(string filePath)
        {
            if (!string.Equals(filePath, _pinnedFilePath, StringComparison.OrdinalIgnoreCase)) return;
            SetSourceAuto();
        }

        private void SetSourceAuto()
        {
            _pinnedFilePath = null;
            SourceMenuIcon.Text  = "\uE72C"; // sync icon
            SourceMenuLabel.Text = ParsedFieldsResources.ParsedFields_Label_AutoMode + " \u25BE";
        }

        private void SetSourcePinned(string filePath, string title)
        {
            _pinnedFilePath = filePath;
            SourceMenuIcon.Text  = "\uE718"; // pin icon
            var shortName = System.IO.Path.GetFileName(filePath);
            SourceMenuLabel.Text = $"{shortName} \u25BE";
        }

        private void SourceMenuButton_Click(object sender, RoutedEventArgs e)
        {
            SourceContextMenu.PlacementTarget = SourceMenuButton;
            SourceContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            SourceContextMenu.IsOpen = true;
        }

        private void SourceContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            SourceContextMenu.Items.Clear();

            // Auto item
            var autoItem = new MenuItem
            {
                Header     = "Auto (sync with active document)",
                Icon       = new TextBlock { Text = "\uE72C", FontFamily = new System.Windows.Media.FontFamily("Segoe MDL2 Assets"), FontSize = 11 },
                IsChecked  = _pinnedFilePath == null,
                FontWeight = _pinnedFilePath == null ? FontWeights.SemiBold : FontWeights.Normal,
            };
            autoItem.Click += (_, _) =>
            {
                SetSourceAuto();
                SourceDocumentSelected?.Invoke(this, null);
            };
            SourceContextMenu.Items.Add(autoItem);

            // Open documents list
            var docs = _openDocumentsProvider?.Invoke();
            if (docs != null && docs.Count > 0)
            {
                SourceContextMenu.Items.Add(new Separator());
                foreach (var (filePath, title) in docs)
                {
                    var fp    = filePath;
                    var isPinned = string.Equals(fp, _pinnedFilePath, StringComparison.OrdinalIgnoreCase);
                    var item  = new MenuItem
                    {
                        Header     = title,
                        ToolTip    = fp,
                        IsChecked  = isPinned,
                        FontWeight = isPinned ? FontWeights.SemiBold : FontWeights.Normal,
                    };
                    item.Click += (_, _) =>
                    {
                        SetSourcePinned(fp, title);
                        SourceDocumentSelected?.Invoke(this, fp);
                    };
                    SourceContextMenu.Items.Add(item);
                }
            }
        }

        /// <summary>
        /// Open Actions context menu
        /// </summary>
        private void ActionsMenuButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.ContextMenu != null)
            {
                button.ContextMenu.PlacementTarget = button;
                button.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
                button.ContextMenu.IsOpen = true;
            }
        }

        /// <summary>
        /// Open Export context menu
        /// </summary>
        private void ExportMenuButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.ContextMenu != null)
            {
                button.ContextMenu.PlacementTarget = button;
                button.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
                button.ContextMenu.IsOpen = true;
            }
        }

        private void QuickNavigate_Click(object sender, RoutedEventArgs e)
        {
            // Trigger selection changed to navigate to field position
            if (FieldsListBox.SelectedItem is ParsedFieldViewModel field)
            {
                FieldSelected?.Invoke(this, field);
            }
        }

        private void FormatterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FormatterComboBox.SelectedItem is ComboBoxItem item && item.Tag is string tag)
            {
                FormatterChanged?.Invoke(this, tag);
            }
        }

        private void ClearSearchButton_Click(object sender, RoutedEventArgs e)
        {
            SearchText = string.Empty;
        }

        private void SearchOption_Changed(object sender, RoutedEventArgs e)
        {
            // Trigger filter refresh when search options change
            ApplyFilter();
        }

        private void SortComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _sortMode = SortComboBox?.SelectedIndex ?? 0;
            ApplyFilter();
        }

        private void InvalidBadge_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // Click on invalid badge to scroll to first invalid field
            var firstInvalid = FilteredFields?.FirstOrDefault(f => !f.IsValid);
            if (firstInvalid != null)
            {
                FieldsListBox.SelectedItem = firstInvalid;
                FieldsListBox.ScrollIntoView(firstInvalid);
            }
        }

        private void FieldItem_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // Works for both ListBoxItem (legacy) and TreeViewItem (C1 tree view)
            ParsedFieldViewModel field = sender switch
            {
                System.Windows.Controls.ListBoxItem li => li.Content as ParsedFieldViewModel,
                System.Windows.Controls.TreeViewItem tvi => tvi.DataContext as ParsedFieldViewModel,
                _ => null
            };

            if (field != null && !field.IsGroup)
            {
                EditField(field);
                e.Handled = true;
            }
        }

        private void EditValue_Click(object sender, RoutedEventArgs e)
        {
            if (FieldsListBox.SelectedItem is ParsedFieldViewModel field)
            {
                EditField(field);
            }
        }

        /// <summary>
        /// Opens the edit dialog for a field
        /// </summary>
        private void EditField(ParsedFieldViewModel field)
        {
            if (field == null)
                return;

            if (!field.IsEditable)
            {
                System.Windows.MessageBox.Show(ParsedFieldsResources.ParsedFields_Error_FieldNotEditable, "Edit Field",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                return;
            }

            // Show edit dialog
            var dialog = new FieldEditDialog(field);
            if (dialog.ShowDialog() == true)
            {
                var newValue = dialog.EditedValue;
                var bytes = field.TryParseEditedValue(newValue);

                if (bytes == null)
                {
                    System.Windows.MessageBox.Show(
                        ParsedFieldsResources.ParsedFields_Error_InvalidValue + $"\n\nType: {field.ValueType}",
                        "Invalid Value", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                    return;
                }

                // Raise event for the HexEditor to handle the actual write
                FieldValueEdited?.Invoke(this, new FieldEditedEventArgs(field, bytes));
            }
        }

        private void CopyValue_Click(object sender, RoutedEventArgs e)
        {
            if (FieldsListBox.SelectedItem is ParsedFieldViewModel field)
            {
                CopyFieldValue(field);
            }
        }

        private void CopyDetails_Click(object sender, RoutedEventArgs e)
        {
            if (FieldsListBox.SelectedItem is ParsedFieldViewModel field)
            {
                CopyFieldDetails(field);
            }
        }

        private void ExportAll_Click(object sender, RoutedEventArgs e)
        {
            // This will be handled by submenu items
        }

        /// <summary>Copy selected field value as C# byte array literal. (C3)</summary>
        private void CopyValueCSharpBytes_Click(object sender, RoutedEventArgs e)
        {
            if (FieldsListBox.SelectedItem is not ParsedFieldViewModel field) return;
            var raw = field.RawValue as byte[];
            if (raw == null && field.RawValue is byte b) raw = new[] { b };
            if (raw == null) { System.Windows.Clipboard.SetText(field.ActiveFormattedValue ?? ""); return; }
            var literal = "new byte[] { " + string.Join(", ", raw.Select(x => $"0x{x:X2}")) + " }";
            System.Windows.Clipboard.SetText(literal);
        }

        /// <summary>Copy selected field value as Python bytes literal. (C3)</summary>
        private void CopyValuePythonBytes_Click(object sender, RoutedEventArgs e)
        {
            if (FieldsListBox.SelectedItem is not ParsedFieldViewModel field) return;
            var raw = field.RawValue as byte[];
            if (raw == null && field.RawValue is byte b) raw = new[] { b };
            if (raw == null) { System.Windows.Clipboard.SetText(field.ActiveFormattedValue ?? ""); return; }
            var literal = "b'" + string.Concat(raw.Select(x => $"\\x{x:x2}")) + "'";
            System.Windows.Clipboard.SetText(literal);
        }

        /// <summary>
        /// Toggle the References info popup on info button click.
        /// </summary>
        private void ReferencesInfoButton_Click(object sender, RoutedEventArgs e)
        {
            ReferencesPopup.IsOpen = !ReferencesPopup.IsOpen;
        }

        private void FormatInfoCollapseButton_Click(object sender, RoutedEventArgs e)
        {
            bool isVisible = FormatInfoScrollViewer.Visibility == Visibility.Visible;
            FormatInfoScrollViewer.Visibility = isVisible ? Visibility.Collapsed : Visibility.Visible;
            FormatInfoCollapseButton.Content = isVisible ? "\uE974" : "\uE972";
        }

        private void RebuildExportTemplatesInMenu()
        {
            if (ExportContextMenu == null) return;

            var toRemove = new System.Collections.Generic.List<object>();
            foreach (var item in ExportContextMenu.Items)
            {
                if (item is FrameworkElement fe && fe.Tag is string t && t == "whfmt-template")
                    toRemove.Add(item);
            }
            foreach (var item in toRemove)
                ExportContextMenu.Items.Remove(item);

            var templates = FormatInfo?.ExportTemplates;
            if (templates == null || templates.Count == 0) return;

            ExportContextMenu.Items.Add(new Separator { Tag = "whfmt-template" });

            foreach (var tmpl in templates)
            {
                var mi = new MenuItem { Header = tmpl.Name, Tag = "whfmt-template" };
                if (!string.IsNullOrEmpty(tmpl.Icon))
                    mi.Icon = new TextBlock { Text = tmpl.Icon, FontSize = 11 };
                var captured = tmpl;
                mi.Click += (s, ev) => ExportTemplate_Click_Execute(captured);
                ExportContextMenu.Items.Add(mi);
            }
        }

        private void ExportTemplate_Click_Execute(WpfHexEditor.Core.Interfaces.ExportTemplateItem template)
        {
            var fields = ParsedFields;
            if (fields == null || fields.Count == 0) return;

            var sb = new System.Text.StringBuilder();
            string extension;
            if (template.Format == "json")       extension = "json";
            else if (template.Format == "csv")   extension = "csv";
            else if (template.Format == "c-struct") extension = "h";
            else if (template.Format == "python-bytes") extension = "py";
            else if (template.Format == "xml")   extension = "xml";
            else                                 extension = "txt";

            if (template.Format == "json")
            {
                sb.AppendLine("{");
                for (int i = 0; i < fields.Count; i++)
                {
                    var f = fields[i];
                    var comma = i < fields.Count - 1 ? "," : "";
                    sb.AppendLine("  \"" + f.Name + "\": \"" + f.ActiveFormattedValue + "\"" + comma);
                }
                sb.AppendLine("}");
            }
            else if (template.Format == "csv")
            {
                sb.AppendLine("Name,Offset,Length,Value");
                foreach (var f in fields)
                    sb.AppendLine("\"" + f.Name + "\"," + f.Offset + "," + f.Length + ",\"" + f.ActiveFormattedValue + "\"");
            }
            else
            {
                foreach (var f in fields)
                    sb.AppendLine(f.Name + ": " + f.ActiveFormattedValue);
            }

            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Filter = template.Format.ToUpper() + " files (*." + extension + ")|*." + extension + "|All files (*.*)|*.*",
                FileName = "Export_" + (FormatInfo?.Name?.Replace(" ", "_") ?? "Format") + "." + extension
            };
            if (dlg.ShowDialog() == true)
                System.IO.File.WriteAllText(dlg.FileName, sb.ToString());
        }

        /// <summary>
        /// Open a web link from the References popup in the default browser.
        /// </summary>
        private void ReferenceLink_Click(object sender, RoutedEventArgs e)
        {
            var url = sender is System.Windows.Controls.Button btn ? btn.Tag as string : null;
            if (!string.IsNullOrEmpty(url) && url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url)
                {
                    UseShellExecute = true
                });
            }
        }

        /// <summary>
        /// Navigate to the offset associated with a bookmark chip click. (C6)
        /// </summary>
        private void NavigatorBookmark_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn &&
                btn.Tag is WpfHexEditor.Core.Interfaces.FormatNavigationBookmark bookmark)
            {
                NavigateToOffsetRequested?.Invoke(this, bookmark.Offset);
            }
        }

        // ── D6 â€” AI Analysis handlers ─────────────────────────────────────────

        private void AiSectionToggleButton_Click(object sender, RoutedEventArgs e)
        {
            if (FormatInfo != null)
                FormatInfo.IsAiSectionExpanded = !FormatInfo.IsAiSectionExpanded;
        }

        private void CopyAiPromptButton_Click(object sender, RoutedEventArgs e)
        {
            var h = FormatInfo?.AiHintsData;
            if (h == null) return;

            var insp  = h.Inspections?.Select(i => i.Text) ?? Enumerable.Empty<string>();
            var vulns = h.Vulnerabilities?.Select(v => v.Text) ?? Enumerable.Empty<string>();
            var prompt = $"Analyze this {FormatInfo.Name} file. " +
                         $"Context: {h.AnalysisContext}. " +
                         $"Check for: {string.Join(", ", insp)}. " +
                         $"Known risks: {string.Join(", ", vulns)}.";
            try { Clipboard.SetText(prompt); } catch { return; }
            ShowCopiedFeedback(CopyAiPromptLabel, "Copy AI Prompt", "Copied!");
        }

        private void CopyChecklistButton_Click(object sender, RoutedEventArgs e)
        {
            var items = FormatInfo?.AiInspections;
            if (items == null) return;

            var text = string.Join(Environment.NewLine,
                items.Where(i => !i.IsChecked).Select(i => $"- {i.Text}"));
            if (string.IsNullOrEmpty(text)) return;
            try { Clipboard.SetText(text); } catch { return; }
            ShowCopiedFeedback(CopyChecklistLabel, "Copy Checklist", "Copied!");
        }

        private void ShowCopiedFeedback(TextBlock label, string original, string feedback)
        {
            label.Text = feedback;
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            timer.Tick += (s, _) => { label.Text = original; ((DispatcherTimer)s).Stop(); };
            timer.Start();
        }

        // ── end D6 ───────────────────────────────────────────────────────────

        /// <summary>
        /// D5 â€” Handle export template button click.
        /// Generates export output and saves to file via SaveFileDialog.
        /// </summary>
        private void ExportTemplate_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not System.Windows.Controls.Button btn ||
                btn.Tag is not WpfHexEditor.Core.Interfaces.ExportTemplateItem template)
                return;

            var fields = ParsedFields;
            if (fields == null || fields.Count == 0) return;

            var sb = new System.Text.StringBuilder();
            var extension = template.Format switch
            {
                "json" => "json", "csv" => "csv", "c-struct" => "h",
                "python-bytes" => "py", "xml" => "xml", _ => "txt"
            };

            if (template.Format == "json")
            {
                sb.AppendLine("{");
                for (int i = 0; i < fields.Count; i++)
                {
                    var f = fields[i];
                    var comma = i < fields.Count - 1 ? "," : "";
                    sb.AppendLine($"  \"{f.Name}\": \"{f.ActiveFormattedValue}\"{comma}");
                }
                sb.AppendLine("}");
            }
            else if (template.Format == "csv")
            {
                sb.AppendLine("Name,Offset,Length,Value");
                foreach (var f in fields)
                    sb.AppendLine($"\"{f.Name}\",{f.Offset},{f.Length},\"{f.ActiveFormattedValue}\"");
            }
            else
            {
                foreach (var f in fields)
                    sb.AppendLine($"{f.Name}: {f.ActiveFormattedValue}");
            }

            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Filter = $"{template.Format.ToUpper()} files (*.{extension})|*.{extension}|All files (*.*)|*.*",
                FileName = $"Export_{FormatInfo?.Name?.Replace(" ", "_") ?? "Format"}.{extension}"
            };
            if (dlg.ShowDialog() == true)
                System.IO.File.WriteAllText(dlg.FileName, sb.ToString());
        }

        /// <summary>Expand all group nodes in the tree. (C1)</summary>
        private void ExpandAllGroups_Click(object sender, RoutedEventArgs e)
        {
            if (TreeRootItems == null) return;
            foreach (var node in TreeRootItems)
                if (node.IsGroup) node.IsExpanded = true;
        }

        /// <summary>Collapse all group nodes in the tree. (C1)</summary>
        private void CollapseAllGroups_Click(object sender, RoutedEventArgs e)
        {
            if (TreeRootItems == null) return;
            foreach (var node in TreeRootItems)
                if (node.IsGroup) node.IsExpanded = false;
        }

        private void ExportAsText_Click(object sender, RoutedEventArgs e)
        {
            ExportWithOptions(ExportFieldsAsText(), "Text", "txt");
        }

        private void ExportAsJson_Click(object sender, RoutedEventArgs e)
        {
            ExportWithOptions(ExportFieldsAsJson(), "JSON", "json");
        }

        private void ExportAsCsv_Click(object sender, RoutedEventArgs e)
        {
            ExportWithOptions(ExportFieldsAsCsv(), "CSV", "csv");
        }

        private void ExportAsXml_Click(object sender, RoutedEventArgs e)
        {
            ExportWithOptions(ExportFieldsAsXml(), "XML", "xml");
        }

        private void ExportAsHtml_Click(object sender, RoutedEventArgs e)
        {
            ExportWithOptions(ExportFieldsAsHtml(), "HTML", "html");
        }

        private void ExportAsMarkdown_Click(object sender, RoutedEventArgs e)
        {
            ExportWithOptions(ExportFieldsAsMarkdown(), "Markdown", "md");
        }

        private void ExportWithOptions(string content, string formatName, string extension)
        {
            // Ask user whether to copy to clipboard or save to file
            var result = System.Windows.MessageBox.Show(
                $"Export fields as {formatName}?\n\nYes = Save to file\nNo = Copy to clipboard\nCancel = Abort",
                "Export Options",
                System.Windows.MessageBoxButton.YesNoCancel,
                System.Windows.MessageBoxImage.Question);

            switch (result)
            {
                case System.Windows.MessageBoxResult.Yes:
                    ExportToFile(content, formatName, extension);
                    break;
                case System.Windows.MessageBoxResult.No:
                    ExportToClipboard(content, formatName);
                    break;
                // Cancel: do nothing
            }
        }

        private void ExportToFile(string content, string formatName, string extension)
        {
            try
            {
                var saveDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Title = $"Save Parsed Fields as {formatName}",
                    Filter = $"{formatName} Files (*.{extension})|*.{extension}|All Files (*.*)|*.*",
                    DefaultExt = extension,
                    FileName = $"ParsedFields_{FormatInfo?.Name?.Replace(" ", "_") ?? "Export"}.{extension}"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    System.IO.File.WriteAllText(saveDialog.FileName, content);
                    System.Windows.MessageBox.Show(
                        $"Fields exported successfully to:\n{saveDialog.FileName}",
                        ParsedFieldsResources.ParsedFields_Dialog_ExportComplete,
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error saving file: {ex.Message}",
                    ParsedFieldsResources.ParsedFields_Dialog_ExportError,
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private void ExportToClipboard(string content, string formatName)
        {
            try
            {
                System.Windows.Clipboard.SetText(content);
                System.Windows.MessageBox.Show($"All fields exported as {formatName} to clipboard!",
                    ParsedFieldsResources.ParsedFields_Dialog_ExportComplete,
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error exporting fields: {ex.Message}",
                    ParsedFieldsResources.ParsedFields_Dialog_ExportError,
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private void ToggleBookmark_Click(object sender, RoutedEventArgs e)
        {
            if (FieldsListBox.SelectedItem is ParsedFieldViewModel field)
            {
                field.IsBookmarked = !field.IsBookmarked;
            }
        }

        private void CopyOffset_Click(object sender, RoutedEventArgs e)
        {
            if (FieldsListBox.SelectedItem is ParsedFieldViewModel field)
            {
                try
                {
                    System.Windows.Clipboard.SetText(field.OffsetHex);
                    System.Windows.MessageBox.Show($"Offset {field.OffsetHex} copied to clipboard!",
                        ParsedFieldsResources.ParsedFields_Dialog_Copied,
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error copying offset: {ex.Message}");
                }
            }
        }

        private void JumpToOffset_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new JumpToOffsetDialog();
            if (dialog.ShowDialog() == true)
            {
                var offset = dialog.Offset;

                // Find field at this offset
                var field = ParsedFields.FirstOrDefault(f => f.Offset <= offset && offset < f.Offset + f.Length);

                if (field != null)
                {
                    SelectField(field);
                }
                else
                {
                    System.Windows.MessageBox.Show($"No field found at offset {offset:X8}.",
                        ParsedFieldsResources.ParsedFields_Dialog_FieldNotFound,
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                }
            }
        }

        private void CopyValueHex_Click(object sender, RoutedEventArgs e)
        {
            if (FieldsListBox.SelectedItem is ParsedFieldViewModel field)
            {
                try
                {
                    var hexValue = field.GetValueAsHex();
                    System.Windows.Clipboard.SetText(hexValue);
                    System.Windows.MessageBox.Show($"Hex value copied to clipboard:\n{hexValue}",
                        ParsedFieldsResources.ParsedFields_Dialog_Copied,
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error copying hex value: {ex.Message}");
                }
            }
        }

        private void CopyValueDecimal_Click(object sender, RoutedEventArgs e)
        {
            if (FieldsListBox.SelectedItem is ParsedFieldViewModel field)
            {
                try
                {
                    var decValue = field.GetValueAsDecimal();
                    System.Windows.Clipboard.SetText(decValue);
                    System.Windows.MessageBox.Show($"Decimal value copied to clipboard:\n{decValue}",
                        ParsedFieldsResources.ParsedFields_Dialog_Copied,
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error copying decimal value: {ex.Message}");
                }
            }
        }

        private void CopyValueBinary_Click(object sender, RoutedEventArgs e)
        {
            if (FieldsListBox.SelectedItem is ParsedFieldViewModel field)
            {
                try
                {
                    var binValue = field.GetValueAsBinary();
                    System.Windows.Clipboard.SetText(binValue);
                    System.Windows.MessageBox.Show($"Binary value copied to clipboard:\n{binValue}",
                        ParsedFieldsResources.ParsedFields_Dialog_Copied,
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error copying binary value: {ex.Message}");
                }
            }
        }

        private void FindSimilar_Click(object sender, RoutedEventArgs e)
        {
            if (FieldsListBox.SelectedItem is ParsedFieldViewModel field)
            {
                try
                {
                    // Search for fields with similar values
                    var searchValue = field.FormattedValue;
                    SearchTextBox.Text = searchValue;
                    ApplyFilter();

                    System.Windows.MessageBox.Show($"Searching for fields with value: {searchValue}",
                        ParsedFieldsResources.ParsedFields_Dialog_FindSimilar,
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error finding similar values: {ex.Message}");
                }
            }
        }

        private void ShowInDataInspector_Click(object sender, RoutedEventArgs e)
        {
            if (FieldsListBox.SelectedItem is ParsedFieldViewModel field)
            {
                try
                {
                    // Get raw bytes from RawValue
                    byte[] data = field.RawValue is byte[] bytes ? bytes : null;

                    // Request to show this field's data in Data Inspector
                    DataInspectorRequested?.Invoke(this, new DataInspectorEventArgs
                    {
                        Offset = field.Offset,
                        Length = field.Length,
                        Data = data
                    });
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error showing in data inspector: {ex.Message}");
                    System.Windows.MessageBox.Show(ParsedFieldsResources.ParsedFields_DataInspectorNotConnected,
                        ParsedFieldsResources.ParsedFields_Dialog_FeatureUnavailable,
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                }
            }
        }

        /// <summary>
        /// Event raised when user requests to view data in Data Inspector
        /// </summary>
        public event EventHandler<DataInspectorEventArgs> DataInspectorRequested;

        private void ShowBookmarksButton_Checked(object sender, RoutedEventArgs e)
        {
            _showBookmarksOnly = true;
            ApplyFilter();
        }

        private void ShowBookmarksButton_Unchecked(object sender, RoutedEventArgs e)
        {
            _showBookmarksOnly = false;
            ApplyFilter();
        }

        /// <summary>
        /// Selects a specific field in the TreeView, expanding its parent group if needed. (C1)
        /// </summary>
        public void SelectField(ParsedFieldViewModel field)
        {
            if (field == null) return;

            // Ensure the parent group is expanded so the item is visible
            if (TreeRootItems != null)
            {
                foreach (var root in TreeRootItems)
                {
                    if (root.IsGroup && root.ChildItems != null)
                    {
                        foreach (var child in root.ChildItems)
                        {
                            if (ReferenceEquals(child, field))
                            {
                                root.IsExpanded = true;
                                break;
                            }
                        }
                    }
                }
            }

            // Mark the field as selected (bound to TreeViewItem.IsSelected)
            foreach (var f in FilteredFields)
                f.IsSelected = ReferenceEquals(f, field);
        }

        /// <summary>
        /// Suppresses CollectionChanged â†’ ApplyFilter during bulk field population.
        /// Call EndBulkUpdate() when done; it fires ApplyFilter exactly once.
        /// </summary>
        public void BeginBulkUpdate() => _suppressFilter = true;

        /// <summary>Ends a bulk update and triggers a single ApplyFilter() pass.</summary>
        public void EndBulkUpdate()
        {
            _suppressFilter = false;
            ApplyFilter();
        }

        /// <summary>
        /// Clear all fields (including injected enriched fields).
        /// </summary>
        public void Clear()
        {
            RemoveEnrichedFields();
            ParsedFields.Clear();
            FilteredFields.Clear();
            TreeRootItems = new ObservableCollection<ParsedFieldViewModel>();
            FormatInfo = new FormatInfo();
        }

        /// <summary>
        /// Refresh the filtered view (useful after updating field values)
        /// </summary>
        public void RefreshView()
        {
            ApplyFilter();
        }

        /// <summary>
        /// Apply search filter to fields with advanced options (regex, type filter, etc.)
        /// </summary>
        private void ApplyFilter()
        {
            // Defensive check: ensure collections are initialized
            if (FilteredFields == null || ParsedFields == null || MetadataFields == null)
                return;

            FilteredFields.Clear();
            MetadataFields.Clear();

            // Get search options
            var hasSearchFilter = !string.IsNullOrWhiteSpace(SearchText);
            var searchLower = hasSearchFilter ? SearchText.ToLowerInvariant() : null;
            var isRegex = RegexSearchCheckBox?.IsChecked == true;
            var isCaseSensitive = CaseSensitiveCheckBox?.IsChecked == true;
            var searchField = SearchFieldCombo?.SelectedIndex ?? 0;
            var typeFilter = TypeFilterCombo?.SelectedIndex ?? 0;

            // Prepare regex if needed
            System.Text.RegularExpressions.Regex regex = null;
            if (isRegex && hasSearchFilter)
            {
                try
                {
                    var options = isCaseSensitive
                        ? System.Text.RegularExpressions.RegexOptions.None
                        : System.Text.RegularExpressions.RegexOptions.IgnoreCase;
                    regex = new System.Text.RegularExpressions.Regex(SearchText, options);
                }
                catch
                {
                    // Invalid regex - fall back to literal search
                    isRegex = false;
                }
            }

            int searchMatchCount = 0;
            int validCount = 0;
            int invalidCount = 0;
            var matchedFields = new System.Collections.Generic.List<ParsedFieldViewModel>();

            foreach (var field in ParsedFields)
            {
                // Bookmark filter
                if (_showBookmarksOnly && !field.IsBookmarked)
                    continue;

                // Type filter
                if (typeFilter > 0)
                {
                    bool typeMatch = typeFilter switch
                    {
                        1 => field.ValueType?.Contains("int") == true, // Integers
                        2 => field.ValueType?.ToLower().Contains("string") == true, // Strings
                        3 => field.ValueType?.Equals("bytes", System.StringComparison.OrdinalIgnoreCase) == true, // Bytes
                        _ => true
                    };

                    if (!typeMatch)
                        continue;
                }

                // Search filter
                bool isMatch = true;
                if (hasSearchFilter)
                {
                    isMatch = searchField switch
                    {
                        1 => MatchesSearch(field.Name, searchLower, regex, isRegex, isCaseSensitive),
                        2 => MatchesSearch(field.FormattedValue, searchLower, regex, isRegex, isCaseSensitive),
                        3 => MatchesSearch(field.Description, searchLower, regex, isRegex, isCaseSensitive),
                        _ => MatchesSearch(field.Name, searchLower, regex, isRegex, isCaseSensitive) ||
                             MatchesSearch(field.FormattedValue, searchLower, regex, isRegex, isCaseSensitive) ||
                             MatchesSearch(field.Description, searchLower, regex, isRegex, isCaseSensitive) ||
                             MatchesSearch(field.ValueType, searchLower, regex, isRegex, isCaseSensitive) ||
                             MatchesSearch(field.OffsetHex, searchLower, regex, isRegex, isCaseSensitive)
                    };

                    if (!isMatch)
                        continue;
                }

                // Mark search matches for visual indicator
                field.IsSearchMatch = hasSearchFilter && isMatch;

                if (hasSearchFilter && isMatch)
                    searchMatchCount++;

                // Track validation counts (C2)
                if (field.IsValid)
                    validCount++;
                else
                    invalidCount++;

                // Ensure GroupName is set for section headers (C3)
                if (string.IsNullOrEmpty(field.GroupName))
                    field.GroupName = "Fields";

                matchedFields.Add(field);
            }

            // Apply sorting (C5)
            System.Collections.Generic.IEnumerable<ParsedFieldViewModel> sorted = _sortMode switch
            {
                1 => matchedFields.OrderBy(f => f.Name ?? ""),
                2 => matchedFields.OrderByDescending(f => f.Name ?? ""),
                3 => matchedFields.OrderBy(f => f.ValueType ?? ""),
                4 => matchedFields.OrderByDescending(f => f.Length),
                _ => matchedFields // Default: preserve original offset order
            };

            foreach (var field in sorted)
                FilteredFields.Add(field);

            // Update search result text
            if (hasSearchFilter)
            {
                SearchResultText = searchMatchCount > 0
                    ? $" - {searchMatchCount} match{(searchMatchCount != 1 ? "es" : "")}"
                    : " - no matches";
            }
            else
            {
                SearchResultText = string.Empty;
            }

            // Update validation badges
            UpdateValidationBadges(validCount, invalidCount);

            // Build key insight chips
            BuildInsightChips();

            // Update byte coverage bar (C6)
            UpdateCoverageBar();

            // Rebuild TreeView hierarchy (C1)
            RebuildTree();
        }

        /// <summary>
        /// Updates the validation summary badges in the header (C2)
        /// </summary>
        private void UpdateValidationBadges(int validCount, int invalidCount)
        {
            if (ValidBadge == null || InvalidBadge == null) return;

            int total = validCount + invalidCount;
            if (total > 0)
            {
                ValidBadge.Visibility = Visibility.Visible;
                ValidCountRun.Text = validCount.ToString();
            }
            else
            {
                ValidBadge.Visibility = Visibility.Collapsed;
            }

            if (invalidCount > 0)
            {
                InvalidBadge.Visibility = Visibility.Visible;
                InvalidCountRun.Text = invalidCount.ToString();
            }
            else
            {
                InvalidBadge.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// Build key insight chips from parsed fields and metadata (C4)
        /// </summary>
        private void BuildInsightChips()
        {
            if (InsightChips == null) return;
            InsightChips.Clear();

            if (ParsedFields == null || ParsedFields.Count == 0)
            {
                if (KeyInsightsSection != null)
                    KeyInsightsSection.Visibility = Visibility.Collapsed;
                return;
            }

            // Look for dimension fields (Width + Height)
            var widthField = ParsedFields.FirstOrDefault(f =>
                f.Name?.IndexOf("Width", StringComparison.OrdinalIgnoreCase) >= 0 && f.Offset >= 0);
            var heightField = ParsedFields.FirstOrDefault(f =>
                f.Name?.IndexOf("Height", StringComparison.OrdinalIgnoreCase) >= 0 && f.Offset >= 0);

            if (widthField != null && heightField != null &&
                !string.IsNullOrEmpty(widthField.FormattedValue) && !string.IsNullOrEmpty(heightField.FormattedValue))
            {
                InsightChips.Add(new InsightChip
                {
                    Icon = "\U0001F5BC", // framed picture
                    Value = $"{widthField.FormattedValue} x {heightField.FormattedValue}",
                    Background = HexBrush("#E3F2FD"),
                    TextForeground = HexBrush("#2B2B2B")
                });
            }

            // Look for color type in computed metadata fields
            var colorType = ParsedFields.FirstOrDefault(f =>
                f.ValueType == "metadata" &&
                f.Name?.IndexOf("Color", StringComparison.OrdinalIgnoreCase) >= 0);
            if (colorType != null && !string.IsNullOrEmpty(colorType.FormattedValue) && colorType.FormattedValue != "0")
            {
                InsightChips.Add(new InsightChip
                {
                    Icon = "\U0001F3A8", // artist palette
                    Value = colorType.FormattedValue,
                    Background = HexBrush("#F3E5F5"),
                    TextForeground = HexBrush("#2B2B2B")
                });
            }

            // Look for formatted file size in computed metadata fields
            var sizeField = ParsedFields.FirstOrDefault(f =>
                f.ValueType == "metadata" &&
                (f.Name?.IndexOf("Size", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 f.Name?.IndexOf("Formatted", StringComparison.OrdinalIgnoreCase) >= 0));
            if (sizeField != null && !string.IsNullOrEmpty(sizeField.FormattedValue) &&
                sizeField.FormattedValue != "0" && sizeField.FormattedValue != "")
            {
                InsightChips.Add(new InsightChip
                {
                    Icon = "\U0001F4CA", // bar chart
                    Value = sizeField.FormattedValue,
                    Background = HexBrush("#E8F5E9"),
                    TextForeground = HexBrush("#2B2B2B")
                });
            }

            // Look for compression name in computed metadata fields
            var compression = ParsedFields.FirstOrDefault(f =>
                f.ValueType == "metadata" &&
                f.Name?.IndexOf("Compression", StringComparison.OrdinalIgnoreCase) >= 0);
            if (compression != null && !string.IsNullOrEmpty(compression.FormattedValue) && compression.FormattedValue != "0")
            {
                InsightChips.Add(new InsightChip
                {
                    Icon = "\U0001F4E6", // package
                    Value = compression.FormattedValue,
                    Background = HexBrush("#FFF3E0"),
                    TextForeground = HexBrush("#2B2B2B")
                });
            }

            // Look for duration in computed metadata fields
            var duration = ParsedFields.FirstOrDefault(f =>
                f.ValueType == "metadata" &&
                f.Name?.IndexOf("Duration", StringComparison.OrdinalIgnoreCase) >= 0);
            if (duration != null && !string.IsNullOrEmpty(duration.FormattedValue) && duration.FormattedValue != "0")
            {
                InsightChips.Add(new InsightChip
                {
                    Icon = "\U0001F554", // clock
                    Value = duration.FormattedValue,
                    Background = HexBrush("#E0F7FA"),
                    TextForeground = HexBrush("#2B2B2B")
                });
            }

            // Validation summary chip — only show errors; skip "All Valid" when it's the only insight
            int totalFields = FilteredFields?.Count ?? 0;
            int invalidCount = FilteredFields?.Count(f => !f.IsValid) ?? 0;
            if (totalFields > 0 && invalidCount > 0)
            {
                InsightChips.Add(new InsightChip
                {
                    Icon = "\u26A0", // warning
                    Value = $"{invalidCount} Error{(invalidCount > 1 ? "s" : "")}",
                    Background = HexBrush("#FFEBEE"),
                    TextForeground = HexBrush("#2B2B2B")
                });
            }

            // Update visibility
            if (KeyInsightsSection != null)
                KeyInsightsSection.Visibility = InsightChips.Count > 0
                    ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>
        /// Update the byte coverage bar (C6)
        /// </summary>
        private void UpdateCoverageBar()
        {
            if (CoverageBarSection == null || CoverageProgressBar == null) return;

            if (TotalFileSize <= 0 || ParsedFields == null || ParsedFields.Count == 0)
            {
                CoverageBarSection.Visibility = Visibility.Collapsed;
                return;
            }

            // Calculate total covered bytes (merge overlapping ranges)
            var ranges = ParsedFields
                .Where(f => f.Offset >= 0 && f.Length > 0)
                .OrderBy(f => f.Offset)
                .Select(f => (Start: f.Offset, End: f.Offset + f.Length))
                .ToList();

            long coveredBytes = 0;
            long currentEnd = -1;

            foreach (var (start, end) in ranges)
            {
                if (start > currentEnd)
                {
                    coveredBytes += end - start;
                    currentEnd = end;
                }
                else if (end > currentEnd)
                {
                    coveredBytes += end - currentEnd;
                    currentEnd = end;
                }
            }

            double percent = (double)coveredBytes / TotalFileSize * 100;
            percent = Math.Min(percent, 100);

            CoverageProgressBar.Progress = percent / 100.0;
            CoverageText.Text = $"{percent:F0}% parsed ({coveredBytes:N0} / {TotalFileSize:N0} bytes)";
            CoverageBarSection.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// Builds <see cref="TreeRootItems"/> from <see cref="FilteredFields"/> by grouping
        /// fields under named group nodes.  Fields that are already group containers
        /// (IsGroup=true, e.g. repeating block entries) retain their own Children. (C1)
        /// </summary>
        private void RebuildTree()
        {
            var roots = new System.Collections.Generic.List<ParsedFieldViewModel>();

            // Group by GroupName â€” preserve insertion order
            var groups = FilteredFields
                .GroupBy(f => f.GroupName ?? string.Empty)
                .ToList();

            foreach (var g in groups)
            {
                if (string.IsNullOrEmpty(g.Key))
                {
                    // Ungrouped â†’ add directly to root
                    foreach (var f in g)
                        roots.Add(f);
                }
                else
                {
                    // Named group â†’ create collapsible parent node (expand all except metadata)
                    var first = g.First();
                    var groupNode = new ParsedFieldViewModel
                    {
                        Name          = g.Key,
                        IsGroup       = true,
                        IsValid       = true,
                        IsExpanded    = !string.Equals(g.Key, EnrichedGroupName, StringComparison.Ordinal),
                        Color         = first.Color,
                        ValueType     = "group",
                        FormattedValue = string.Empty
                    };
                    foreach (var f in g)
                        groupNode.AddChild(f);
                    roots.Add(groupNode);
                }
            }

            TreeRootItems = new ObservableCollection<ParsedFieldViewModel>(roots);
        }

        /// <summary>
        /// Handle H/D/B display mode toggle button click (C7)
        /// </summary>
        private void DisplayModeButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn &&
                btn.DataContext is ParsedFieldViewModel field &&
                btn.Tag is string mode)
            {
                field.DisplayMode = mode switch
                {
                    "hex" => FieldDisplayMode.Hex,
                    "dec" => FieldDisplayMode.Decimal,
                    "bin" => FieldDisplayMode.Binary,
                    _ => FieldDisplayMode.Auto
                };
            }
        }

        /// <summary>
        /// Create a SolidColorBrush from a hex color string
        /// </summary>
        private static SolidColorBrush HexBrush(string hex)
        {
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        }

        /// <summary>
        /// Helper method to check if text matches search criteria (with regex support)
        /// </summary>
        private bool MatchesSearch(string text, string searchLower,
            System.Text.RegularExpressions.Regex regex, bool isRegex, bool isCaseSensitive)
        {
            if (string.IsNullOrEmpty(text))
                return false;

            if (isRegex && regex != null)
                return regex.IsMatch(text);

            if (isCaseSensitive)
                return text.Contains(searchLower);
            else
                return text.ToLowerInvariant().Contains(searchLower);
        }

        /// <summary>
        /// Copy field value to clipboard
        /// </summary>
        public void CopyFieldValue(ParsedFieldViewModel field)
        {
            if (field == null || string.IsNullOrEmpty(field.FormattedValue))
                return;

            try
            {
                System.Windows.Clipboard.SetText(field.FormattedValue);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error copying to clipboard: {ex.Message}");
            }
        }

        /// <summary>
        /// Copy field details to clipboard
        /// </summary>
        public void CopyFieldDetails(ParsedFieldViewModel field)
        {
            if (field == null)
                return;

            try
            {
                var details = $"{field.Name}: {field.FormattedValue}\n" +
                             $"Type: {field.ValueType}\n" +
                             $"Range: {field.RangeDisplay}\n" +
                             $"Description: {field.Description}";
                System.Windows.Clipboard.SetText(details);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error copying to clipboard: {ex.Message}");
            }
        }

        /// <summary>
        /// Export all fields to text
        /// </summary>
        public string ExportFieldsAsText()
        {
            var sb = new System.Text.StringBuilder();

            if (FormatInfo.IsDetected)
            {
                sb.AppendLine($"Format: {FormatInfo.Name}");
                sb.AppendLine($"Description: {FormatInfo.Description}");

                // Add references if available
                if (FormatInfo.References != null)
                {
                    if (FormatInfo.References.Specifications?.Count > 0)
                    {
                        sb.AppendLine();
                        sb.AppendLine("Specifications:");
                        foreach (var spec in FormatInfo.References.Specifications)
                            sb.AppendLine($"  - {spec}");
                    }

                    if (FormatInfo.References.WebLinks?.Count > 0)
                    {
                        sb.AppendLine();
                        sb.AppendLine("Web Links:");
                        foreach (var link in FormatInfo.References.WebLinks)
                            sb.AppendLine($"  - {link}");
                    }
                }

                sb.AppendLine();
            }

            sb.AppendLine($"Parsed Fields ({ParsedFields.Count}):");
            sb.AppendLine(new string('=', 80));

            foreach (var field in ParsedFields)
            {
                var indent = new string(' ', field.IndentLevel * 2);
                sb.AppendLine($"{indent}{field.FieldIcon} {field.Name}");
                sb.AppendLine($"{indent}  Value: {field.FormattedValue}");
                sb.AppendLine($"{indent}  Range: {field.RangeDisplay}");
                if (!string.IsNullOrEmpty(field.Description))
                    sb.AppendLine($"{indent}  Desc:  {field.Description}");
                sb.AppendLine();
            }

            return sb.ToString();
        }

        /// <summary>
        /// Export all fields to JSON format
        /// </summary>
        public string ExportFieldsAsJson()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("{");

            if (FormatInfo.IsDetected)
            {
                sb.AppendLine("  \"format\": {");
                sb.AppendLine($"    \"name\": \"{EscapeJson(FormatInfo.Name)}\",");
                sb.AppendLine($"    \"description\": \"{EscapeJson(FormatInfo.Description)}\",");

                // Add references if available
                if (FormatInfo.References != null &&
                    (FormatInfo.References.Specifications?.Count > 0 || FormatInfo.References.WebLinks?.Count > 0))
                {
                    sb.AppendLine("    \"references\": {");

                    if (FormatInfo.References.Specifications?.Count > 0)
                    {
                        sb.AppendLine("      \"specifications\": [");
                        for (int i = 0; i < FormatInfo.References.Specifications.Count; i++)
                        {
                            sb.Append($"        \"{EscapeJson(FormatInfo.References.Specifications[i])}\"");
                            if (i < FormatInfo.References.Specifications.Count - 1)
                                sb.AppendLine(",");
                            else
                                sb.AppendLine();
                        }
                        sb.Append("      ]");
                        if (FormatInfo.References.WebLinks?.Count > 0)
                            sb.AppendLine(",");
                        else
                            sb.AppendLine();
                    }

                    if (FormatInfo.References.WebLinks?.Count > 0)
                    {
                        sb.AppendLine("      \"webLinks\": [");
                        for (int i = 0; i < FormatInfo.References.WebLinks.Count; i++)
                        {
                            sb.Append($"        \"{EscapeJson(FormatInfo.References.WebLinks[i])}\"");
                            if (i < FormatInfo.References.WebLinks.Count - 1)
                                sb.AppendLine(",");
                            else
                                sb.AppendLine();
                        }
                        sb.AppendLine("      ]");
                    }

                    sb.AppendLine("    }");
                }
                else
                {
                    // Remove trailing comma if no references
                    sb.Length -= 3; // Remove ",\n"
                    sb.AppendLine();
                }

                sb.AppendLine("  },");
            }

            sb.AppendLine("  \"fields\": [");

            for (int i = 0; i < ParsedFields.Count; i++)
            {
                var field = ParsedFields[i];
                sb.AppendLine("    {");
                sb.AppendLine($"      \"name\": \"{EscapeJson(field.Name)}\",");
                sb.AppendLine($"      \"value\": \"{EscapeJson(field.FormattedValue)}\",");
                sb.AppendLine($"      \"offset\": {field.Offset},");
                sb.AppendLine($"      \"length\": {field.Length},");
                sb.AppendLine($"      \"type\": \"{EscapeJson(field.ValueType)}\",");
                sb.AppendLine($"      \"description\": \"{EscapeJson(field.Description)}\",");
                sb.AppendLine($"      \"indentLevel\": {field.IndentLevel}");
                sb.Append("    }");

                if (i < ParsedFields.Count - 1)
                    sb.AppendLine(",");
                else
                    sb.AppendLine();
            }

            sb.AppendLine("  ]");
            sb.AppendLine("}");

            return sb.ToString();
        }

        /// <summary>
        /// Export all fields to CSV format
        /// </summary>
        public string ExportFieldsAsCsv()
        {
            var sb = new System.Text.StringBuilder();

            // Header
            sb.AppendLine("Name,Value,Offset,Length,Type,Description,IndentLevel");

            // Rows
            foreach (var field in ParsedFields)
            {
                sb.AppendLine($"\"{EscapeCsv(field.Name)}\"," +
                             $"\"{EscapeCsv(field.FormattedValue)}\"," +
                             $"{field.Offset}," +
                             $"{field.Length}," +
                             $"\"{EscapeCsv(field.ValueType)}\"," +
                             $"\"{EscapeCsv(field.Description)}\"," +
                             $"{field.IndentLevel}");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Export all fields to XML format
        /// </summary>
        public string ExportFieldsAsXml()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            sb.AppendLine("<ParsedFields>");

            if (FormatInfo.IsDetected)
            {
                sb.AppendLine("  <Format>");
                sb.AppendLine($"    <Name>{EscapeXml(FormatInfo.Name)}</Name>");
                sb.AppendLine($"    <Description>{EscapeXml(FormatInfo.Description)}</Description>");

                // Add references if available
                if (FormatInfo.References != null &&
                    (FormatInfo.References.Specifications?.Count > 0 || FormatInfo.References.WebLinks?.Count > 0))
                {
                    sb.AppendLine("    <References>");

                    if (FormatInfo.References.Specifications?.Count > 0)
                    {
                        sb.AppendLine("      <Specifications>");
                        foreach (var spec in FormatInfo.References.Specifications)
                            sb.AppendLine($"        <Specification>{EscapeXml(spec)}</Specification>");
                        sb.AppendLine("      </Specifications>");
                    }

                    if (FormatInfo.References.WebLinks?.Count > 0)
                    {
                        sb.AppendLine("      <WebLinks>");
                        foreach (var link in FormatInfo.References.WebLinks)
                            sb.AppendLine($"        <WebLink>{EscapeXml(link)}</WebLink>");
                        sb.AppendLine("      </WebLinks>");
                    }

                    sb.AppendLine("    </References>");
                }

                sb.AppendLine("  </Format>");
            }

            sb.AppendLine("  <Fields>");

            foreach (var field in ParsedFields)
            {
                sb.AppendLine("    <Field>");
                sb.AppendLine($"      <Name>{EscapeXml(field.Name)}</Name>");
                sb.AppendLine($"      <Value>{EscapeXml(field.FormattedValue)}</Value>");
                sb.AppendLine($"      <Offset>{field.Offset}</Offset>");
                sb.AppendLine($"      <Length>{field.Length}</Length>");
                sb.AppendLine($"      <Type>{EscapeXml(field.ValueType)}</Type>");
                sb.AppendLine($"      <Description>{EscapeXml(field.Description)}</Description>");
                sb.AppendLine($"      <IndentLevel>{field.IndentLevel}</IndentLevel>");
                sb.AppendLine("    </Field>");
            }

            sb.AppendLine("  </Fields>");
            sb.AppendLine("</ParsedFields>");

            return sb.ToString();
        }

        /// <summary>
        /// Export all fields to HTML format
        /// </summary>
        public string ExportFieldsAsHtml()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html>");
            sb.AppendLine("<head>");
            sb.AppendLine("    <meta charset=\"utf-8\">");
            sb.AppendLine("    <title>Parsed Fields</title>");
            sb.AppendLine("    <style>");
            sb.AppendLine("        body { font-family: Arial, sans-serif; margin: 20px; background: #f5f5f5; }");
            sb.AppendLine("        h1 { color: #2196F3; }");
            sb.AppendLine("        .format-info { background: #fff; padding: 15px; margin: 20px 0; border-radius: 4px; box-shadow: 0 2px 4px rgba(0,0,0,0.1); }");
            sb.AppendLine("        table { width: 100%; border-collapse: collapse; background: white; box-shadow: 0 2px 4px rgba(0,0,0,0.1); }");
            sb.AppendLine("        th { background: #2196F3; color: white; padding: 12px; text-align: left; }");
            sb.AppendLine("        td { padding: 10px; border-bottom: 1px solid #e0e0e0; }");
            sb.AppendLine("        tr:hover { background: #f5f5f5; }");
            sb.AppendLine("        .value { font-family: Consolas, monospace; color: #2196F3; }");
            sb.AppendLine("        .bookmarked { color: #FFB300; }");
            sb.AppendLine("    </style>");
            sb.AppendLine("</head>");
            sb.AppendLine("<body>");
            sb.AppendLine("    <h1>Parsed Fields</h1>");

            if (FormatInfo.IsDetected)
            {
                sb.AppendLine("    <div class=\"format-info\">");
                sb.AppendLine($"        <h2>{EscapeHtml(FormatInfo.Name)}</h2>");
                sb.AppendLine($"        <p>{EscapeHtml(FormatInfo.Description)}</p>");

                // Add references if available
                if (FormatInfo.References != null &&
                    (FormatInfo.References.Specifications?.Count > 0 || FormatInfo.References.WebLinks?.Count > 0))
                {
                    if (FormatInfo.References.Specifications?.Count > 0)
                    {
                        sb.AppendLine("        <h3>Specifications</h3>");
                        sb.AppendLine("        <ul>");
                        foreach (var spec in FormatInfo.References.Specifications)
                            sb.AppendLine($"            <li>{EscapeHtml(spec)}</li>");
                        sb.AppendLine("        </ul>");
                    }

                    if (FormatInfo.References.WebLinks?.Count > 0)
                    {
                        sb.AppendLine("        <h3>Web Links</h3>");
                        sb.AppendLine("        <ul>");
                        foreach (var link in FormatInfo.References.WebLinks)
                            sb.AppendLine($"            <li><a href=\"{EscapeHtml(link)}\" target=\"_blank\">{EscapeHtml(link)}</a></li>");
                        sb.AppendLine("        </ul>");
                    }
                }

                sb.AppendLine("    </div>");
            }

            sb.AppendLine("    <table>");
            sb.AppendLine("        <thead>");
            sb.AppendLine("            <tr>");
            sb.AppendLine("                <th>Name</th>");
            sb.AppendLine("                <th>Value</th>");
            sb.AppendLine("                <th>Offset</th>");
            sb.AppendLine("                <th>Length</th>");
            sb.AppendLine("                <th>Type</th>");
            sb.AppendLine("                <th>Description</th>");
            sb.AppendLine("            </tr>");
            sb.AppendLine("        </thead>");
            sb.AppendLine("        <tbody>");

            foreach (var field in ParsedFields)
            {
                var indent = new string(' ', field.IndentLevel * 4);
                var bookmark = field.IsBookmarked ? "<span class=\"bookmarked\">â­</span> " : "";

                sb.AppendLine("            <tr>");
                sb.AppendLine($"                <td>{indent}{bookmark}{EscapeHtml(field.Name)}</td>");
                sb.AppendLine($"                <td class=\"value\">{EscapeHtml(field.FormattedValue)}</td>");
                sb.AppendLine($"                <td>{field.OffsetHex}</td>");
                sb.AppendLine($"                <td>{field.Length}</td>");
                sb.AppendLine($"                <td>{EscapeHtml(field.ValueType)}</td>");
                sb.AppendLine($"                <td>{EscapeHtml(field.Description)}</td>");
                sb.AppendLine("            </tr>");
            }

            sb.AppendLine("        </tbody>");
            sb.AppendLine("    </table>");
            sb.AppendLine("</body>");
            sb.AppendLine("</html>");

            return sb.ToString();
        }

        /// <summary>
        /// Export all fields to Markdown format
        /// </summary>
        public string ExportFieldsAsMarkdown()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("# Parsed Fields");
            sb.AppendLine();

            if (FormatInfo.IsDetected)
            {
                sb.AppendLine($"## Format: {FormatInfo.Name}");
                sb.AppendLine();
                sb.AppendLine(FormatInfo.Description);
                sb.AppendLine();

                // Add references if available
                if (FormatInfo.References != null &&
                    (FormatInfo.References.Specifications?.Count > 0 || FormatInfo.References.WebLinks?.Count > 0))
                {
                    if (FormatInfo.References.Specifications?.Count > 0)
                    {
                        sb.AppendLine("### Specifications");
                        sb.AppendLine();
                        foreach (var spec in FormatInfo.References.Specifications)
                            sb.AppendLine($"- {spec}");
                        sb.AppendLine();
                    }

                    if (FormatInfo.References.WebLinks?.Count > 0)
                    {
                        sb.AppendLine("### Web Links");
                        sb.AppendLine();
                        foreach (var link in FormatInfo.References.WebLinks)
                            sb.AppendLine($"- [{link}]({link})");
                        sb.AppendLine();
                    }
                }
            }

            sb.AppendLine("| Name | Value | Offset | Length | Type | Description |");
            sb.AppendLine("|------|-------|--------|--------|------|-------------|");

            foreach (var field in ParsedFields)
            {
                var indent = new string(' ', field.IndentLevel * 2);
                var bookmark = field.IsBookmarked ? "â­ " : "";
                var name = EscapeMarkdown(field.Name);
                var value = EscapeMarkdown(field.FormattedValue);
                var type = EscapeMarkdown(field.ValueType);
                var desc = EscapeMarkdown(field.Description);

                sb.AppendLine($"| {indent}{bookmark}{name} | `{value}` | {field.OffsetHex} | {field.Length} | {type} | {desc} |");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Escape string for HTML
        /// </summary>
        private string EscapeHtml(string input)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            return input.Replace("&", "&amp;")
                        .Replace("<", "&lt;")
                        .Replace(">", "&gt;")
                        .Replace("\"", "&quot;")
                        .Replace("'", "&#39;");
        }

        /// <summary>
        /// Escape string for Markdown
        /// </summary>
        private string EscapeMarkdown(string input)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            return input.Replace("|", "\\|")
                        .Replace("\n", " ")
                        .Replace("\r", "");
        }

        /// <summary>
        /// Escape string for JSON
        /// </summary>
        private string EscapeJson(string input)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            return input.Replace("\\", "\\\\")
                        .Replace("\"", "\\\"")
                        .Replace("\n", "\\n")
                        .Replace("\r", "\\r")
                        .Replace("\t", "\\t");
        }

        /// <summary>
        /// Escape string for CSV
        /// </summary>
        private string EscapeCsv(string input)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            // Escape quotes by doubling them
            return input.Replace("\"", "\"\"");
        }

        /// <summary>
        /// Escape string for XML
        /// </summary>
        private string EscapeXml(string input)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            return input.Replace("&", "&amp;")
                        .Replace("<", "&lt;")
                        .Replace(">", "&gt;")
                        .Replace("\"", "&quot;")
                        .Replace("'", "&apos;");
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));


        // ── Toolbar overflow ─────────────────────────────────────────────────

        private void OnToolbarSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.WidthChanged) _overflowManager?.Update();
        }

        private void OnOverflowButtonClick(object sender, RoutedEventArgs e)
        {
            OverflowContextMenu.PlacementTarget = ToolbarOverflowButton;
            OverflowContextMenu.Placement       = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            OverflowContextMenu.IsOpen          = true;
        }

        private void OnOverflowMenuOpened(object sender, RoutedEventArgs e)
        {
            _overflowManager?.SyncMenuVisibility();
        }

    }

    /// <summary>
    /// Information about a detected format
    /// </summary>
    public class DataInspectorEventArgs : EventArgs
    {
        public long Offset { get; set; }
        public int Length { get; set; }
        public byte[] Data { get; set; }
    }

    /// <summary>
    /// Represents a key insight chip for at-a-glance summary (C4)
    /// </summary>
    public class InsightChip
    {
        public string Icon { get; set; }
        public string Value { get; set; }
        public System.Windows.Media.Brush Background { get; set; }
        public System.Windows.Media.Brush TextForeground { get; set; }
    }

    /// <summary>
    /// Converts a group name to an IsExpanded bool.
    /// Groups listed in the ConverterParameter (comma-separated) start collapsed.
    /// Default collapsed groups: "Format Metadata"
    /// </summary>
    public class GroupNameToExpandedConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not string groupName) return true;

            // Parameter overrides the default collapsed list
            string collapsed = parameter as string ?? "Format Metadata";
            foreach (var entry in collapsed.Split(','))
                if (string.Equals(groupName, entry.Trim(), StringComparison.Ordinal))
                    return false;

            return true;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// Converts bool to inverse Visibility (true=Collapsed, false=Visible)
    /// </summary>
    public class InverseBoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
                return boolValue ? Visibility.Collapsed : Visibility.Visible;
            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>D6 â€” Maps IsAiSectionExpanded bool to Segoe MDL2 chevron glyph.</summary>
    public class BoolToChevronConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is true ? "\uE70D" : "\uE70E";   // chevron-down : chevron-right

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => DependencyProperty.UnsetValue;
    }
}
