//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.6, Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace WpfHexEditor.App.Controls;

/// <summary>
/// VS-style Output panel with toolbar (source filter, clear, word wrap, copy, auto-scroll).
/// Supports colored output per log level via <see cref="AppendLine"/>.
/// Register with <see cref="OutputLogger.Register"/> to receive log messages.
/// </summary>
public partial class OutputPanel : UserControl
{
    private bool _autoScroll = true;
    private bool _wordWrap   = false;

    // One FlowDocument per named source channel.
    private readonly Dictionary<string, FlowDocument> _sourceDocs = new();
    private string _activeSource = "General";

    public OutputPanel()
    {
        InitializeComponent();
        OutputLogger.Register(this);

        // Pre-create one document per source channel.
        foreach (var src in new[] { "General", "Plugin System", "Build", "Debug", "Unit Testing" })
            _sourceDocs[src] = CreateDocument();

        OutputTextBox.Document = _sourceDocs[_activeSource];
        Loaded += (_, _) => UpdateAutoScrollVisual();
    }

    private static FlowDocument CreateDocument()
        => new FlowDocument { PagePadding = new Thickness(0), PageWidth = 10000 };

    /// <summary>
    /// The internal RichTextBox used by <see cref="OutputLogger"/> to append messages.
    /// </summary>
    internal RichTextBox OutputBox => OutputTextBox;

    /// <summary>
    /// The currently selected source channel name (e.g. "General", "Plugin System").
    /// </summary>
    internal string ActiveSource => _activeSource;

    // --- Public append API (called by OutputLogger) --------------------

    /// <summary>
    /// Appends a line of text to the given source channel with an optional foreground color.
    /// <c>null</c> color = default theme foreground.
    /// </summary>
    internal void AppendLine(string text, Brush? color, string source = "General")
    {
        if (!_sourceDocs.TryGetValue(source, out var doc))
        {
            doc = CreateDocument();
            _sourceDocs[source] = doc;
        }

        var run = new Run(text);
        if (color is not null) run.Foreground = color;

        var para = new Paragraph(run)
        {
            Margin               = new Thickness(0),
            LineHeight           = 16,
            LineStackingStrategy = LineStackingStrategy.BlockLineHeight
        };

        doc.Blocks.Add(para);

        if (_autoScroll && source == _activeSource)
            OutputTextBox.ScrollToEnd();
    }

    /// <summary>
    /// Clears the currently active source channel.
    /// </summary>
    internal void ClearOutput()
    {
        if (_sourceDocs.TryGetValue(_activeSource, out var doc))
            doc.Blocks.Clear();
    }

    /// <summary>
    /// Returns the full plain text of the active source document (for Copy All).
    /// </summary>
    internal string GetAllText() =>
        new TextRange(OutputTextBox.Document.ContentStart,
                      OutputTextBox.Document.ContentEnd).Text;

    // --- Toolbar handlers ----------------------------------------------

    private void OnSourceChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SourceComboBox.SelectedItem is not ComboBoxItem item) return;
        _activeSource = item.Content?.ToString() ?? "General";
        if (_sourceDocs.TryGetValue(_activeSource, out var doc))
        {
            OutputTextBox.Document = doc;
            // Apply current word-wrap preference to the newly displayed document.
            doc.PageWidth = _wordWrap ? double.NaN : 10000;
            if (_autoScroll) OutputTextBox.ScrollToEnd();
        }
    }

    private void OnClear(object sender, RoutedEventArgs e)
        => OutputLogger.Clear();

    private void OnToggleWordWrap(object sender, RoutedEventArgs e)
    {
        _wordWrap = !_wordWrap;
        OutputTextBox.Document.PageWidth = _wordWrap ? double.NaN : 10000;
        OutputTextBox.HorizontalScrollBarVisibility = _wordWrap
            ? ScrollBarVisibility.Disabled
            : ScrollBarVisibility.Auto;
        WrapButton.Opacity = _wordWrap ? 1.0 : 0.5;
    }

    private void OnCopyAll(object sender, RoutedEventArgs e)
    {
        var text = GetAllText();
        if (!string.IsNullOrEmpty(text))
            Clipboard.SetText(text);
    }

    private void OnToggleAutoScroll(object sender, RoutedEventArgs e)
    {
        _autoScroll = !_autoScroll;
        UpdateAutoScrollVisual();
    }

    /// <summary>
    /// Returns the last <paramref name="count"/> lines from the specified source channel as plain strings.
    /// Called by <see cref="OutputLogger.GetRecentLines"/>.
    /// </summary>
    internal IReadOnlyList<string> GetRecentLinesFromSource(string source, int count)
    {
        if (!_sourceDocs.TryGetValue(source, out var doc)) return [];
        var result = new List<string>(count);
        foreach (var block in doc.Blocks.Reverse().OfType<Paragraph>())
        {
            if (result.Count >= count) break;
            result.Insert(0, new TextRange(block.ContentStart, block.ContentEnd).Text.TrimEnd());
        }
        return result;
    }

    /// <summary>
    /// Programmatically switches the active source channel and updates the ComboBox.
    /// Called by <see cref="OutputLogger.FocusChannel"/>.
    /// </summary>
    internal void SetActiveSource(string source)
    {
        foreach (ComboBoxItem item in SourceComboBox.Items)
        {
            if (item.Content?.ToString() == source)
            {
                SourceComboBox.SelectedItem = item;
                break;
            }
        }
    }

    /// <summary>
    /// Scrolls to the end if auto-scroll is enabled. Called by <see cref="OutputLogger"/>.
    /// </summary>
    internal void ScrollToEndIfEnabled()
    {
        if (_autoScroll)
            OutputTextBox.ScrollToEnd();
    }

    private void UpdateAutoScrollVisual()
        => AutoScrollButton.Opacity = _autoScroll ? 1.0 : 0.5;
}
