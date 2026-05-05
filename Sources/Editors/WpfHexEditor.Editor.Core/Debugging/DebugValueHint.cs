// ==========================================================
// Project: WpfHexEditor.Editor.Core
// File: Debug/DebugValueHint.cs
// Description:
//     Data carrier for one inline debug-value annotation produced by
//     IDebugValueProvider during a debugger pause. Consumed by
//     DebugValueHintsLayer to render " = <value>" pills in the editor.
// Architecture: Immutable record — no WPF dependency.
// ==========================================================

namespace WpfHexEditor.Editor.Core.Debugging;

/// <summary>Kind of value shown in a debug-value hint.</summary>
public enum DebugValueKind
{
    /// <summary>Local variable or parameter.</summary>
    Local,
    /// <summary>Expression result (watch / conditional breakpoint).</summary>
    Expression,
    /// <summary>Return value from the last method call.</summary>
    ReturnValue,
}

/// <summary>
/// One inline debug-value annotation: the name/value pair rendered as a
/// grey pill after the identifier on a specific source line.
/// </summary>
/// <param name="Line">0-based source line.</param>
/// <param name="Column">0-based character column (end of the identifier).</param>
/// <param name="Name">Variable or expression name.</param>
/// <param name="Value">String representation of the current value.</param>
/// <param name="Kind">Classification (Local / Expression / ReturnValue).</param>
public sealed record DebugValueHint(
    int            Line,
    int            Column,
    string         Name,
    string         Value,
    DebugValueKind Kind = DebugValueKind.Local);
