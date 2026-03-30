// ==========================================================
// Project: WpfHexEditor.Editor.DocumentEditor.Core
// File: Options/DocumentEditorOptionsService.cs
// Description:
//     Default implementation of IDocumentEditorOptionsService.
//     Wraps a mutable DocumentEditorOptions and exposes OptionsChanged
//     so the WPF layer can refresh when settings are updated.
// ==========================================================

namespace WpfHexEditor.Editor.DocumentEditor.Core.Options;

/// <summary>
/// Simple implementation of <see cref="IDocumentEditorOptionsService"/>.
/// </summary>
public sealed class DocumentEditorOptionsService : IDocumentEditorOptionsService
{
    private DocumentEditorOptions _current;

    public DocumentEditorOptionsService(DocumentEditorOptions initial)
    {
        _current = initial;
    }

    /// <inheritdoc/>
    public DocumentEditorOptions Current => _current;

    /// <inheritdoc/>
    public event EventHandler? OptionsChanged;

    /// <summary>
    /// Replaces the current options and raises <see cref="OptionsChanged"/>.
    /// </summary>
    public void Update(DocumentEditorOptions newOptions)
    {
        _current = newOptions;
        OptionsChanged?.Invoke(this, EventArgs.Empty);
    }
}
