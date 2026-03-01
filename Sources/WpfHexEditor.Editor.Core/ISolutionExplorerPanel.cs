//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.Editor.Core;

/// <summary>
/// Event args for "Add New Item" / "Add Existing Item" requests from the Solution Explorer context menu.
/// </summary>
public sealed class AddItemRequestedEventArgs : EventArgs
{
    /// <summary>
    /// Project that should receive the new item.
    /// </summary>
    public IProject Project { get; set; } = null!;

    /// <summary>
    /// Virtual folder id to place the item in, or <see langword="null"/> for the project root.
    /// </summary>
    public string? TargetFolderId { get; set; }
}

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
    /// <summary>
    /// Replaces the tree with the given solution, or clears it when <see langword="null"/>.
    /// </summary>
    void SetSolution(ISolution? solution);

    /// <summary>
    /// Highlights the tree node that corresponds to <paramref name="absolutePath"/>.
    /// </summary>
    void SyncWithFile(string absolutePath);

    /// <summary>
    /// Fired when the user double-clicks an item (or presses Enter). The host opens the file.
    /// </summary>
    event EventHandler<ProjectItemActivatedEventArgs>? ItemActivated;

    /// <summary>
    /// Fired when the user single-clicks an item. The host updates the Properties panel.
    /// </summary>
    event EventHandler<ProjectItemEventArgs>? ItemSelected;

    /// <summary>
    /// Fired when the user commits an inline rename on a project node.
    /// </summary>
    event EventHandler<ProjectRenameRequestedEventArgs>? ProjectRenameRequested;

    /// <summary>
    /// Fired when the user requests to rename an item via the context menu.
    /// </summary>
    event EventHandler<ProjectItemEventArgs>? ItemRenameRequested;

    /// <summary>
    /// Fired when the user requests to delete an item via the context menu.
    /// </summary>
    event EventHandler<ProjectItemEventArgs>? ItemDeleteRequested;

    /// <summary>
    /// Fired when the user drags a file node to a new folder or the project root.
    /// </summary>
    event EventHandler<ItemMoveRequestedEventArgs>? ItemMoveRequested;

    /// <summary>
    /// Fired when the user requests to close the solution from the Solution node context menu.
    /// </summary>
    event EventHandler? CloseSolutionRequested;

    /// <summary>
    /// Fired when the user chooses "Add New Item…" from the context menu on a project or folder node.
    /// </summary>
    event EventHandler<AddItemRequestedEventArgs>? AddNewItemRequested;

    /// <summary>
    /// Fired when the user chooses "Add Existing Item…" from the context menu on a project or folder node.
    /// </summary>
    event EventHandler<AddItemRequestedEventArgs>? AddExistingItemRequested;

    /// <summary>
    /// Fired when the user chooses "Import Built-in Format…" from the context menu on a project node.
    /// </summary>
    event EventHandler<AddItemRequestedEventArgs>? ImportFormatDefinitionRequested;

    /// <summary>
    /// Fired when the user chooses "Convert to TBLX…" from the context menu on a .tbl file node.
    /// The host is responsible for showing the conversion dialog and producing the .tblx output.
    /// </summary>
    event EventHandler<ProjectItemEventArgs>? ConvertTblRequested;

    /// <summary>
    /// Fired when the user requests to rename a virtual folder.
    /// </summary>
    event EventHandler<FolderRenameEventArgs>? FolderRenameRequested;

    /// <summary>
    /// Fired when the user requests to delete a virtual folder.
    /// </summary>
    event EventHandler<FolderDeleteEventArgs>? FolderDeleteRequested;

    /// <summary>
    /// Fired when the user requests creation of a new virtual (or physical) folder.
    /// </summary>
    event EventHandler<FolderCreateRequestedEventArgs>?  FolderCreateRequested;

    /// <summary>
    /// Fired when the user chooses "Add Existing Folder…" to import a directory from disk.
    /// </summary>
    event EventHandler<FolderFromDiskRequestedEventArgs>? FolderFromDiskRequested;

    /// <summary>
    /// Enters inline-rename mode on the tree node that corresponds to <paramref name="folder"/>.
    /// Call this immediately after creating a new folder to give it a VS-like creation-rename flow.
    /// </summary>
    void BeginFolderRename(IVirtualFolder folder);

    /// <summary>
    /// Gets or sets whether the tree displays the physical file system under the project directory
    /// instead of the virtual folder organisation.
    /// </summary>
    bool ShowAllFiles { get; set; }
}
