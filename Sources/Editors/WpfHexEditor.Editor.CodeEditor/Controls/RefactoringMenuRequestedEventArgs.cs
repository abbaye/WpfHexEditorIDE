// ==========================================================
// Project: WpfHexEditor.Editor.CodeEditor
// File: Controls/RefactoringMenuRequestedEventArgs.cs
// Description:
//     Event payload carrying the refactoring picked by the user in the
//     CodeEditor Refactor ▶ submenu plus the document snapshot used to
//     build a RefactoringContext on the host side.
// ==========================================================

namespace WpfHexEditor.Editor.CodeEditor.Controls;

/// <summary>Refactoring identifiers surfaced by the CodeEditor Refactor menu.</summary>
public enum RefactoringKind
{
    Rename,
    ExtractMethod,
    ExtractClass,
    IntroduceVariable,
    InlineMethod,
}

/// <summary>Refactoring request raised from the CodeEditor Refactor menu.</summary>
public sealed class RefactoringMenuRequestedEventArgs : EventArgs
{
    public RefactoringKind Kind            { get; }
    public string          DocumentText    { get; init; } = string.Empty;
    public string          FilePath        { get; init; } = string.Empty;
    public int             CaretOffset     { get; init; }
    public int             SelectionStart  { get; init; }
    public int             SelectionLength { get; init; }

    public RefactoringMenuRequestedEventArgs(RefactoringKind kind) => Kind = kind;
}
