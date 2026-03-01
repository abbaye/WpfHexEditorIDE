//////////////////////////////////////////////
// Apache 2.0  - 2026
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
    public string Title           { get; init; } = "";
    public string Message         { get; init; } = "";
    public int    Percentage      { get; init; }
    public bool   IsIndeterminate { get; init; }
    public bool   CanCancel       { get; init; }
}

/// <summary>
/// Event arguments raised when a document long-running operation completes
/// (success, failure, or user cancellation).
/// </summary>
public class DocumentOperationCompletedEventArgs : EventArgs
{
    public bool   Success      { get; init; }
    public bool   WasCancelled { get; init; }
    public string ErrorMessage { get; init; } = "";
}
