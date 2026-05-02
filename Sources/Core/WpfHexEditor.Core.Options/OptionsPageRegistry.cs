//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Windows.Controls;
using WpfHexEditor.Core.Options.Pages;
using static WpfHexEditor.Core.Options.OptionsPageStrings;

namespace WpfHexEditor.Core.Options;

/// <summary>
/// Central catalog of all registered options pages.
/// Adding a new page requires only a single descriptor entry here — no other file changes needed.
/// Dynamic pages (e.g. from plugins) can be registered at runtime via <see cref="RegisterDynamic"/>.
/// </summary>
public static class OptionsPageRegistry
{
    private static readonly List<OptionsPageDescriptor> _pages =
    [
        // Environment
        new(() => CategoryEnvironment, () => PageGeneral,    () => new EnvironmentGeneralPage(),  "🌍",
            ["theme", "color", "language", "font", "startup", "culture", "appearance", "locale"]),
        new(() => CategoryEnvironment, () => PageSave,       () => new EnvironmentSavePage(),     "🌍",
            ["autosave", "auto save", "backup", "encoding", "bom", "newline", "eol", "line ending"]),
        new(() => CategoryEnvironment, () => PageOutput,     () => new OutputOptionsPage(),       "🌍",
            ["log", "verbose", "output", "console", "warning", "error", "trace"]),
        new(() => CategoryEnvironment, () => PageTabGroups,  () => new TabGroupsOptionsPage(),    "🌍",
            ["tab", "group", "split", "layout", "panel", "dock", "column"]),

        // Hex Editor
        new(() => CategoryHexEditor, () => PageDisplay,    () => new HexEditorDisplayPage(),    "🔧",
            ["offset", "column", "bytes", "width", "ascii", "header", "ruler", "zoom", "grouping"]),
        new(() => CategoryHexEditor, () => PageEditing,    () => new HexEditorEditingPage(),    "🔧",
            ["insert", "overwrite", "clipboard", "paste", "readonly", "undo", "delete"]),
        new(() => CategoryHexEditor, () => PageStatusBar,  () => new HexEditorStatusBarPage(),  "🔧",
            ["statusbar", "status", "position", "selection", "size", "encoding", "info"]),
        new(() => CategoryHexEditor, () => PageBehavior,   () => new HexEditorBehaviorPage(),   "🔧",
            ["scroll", "click", "mouse", "keyboard", "highlight", "follow", "caret"]),

        // Solution Explorer
        new(() => CategorySolutionExplorer, () => PageGeneral, () => new SolutionExplorerOptionsPage(), "📁",
            ["file", "filter", "sort", "icon", "project", "solution", "tree", "explorer"]),

        // Code Editor
        new(() => CategoryCodeEditor, () => PageAppearanceColors, () => new CodeEditorOptionsPage(),   "💻",
            ["font", "color", "syntax", "highlight", "theme", "bracket", "indent", "guide", "gutter"]),
        new(() => CategoryCodeEditor, () => PageInlineHints,      () => new CodeEditorInlineHintsPage(), "💻",
            ["inlay", "hint", "roslyn", "type", "parameter", "lambda", "variable", "annotation"]),
        new(() => CategoryCodeEditor, () => PageNavigation,       () => new CodeEditorNavigationPage(),  "💻",
            ["goto", "navigate", "breadcrumb", "scroll", "margin", "minimap", "overview"]),
        new(() => CategoryCodeEditor, () => PageFormatting,       () => new CodeEditorFormattingPage(),  "💻",
            ["indent", "tab", "space", "brace", "format", "align", "whitespace", "trailing"]),

        // Text Editor
        new(() => CategoryTextEditor, () => PageGeneral,   () => new TextEditorOptionsPage(),    "📝",
            ["word wrap", "line", "ruler", "encoding", "eol", "link", "email", "plain text"]),
        new(() => CategoryTextEditor, () => PageMarkdown,  () => new MarkdownEditorOptionsPage(), "📝",
            ["markdown", "preview", "sync", "auto-pair", "yaml", "toc", "frontmatter", "list continuation"]),

        // Plugin System
        new(() => CategoryPluginSystem, () => PageGeneral,     () => new PluginSystemOptionsPage(),    "⚙️",
            ["plugin", "extension", "load", "sandbox", "enable", "disable", "alc"]),
        new(() => CategoryPluginSystem, () => PageDevelopment, () => new PluginDevLoaderOptionsPage(), "⚙️",
            ["dev", "debug", "hot reload", "path", "loader", "dll", "development"]),

        // Build & Run
        new(() => CategoryBuildRun, () => PageGeneral,   () => new BuildRunGeneralOptionsPage(), "🔨",
            ["build", "run", "output", "clean", "restore", "nuget", "msbuild"]),
        new(() => CategoryBuildRun, () => PageCompiler,  () => new BuildCompilerOptionsPage(),   "🔨",
            ["compiler", "roslyn", "warning", "error", "nullable", "optimize", "csharp"]),

        // Debugger
        new(() => CategoryDebugger, () => PageBreakpoints, () => new DebuggerBreakpointOptionsPage(), "🐛",
            ["breakpoint", "condition", "exception", "step", "attach", "dap", "debug"]),

        // Extensions
        new(() => CategoryExtensions, () => PageMarketplace, () => new MarketplaceOptionsPage(), "🔌",
            ["marketplace", "install", "update", "package", "gallery", "extension", "download"]),

        // Format Editor (.whfmt)
        new(() => CategoryFormatEditor, () => PageGeneral, () => new StructureEditorOptionsPage(), "📋",
            ["whfmt", "block", "validation", "code preview", "structure", "format definition",
             "endianness", "version", "test panel", "debounce", "auto validate"]),
        new(() => CategoryFormatEditor, () => PageFormatExplorer, () => new WhfmtExplorerOptionsPage(), "📋",
            ["format browser", "catalog", "whfmt explorer", "hot reload", "user formats",
             "quality score", "adhoc", "additional search paths", "built-in", "load failures",
             "format manager", "view mode"]),
    ];

    /// <summary>
    /// Raised when a new options page is registered dynamically (e.g., from a plugin).
    /// Allows UI components (like OptionsEditorControl) to refresh their display.
    /// </summary>
    public static event EventHandler<OptionsPageDescriptor>? PageRegistered;

    /// <summary>
    /// Raised when an options page is unregistered dynamically (e.g., when a plugin unloads).
    /// Allows UI components to remove the page from their display.
    /// </summary>
    public static event EventHandler<(string Category, string PageName)>? PageUnregistered;

    public static IReadOnlyList<OptionsPageDescriptor> Pages => _pages;

    /// <summary>
    /// Registers a runtime-provided options page (e.g. from a plugin).
    /// Idempotent: replaces any existing entry with the same category + page name.
    /// Fires the <see cref="PageRegistered"/> event to notify listeners.
    /// </summary>
    /// <param name="category">Category name (e.g., "Plugins", "Editors").</param>
    /// <param name="pageName">Page display name.</param>
    /// <param name="factory">Factory to create the options page control.</param>
    /// <param name="categoryIcon">Optional icon for the category (e.g., "🔌"). If null, uses "📂" as default.</param>
    /// <param name="searchKeywords">Optional extra keywords the search bar matches against (e.g. ["font", "color"]).</param>
    public static void RegisterDynamic(string category, string pageName, Func<UserControl> factory,
        string? categoryIcon = null, string[]? searchKeywords = null)
    {
        var existing = _pages.FindIndex(p =>
            p.Category == category && p.PageName == pageName);

        var descriptor = new OptionsPageDescriptor(category, pageName, factory,
            categoryIcon ?? "📂", searchKeywords);
        if (existing >= 0)
        {
            _pages[existing] = descriptor;
        }
        else
        {
            _pages.Add(descriptor);
        }

        // Notify listeners that a new page was registered
        PageRegistered?.Invoke(null, descriptor);
    }

    /// <summary>
    /// Removes a dynamically registered page by category and page name.
    /// Fires the <see cref="PageUnregistered"/> event to notify listeners.
    /// </summary>
    public static void UnregisterDynamic(string category, string pageName)
    {
        var removed = _pages.RemoveAll(p => p.Category == category && p.PageName == pageName);

        if (removed > 0)
        {
            // Notify listeners that a page was unregistered
            PageUnregistered?.Invoke(null, (category, pageName));
        }
    }
}
