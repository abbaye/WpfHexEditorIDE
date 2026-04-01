// ==========================================================
// Project: WpfHexEditor.Editor.CodeEditor
// File: MultiCaret/CaretState.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Immutable snapshot of a single caret's position and optional selection.
//     Used by CaretManager as the per-caret state record.
//
// Architecture Notes:
//     Record type — value equality, deconstructible, immutable.
//     Lines and columns are 0-based internally; UI layer converts to 1-based for display.
// ==========================================================

namespace WpfHexEditor.Editor.CodeEditor.MultiCaret;

/// <summary>
/// Captures the position (and optional selection anchor) for a single caret.
/// </summary>
/// <param name="Line">0-based line index of the caret head.</param>
/// <param name="Column">0-based column index of the caret head.</param>
/// <param name="AnchorLine">0-based line of the selection anchor. Equal to <paramref name="Line"/> when there is no selection.</param>
/// <param name="AnchorColumn">0-based column of the selection anchor. Equal to <paramref name="Column"/> when there is no selection.</param>
public sealed record CaretState(int Line, int Column, int AnchorLine, int AnchorColumn)
{
    /// <summary>Creates a collapsed caret (no selection) at (<paramref name="line"/>, <paramref name="col"/>).</summary>
    public static CaretState At(int line, int col) => new(line, col, line, col);

    /// <summary><c>true</c> when the selection anchor differs from the caret head.</summary>
    public bool HasSelection
        => Line != AnchorLine || Column != AnchorColumn;

    /// <summary>
    /// Returns a new <see cref="CaretState"/> moved to (<paramref name="newLine"/>, <paramref name="newCol"/>).
    /// The anchor is updated to match (collapses any selection).
    /// </summary>
    public CaretState MoveTo(int newLine, int newCol)
        => new(newLine, newCol, newLine, newCol);

    /// <summary>
    /// Returns a new <see cref="CaretState"/> with the head moved to (<paramref name="newLine"/>, <paramref name="newCol"/>)
    /// while keeping the existing anchor (extends the selection).
    /// </summary>
    public CaretState ExtendTo(int newLine, int newCol)
        => new(newLine, newCol, AnchorLine, AnchorColumn);
}
