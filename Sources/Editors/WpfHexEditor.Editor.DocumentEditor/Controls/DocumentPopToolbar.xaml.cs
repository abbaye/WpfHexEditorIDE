// ==========================================================
// Project: WpfHexEditor.Editor.DocumentEditor
// File: Controls/DocumentPopToolbar.xaml.cs
// Description:
//     VS-style floating contextual toolbar that appears above the
//     current text selection. Provides formatting shortcuts and
//     navigation actions for the selected DocumentBlock.
// Architecture: Standalone UserControl hosted inside a Popup in
//     DocumentEditorHost. Communicates up via routed events.
// ==========================================================

using System.Windows;
using System.Windows.Controls;
using WpfHexEditor.Editor.DocumentEditor.Core.Model;

namespace WpfHexEditor.Editor.DocumentEditor.Controls;

/// <summary>
/// Floating selection toolbar shown inside a <see cref="System.Windows.Controls.Primitives.Popup"/>
/// when the user selects text in the <see cref="DocumentTextPane"/>.
/// </summary>
public partial class DocumentPopToolbar : UserControl
{
    // ── Events ────────────────────────────────────────────────────────────────

    /// <summary>Raised when the user clicks a format button (bold/italic/underline).</summary>
    public event EventHandler<string>? FormatRequested;

    /// <summary>Raised when the user clicks "Copy text".</summary>
    public event EventHandler<DocumentBlock?>? CopyTextRequested;

    /// <summary>Raised when the user clicks "Copy as hex".</summary>
    public event EventHandler<DocumentBlock?>? CopyHexRequested;

    /// <summary>Raised when the user clicks "Inspect" (navigate to Structure pane).</summary>
    public event EventHandler<DocumentBlock?>? InspectRequested;

    /// <summary>Raised when the user clicks "Jump to hex offset".</summary>
    public event EventHandler<DocumentBlock?>? JumpHexRequested;

    // ── Fields ────────────────────────────────────────────────────────────────

    private DocumentBlock? _contextBlock;

    // ── Constructor ──────────────────────────────────────────────────────────

    public DocumentPopToolbar()
    {
        InitializeComponent();
    }

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>Sets the block context for navigation actions.</summary>
    public void SetContext(DocumentBlock? block) => _contextBlock = block;

    // ── Click handlers ────────────────────────────────────────────────────────

    private void OnBoldClicked(object sender, RoutedEventArgs e)      => FormatRequested?.Invoke(this, "bold");
    private void OnItalicClicked(object sender, RoutedEventArgs e)    => FormatRequested?.Invoke(this, "italic");
    private void OnUnderlineClicked(object sender, RoutedEventArgs e) => FormatRequested?.Invoke(this, "underline");

    private void OnCopyTextClicked(object sender, RoutedEventArgs e)  => CopyTextRequested?.Invoke(this, _contextBlock);
    private void OnCopyHexClicked(object sender, RoutedEventArgs e)   => CopyHexRequested?.Invoke(this, _contextBlock);
    private void OnInspectClicked(object sender, RoutedEventArgs e)   => InspectRequested?.Invoke(this, _contextBlock);
    private void OnJumpHexClicked(object sender, RoutedEventArgs e)   => JumpHexRequested?.Invoke(this, _contextBlock);
}
