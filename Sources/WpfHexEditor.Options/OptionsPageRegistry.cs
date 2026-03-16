//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Windows.Controls;
using WpfHexEditor.Options.Pages;

namespace WpfHexEditor.Options;

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
        new("Environment",        "General",          () => new EnvironmentGeneralPage(),     "🌍"),
        new("Environment",        "Save",             () => new EnvironmentSavePage(),        "🌍"),
        new("Environment",        "Output",           () => new OutputOptionsPage(),          "🌍"),

        // Hex Editor
        new("Hex Editor",         "Display",          () => new HexEditorDisplayPage(),       "🔧"),
        new("Hex Editor",         "Editing",          () => new HexEditorEditingPage(),       "🔧"),
        new("Hex Editor",         "Status Bar",       () => new HexEditorStatusBarPage(),     "🔧"),
        new("Hex Editor",         "Behavior",         () => new HexEditorBehaviorPage(),      "🔧"),

        // Solution Explorer
        new("Solution Explorer",  "General",          () => new SolutionExplorerOptionsPage(), "📁"),

        // Code Editor
        new("Code Editor",        "General",          () => new CodeEditorOptionsPage(),      "💻"),

        // Text Editor
        new("Text Editor",        "General",          () => new TextEditorOptionsPage(),      "📝"),

        // Plugin System
        new("Plugin System",      "General",          () => new PluginSystemOptionsPage(),    "⚙️"),
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
    public static void RegisterDynamic(string category, string pageName, Func<UserControl> factory, string? categoryIcon = null)
    {
        var existing = _pages.FindIndex(p =>
            p.Category == category && p.PageName == pageName);

        var descriptor = new OptionsPageDescriptor(category, pageName, factory, categoryIcon ?? "📂");
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
