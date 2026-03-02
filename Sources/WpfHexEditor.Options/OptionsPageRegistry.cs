// Apache 2.0 - 2026
// Contributors: Claude Sonnet 4.6

using System.Collections.Generic;
using WpfHexEditor.Options.Pages;

namespace WpfHexEditor.Options;

/// <summary>
/// Central catalog of all registered options pages.
/// Adding a new page requires only a single line here — no other file changes needed.
/// </summary>
internal static class OptionsPageRegistry
{
    public static IReadOnlyList<OptionsPageDescriptor> Pages { get; } =
    [
        new("Environment", "General",     () => new EnvironmentGeneralPage()),
        new("Environment", "Save",        () => new EnvironmentSavePage()),
        new("Hex Editor",  "Display",     () => new HexEditorDisplayPage()),
        new("Hex Editor",  "Editing",     () => new HexEditorEditingPage()),
    ];
}
