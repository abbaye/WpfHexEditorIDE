//////////////////////////////////////////////
// Apache 2.0  - 2026
// Contributors: Claude Sonnet 4.5
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
using WpfHexaEditor.ViewModels;

namespace WpfHexaEditor.Views.Panels
{
    /// <summary>
    /// Panel for displaying parsed fields from format definitions
    /// Shows field names, values, offsets, and descriptions
    /// </summary>
    public partial class ParsedFieldsPanel : UserControl, INotifyPropertyChanged
    {
        private ObservableCollection<ParsedFieldViewModel> _parsedFields;
        private ObservableCollection<ParsedFieldViewModel> _filteredFields;
        private ObservableCollection<ParsedFieldViewModel> _metadataFields;
        private ObservableCollection<InsightChip> _insightChips;
        private FormatInfo _formatInfo;
        private string _searchText;
        private bool _showBookmarksOnly;
        private string _searchResultText;
        private bool _hasSelection;
        private int _sortMode = 0; // 0=offset, 1=nameAZ, 2=nameZA, 3=type, 4=size
        private long _totalFileSize;

        public ParsedFieldsPanel()
        {
            InitializeComponent();
            DataContext = this;
            // Initialize collections FIRST to avoid NullReferenceException in ApplyFilter()
            FilteredFields = new ObservableCollection<ParsedFieldViewModel>();
            MetadataFields = new ObservableCollection<ParsedFieldViewModel>();
            InsightChips = new ObservableCollection<InsightChip>();
            ParsedFields = new ObservableCollection<ParsedFieldViewModel>();

            // Set up CollectionView grouping for section headers (C3)
            var view = CollectionViewSource.GetDefaultView(FilteredFields);
            view?.GroupDescriptions.Add(new PropertyGroupDescription("GroupName"));

            // Subscribe to collection changes to automatically update filtered view
            ParsedFields.CollectionChanged += (s, e) => ApplyFilter();

            FormatInfo = new FormatInfo();
        }

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
            }
        }

        /// <summary>
        /// Event fired when a field is selected
        /// </summary>
        public event EventHandler<ParsedFieldViewModel> FieldSelected;

        /// <summary>
        /// Event fired when the refresh button is clicked
        /// </summary>
        public event EventHandler RefreshRequested;

        /// <summary>
        /// Fired when the user presses the left mouse button on the title bar.
        /// The host window can handle this to implement tear-off float behavior.
        /// </summary>
        public event System.Windows.Input.MouseButtonEventHandler TitleBarDragStarted;

        private void PanelTitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            TitleBarDragStarted?.Invoke(this, e);
        }

        /// <summary>
        /// Set the format definition for enriched metadata display
        /// </summary>
        public void SetEnrichedFormat(Core.FormatDetection.FormatDefinition format)
        {
            if (EnrichedFormatPanel != null)
            {
                EnrichedFormatPanel.SetFormat(format);
            }
        }

        /// <summary>
        /// Clear the enriched format information
        /// </summary>
        public void ClearEnrichedFormat()
        {
            if (EnrichedFormatPanel != null)
            {
                EnrichedFormatPanel.ClearFormat();
            }
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

        private void FormatCandidateCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
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
            // Double-click on a field item opens the edit dialog
            if (sender is System.Windows.Controls.ListBoxItem item && item.Content is ParsedFieldViewModel field)
            {
                EditField(field);
                e.Handled = true; // Prevent event bubbling
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
                System.Windows.MessageBox.Show("This field cannot be edited.", "Edit Field",
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
                    System.Windows.MessageBox.Show($"Invalid value for type '{field.ValueType}'.\n\nPlease enter a valid value.",
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
                        "Export Complete",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error saving file: {ex.Message}", "Export Error",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private void ExportToClipboard(string content, string formatName)
        {
            try
            {
                System.Windows.Clipboard.SetText(content);
                System.Windows.MessageBox.Show($"All fields exported as {formatName} to clipboard!", "Export Complete",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error exporting fields: {ex.Message}", "Export Error",
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
                    System.Windows.MessageBox.Show($"Offset {field.OffsetHex} copied to clipboard!", "Copied",
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
                    System.Windows.MessageBox.Show($"No field found at offset {offset:X8}.", "Field Not Found",
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
                    System.Windows.MessageBox.Show($"Hex value copied to clipboard:\n{hexValue}", "Copied",
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
                    System.Windows.MessageBox.Show($"Decimal value copied to clipboard:\n{decValue}", "Copied",
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
                    System.Windows.MessageBox.Show($"Binary value copied to clipboard:\n{binValue}", "Copied",
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

                    System.Windows.MessageBox.Show($"Searching for fields with value: {searchValue}", "Find Similar",
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
                    System.Windows.MessageBox.Show("Data Inspector feature not yet connected to this panel.", "Feature Unavailable",
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
        /// Scroll to and select a specific field
        /// </summary>
        public void SelectField(ParsedFieldViewModel field)
        {
            FieldsListBox.SelectedItem = field;
            FieldsListBox.ScrollIntoView(field);
        }

        /// <summary>
        /// Clear all fields
        /// </summary>
        public void Clear()
        {
            ParsedFields.Clear();
            FilteredFields.Clear();
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
                // Separate metadata fields into spotlight section (C1)
                bool isMetadata = field.Offset < 0 ||
                    string.Equals(field.ValueType, "metadata", System.StringComparison.OrdinalIgnoreCase);

                if (isMetadata && !hasSearchFilter && typeFilter == 0 && !_showBookmarksOnly)
                {
                    MetadataFields.Add(field);
                    continue;
                }

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

            // Update validation badges (C2)
            UpdateValidationBadges(validCount, invalidCount);

            // Update computed values spotlight visibility (C1)
            if (ComputedValuesSection != null)
                ComputedValuesSection.Visibility = MetadataFields.Count > 0
                    ? Visibility.Visible : Visibility.Collapsed;

            // Build key insight chips (C4)
            BuildInsightChips();

            // Update byte coverage bar (C6)
            UpdateCoverageBar();
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
                    Background = HexBrush("#E3F2FD")
                });
            }

            // Look for color type in metadata
            var colorType = MetadataFields?.FirstOrDefault(f =>
                f.Name?.IndexOf("Color", StringComparison.OrdinalIgnoreCase) >= 0);
            if (colorType != null && !string.IsNullOrEmpty(colorType.FormattedValue) && colorType.FormattedValue != "0")
            {
                InsightChips.Add(new InsightChip
                {
                    Icon = "\U0001F3A8", // artist palette
                    Value = colorType.FormattedValue,
                    Background = HexBrush("#F3E5F5")
                });
            }

            // Look for formatted file size in metadata
            var sizeField = MetadataFields?.FirstOrDefault(f =>
                f.Name?.IndexOf("Size", StringComparison.OrdinalIgnoreCase) >= 0 ||
                f.Name?.IndexOf("Formatted", StringComparison.OrdinalIgnoreCase) >= 0);
            if (sizeField != null && !string.IsNullOrEmpty(sizeField.FormattedValue) &&
                sizeField.FormattedValue != "0" && sizeField.FormattedValue != "")
            {
                InsightChips.Add(new InsightChip
                {
                    Icon = "\U0001F4CA", // bar chart
                    Value = sizeField.FormattedValue,
                    Background = HexBrush("#E8F5E9")
                });
            }

            // Look for compression name in metadata
            var compression = MetadataFields?.FirstOrDefault(f =>
                f.Name?.IndexOf("Compression", StringComparison.OrdinalIgnoreCase) >= 0);
            if (compression != null && !string.IsNullOrEmpty(compression.FormattedValue) && compression.FormattedValue != "0")
            {
                InsightChips.Add(new InsightChip
                {
                    Icon = "\U0001F4E6", // package
                    Value = compression.FormattedValue,
                    Background = HexBrush("#FFF3E0")
                });
            }

            // Look for duration in metadata
            var duration = MetadataFields?.FirstOrDefault(f =>
                f.Name?.IndexOf("Duration", StringComparison.OrdinalIgnoreCase) >= 0);
            if (duration != null && !string.IsNullOrEmpty(duration.FormattedValue) && duration.FormattedValue != "0")
            {
                InsightChips.Add(new InsightChip
                {
                    Icon = "\U0001F554", // clock
                    Value = duration.FormattedValue,
                    Background = HexBrush("#E0F7FA")
                });
            }

            // Validation summary chip
            int totalFields = FilteredFields?.Count ?? 0;
            int invalidCount = FilteredFields?.Count(f => !f.IsValid) ?? 0;
            if (totalFields > 0)
            {
                if (invalidCount == 0)
                {
                    InsightChips.Add(new InsightChip
                    {
                        Icon = "\u2713", // check mark
                        Value = "All Valid",
                        Background = HexBrush("#E8F5E9")
                    });
                }
                else
                {
                    InsightChips.Add(new InsightChip
                    {
                        Icon = "\u26A0", // warning
                        Value = $"{invalidCount} Error{(invalidCount > 1 ? "s" : "")}",
                        Background = HexBrush("#FFEBEE")
                    });
                }
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

            CoverageProgressBar.Value = percent;
            CoverageText.Text = $"{percent:F0}% parsed ({coveredBytes:N0} / {TotalFileSize:N0} bytes)";
            CoverageBarSection.Visibility = Visibility.Visible;
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
                var bookmark = field.IsBookmarked ? "<span class=\"bookmarked\">⭐</span> " : "";

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
                var bookmark = field.IsBookmarked ? "⭐ " : "";
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

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// Information about a detected format
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

        public bool IsDetected
        {
            get => _isDetected;
            set
            {
                _isDetected = value;
                OnPropertyChanged();
            }
        }

        public string Name
        {
            get => _name;
            set
            {
                _name = value;
                OnPropertyChanged();
            }
        }

        public string Description
        {
            get => _description;
            set
            {
                _description = value;
                OnPropertyChanged();
            }
        }

        public string Category
        {
            get => _category;
            set
            {
                _category = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// List of format candidates for the dropdown selector
        /// </summary>
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

        /// <summary>
        /// Currently selected format candidate in the dropdown
        /// </summary>
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

        /// <summary>
        /// True when multiple candidates are available (shows dropdown instead of static text)
        /// </summary>
        public bool HasMultipleCandidates => _candidates?.Count > 1;

        /// <summary>
        /// Set selected candidate without triggering change events (for initial population)
        /// </summary>
        public void SetSelectedCandidateSilently(FormatCandidateItem item)
        {
            _isUpdatingSelection = true;
            _selectedCandidate = item;
            OnPropertyChanged(nameof(SelectedCandidate));
            _isUpdatingSelection = false;
        }

        private WpfHexaEditor.Core.FormatDetection.FormatReferences _references;

        /// <summary>
        /// References (specifications and web links) for this format
        /// </summary>
        public WpfHexaEditor.Core.FormatDetection.FormatReferences References
        {
            get => _references;
            set
            {
                _references = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasReferences));
            }
        }

        /// <summary>
        /// Returns true if this format has any references (specifications or web links)
        /// </summary>
        public bool HasReferences => References != null &&
            (References.Specifications?.Count > 0 || References.WebLinks?.Count > 0);

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// Lightweight wrapper for displaying a format candidate in a ComboBox
    /// </summary>
    public class FormatCandidateItem
    {
        public string DisplayName { get; set; }
        public WpfHexaEditor.Core.FormatDetection.FormatMatchCandidate Candidate { get; set; }

        public override string ToString() => DisplayName;
    }

    /// <summary>
    /// Event args when user selects a different format candidate
    /// </summary>
    public class FormatCandidateSelectedEventArgs : EventArgs
    {
        public WpfHexaEditor.Core.FormatDetection.FormatMatchCandidate Candidate { get; }

        public FormatCandidateSelectedEventArgs(WpfHexaEditor.Core.FormatDetection.FormatMatchCandidate candidate)
        {
            Candidate = candidate;
        }
    }

    /// <summary>
    /// Event args for field value edited
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
    /// Event args for Data Inspector request
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
}
