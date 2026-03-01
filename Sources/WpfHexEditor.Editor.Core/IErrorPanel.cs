//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System;

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
    /// Raised when the user requests navigation to an entry (e.g. double-click).
    /// The host should open/activate the file and scroll to the indicated offset/line.
    /// </summary>
    event EventHandler<DiagnosticEntry>? EntryNavigationRequested;
}
