// ==========================================================
// Project: WpfHexEditor.Editor.Core
// File: SpellCheck/SpellCheckResult.cs
// Description:
//     Value types produced by ISpellChecker analysis of a text run.
// ==========================================================

namespace WpfHexEditor.Editor.Core.SpellCheck;

/// <summary>
/// A single misspelled word found within a text block.
/// Offsets are character positions within the block's raw text string.
/// </summary>
public sealed record SpellCheckResult(
    int    CharStart,
    int    CharLength,
    string Word);

/// <summary>
/// Canvas-space error marker derived from <see cref="SpellCheckResult"/>
/// after the renderer maps character offsets to pixel coordinates.
/// </summary>
public sealed record SpellCheckError(
    SpellCheckResult Source,
    double           CanvasX,
    double           CanvasY,
    double           CanvasWidth,
    double           LineHeight);
