//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.6
//////////////////////////////////////////////

using System.Windows;
using System.Windows.Controls;

namespace WpfHexEditor.App.Controls;

/// <summary>
/// VS-style Output panel with toolbar (source filter, clear, word wrap, copy, auto-scroll).
/// Register with <see cref="OutputLogger.Register"/> to receive log messages.
/// </summary>
public partial class OutputPanel : UserControl
{
    private bool _autoScroll = true;

    public OutputPanel()
    {
        InitializeComponent();
        OutputLogger.Register(this);
        Loaded += (_, _) => UpdateAutoScrollVisual();
    }

    /// <summary>
    /// The internal TextBox used by <see cref="OutputLogger"/> to append messages.
    /// </summary>
    internal TextBox TextBox => OutputTextBox;

    // ─── Toolbar handlers ──────────────────────────────────────────────

    private void OnClear(object sender, RoutedEventArgs e)
    {
        OutputLogger.Clear();
    }

    private void OnToggleWordWrap(object sender, RoutedEventArgs e)
    {
        var isWrapping = OutputTextBox.TextWrapping == TextWrapping.Wrap;
        OutputTextBox.TextWrapping = isWrapping ? TextWrapping.NoWrap : TextWrapping.Wrap;
        OutputTextBox.HorizontalScrollBarVisibility = isWrapping
            ? ScrollBarVisibility.Auto
            : ScrollBarVisibility.Disabled;

        WrapButton.Opacity = isWrapping ? 0.5 : 1.0;
    }

    private void OnCopyAll(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(OutputTextBox.Text))
            Clipboard.SetText(OutputTextBox.Text);
    }

    private void OnToggleAutoScroll(object sender, RoutedEventArgs e)
    {
        _autoScroll = !_autoScroll;
        UpdateAutoScrollVisual();
    }

    /// <summary>
    /// Scrolls to the end if auto-scroll is enabled. Called by <see cref="OutputLogger.Append"/>.
    /// </summary>
    internal void ScrollToEndIfEnabled()
    {
        if (_autoScroll)
            OutputTextBox.ScrollToEnd();
    }

    private void UpdateAutoScrollVisual()
    {
        AutoScrollButton.Opacity = _autoScroll ? 1.0 : 0.5;
    }
}
