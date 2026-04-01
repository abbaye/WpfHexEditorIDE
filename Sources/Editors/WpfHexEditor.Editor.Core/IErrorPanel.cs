//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System;
using System.Collections.Generic;

namespace WpfHexEditor.Editor.Core;

/// <summary>
/// Contract for the error panel that aggregates and displays diagnostics
/// from multiple <see cref="IDiagnosticSource"/> instances.
/// </summary>
public interface IErrorPanel
{
    /// <summary>
    /// Registers a diagnostic source and subscribes to its changes.
    /// </summary>
    void AddSource(IDiagnosticSource source);

    /// <summary>
    /// Unregisters a diagnostic source and unsubscribes from its changes.
    /// </summary>
    void RemoveSource(IDiagnosticSource source);

    /// <summary>
    /// Removes all sources and clears the displayed entries.
    /// </summary>
    void ClearAll();

    /// <summary>
    /// Controls which scope of diagnostics is displayed.
    /// </summary>
    ErrorPanelScope Scope { get; set; }

    /// <summary>
    /// Updates the list of currently open document paths.
    /// Used for <see cref="ErrorPanelScope.OpenDocuments"/> filtering.
    /// </summary>
    void SetOpenDocuments(IReadOnlyCollection<string> paths);

    /// <summary>
    /// Updates the list of currently modified (dirty) document paths.
    /// Used for <see cref="ErrorPanelScope.ChangedDocuments"/> filtering.
    /// </summary>
    void SetChangedDocuments(IReadOnlyCollection<string> paths);

    /// <summary>
    /// Raised when the user requests navigation to an entry (e.g. double-click).
    /// The host should open/activate the file and scroll to the indicated offset/line.
    /// </summary>
    event EventHandler<DiagnosticEntry>? EntryNavigationRequested;

    /// <summary>
    /// Raised when the user explicitly requests the file be opened in the built-in
    /// text editor (context menu "Open in Text Editor").
    /// The host should open the file via the text editor factory and navigate to
    /// <see cref="DiagnosticEntry.Line"/>/<see cref="DiagnosticEntry.Column"/>.
    /// </summary>
    event EventHandler<DiagnosticEntry>? OpenInTextEditorRequested;
}
