// ==========================================================
// Project: WpfHexEditor.SDK
// File: Contracts/Services/IScriptingService.cs
// Description:
//     Public SDK contract for the scripting engine.
//     Exposed via IDEHostContext.Scripting (nullable — absent if feature disabled).
//     Plugins use this to execute or validate C# scripts on behalf of the user.
// ==========================================================

namespace WpfHexEditor.SDK.Contracts.Services;

/// <summary>
/// Service for running C# scripts inside the IDE.
/// Accessible to plugins via <c>context.Scripting</c>.
/// </summary>
public interface IScriptingService
{
    /// <summary>
    /// Compiles and runs the supplied C# <paramref name="code"/> string.
    /// Returns a result containing output lines and any diagnostics.
    /// </summary>
    /// <param name="code">The C# script source code.</param>
    /// <param name="ct">Optional cancellation token (linked to the panel's Cancel button).</param>
    Task<IScriptResult> RunAsync(string code, CancellationToken ct = default);

    /// <summary>
    /// Compiles <paramref name="code"/> without running it.
    /// Useful for providing real-time error markers in the editor.
    /// </summary>
    Task<IScriptResult> ValidateAsync(string code, CancellationToken ct = default);

    /// <summary>Raised after each successful or failed script execution.</summary>
    event EventHandler<ScriptExecutedEventArgs>? ScriptExecuted;
}

/// <summary>Lightweight result DTO returned by <see cref="IScriptingService"/>.</summary>
public interface IScriptResult
{
    bool   Success   { get; }
    string Output    { get; }
    bool   HasErrors { get; }
    IReadOnlyList<IScriptDiagnostic> Diagnostics { get; }
    TimeSpan Duration { get; }
}

/// <summary>A single compiler or runtime diagnostic.</summary>
public interface IScriptDiagnostic
{
    string Message   { get; }
    int    Line      { get; }
    int    Column    { get; }
    bool   IsWarning { get; }
}

/// <summary>Event args for <see cref="IScriptingService.ScriptExecuted"/>.</summary>
public sealed class ScriptExecutedEventArgs(IScriptResult result) : EventArgs
{
    public IScriptResult Result { get; } = result;
}
