// ==========================================================
// Project: WpfHexEditor.Editor.DocumentEditor.Core
// File: Options/DocumentEditorContext.cs
// Description:
//     Per-document options context. Merges the global default with an
//     optional per-document override so settings can be customised
//     on a file-by-file basis without touching the global settings.
// ==========================================================

namespace WpfHexEditor.Editor.DocumentEditor.Core.Options;

/// <summary>
/// Resolves effective options for a single open document by merging
/// the global <see cref="DocumentEditorOptions"/> with an optional
/// per-document override.
/// </summary>
public sealed class DocumentEditorContext
{
    public DocumentEditorContext(DocumentEditorOptions global)
    {
        Global = global;
    }

    /// <summary>IDE-wide defaults from <c>AppSettings.DocumentEditor</c>.</summary>
    public DocumentEditorOptions Global { get; }

    /// <summary>
    /// Per-document override. When set, properties from this object take
    /// precedence over <see cref="Global"/> via <see cref="Effective"/>.
    /// </summary>
    public DocumentEditorOptions? DocumentOverride { get; set; }

    /// <summary>
    /// Returns the effective options: document override if set, otherwise global.
    /// </summary>
    public DocumentEditorOptions Effective => DocumentOverride ?? Global;

    // Future: full property-level merge can be implemented here if needed.
}
