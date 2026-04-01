//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.Editor.Core;

/// <summary>
/// Event arguments for the start and progress of a document long-running operation.
/// Mirrors WpfHexEditor.Core.Events.OperationProgressEventArgs without creating
/// a cross-assembly dependency from Editor.Core to Core.
/// </summary>
public class DocumentOperationEventArgs : EventArgs
{
    public string Title           { get; set; } = "";
    public string Message         { get; set; } = "";
    public int    Percentage      { get; set; }
    public bool   IsIndeterminate { get; set; }
    public bool   CanCancel       { get; set; }
}

/// <summary>
/// Event arguments raised when a document long-running operation completes
/// (success, failure, or user cancellation).
/// </summary>
public class DocumentOperationCompletedEventArgs : EventArgs
{
    public bool   Success           { get; set; }
    public bool   WasCancelled      { get; set; }
    public string ErrorMessage      { get; set; } = "";
    /// <summary>When true, the host should silently close the editor tab after logging the error.</summary>
    public bool   CloseTabOnFailure { get; set; }
}
