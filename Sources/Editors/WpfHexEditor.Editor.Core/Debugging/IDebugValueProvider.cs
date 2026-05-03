// ==========================================================
// Project: WpfHexEditor.Editor.Core
// File: Debug/IDebugValueProvider.cs
// Description:
//     Contract between the DAP (Debug Adapter Protocol) integration layer
//     and DebugValueHintsLayer. When a debug session pauses, the debugger
//     subsystem sets an IDebugValueProvider on the active CodeEditor so the
//     layer can render inline variable/expression values.
// Architecture: Interface only — no WPF or DAP dependency here.
// ==========================================================

namespace WpfHexEditor.Editor.Core.Debugging;

/// <summary>
/// Provides inline variable values for the current debugger pause state.
/// Implemented by the DAP integration layer (WpfHexEditor.Debugger, #44).
/// </summary>
public interface IDebugValueProvider
{
    /// <summary>
    /// Returns the set of debug-value hints visible in
    /// [<paramref name="startLine"/>, <paramref name="endLine"/>] for the given file.
    /// Returns empty when the session is running or the file is not paused.
    /// </summary>
    IReadOnlyList<DebugValueHint> GetValues(string filePath, int startLine, int endLine);
}
