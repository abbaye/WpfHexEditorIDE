// ==========================================================
// Project: WpfHexEditor.SDK
// File: Contracts/Services/IDocumentHostService.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     High-level document management service exposed to plugins and IDE modules.
//     Wraps IDocumentManager + IEditorRegistry + WHFMTDetectionAdapter to provide
//     a single entry point for opening, navigating, and saving documents.
//
// Architecture Notes:
//     Pattern: Facade
//     - Plugins and IDE panels (ErrorList, BuildSystem) should always use this
//       service rather than calling IDocumentManager directly.
//     - IDocumentManager is still accessible via the Documents property when
//       fine-grained lifecycle control is needed.
//     - ActivateAndNavigateTo is the primary entry point for ErrorList double-click.
// ==========================================================

using WpfHexEditor.Editor.Core.Documents;

namespace WpfHexEditor.SDK.Contracts.Services;

/// <summary>
/// High-level document management service.
/// Opens, activates, and navigates to document tabs in the IDE central editor area.
/// </summary>
public interface IDocumentHostService
{
    // -- State ----------------------------------------------------------------

    /// <summary>Low-level document registry (all open tabs and their live state).</summary>
    IDocumentManager Documents { get; }

    // -- Operations -----------------------------------------------------------

    /// <summary>
    /// Opens a document tab for <paramref name="filePath"/>.
    /// If a tab is already open for that file, activates it instead of creating a duplicate.
    /// </summary>
    /// <param name="filePath">Absolute path of the file to open.</param>
    /// <param name="preferredEditorId">
    /// Optional editor ID override (e.g. "hex-editor" to force hex view for a .cs file).
    /// When <c>null</c>, the preferred editor is determined via WHFMTDetectionAdapter.
    /// </param>
    void OpenDocument(string filePath, string? preferredEditorId = null);

    /// <summary>
    /// Opens (or activates) the document tab for <paramref name="filePath"/> and scrolls
    /// the editor to the specified 1-based line and column position.
    /// Intended for ErrorList double-click navigation.
    /// No-op if the editor does not implement <see cref="WpfHexEditor.Editor.Core.INavigableDocument"/>.
    /// </summary>
    /// <param name="filePath">Absolute path of the file.</param>
    /// <param name="line">1-based target line number.</param>
    /// <param name="column">1-based target column number. Pass 1 for start-of-line.</param>
    void ActivateAndNavigateTo(string filePath, int line, int column);

    /// <summary>
    /// Saves all documents with unsaved changes.
    /// Called by BuildSystem before starting a compilation.
    /// </summary>
    void SaveAll();
}
