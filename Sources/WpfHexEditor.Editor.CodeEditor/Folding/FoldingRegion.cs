// ==========================================================
// Project: WpfHexEditor.Editor.CodeEditor
// File: FoldingRegion.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-05
// Description:
//     Represents a collapsible region in the code editor
//     (e.g. a brace block or an indented section).
// ==========================================================

namespace WpfHexEditor.Editor.CodeEditor.Folding;

/// <summary>Identifies the visual style used to render the folding toggle for a region.</summary>
public enum FoldingRegionKind
{
    /// <summary>Brace-delimited block — rendered as [+] / [−] box in the gutter.</summary>
    Brace,

    /// <summary>#region / #endregion directive — rendered as ▶ / ▼ triangle in the gutter.</summary>
    Directive
}

/// <summary>
/// Describes a foldable region in the document, spanning from
/// <see cref="StartLine"/> to <see cref="EndLine"/> (both 0-based, inclusive).
/// </summary>
public sealed class FoldingRegion
{
    /// <summary>0-based index of the line that opens the region (e.g. the '{' line).</summary>
    public int StartLine { get; }

    /// <summary>0-based index of the closing line (e.g. the '}' line).</summary>
    public int EndLine { get; }

    /// <summary>
    /// Short text shown in place of the folded content (e.g. "{ … }").
    /// </summary>
    public string Label { get; }

    /// <summary>Whether this region is currently collapsed (hidden).</summary>
    public bool IsCollapsed { get; set; }

    /// <summary>Visual kind that determines how the gutter toggle is rendered.</summary>
    public FoldingRegionKind Kind { get; }

    /// <summary>Creates a brace-style region (default kind).</summary>
    public FoldingRegion(int startLine, int endLine, string label)
        : this(startLine, endLine, label, FoldingRegionKind.Brace) { }

    /// <summary>Creates a region with an explicit kind.</summary>
    public FoldingRegion(int startLine, int endLine, string label, FoldingRegionKind kind)
    {
        StartLine = startLine;
        EndLine   = endLine;
        Label     = label;
        Kind      = kind;
    }

    /// <summary>Number of body lines hidden when collapsed (EndLine − StartLine − 1).</summary>
    public int HiddenLineCount => EndLine - StartLine - 1;
}
