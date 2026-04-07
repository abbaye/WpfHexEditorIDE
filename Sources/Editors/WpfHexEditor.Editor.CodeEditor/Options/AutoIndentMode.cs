// ==========================================================
// Project: WpfHexEditor.Editor.CodeEditor
// File: Options/AutoIndentMode.cs
// Description:
//     Controls the automatic indentation behaviour when the user
//     presses Enter in the code editor.
//
// Architecture Notes:
//     Consumed by CodeEditorOptions and the AutoIndentMode DP on CodeEditor.
// ==========================================================

namespace WpfHexEditor.Editor.CodeEditor.Options;

/// <summary>
/// Determines how the editor indents the new line after the user presses Enter.
/// </summary>
public enum AutoIndentMode
{
    /// <summary>No automatic indentation — the caret is placed at column 0.</summary>
    None,

    /// <summary>
    /// Copy the leading whitespace of the previous line.
    /// This is the classic "keep indent" behaviour (default).
    /// </summary>
    KeepIndent,

    /// <summary>
    /// Language-aware smart indentation: increases indent after opening braces,
    /// decreases after closing braces, etc.  Falls back to <see cref="KeepIndent"/>
    /// for languages that have no smart-indent rules defined.
    /// </summary>
    Smart,
}
