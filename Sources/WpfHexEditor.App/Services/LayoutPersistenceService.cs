//////////////////////////////////////////////
// Project: WpfHexEditor.App
// File: Services/LayoutPersistenceService.cs
// Description:
//     Handles saving, loading, and pruning of dock layouts.
//     Extracted from MainWindow to centralize layout I/O.
// Architecture:
//     Static utility — no instance state. All operations work on
//     DockLayoutRoot passed as parameter. File path is a constant.
//////////////////////////////////////////////

using System.IO;
using WpfHexEditor.Docking.Core;
using WpfHexEditor.Docking.Core.Nodes;
using WpfHexEditor.Docking.Core.Serialization;

namespace WpfHexEditor.App.Services;

/// <summary>
/// Centralized dock layout persistence: load, save, prune stale items.
/// </summary>
internal static class LayoutPersistenceService
{
    public static readonly string LayoutFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "WpfHexEditor", "App", "layout.json");

    /// <summary>
    /// Loads layout from disk. Returns null if file doesn't exist or is corrupt.
    /// </summary>
    public static DockLayoutRoot? LoadFromDisk()
    {
        if (!File.Exists(LayoutFilePath)) return null;

        try
        {
            return DockLayoutSerializer.Deserialize(File.ReadAllText(LayoutFilePath));
        }
        catch (Exception ex)
        {
            OutputLogger.Error($"Failed to restore layout: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Saves layout to the default location on disk.
    /// </summary>
    public static void SaveToDisk(DockLayoutRoot layout)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LayoutFilePath)!);
            File.WriteAllText(LayoutFilePath, DockLayoutSerializer.Serialize(layout));
            OutputLogger.Debug($"Layout auto-saved to: {LayoutFilePath}");
        }
        catch (Exception ex)
        {
            OutputLogger.Error($"Failed to save layout: {ex.Message}");
        }
    }

    /// <summary>
    /// Saves layout to a user-specified path.
    /// </summary>
    public static void SaveToPath(DockLayoutRoot layout, string filePath)
    {
        File.WriteAllText(filePath, DockLayoutSerializer.Serialize(layout));
        OutputLogger.Info($"Layout saved to: {filePath}");
    }

    /// <summary>
    /// Loads layout from a user-specified path. Returns null on failure.
    /// </summary>
    public static DockLayoutRoot? LoadFromPath(string filePath)
    {
        try
        {
            return DockLayoutSerializer.Deserialize(File.ReadAllText(filePath));
        }
        catch (Exception ex)
        {
            OutputLogger.Error($"Failed to load layout from {filePath}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Removes document tabs whose backing file no longer exists on disk.
    /// </summary>
    public static void PruneStaleDocumentItems(DockLayoutRoot layout)
    {
        var pruned = new List<string>();
        PruneStaleItemsFromNode(layout.RootNode, pruned);
        PruneStaleItemsFromList(layout.FloatingItems, pruned);
        PruneStaleItemsFromList(layout.AutoHideItems, pruned);
        PruneStaleItemsFromList(layout.HiddenItems,   pruned);

        if (pruned.Count > 0)
            OutputLogger.Info(
                $"Layout restore: skipped {pruned.Count} document(s) — file no longer exists: " +
                string.Join(", ", pruned));
    }

    /// <summary>
    /// Removes duplicate document tabs (same file + same editor) from the layout.
    /// Keeps the first occurrence; subsequent duplicates are removed.
    /// </summary>
    public static void PruneDuplicateDocumentItems(DockLayoutRoot layout)
    {
        var seen    = new HashSet<(string Path, string EditorId)>();
        var pruned  = new List<string>();

        // Walk groups inside the tree (normal + document host nodes).
        foreach (var group in layout.GetAllGroups())
        {
            foreach (var item in group.Items.ToList())
            {
                if (GetDocumentEditorKey(item) is not var (path, editorId)) continue;
                if (!seen.Add((path, editorId)))
                {
                    group.RemoveItem(item);
                    pruned.Add(item.Title ?? Path.GetFileName(path) ?? path);
                }
            }
        }

        // Walk flat lists: floating, auto-hide, hidden.
        PruneDuplicatesFromList(layout.FloatingItems, seen, pruned);
        PruneDuplicatesFromList(layout.AutoHideItems, seen, pruned);
        PruneDuplicatesFromList(layout.HiddenItems,   seen, pruned);

        if (pruned.Count > 0)
            OutputLogger.Info(
                $"Layout restore: removed {pruned.Count} duplicate document(s): " +
                string.Join(", ", pruned));
    }

    /// <summary>
    /// Captures window state (position, size) into the layout before saving.
    /// </summary>
    public static void CaptureWindowState(
        DockLayoutRoot layout,
        System.Windows.WindowState windowState,
        System.Windows.Rect restoreBounds,
        double left, double top, double width, double height)
    {
        layout.WindowState = (int)windowState;
        if (restoreBounds != System.Windows.Rect.Empty)
        {
            layout.WindowLeft   = restoreBounds.Left;
            layout.WindowTop    = restoreBounds.Top;
            layout.WindowWidth  = restoreBounds.Width;
            layout.WindowHeight = restoreBounds.Height;
        }
        else
        {
            layout.WindowLeft   = left;
            layout.WindowTop    = top;
            layout.WindowWidth  = width;
            layout.WindowHeight = height;
        }
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private static void PruneStaleItemsFromNode(DockNode node, List<string> pruned)
    {
        switch (node)
        {
            case DockGroupNode group:
                foreach (var item in group.Items.ToList())
                {
                    if (IsStaleDocumentItem(item, out var label))
                    {
                        group.RemoveItem(item);
                        pruned.Add(label);
                    }
                }
                break;

            case DockSplitNode split:
                foreach (var child in split.Children)
                    PruneStaleItemsFromNode(child, pruned);
                break;
        }
    }

    private static void PruneStaleItemsFromList(List<DockItem> items, List<string> pruned)
    {
        for (int i = items.Count - 1; i >= 0; i--)
        {
            if (IsStaleDocumentItem(items[i], out var label))
            {
                items.RemoveAt(i);
                pruned.Add(label);
            }
        }
    }

    private static bool IsStaleDocumentItem(DockItem item, out string label)
    {
        label = string.Empty;

        bool isDocument = IsDocumentItem(item);
        if (!isDocument) return false;

        if (!item.Metadata.TryGetValue("FilePath", out var filePath) || filePath is null)
            return false;

        if (item.Metadata.TryGetValue("IsNewFile", out var isNew) && isNew == "true")
            return false;

        if (File.Exists(filePath)) return false;

        label = item.Title ?? Path.GetFileName(filePath) ?? filePath;
        return true;
    }

    private static bool IsDocumentItem(DockItem item)
        => item.ContentId.StartsWith("doc-file-")
        || item.ContentId.StartsWith("doc-hex-")
        || item.ContentId.StartsWith("doc-proj-");

    /// <summary>
    /// Returns a normalized (path, editorId) key for dedup, or null if item is not a document.
    /// </summary>
    private static (string Path, string EditorId)? GetDocumentEditorKey(DockItem item)
    {
        if (!IsDocumentItem(item)) return null;
        if (!item.Metadata.TryGetValue("FilePath", out var filePath) || filePath is null) return null;

        var editorId = "auto";
        if (item.Metadata.TryGetValue("ForceEditorId", out var feid) && feid is not null)
            editorId = feid;
        else if (item.Metadata.TryGetValue("ActiveEditorId", out var aeid) && aeid is not null)
            editorId = aeid;
        else if (item.Metadata.TryGetValue("ForceHexEditor", out var fh) && fh == "true")
            editorId = "hex-editor";

        return (filePath.ToUpperInvariant(), editorId.ToLowerInvariant());
    }

    private static void PruneDuplicatesFromList(
        List<DockItem> items,
        HashSet<(string Path, string EditorId)> seen,
        List<string> pruned)
    {
        for (int i = items.Count - 1; i >= 0; i--)
        {
            if (GetDocumentEditorKey(items[i]) is not var (path, editorId)) continue;
            if (seen.Add((path, editorId))) continue;

            pruned.Add(items[i].Title ?? Path.GetFileName(path) ?? path);
            items.RemoveAt(i);
        }
    }
}
