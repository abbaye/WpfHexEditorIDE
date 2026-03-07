//////////////////////////////////////////////
// Apache 2.0  - 2026
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

    public OutputPanel()
    {
        InitializeComponent();
        OutputLogger.Register(this);
        // Start with no-wrap: large page width = horizontal scroll
        OutputTextBox.Document.PageWidth = 10000;
        Loaded += (_, _) => UpdateAutoScrollVisual();
    }

    /// <summary>
    /// The internal RichTextBox used by <see cref="OutputLogger"/> to append messages.
    /// </summary>
    internal RichTextBox OutputBox => OutputTextBox;

    // --- Public append API (called by OutputLogger) --------------------

    /// <summary>
    /// Appends a line of text with an optional foreground color.
    /// <c>null</c> color = default theme foreground.
    /// </summary>
    internal void AppendLine(string text, Brush? color)
    {
        var run = new Run(text);
        if (color is not null) run.Foreground = color;

        var para = new Paragraph(run)
        {
            Margin               = new Thickness(0),
            LineHeight           = 16,
            LineStackingStrategy = LineStackingStrategy.BlockLineHeight
        };

        OutputTextBox.Document.Blocks.Add(para);

        if (_autoScroll)
            OutputTextBox.ScrollToEnd();
    }

    /// <summary>
    /// Clears all output.
    /// </summary>
    internal void ClearOutput() => OutputTextBox.Document.Blocks.Clear();

    /// <summary>
    /// Returns the full plain text of the document (for Copy All).
    /// </summary>
    internal string GetAllText() =>
        new TextRange(OutputTextBox.Document.ContentStart,
                      OutputTextBox.Document.ContentEnd).Text;

    // --- Toolbar handlers ----------------------------------------------

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
