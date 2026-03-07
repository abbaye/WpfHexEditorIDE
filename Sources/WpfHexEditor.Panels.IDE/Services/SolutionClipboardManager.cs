// ==========================================================
// Project: WpfHexEditor.Panels.IDE
// File: Services/SolutionClipboardManager.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-05
// Description:
//     Manages clipboard operations (Copy / Cut / Paste) for project items
//     in the Solution Explorer. Uses the Windows file drop-list format so
//     items can be pasted into the Windows Explorer as well.
//
// Architecture Notes:
//     Repository Pattern — single point of clipboard truth for SE.
//     The "cut" state is tracked in-memory via _pendingCutPaths.
//     Paste raises AddExistingItemRequested so the host (MainWindow)
//     can perform the actual project-model mutation.
// ==========================================================

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Windows;
using WpfHexEditor.Editor.Core;

namespace WpfHexEditor.Panels.IDE.Services;

/// <summary>
/// Provides Copy / Cut / Paste clipboard operations for <see cref="IProjectItem"/> collections.
/// </summary>
public sealed class SolutionClipboardManager
{
    // Paths that have been "cut" and are pending move on next paste.
    private readonly HashSet<string> _pendingCutPaths = new(StringComparer.OrdinalIgnoreCase);

    // -- Events ----------------------------------------------------------------

    /// <summary>
    /// Raised when the user pastes items. The host should add the provided paths
    /// to the project (as existing items or physical move, depending on <see cref="AddExistingItemEventArgs.IsCut"/>).
    /// </summary>
    public event EventHandler<AddExistingItemEventArgs>? AddExistingItemRequested;

    // -- Public API ------------------------------------------------------------

    /// <summary>
    /// Copies the absolute paths of <paramref name="items"/> to the clipboard
    /// as a file drop list.
    /// </summary>
    public void Copy(IEnumerable<IProjectItem> items)
    {
        _pendingCutPaths.Clear();

        var paths  = items.Select(i => i.AbsolutePath)
                          .Where(p => !string.IsNullOrEmpty(p))
                          .ToList();

        if (paths.Count == 0) return;

        PutFileDropList(paths);
    }

    /// <summary>
    /// Copies the absolute paths of <paramref name="items"/> to the clipboard and
    /// marks them as "pending cut" (shown at reduced opacity in the SE).
    /// The physical move happens only when <see cref="Paste"/> is called.
    /// </summary>
    public void Cut(IEnumerable<IProjectItem> items)
    {
        _pendingCutPaths.Clear();

        var paths = items.Select(i => i.AbsolutePath)
                         .Where(p => !string.IsNullOrEmpty(p))
                         .ToList();

        if (paths.Count == 0) return;

        foreach (var p in paths)
            _pendingCutPaths.Add(p);

        PutFileDropList(paths);
    }

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="absolutePath"/> is in the
    /// pending-cut set (the item should be shown dimmed in the SE).
    /// </summary>
    public bool IsPendingCut(string? absolutePath)
        => absolutePath is not null && _pendingCutPaths.Contains(absolutePath);

    /// <summary>
    /// Pastes the clipboard file drop list into <paramref name="targetFolder"/>.
    /// Raises <see cref="AddExistingItemRequested"/>; clears the cut state on success.
    /// </summary>
    public void Paste(IVirtualFolder? targetFolder)
    {
        var drop = Clipboard.GetFileDropList();
        if (drop is null || drop.Count == 0) return;

        var paths = new List<string>(drop.Count);
        foreach (string? p in drop)
            if (!string.IsNullOrEmpty(p)) paths.Add(p);

        if (paths.Count == 0) return;

        bool isCut = _pendingCutPaths.Count > 0 &&
                     _pendingCutPaths.IsSupersetOf(paths);

        AddExistingItemRequested?.Invoke(this, new AddExistingItemEventArgs(paths, targetFolder, isCut));

        if (isCut)
        {
            _pendingCutPaths.Clear();
            Clipboard.Clear();
        }
    }

    /// <summary>
    /// Returns <see langword="true"/> when the clipboard contains a file drop list
    /// that can be pasted (at least one path exists on disk).
    /// </summary>
    public bool CanPaste()
    {
        if (!Clipboard.ContainsFileDropList()) return false;
        var drop = Clipboard.GetFileDropList();
        foreach (string? p in drop)
            if (!string.IsNullOrEmpty(p) && File.Exists(p)) return true;
        return false;
    }

    // -- Private helpers -------------------------------------------------------

    private static void PutFileDropList(IEnumerable<string> paths)
    {
        var sc = new StringCollection();
        foreach (var p in paths) sc.Add(p);
        Clipboard.SetFileDropList(sc);
    }
}

/// <summary>
/// Event arguments for <see cref="SolutionClipboardManager.AddExistingItemRequested"/>.
/// </summary>
public sealed class AddExistingItemEventArgs : EventArgs
{
    public AddExistingItemEventArgs(
        IReadOnlyList<string> filePaths,
        IVirtualFolder?       targetFolder,
        bool                  isCut)
    {
        FilePaths    = filePaths;
        TargetFolder = targetFolder;
        IsCut        = isCut;
    }

    /// <summary>Absolute paths of the files being added.</summary>
    public IReadOnlyList<string> FilePaths { get; }

    /// <summary>Destination virtual folder, or <see langword="null"/> for the project root.</summary>
    public IVirtualFolder? TargetFolder { get; }

    /// <summary>
    /// When <see langword="true"/> the operation is a move (Cut+Paste);
    /// the host should delete or move the source files after adding them.
    /// </summary>
    public bool IsCut { get; }
}
