//////////////////////////////////////////////
// Apache 2.0  - 2026
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.Editor.Core;

/// <summary>
/// Contract for the Solution Explorer panel.
/// <para>
/// The host sets the active solution via <see cref="SetSolution"/> and listens
/// to <see cref="ItemActivated"/> to open files in the appropriate editor.
/// The host also listens to <see cref="ItemSelected"/> (single-click) to drive
/// the Properties panel.
/// </para>
/// <para>
/// The panel never controls document visibility — that is the host's responsibility.
/// </para>
/// </summary>
public interface ISolutionExplorerPanel
{
    /// <summary>Replaces the tree with the given solution, or clears it when <see langword="null"/>.</summary>
    void SetSolution(ISolution? solution);

    /// <summary>Highlights the tree node that corresponds to <paramref name="absolutePath"/>.</summary>
    void SyncWithFile(string absolutePath);

    /// <summary>Fired when the user double-clicks an item (or presses Enter). The host opens the file.</summary>
    event EventHandler<ProjectItemActivatedEventArgs>? ItemActivated;

    /// <summary>Fired when the user single-clicks an item. The host updates the Properties panel.</summary>
    event EventHandler<ProjectItemEventArgs>? ItemSelected;

    /// <summary>Fired when the user requests to rename an item via the context menu.</summary>
    event EventHandler<ProjectItemEventArgs>? ItemRenameRequested;

    /// <summary>Fired when the user requests to delete an item via the context menu.</summary>
    event EventHandler<ProjectItemEventArgs>? ItemDeleteRequested;
}
