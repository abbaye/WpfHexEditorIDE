//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.6, Claude Sonnet 4.6
//////////////////////////////////////////////

using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Microsoft.Win32;

namespace WpfHexEditor.App.Controls;

/// <summary>
/// Bindable model for a single output channel shown in the source ComboBox.
/// Exposes <see cref="HasUnread"/> so the ItemTemplate can show/hide the dot.
/// </summary>
internal sealed class OutputChannel : INotifyPropertyChanged
{
    private bool _hasUnread;

    public OutputChannel(string name, FlowDocument document)
    {
        Name     = name;
        Document = document;
    }

    public string       Name     { get; }
    public FlowDocument Document { get; }

    public bool HasUnread
    {
        get => _hasUnread;
        set
        {
            if (_hasUnread == value) return;
            _hasUnread = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasUnread)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

/// <summary>
/// VS-style Output panel with toolbar (source filter, clear, word wrap, copy, auto-scroll).
/// Supports colored output per log level via <see cref="AppendLine"/>.
/// Register with <see cref="OutputLogger.Register"/> to receive log messages.
/// </summary>
public partial class OutputPanel : UserControl
{
    private bool _autoScroll = true;
    private bool _wordWrap   = false;

    private readonly Dictionary<string, OutputChannel> _channels = new();
    private string _activeSource = "General";

    public OutputPanel()
    {
        InitializeComponent();
        OutputLogger.Register(this);

        var names = new[] { "General", "Plugin System", "Build", "Debug", "Unit Testing", "Language Server" };
        foreach (var name in names)
            _channels[name] = new OutputChannel(name, CreateDocument());

        SourceComboBox.ItemsSource  = _channels.Values.ToList();
        SourceComboBox.SelectedIndex = 0;
        OutputTextBox.Document = _channels[_activeSource].Document;
        Loaded += (_, _) =>
        {
            UpdateAutoScrollVisual();
            SyncMenuChecks();
        };
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
        if (!_channels.TryGetValue(source, out var channel))
            return;

        var run = new Run(text);
        if (color is not null) run.Foreground = color;

        var para = new Paragraph(run)
        {
            Margin               = new Thickness(0),
            LineHeight           = 16,
            LineStackingStrategy = LineStackingStrategy.BlockLineHeight
        };

        channel.Document.Blocks.Add(para);

        if (source != _activeSource)
            channel.HasUnread = true;

        if (_autoScroll && source == _activeSource)
            OutputTextBox.ScrollToEnd();
    }

    /// <summary>
    /// Clears the currently active source channel.
    /// </summary>
    internal void ClearOutput()
    {
        if (_channels.TryGetValue(_activeSource, out var channel))
            channel.Document.Blocks.Clear();
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
        if (SourceComboBox.SelectedItem is not OutputChannel channel) return;
        _activeSource    = channel.Name;
        channel.HasUnread = false;

        OutputTextBox.Document = channel.Document;
        channel.Document.PageWidth = _wordWrap ? double.NaN : 10000;
        if (_autoScroll) OutputTextBox.ScrollToEnd();
    }

    private void OnClear(object sender, RoutedEventArgs e)
        => OutputLogger.Clear();

    private void OnToggleWordWrap(object sender, RoutedEventArgs e)
    {
        // When invoked from a checkable MenuItem, the IsChecked has already
        // toggled before the Click event — align our internal state with it
        // instead of toggling again.
        _wordWrap = sender is MenuItem mi
            ? mi.IsChecked
            : !_wordWrap;

        OutputTextBox.Document.PageWidth = _wordWrap ? double.NaN : 10000;
        OutputTextBox.HorizontalScrollBarVisibility = _wordWrap
            ? ScrollBarVisibility.Disabled
            : ScrollBarVisibility.Auto;
        WrapButton.Opacity = _wordWrap ? 1.0 : 0.5;
        SyncMenuChecks();
    }

    private void OnCopyAll(object sender, RoutedEventArgs e)
    {
        var text = GetAllText();
        if (!string.IsNullOrEmpty(text))
            Clipboard.SetText(text);
    }

    private void OnToggleAutoScroll(object sender, RoutedEventArgs e)
    {
        _autoScroll = sender is MenuItem mi
            ? mi.IsChecked
            : !_autoScroll;
        UpdateAutoScrollVisual();
        SyncMenuChecks();
    }

    private void OnSaveOutputAs(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "Rich Text Format (*.rtf)|*.rtf|Text files (*.txt)|*.txt|Log files (*.log)|*.log|All files (*.*)|*.*",
            FilterIndex = 1,
            DefaultExt = ".rtf",
            FileName = $"{_activeSource}-output.rtf"
        };

        if (dialog.ShowDialog() != true) return;

        var ext = Path.GetExtension(dialog.FileName);
        var format = string.Equals(ext, ".rtf", StringComparison.OrdinalIgnoreCase)
            ? DataFormats.Rtf
            : DataFormats.Text;

        var range = new TextRange(OutputTextBox.Document.ContentStart,
                                  OutputTextBox.Document.ContentEnd);
        using var fs = File.Create(dialog.FileName);
        range.Save(fs, format);
    }

    private void SyncMenuChecks()
    {
        if (WordWrapMenuItem   != null) WordWrapMenuItem.IsChecked   = _wordWrap;
        if (AutoScrollMenuItem != null) AutoScrollMenuItem.IsChecked = _autoScroll;
    }

    /// <summary>
    /// Returns the last <paramref name="count"/> lines from the specified source channel as plain strings.
    /// Called by <see cref="OutputLogger.GetRecentLines"/>.
    /// </summary>
    internal IReadOnlyList<string> GetRecentLinesFromSource(string source, int count)
    {
        if (!_channels.TryGetValue(source, out var channel)) return [];
        var result = new List<string>(count);
        foreach (var block in channel.Document.Blocks.Reverse().OfType<Paragraph>())
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
        if (_channels.TryGetValue(source, out var channel))
            SourceComboBox.SelectedItem = channel;
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
