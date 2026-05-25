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
using System.Linq;
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
    /// Maximum number of dock items a persisted layout may contain before it is
    /// considered potentially corrupt or incompatible. Layouts exceeding this
    /// threshold are rejected at load time so the IDE falls back gracefully.
    /// </summary>
    public const int MaxLayoutItems = 60;

    /// <summary>
    /// Returns true if the layout is within acceptable complexity bounds.
    /// When false, <paramref name="reason"/> describes the problem.
    /// </summary>
    public static bool IsLayoutHealthy(DockLayoutRoot layout, out string reason)
    {
        var totalItems = layout.GetAllItems().Count();
        if (totalItems > MaxLayoutItems)
        {
            reason = $"layout contains {totalItems} items (limit: {MaxLayoutItems}). " +
                     "It may be from an older version or a configuration that is no longer supported.";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    /// <summary>
    /// Removes all document tabs (doc-file-*, doc-hex-*, doc-proj-*, etc.) from the layout
    /// to reduce complexity while preserving panel positions (Solution Explorer, Output, etc.).
    /// Used as a recovery step when a layout exceeds <see cref="MaxLayoutItems"/>.
    /// </summary>
    public static int PruneAllDocumentItems(DockLayoutRoot layout)
    {
        var pruned = new List<string>();

        PruneAllDocsFromNode(layout.RootNode, pruned);
        PruneDocsFromList(layout.FloatingItems, pruned);
        PruneDocsFromList(layout.AutoHideItems, pruned);
        PruneDocsFromList(layout.HiddenItems,   pruned);

        if (pruned.Count > 0)
            OutputLogger.Info($"Layout recovery: removed {pruned.Count} document tab(s) to restore healthy complexity.");

        return pruned.Count;
    }

    private static void PruneAllDocsFromNode(DockNode node, List<string> pruned)
    {
        switch (node)
        {
            case DockGroupNode group:
                foreach (var item in group.Items.ToList())
                {
                    if (IsDocumentItem(item))
                    {
                        group.RemoveItem(item);
                        pruned.Add(item.Title ?? item.ContentId);
                    }
                }
                break;

            case DockSplitNode split:
                foreach (var child in split.Children)
                    PruneAllDocsFromNode(child, pruned);
                break;
        }
    }

    private static void PruneDocsFromList(List<DockItem> items, List<string> pruned)
    {
        for (int i = items.Count - 1; i >= 0; i--)
        {
            if (IsDocumentItem(items[i]))
            {
                pruned.Add(items[i].Title ?? items[i].ContentId);
                items.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// Creates a timestamped backup of the layout file next to the original.
    /// Safe to call even if the file does not exist.
    /// </summary>
    public static void BackupLayoutFile()
    {
        if (!File.Exists(LayoutFilePath)) return;
        try
        {
            var stamp  = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var backup = Path.ChangeExtension(LayoutFilePath, $".{stamp}.bak.json");
            File.Copy(LayoutFilePath, backup, overwrite: true);
            OutputLogger.Info($"Layout backup created: {backup}");
        }
        catch (Exception ex)
        {
            OutputLogger.Error($"Failed to create layout backup: {ex.Message}");
        }
    }

    /// <summary>
    /// Removes document tabs whose backing file no longer exists on disk,
    /// and plugin-managed document tabs that are always recreated by their plugin on startup.
    /// </summary>
    public static void PruneStaleDocumentItems(DockLayoutRoot layout)
    {
        var pruned = new List<string>();
        PruneStaleItemsFromNode(layout.RootNode, pruned);
        PruneStaleItemsFromList(layout.FloatingItems, pruned);
        PruneStaleItemsFromList(layout.AutoHideItems, pruned);
        PruneStaleItemsFromList(layout.HiddenItems,   pruned);

        PrunePluginDocItemsFromNode(layout.RootNode, pruned);
        PrunePluginDocItemsFromList(layout.FloatingItems, pruned);
        PrunePluginDocItemsFromList(layout.AutoHideItems, pruned);
        PrunePluginDocItemsFromList(layout.HiddenItems,   pruned);

        if (pruned.Count > 0)
            OutputLogger.Info(
                $"Layout restore: skipped {pruned.Count} document(s) — file no longer exists or plugin-managed: " +
                string.Join(", ", pruned));
    }

    private static void PrunePluginDocItemsFromNode(DockNode node, List<string> pruned)
    {
        switch (node)
        {
            case DockGroupNode group:
                foreach (var item in group.Items.ToList())
                {
                    if (IsPluginDocumentItem(item))
                    {
                        group.RemoveItem(item);
                        pruned.Add(item.Title ?? item.ContentId);
                    }
                }
                break;
            case DockSplitNode split:
                foreach (var child in split.Children)
                    PrunePluginDocItemsFromNode(child, pruned);
                break;
        }
    }

    private static void PrunePluginDocItemsFromList(List<DockItem> items, List<string> pruned)
    {
        for (int i = items.Count - 1; i >= 0; i--)
        {
            if (IsPluginDocumentItem(items[i]))
            {
                pruned.Add(items[i].Title ?? items[i].ContentId);
                items.RemoveAt(i);
            }
        }
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

    // Plugin-managed document tabs whose content is recreated by the plugin on startup.
    // They must never be saved into the layout XML — the plugin owns their lifecycle.
    private static readonly string[] PluginDocumentPrefixes =
    [
        "doc-WpfHexEditor.Plugins.ScreenRecorder.Document",
    ];

    public static bool IsPluginDocumentItem(DockItem item)
        => PluginDocumentPrefixes.Any(p => item.ContentId.StartsWith(p));

    private static bool IsDocumentItem(DockItem item)
        => item.ContentId.StartsWith("doc-file-")
        || item.ContentId.StartsWith("doc-hex-")
        || item.ContentId.StartsWith("doc-proj-")
        || item.ContentId.StartsWith("doc-src-nav-")
        || item.ContentId.StartsWith("doc-new-text-")
        || item.ContentId.StartsWith("doc-new-code-")
        || IsPluginDocumentItem(item);

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
