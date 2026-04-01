// ==========================================================
// Project: WpfHexEditor.Editor.MarkdownEditor.Core
// File: Services/MarkdownThemeSelector.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-19
// Description:
//     Maps the active IDE theme name to a boolean indicating
//     whether the Markdown preview should use a dark stylesheet.
//
// Architecture Notes:
//     Strategy pattern — stateless, pure mapping function.
//     Called by MarkdownRenderService when building the HTML page.
//     Extracted from WpfHexEditor.Editor.MarkdownEditor into this
//     WPF-free Core project.
// ==========================================================

namespace WpfHexEditor.Editor.MarkdownEditor.Core.Services;

/// <summary>
/// Maps IDE theme names to the dark/light CSS variant selection
/// for the Markdown preview pane.
/// </summary>
public static class MarkdownThemeSelector
{
    // Dark IDE themes that should use the dark GitHub Markdown CSS.
    private static readonly HashSet<string> DarkThemeIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "dark",
        "vs2022dark",
        "visualstudio",
        "cyberpunk",
        "darkglass",
        "dracula",
        "gruvboxdark",
        "matrix",
        "synthwave84",
        "tokyonight",
        "nord",
        "catppuccinmocha",
        "forest",
        "highcontrast",
    };

    /// <summary>
    /// Returns <see langword="true"/> when the given theme name maps to a dark
    /// variant (GitHub dark CSS should be selected for the preview).
    /// </summary>
    public static bool IsDarkTheme(string? themeId)
        => DarkThemeIds.Contains(themeId ?? string.Empty);
}
