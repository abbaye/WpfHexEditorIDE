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

        public ParsedFieldsPanel()
        {
            InitializeComponent();
            DataContext = this;
            // Initialize FilteredFields FIRST to avoid NullReferenceException in ApplyFilter()
            FilteredFields = new ObservableCollection<ParsedFieldViewModel>();
            ParsedFields = new ObservableCollection<ParsedFieldViewModel>();
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
        /// Event fired when the formatter selection changes
        /// </summary>
        public event EventHandler<string> FormatterChanged;

        /// <summary>
        /// Event fired when a field value is edited
        /// </summary>
        public event EventHandler<FieldEditedEventArgs> FieldValueEdited;

        private void FieldsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FieldsListBox.SelectedItem is ParsedFieldViewModel field)
            {
                FieldSelected?.Invoke(this, field);
            }
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshRequested?.Invoke(this, EventArgs.Empty);
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

        private void EditValue_Click(object sender, RoutedEventArgs e)
        {
            if (FieldsListBox.SelectedItem is not ParsedFieldViewModel field)
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
            ExportToClipboard(ExportFieldsAsText(), "Text");
        }

        private void ExportAsJson_Click(object sender, RoutedEventArgs e)
        {
            ExportToClipboard(ExportFieldsAsJson(), "JSON");
        }

        private void ExportAsCsv_Click(object sender, RoutedEventArgs e)
        {
            ExportToClipboard(ExportFieldsAsCsv(), "CSV");
        }

        private void ExportAsXml_Click(object sender, RoutedEventArgs e)
        {
            ExportToClipboard(ExportFieldsAsXml(), "XML");
        }

        private void ExportAsHtml_Click(object sender, RoutedEventArgs e)
        {
            ExportToClipboard(ExportFieldsAsHtml(), "HTML");
        }

        private void ExportAsMarkdown_Click(object sender, RoutedEventArgs e)
        {
            ExportToClipboard(ExportFieldsAsMarkdown(), "Markdown");
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
        /// Apply search filter to fields
        /// </summary>
        private void ApplyFilter()
        {
            // Defensive check: ensure FilteredFields is initialized
            if (FilteredFields == null || ParsedFields == null)
                return;

            FilteredFields.Clear();

            // Apply filters
            var hasSearchFilter = !string.IsNullOrWhiteSpace(SearchText);
            var searchLower = hasSearchFilter ? SearchText.ToLowerInvariant() : null;

            foreach (var field in ParsedFields)
            {
                // Bookmark filter
                if (_showBookmarksOnly && !field.IsBookmarked)
                    continue;

                // Search filter
                if (hasSearchFilter)
                {
                    if (!(field.Name?.ToLowerInvariant().Contains(searchLower) == true ||
                          field.FormattedValue?.ToLowerInvariant().Contains(searchLower) == true ||
                          field.Description?.ToLowerInvariant().Contains(searchLower) == true ||
                          field.ValueType?.ToLowerInvariant().Contains(searchLower) == true ||
                          field.OffsetHex?.ToLowerInvariant().Contains(searchLower) == true))
                    {
                        continue;
                    }
                }

                FilteredFields.Add(field);
            }
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
                sb.AppendLine($"    \"description\": \"{EscapeJson(FormatInfo.Description)}\"");
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
