// ==========================================================
// Project: WpfHexEditor.Editor.DocumentEditor.Core
// File: Options/IDocumentEditorOptionsService.cs
// Description:
//     Service contract for accessing and hot-reloading document editor options.
// ==========================================================

namespace WpfHexEditor.Editor.DocumentEditor.Core.Options;

/// <summary>
/// Provides access to the global document editor options and
/// notifies subscribers when settings change.
/// </summary>
public interface IDocumentEditorOptionsService
{
    /// <summary>Current global options snapshot.</summary>
    DocumentEditorOptions Current { get; }

    /// <summary>Raised when the global options are updated.</summary>
    event EventHandler? OptionsChanged;
}
