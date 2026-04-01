// ==========================================================
// Project: WpfHexEditor.Core.Scripting
// File: ScriptError.cs
// Description: Represents a single diagnostic from script compilation or runtime.
// ==========================================================

namespace WpfHexEditor.Core.Scripting;

/// <summary>
/// A diagnostic produced during script compilation or execution.
/// </summary>
public sealed record ScriptError(
    string Message,
    int    Line,
    int    Column,
    bool   IsWarning);
