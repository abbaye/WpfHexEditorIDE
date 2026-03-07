//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Collections.Generic;
using System.Windows.Controls;
using WpfHexEditor.Options.Pages;

namespace WpfHexEditor.Options;

/// <summary>
/// Central catalog of all registered options pages.
/// Adding a new page requires only a single descriptor entry here — no other file changes needed.
/// Dynamic pages (e.g. from plugins) can be registered at runtime via <see cref="RegisterDynamic"/>.
/// </summary>
internal static class OptionsPageRegistry
{
    private static readonly List<OptionsPageDescriptor> _pages =
    [
        // Environment
        new("Environment",        "General",          () => new EnvironmentGeneralPage()),
        new("Environment",        "Save",             () => new EnvironmentSavePage()),

        // Hex Editor
        new("Hex Editor",         "Display",          () => new HexEditorDisplayPage()),
        new("Hex Editor",         "Editing",          () => new HexEditorEditingPage()),
        new("Hex Editor",         "Status Bar",       () => new HexEditorStatusBarPage()),
        new("Hex Editor",         "Behavior",         () => new HexEditorBehaviorPage()),

        // Solution Explorer
        new("Solution Explorer",  "General",          () => new SolutionExplorerOptionsPage()),

        // Code Editor
        new("Code Editor",        "General",          () => new CodeEditorOptionsPage()),

        // Text Editor
        new("Text Editor",        "General",          () => new TextEditorOptionsPage()),

        // Plugin System
        new("Plugin System",      "General",          () => new PluginSystemOptionsPage()),
    ];

    public static IReadOnlyList<OptionsPageDescriptor> Pages => _pages;

    /// <summary>
    /// Registers a runtime-provided options page (e.g. from a plugin).
    /// Idempotent: replaces any existing entry with the same category + page name.
    /// </summary>
    public static void RegisterDynamic(string category, string pageName, Func<UserControl> factory)
    {
        var existing = _pages.FindIndex(p =>
            p.Category == category && p.PageName == pageName);

        var descriptor = new OptionsPageDescriptor(category, pageName, factory);
        if (existing >= 0) _pages[existing] = descriptor;
        else               _pages.Add(descriptor);
    }

    /// <summary>Removes a dynamically registered page by category and page name.</summary>
    public static void UnregisterDynamic(string category, string pageName)
        => _pages.RemoveAll(p => p.Category == category && p.PageName == pageName);
}
