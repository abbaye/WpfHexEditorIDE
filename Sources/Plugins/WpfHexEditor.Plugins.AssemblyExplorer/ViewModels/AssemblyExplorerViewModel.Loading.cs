// ==========================================================
// Project: WpfHexEditor.Plugins.AssemblyExplorer
// File: ViewModels/AssemblyExplorerViewModel.Loading.cs
// Description:
//     Assembly loading, workspace management, Clear, Close, Pin.
// ==========================================================

using System.Diagnostics;
using System.IO;
using WpfHexEditor.Core.AssemblyAnalysis.Models;
using WpfHexEditor.Plugins.AssemblyExplorer.Events;
using WpfHexEditor.Plugins.AssemblyExplorer.Options;

namespace WpfHexEditor.Plugins.AssemblyExplorer.ViewModels;

public sealed partial class AssemblyExplorerViewModel
{
    // ── Public workspace API ──────────────────────────────────────────────────

    /// <summary>Returns true when the specified file is already loaded in the workspace.</summary>
    public bool IsAssemblyLoaded(string filePath)
        => !string.IsNullOrEmpty(filePath) && _workspace.ContainsKey(filePath);

    /// <summary>Returns all file paths currently loaded in the workspace.</summary>
    public IReadOnlyList<string> GetWorkspaceFilePaths()
        => _workspace.Keys.ToList();

    /// <summary>Closes the workspace entry that corresponds to <paramref name="node"/>.</summary>
    public void CloseAssembly(AssemblyNodeViewModel node)
    {
        var entry = FindEntryForNode(node);
        if (entry is not null) CloseEntry(entry.Model.FilePath);
    }

    /// <summary>Closes a specific workspace entry by file path.</summary>
    public void CloseAssembly(string filePath)
        => CloseEntry(filePath);

    /// <summary>Toggles the pinned state of the root node / workspace entry.</summary>
    public void TogglePin(AssemblyRootNodeViewModel root)
    {
        var entry = _workspace.Values.FirstOrDefault(e => ReferenceEquals(e.Root, root));
        if (entry is null) return;
        entry.IsPinned = !entry.IsPinned;
    }

    /// <summary>
    /// Loads and analyzes the assembly at <paramref name="filePath"/>.
    /// Runs analysis on a background thread; populates the tree on the UI thread.
    /// If this file is already in the workspace, reloads it in-place.
    /// Safe to call from any thread (cancels any in-progress load for this file first).
    /// </summary>
    public async Task LoadAssemblyAsync(string filePath, CancellationToken externalCt = default)
    {
        if (string.IsNullOrEmpty(filePath) || !_analysisService.CanAnalyze(filePath))
        {
            if (_workspace.Count == 0)
                StatusText = "No assembly loaded";
            return;
        }

        // Cancel and remove any existing load for this specific file.
        if (_workspace.TryGetValue(filePath, out var existing))
        {
            existing.Cts.Cancel();
            RootNodes.Remove(existing.Root);
            _workspace.Remove(filePath);
            _decompileCache.Invalidate(filePath);
        }

        // Enforce the max-assembly limit by evicting the oldest unpinned entry.
        var maxCount = AssemblyExplorerOptions.Instance.MaxLoadedAssemblies;
        while (_workspace.Count >= maxCount)
        {
            var oldest = _workspace.Values.FirstOrDefault(e => !e.IsPinned);
            if (oldest is null) break;
            CloseEntry(oldest.Model.FilePath, silent: true);
        }

        var cts = CancellationTokenSource.CreateLinkedTokenSource(externalCt);
        bool first = TryFirstLoad();
        if (first) IsLoading = true;
        StatusText = $"Analyzing {Path.GetFileName(filePath)}…";

        try
        {
            var sw    = Stopwatch.StartNew();
            var model = await Task.Run(
                () => _analysisService.AnalyzeAsync(filePath, cts.Token), cts.Token);
            cts.Token.ThrowIfCancellationRequested();
            sw.Stop();

            var rootNode = new AssemblyRootNodeViewModel(model);
            BuildTreeChildren(rootNode, model);
            PropagateOwnerFilePath(rootNode, filePath);

            var entry = new AssemblyWorkspaceEntry(model, rootNode, cts) { LoadTimeMs = sw.ElapsedMilliseconds };
            _workspace[filePath] = entry;
            RootNodes.Add(rootNode);

            var typeCount   = model.Types.Count;
            var methodCount = model.Types.Sum(t => t.Methods.Count);
            UpdateStatusText();
            RaiseWorkspaceStatsChanged();

            AssemblyLoaded?.Invoke(this, new AssemblyLoadedEvent
            {
                FilePath    = model.FilePath,
                Name        = model.Name,
                Version     = model.Version,
                IsManaged   = model.IsManaged,
                TypeCount   = typeCount,
                MethodCount = methodCount
            });

            _output.Write("Plugin System",
                $"[Assembly Explorer] Loaded '{model.Name}'" +
                $" ({typeCount} types, {methodCount} methods) in {sw.ElapsedMilliseconds}ms");
        }
        catch (OperationCanceledException)
        {
            StatusText = "Analysis cancelled.";
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
            _output.Write("Plugin System", $"[Assembly Explorer] Failed to analyze '{filePath}': {ex.Message}");
        }
        finally
        {
            if (first) IsLoading = false;
        }
    }

    /// <summary>Closes all loaded assemblies and resets all state.</summary>
    public void Clear()
    {
        foreach (var entry in _workspace.Values)
            entry.Cts.Cancel();
        _nodeSelectionCts?.Cancel();
        _nodeSelectionCts = null;

        _workspace.Clear();
        _decompileCache.Clear();
        RootNodes.Clear();
        DetailViewModel.Clear();
        StatusText    = "No assembly loaded";
        FilterText    = string.Empty;
        _selectedNode = null;
        TotalLoadedAssemblies = 0;
        TotalLoadedTypes      = 0;
        AssemblyCleared?.Invoke(this, EventArgs.Empty);
    }

    private Task LoadCurrentFileAsync()
        => LoadAssemblyAsync(_hexEditor.CurrentFilePath ?? string.Empty);

    // ── Private helpers ───────────────────────────────────────────────────────

    private AssemblyWorkspaceEntry? FindEntryForNode(AssemblyNodeViewModel node)
    {
        if (node is AssemblyRootNodeViewModel root)
            return _workspace.Values.FirstOrDefault(e => ReferenceEquals(e.Root, root));

        if (!string.IsNullOrEmpty(node.OwnerFilePath)
            && _workspace.TryGetValue(node.OwnerFilePath, out var entry))
            return entry;

        return null;
    }

    private void CloseEntry(string filePath, bool silent = false)
    {
        if (!_workspace.TryGetValue(filePath, out var entry)) return;

        entry.Cts.Cancel();
        RootNodes.Remove(entry.Root);
        _workspace.Remove(filePath);
        _decompileCache.Invalidate(filePath);

        if (_selectedNode?.OwnerFilePath is not null
            && string.Equals(_selectedNode.OwnerFilePath, filePath, StringComparison.OrdinalIgnoreCase))
        {
            _selectedNode = null;
            DetailViewModel.Clear();
        }

        UpdateStatusText();
        RaiseWorkspaceStatsChanged();

        if (!silent)
            _output.Write("Plugin System", $"[Assembly Explorer] Closed '{entry.Model.Name}'");

        if (_workspace.Count == 0)
            AssemblyCleared?.Invoke(this, EventArgs.Empty);
        else
            AssemblyUnloaded?.Invoke(this, EventArgs.Empty);
    }

    private static void SetAllExpanded(IEnumerable<AssemblyNodeViewModel> nodes, bool expanded)
    {
        foreach (var node in nodes)
        {
            node.IsExpanded = expanded;
            SetAllExpanded(node.Children, expanded);
        }
    }
}
