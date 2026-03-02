//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using WpfHexEditor.Editor.Core;

namespace WpfHexEditor.App.Controls;

/// <summary>
/// A thin, dismissible banner shown above any non-hex viewer (ImageViewer, TblEditor, etc.)
/// to surface quick "View in …" cross-editor action buttons.
/// <para>
/// Call <see cref="Configure"/> once after creation.  The bar fires
/// <see cref="OpenWithRequested"/> when the user clicks an action button.
/// </para>
/// </summary>
public partial class DocumentInfoBar : UserControl
{
    // ── Segoe MDL2 icon codepoints for common editor types ─────────────────
    private static readonly Dictionary<string, string> _editorIcons = new(StringComparer.OrdinalIgnoreCase)
    {
        { "image-viewer",       "\uEB9F" },   // Photo
        { "tbl-editor",         "\uE8FD" },   // Page
        { "json-editor",        "\uE943" },   // Code
        { "text-editor",        "\uE8A5" },   // Document
        { "entropy-viewer",     "\uE9D9" },   // Chart
        { "diff-viewer",        "\uE8C8" },   // Split
        { "disassembly-viewer", "\uE943" },   // Code
        { "structure-editor",   "\uE8A5" },   // Layers
        { "tile-editor",        "\uEB9F" },   // Tiles
        { "audio-viewer",       "\uE8D6" },   // Music
        { "script-editor",      "\uE943" },   // Script
        { "changeset-editor",   "\uE8AB" },   // History
    };

    private string _filePath        = string.Empty;
    private string _sourceContentId = string.Empty;

    /// <summary>
    /// Fired when the user clicks one of the "View in …" action buttons.
    /// </summary>
    public event EventHandler<OpenWithEditorRequestedEventArgs>? OpenWithRequested;

    public DocumentInfoBar()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Configures the bar content.
    /// </summary>
    /// <param name="filePath">Absolute path of the document being viewed.</param>
    /// <param name="sourceContentId">ContentId of the host <c>DockItem</c> tab.</param>
    /// <param name="currentEditorName">Display name of the currently active editor.</param>
    /// <param name="currentEditorId">Factory id of the current editor (used for icon lookup).</param>
    /// <param name="alternatives">Alternative editor factories to offer as action buttons.</param>
    public void Configure(
        string filePath,
        string sourceContentId,
        string currentEditorName,
        string currentEditorId,
        IEnumerable<IEditorFactory> alternatives)
    {
        _filePath        = filePath;
        _sourceContentId = sourceContentId;

        // Set icon + name for the current editor
        EditorIcon.Text    = _editorIcons.TryGetValue(currentEditorId, out var ic) ? ic : "\uE8A5";
        EditorNameText.Text = currentEditorName;

        // Remove previously added dynamic buttons (keep ViewInLabel)
        while (ActionButtons.Children.Count > 1)
            ActionButtons.Children.RemoveAt(ActionButtons.Children.Count - 1);

        // Always offer Hex Editor as the first action
        ActionButtons.Children.Add(MakeButton("Hex Editor", null));

        // Add alternatives from registry
        foreach (var factory in alternatives)
            ActionButtons.Children.Add(MakeButton(factory.Descriptor.DisplayName, factory.Descriptor.Id));
    }

    // ── Event handlers ──────────────────────────────────────────────────────

    private void OnDismiss(object sender, RoutedEventArgs e)
        => Visibility = Visibility.Collapsed;

    private void OnActionButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string factoryId })
            OpenWithRequested?.Invoke(this, new OpenWithEditorRequestedEventArgs
            {
                FactoryId       = factoryId,
                FilePath        = _filePath,
                SourceContentId = _sourceContentId,
            });
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private Button MakeButton(string label, string? factoryId)
    {
        var btn = new Button
        {
            Content = label,
            Tag     = factoryId,        // null ⇒ Hex Editor fallback
            Style   = (Style)FindResource("InfoBarButtonStyle"),
        };
        btn.Click += OnActionButtonClick;
        return btn;
    }
}
