//////////////////////////////////////////////
// Apache 2.0  - 2026
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
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
        private FormatInfo _formatInfo;
        private string _searchText;
        private bool _showBookmarksOnly;
        private string _searchResultText;
        private bool _hasSelection;

        public ParsedFieldsPanel()
        {
            InitializeComponent();
            DataContext = this;
            // Initialize FilteredFields FIRST to avoid NullReferenceException in ApplyFilter()
            FilteredFields = new ObservableCollection<ParsedFieldViewModel>();
            ParsedFields = new ObservableCollection<ParsedFieldViewModel>();

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
        /// Handler for clicking hyperlinks in references section
        /// Opens the URL in the default browser
        /// </summary>
        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = e.Uri.AbsoluteUri,
                    UseShellExecute = true
                });
                e.Handled = true;
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Could not open link: {ex.Message}",
                    "Error", System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Handler for Copy References button
        /// Copies specifications and web links to clipboard
        /// </summary>
        private void CopyReferences_Click(object sender, RoutedEventArgs e)
        {
            if (FormatInfo?.References == null)
                return;

            try
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"Format: {FormatInfo.Name}");
                sb.AppendLine($"Category: {FormatInfo.Category}");
                sb.AppendLine();

                if (FormatInfo.References.Specifications?.Count > 0)
                {
                    sb.AppendLine("Specifications:");
                    foreach (var spec in FormatInfo.References.Specifications)
                        sb.AppendLine($"  - {spec}");
                    sb.AppendLine();
                }

                if (FormatInfo.References.WebLinks?.Count > 0)
                {
                    sb.AppendLine("Web Links:");
                    foreach (var link in FormatInfo.References.WebLinks)
                        sb.AppendLine($"  - {link}");
                }

                System.Windows.Clipboard.SetText(sb.ToString());
                System.Windows.MessageBox.Show("References copied to clipboard!",
                    "Copied", System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error copying references: {ex.Message}");
                System.Windows.MessageBox.Show($"Error copying references: {ex.Message}",
                    "Error", System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
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
            // Defensive check: ensure FilteredFields is initialized
            if (FilteredFields == null || ParsedFields == null)
                return;

            FilteredFields.Clear();

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

                FilteredFields.Add(field);
            }

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
}
