// ==========================================================
// Project: WpfHexEditor.App
// File: SolutionExplorerConvertContributor.cs
// Description:
//     ISolutionExplorerContextMenuContributor that surfaces solution format
//     conversion actions (→ .slnx / → .sln / → .whsln) on the Solution node
//     of the Solution Explorer context menu.
//
// Architecture Notes:
//     Registered from MainWindow (not from a plugin) because the conversion
//     logic lives in MainWindow.SolutionConvert.cs.  Action callbacks are
//     injected at registration time — no direct MainWindow reference here.
// ==========================================================

using System.IO;
using System.Windows.Input;
using WpfHexEditor.SDK.Commands;
using WpfHexEditor.SDK.Contracts;

namespace WpfHexEditor.App.Services;

internal sealed class SolutionExplorerConvertContributor : ISolutionExplorerContextMenuContributor
{
    private readonly Func<bool, Task> _convertSlnxOrSln;   // true → .slnx,  false → .sln
    private readonly Func<Task>       _convertWhsln;

    public SolutionExplorerConvertContributor(
        Func<bool, Task> convertSlnxOrSln,
        Func<Task>       convertWhsln)
    {
        _convertSlnxOrSln = convertSlnxOrSln;
        _convertWhsln     = convertWhsln;
    }

    // -----------------------------------------------------------------------

    public IReadOnlyList<SolutionContextMenuItem> GetContextMenuItems(
        string nodeKind, string? nodePath)
    {
        if (nodeKind != "Solution" || string.IsNullOrEmpty(nodePath))
            return [];

        var ext   = Path.GetExtension(nodePath).ToLowerInvariant();
        var items = new List<SolutionContextMenuItem>();

        if (ext != ".slnx")
            items.Add(SolutionContextMenuItem.Item(
                "Convert to .slnx",
                MakeCommand(() => _convertSlnxOrSln(true)),
                iconGlyph: "\uE8AB"));

        if (ext != ".sln")
            items.Add(SolutionContextMenuItem.Item(
                "Convert to .sln",
                MakeCommand(() => _convertSlnxOrSln(false)),
                iconGlyph: "\uE8AB"));

        if (ext != ".whsln")
            items.Add(SolutionContextMenuItem.Item(
                "Convert to .whsln",
                MakeCommand(_convertWhsln),
                iconGlyph: "\uE8AB"));

        return items;
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Wraps an async factory in a fire-and-forget <see cref="RelayCommand"/>.
    /// Exceptions are swallowed here — callers handle them via MessageBox.
    /// </summary>
    private static ICommand MakeCommand(Func<Task> action)
        => new RelayCommand(() => _ = action());
}
