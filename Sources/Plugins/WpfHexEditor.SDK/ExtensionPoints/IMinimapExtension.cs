//////////////////////////////////////////////
// Project: WpfHexEditor.SDK
// File: ExtensionPoints/IMinimapExtension.cs
// Description:
//     Extension point allowing plugins to contribute decorations
//     to the CodeEditor minimap (e.g. error markers, search highlights,
//     bookmarks, git changes).
// Architecture:
//     Plugins implement this interface and register via IExtensionRegistry.
//     The MinimapControl queries all registered extensions during render.
//////////////////////////////////////////////

namespace WpfHexEditor.SDK.ExtensionPoints;

/// <summary>
/// A minimap decoration contributed by a plugin.
/// Rendered as a colored marker at a specific line range.
/// </summary>
public sealed class MinimapDecoration
{
    /// <summary>First line (1-based) of the decoration.</summary>
    public int StartLine { get; init; }

    /// <summary>Last line (1-based) of the decoration (same as StartLine for single-line).</summary>
    public int EndLine { get; init; }

    /// <summary>Color of the marker (ARGB hex, e.g. "#80FF0000" for semi-transparent red).</summary>
    public string Color { get; init; } = "#80FF0000";

    /// <summary>Tooltip text shown when hovering over the marker.</summary>
    public string? Tooltip { get; init; }

    /// <summary>
    /// Rendering lane: "left" (gutter), "center" (over code), or "right" (scrollbar-style).
    /// Default: "right".
    /// </summary>
    public string Lane { get; init; } = "right";
}

/// <summary>
/// Extension point for contributing decorations to the CodeEditor minimap.
/// Implement this interface and register via <c>IExtensionRegistry</c>
/// to add colored markers (errors, search results, bookmarks, etc.) to the minimap.
/// </summary>
public interface IMinimapExtension
{
    /// <summary>Human-readable name of this decoration provider (for diagnostics).</summary>
    string DisplayName { get; }

    /// <summary>
    /// Returns decorations for the given file. Called on each minimap render cycle
    /// (coalesced to ~150ms intervals). Return an empty list to clear decorations.
    /// </summary>
    /// <param name="filePath">Absolute path of the file being displayed.</param>
    /// <param name="totalLines">Total number of lines in the document.</param>
    /// <returns>Decorations to render on the minimap.</returns>
    IReadOnlyList<MinimapDecoration> GetDecorations(string filePath, int totalLines);
}
