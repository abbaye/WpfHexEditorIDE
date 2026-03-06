// ==========================================================
// Project: WpfHexEditor.Options
// File: OptionsPageRegistry.cs
// Author: Auto
// Created: 2026-03-06
// Description:
//     Central catalog of all registered options pages.
//     Adding a new page requires only a single line here.
//
// Architecture Notes:
//     Pattern: Registry / Factory — lazy creation via Func<UserControl>
// ==========================================================

using System.Collections.Generic;
using WpfHexEditor.Options.Pages;

namespace WpfHexEditor.Options;

/// <summary>
/// Central catalog of all registered options pages.
/// Adding a new page requires only a single descriptor entry here — no other file changes needed.
/// </summary>
internal static class OptionsPageRegistry
{
    public static IReadOnlyList<OptionsPageDescriptor> Pages { get; } =
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
    ];
}
