// ==========================================================
// Project: WpfHexEditor.Core.Scripting
// File: ScriptResult.cs
// Description: Return value of IScriptEngine.RunAsync / ValidateAsync.
// ==========================================================

namespace WpfHexEditor.Core.Scripting;

/// <summary>
/// Result of a script execution or validation pass.
/// </summary>
public sealed record ScriptResult(
    bool                       Success,
    string                     Output,
    IReadOnlyList<ScriptError> Errors,
    TimeSpan                   Duration,
    Exception?                 Exception = null)
{
    /// <summary>True when the script produced at least one compilation error.</summary>
    public bool HasErrors => Errors.Any(e => !e.IsWarning);

    /// <summary>Shortcut for a succeeded result with no output.</summary>
    public static ScriptResult Ok(TimeSpan duration) =>
        new(true, string.Empty, [], duration);

    /// <summary>Shortcut for a failed result with a single error message.</summary>
    public static ScriptResult Fail(string message, Exception? ex = null) =>
        new(false, string.Empty,
            [new ScriptError(message, 0, 0, false)],
            TimeSpan.Zero, ex);
}
