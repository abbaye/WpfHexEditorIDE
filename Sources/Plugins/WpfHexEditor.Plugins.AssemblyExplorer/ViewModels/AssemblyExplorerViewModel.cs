// ==========================================================
// Project: WpfHexEditor.Plugins.AssemblyExplorer
// File: ViewModels/AssemblyExplorerViewModel.cs
// Author: Derek Tremblay
// Created: 2026-03-08
// Updated: 2026-03-16 — v2.0 multi-assembly workspace, hex editor deep integration,
//                        cross-assembly search, decompiler backend abstraction,
//                        cross-ref navigation, status bar stats.
// Description:
//     Root orchestrator ViewModel for the Assembly Explorer panel.
//     Manages a multi-assembly workspace (Dictionary<string, AssemblyWorkspaceEntry>)
//     allowing any number of .dll/.exe files to be loaded and displayed simultaneously.
//     Each entry has its own background CancellationTokenSource so loads can be
//     cancelled independently.
//
// Architecture Notes:
//     Pattern: MVVM orchestrator.
//     Analysis runs on Task.Run background thread; all tree mutations
//     occur on the UI thread (direct call after await).
//     EventBus publishing is done here — the plugin entry point wires
//     AssemblyLoaded / WorkspaceStatsChanged to update status bar items and EventBus.
// ==========================================================

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using WpfHexEditor.Core.AssemblyAnalysis.Languages;
using WpfHexEditor.Core.AssemblyAnalysis.Models;
using WpfHexEditor.Core.AssemblyAnalysis.Services;
using IAssemblyAnalysisEngine = WpfHexEditor.Core.AssemblyAnalysis.Services.IAssemblyAnalysisEngine;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WpfHexEditor.Editor.CodeEditor.Controls;
using WpfHexEditor.Editor.TextEditor.Controls;
using WpfHexEditor.Editor.TextEditor.Models;
using WpfHexEditor.Core.ProjectSystem.Languages;
using WpfHexEditor.Plugins.AssemblyExplorer.Events;
using WpfHexEditor.Plugins.AssemblyExplorer.Options;
using WpfHexEditor.Plugins.AssemblyExplorer.Services;
using WpfHexEditor.SDK.Commands;
using WpfHexEditor.SDK.Contracts;
using WpfHexEditor.SDK.Contracts.Services;
using WpfHexEditor.SDK.Descriptors;

namespace WpfHexEditor.Plugins.AssemblyExplorer.ViewModels;

/// <summary>
/// Root ViewModel for the Assembly Explorer panel.
/// Loaded once and kept alive for the plugin lifetime.
/// Supports simultaneous loading of multiple assemblies (multi-assembly workspace).
/// </summary>
public sealed class AssemblyExplorerViewModel : INotifyPropertyChanged
{
    private readonly IAssemblyAnalysisEngine _analysisService;
    private readonly IDecompilerBackend      _decompilerBackend;
    private readonly DecompilerService       _decompiler;
    private readonly IHexEditorService       _hexEditor;
    private readonly IDocumentHostService?   _documentHost;
    private readonly IOutputService          _output;
    private readonly IUIRegistry             _uiRegistry;
    private readonly string                  _pluginId;
    private readonly DecompileCache          _decompileCache = new();

    // Cancels the previous ShowNodeAsync whenever a new node is selected.
    private CancellationTokenSource? _nodeSelectionCts;

    // ── Multi-assembly workspace ───────────────────────────────────────────────
    // Keyed by file path, case-insensitive.

    private readonly Dictionary<string, AssemblyWorkspaceEntry> _workspace =
        new(StringComparer.OrdinalIgnoreCase);

    public AssemblyExplorerViewModel(
        IAssemblyAnalysisEngine   analysisService,
        IDecompilerBackend        decompilerBackend,
        DecompilerService         decompiler,
        IHexEditorService         hexEditor,
        IDocumentHostService?     documentHost,
        IOutputService            output,
        IUIRegistry               uiRegistry,
        string                    pluginId)
    {
        _analysisService   = analysisService;
        _decompilerBackend = decompilerBackend;
        _decompiler        = decompiler;
        _hexEditor         = hexEditor;
        _documentHost      = documentHost;
        _output            = output;
        _uiRegistry        = uiRegistry;
        _pluginId          = pluginId;

        DetailViewModel = new AssemblyDetailViewModel(_decompilerBackend, _decompileCache);

        LoadCurrentFileCommand = new RelayCommand(
            _ => _ = LoadCurrentFileAsync(),
            _ => _hexEditor.IsActive && !IsLoading);

        CollapseAllCommand = new RelayCommand(
            _ => SetAllExpanded(RootNodes, false),
            _ => RootNodes.Count > 0);

        ExpandAllCommand = new RelayCommand(
            _ => SetAllExpanded(RootNodes, true),
            _ => RootNodes.Count > 0);

        ClearCommand = new RelayCommand(_ => Clear());

        CloseAssemblyCommand = new RelayCommand(
            p => { if (p is AssemblyNodeViewModel node) CloseAssembly(node); },
            p => p is AssemblyNodeViewModel);

        CloseAllCommand = new RelayCommand(_ => Clear());

        PinAssemblyCommand = new RelayCommand(
            p => { if (p is AssemblyRootNodeViewModel root) TogglePin(root); },
            p => p is AssemblyRootNodeViewModel);

        // Opens decompiled text in a syntax-highlighted TextEditor document tab.
        OpenInEditorCommand = new RelayCommand(
            _ => _ = OpenSelectedNodeInEditorAsync(),
            _ => SelectedNode is not null);

        // Opens assembly file in hex editor, navigating to the member's PE offset.
        OpenInHexEditorCommand = new RelayCommand(
            p => { if (p is AssemblyNodeViewModel n) _ = OpenMemberInHexEditorAsync(n); },
            p => p is AssemblyNodeViewModel node && node.PeOffset > 0);
    }

    // ── INotifyPropertyChanged ────────────────────────────────────────────────

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }

    // ── Tree state ────────────────────────────────────────────────────────────

    public ObservableCollection<AssemblyNodeViewModel> RootNodes { get; } = [];

    private AssemblyNodeViewModel? _selectedNode;
    public AssemblyNodeViewModel? SelectedNode
    {
        get => _selectedNode;
        set
        {
            if (!SetField(ref _selectedNode, value)) return;
            if (value is not null) OnNodeSelected(value);
        }
    }

    public AssemblyDetailViewModel DetailViewModel { get; }

    /// <summary>The active decompiler backend — exposed for language-aware Extract operations.</summary>
    public IDecompilerBackend Backend => _decompilerBackend;

    /// <summary>The BCL-only decompiler service — exposed for IL and C# single-item export.</summary>
    public DecompilerService Decompiler => _decompiler;

    // ── Language management ────────────────────────────────────────────────────

    /// <summary>
    /// Sets <paramref name="languageId"/> as the new default decompilation language,
    /// then opens the node in a new editor tab.
    /// </summary>
    public void OpenNodeInEditorWithLanguage(AssemblyNodeViewModel node, string languageId)
    {
        SetDefaultLanguage(languageId);
        SelectedNode = node;
        _ = OpenSelectedNodeInEditorAsync();
    }

    /// <summary>
    /// Opens the node in a new editor tab using <paramref name="languageId"/>
    /// without changing the persistent default language.
    /// Used by "Go to Definition" which must always show C# regardless of the current default.
    /// </summary>
    public void OpenNodeInEditorWithLanguageWithoutChangingDefault(
        AssemblyNodeViewModel node, string languageId)
    {
        var savedId = _decompilerBackend.Options.TargetLanguageId;
        SetDefaultLanguage(languageId);
        SelectedNode = node;
        _ = OpenSelectedNodeInEditorAsync();
        // Restore the previous default so the persistent setting is unaffected.
        SetDefaultLanguage(savedId ?? "CSharp");
    }

    /// <summary>
    /// Persists <paramref name="languageId"/> as the target decompilation language.
    /// Subsequent calls to <see cref="OpenSelectedNodeInEditorAsync"/> will use it.
    /// </summary>
    public void SetDefaultLanguage(string languageId)
    {
        var opts = _decompilerBackend.Options with { TargetLanguageId = languageId };
        _decompilerBackend.Options = opts;
    }

    // ── Loading state ─────────────────────────────────────────────────────────

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        private set => SetField(ref _isLoading, value);
    }

    private string _statusText = "No assembly loaded";
    public string StatusText
    {
        get => _statusText;
        private set => SetField(ref _statusText, value);
    }

    // ── Workspace stats (for status bar) ─────────────────────────────────────

    private int _totalLoadedAssemblies;
    public int TotalLoadedAssemblies
    {
        get => _totalLoadedAssemblies;
        private set => SetField(ref _totalLoadedAssemblies, value);
    }

    private int _totalLoadedTypes;
    public int TotalLoadedTypes
    {
        get => _totalLoadedTypes;
        private set => SetField(ref _totalLoadedTypes, value);
    }

    /// <summary>
    /// Raised after any workspace change (add or remove) so the plugin entry point
    /// can update the status bar item and publish AssemblyWorkspaceChangedEvent.
    /// </summary>
    public event EventHandler? WorkspaceStatsChanged;

    // ── Toolbar toggles ───────────────────────────────────────────────────────

    private bool _sortAlphabetical = true;
    public bool SortAlphabetical
    {
        get => _sortAlphabetical;
        set { if (SetField(ref _sortAlphabetical, value)) RebuildAllTrees(); }
    }

    private bool _showReferences = true;
    public bool ShowReferences
    {
        get => _showReferences;
        set { if (SetField(ref _showReferences, value)) RebuildAllTrees(); }
    }

    private bool _showResources = true;
    public bool ShowResources
    {
        get => _showResources;
        set { if (SetField(ref _showResources, value)) RebuildAllTrees(); }
    }

    private bool _showMetadata;
    public bool ShowMetadata
    {
        get => _showMetadata;
        set { if (SetField(ref _showMetadata, value)) RebuildAllTrees(); }
    }

    private bool _syncWithHexEditor = true;
    public bool SyncWithHexEditor
    {
        get => _syncWithHexEditor;
        set => SetField(ref _syncWithHexEditor, value);
    }

    private string _filterText = string.Empty;
    public string FilterText
    {
        get => _filterText;
        set { if (SetField(ref _filterText, value)) ApplyFilter(value); }
    }

    // ── Commands ──────────────────────────────────────────────────────────────

    public ICommand LoadCurrentFileCommand  { get; }
    public ICommand CollapseAllCommand      { get; }
    public ICommand ExpandAllCommand        { get; }
    public ICommand ClearCommand            { get; }
    public ICommand CloseAssemblyCommand    { get; }
    public ICommand CloseAllCommand         { get; }
    public ICommand PinAssemblyCommand      { get; }
    public ICommand OpenInEditorCommand     { get; }
    public ICommand OpenInHexEditorCommand  { get; }

    // ── Events (consumed by plugin entry point) ───────────────────────────────

    public event EventHandler<AssemblyLoadedEvent>? AssemblyLoaded;
    public event EventHandler?                      AssemblyCleared;
    public event EventHandler?                      AssemblyUnloaded;
    public event EventHandler<AssemblyMemberSelectedEvent>? MemberSelected;

    // ── Public workspace API ──────────────────────────────────────────────────

    /// <summary>
    /// Returns true when the specified file is already loaded in the workspace.
    /// Case-insensitive file path comparison.
    /// </summary>
    public bool IsAssemblyLoaded(string filePath)
        => !string.IsNullOrEmpty(filePath) && _workspace.ContainsKey(filePath);

    /// <summary>
    /// Returns all file paths currently loaded in the workspace (for session persistence).
    /// </summary>
    public IReadOnlyList<string> GetWorkspaceFilePaths()
        => _workspace.Keys.ToList();

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
            if (oldest is null) break; // All pinned — cannot evict.
            CloseEntry(oldest.Model.FilePath, silent: true);
        }

        var cts = CancellationTokenSource.CreateLinkedTokenSource(externalCt);
        IsLoading  = true;
        StatusText = $"Analyzing {Path.GetFileName(filePath)}…";

        try
        {
            var sw    = Stopwatch.StartNew();
            var model = await Task.Run(
                () => _analysisService.AnalyzeAsync(filePath, cts.Token), cts.Token);
            cts.Token.ThrowIfCancellationRequested();
            sw.Stop();

            // Build tree on the UI thread (we are already here after await).
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
            IsLoading = false;
        }
    }

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

    private Task LoadCurrentFileAsync()
        => LoadAssemblyAsync(_hexEditor.CurrentFilePath ?? string.Empty);

    /// <summary>Closes all loaded assemblies and resets all state.</summary>
    public void Clear()
    {
        // Cancel all in-flight loads and any in-progress decompile.
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

    /// <summary>
    /// Called when the user selects a tree node.
    /// Fast side-effects (hex-editor sync, event publishing) happen synchronously;
    /// the expensive decompilation is dispatched asynchronously via ShowNodeAsync.
    /// </summary>
    public void OnNodeSelected(AssemblyNodeViewModel node)
    {
        // Fast synchronous side-effects (instant — no decompilation).
        NavigateHexEditorToNode(node);
        PublishMemberSelected(node);

        // Phase 6: cross-assembly reference navigation.
        if (node is ReferenceNodeViewModel refNode)
            TryNavigateToReference(refNode);

        // Phase 8: cancel any in-flight decompile and start a new async one.
        StartDecompileAsync(node);
    }

    /// <summary>
    /// Cancels the previous ShowNodeAsync and starts a new one for <paramref name="node"/>.
    /// Fire-and-forget: exceptions are caught in <see cref="SafeShowNodeAsync"/>.
    /// </summary>
    private void StartDecompileAsync(AssemblyNodeViewModel node)
    {
        _nodeSelectionCts?.Cancel();
        _nodeSelectionCts = new CancellationTokenSource();
        var ct       = _nodeSelectionCts.Token;
        var filePath = node.OwnerFilePath ?? string.Empty;
        _ = SafeShowNodeAsync(node, filePath, ct);
    }

    private async Task SafeShowNodeAsync(AssemblyNodeViewModel node, string filePath, CancellationToken ct)
    {
        try
        {
            await DetailViewModel.ShowNodeAsync(node, filePath, ct);
        }
        catch (OperationCanceledException)
        {
            // Normal — a newer selection cancelled this one.
        }
        catch (Exception ex)
        {
            // Don't crash the IDE for a decompile failure; detail pane already shows error text.
            _output.Write("Plugin System", $"[Assembly Explorer] Decompile error for '{node.DisplayName}': {ex.Message}");
        }
    }

    // ── Tree construction ─────────────────────────────────────────────────────

    private void BuildTreeChildren(AssemblyRootNodeViewModel root, AssemblyModel model)
    {
        root.Children.Clear();

        if (model.IsManaged)
        {
            AddNamespaceGroups(root, model);

            // Show type forwarders when the assembly has no types of its own (facade assemblies).
            if (model.TypeForwarders.Count > 0)
                AddTypeForwardersGroup(root, model);

            if (_showReferences && model.References.Count > 0)
                AddReferencesGroup(root, model);

            if (_showResources && model.Resources.Count > 0)
                AddResourcesGroup(root, model);

            if (_showMetadata)
                AddMetadataGroup(root, model);
        }
        else
        {
            AddSectionsGroup(root, model);
        }
    }

    private void AddNamespaceGroups(AssemblyRootNodeViewModel root, AssemblyModel model)
    {
        var byNs = model.Types
            .GroupBy(t => t.Namespace)
            .OrderBy(g => string.IsNullOrEmpty(g.Key) ? string.Empty : g.Key,
                     StringComparer.OrdinalIgnoreCase);

        foreach (var group in byNs)
        {
            // Capture locals for the lambda closure.
            var capturedTypes    = group.ToList();
            var sortAlphabetical = _sortAlphabetical;

            // Large namespaces (>50 types): use async lazy load to keep first-paint fast.
            if (capturedTypes.Count > 50)
            {
                var nsNode = new NamespaceNodeViewModel(group.Key, async () =>
                {
                    var ordered = sortAlphabetical
                        ? capturedTypes.OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
                        : (IEnumerable<TypeModel>)capturedTypes.OrderBy(t => t.PeOffset);
                    await Task.Yield(); // yield to avoid blocking UI thread
                    return ordered.Select(BuildTypeNode).ToList<AssemblyNodeViewModel>();
                });
                root.Children.Add(nsNode);
            }
            else
            {
                // Small namespace: build eagerly (simpler, avoids flicker).
                var nsNode = new NamespaceNodeViewModel(group.Key);
                var types  = sortAlphabetical
                    ? capturedTypes.OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
                    : (IEnumerable<TypeModel>)capturedTypes.OrderBy(t => t.PeOffset);

                foreach (var type in types)
                    nsNode.Children.Add(BuildTypeNode(type));

                root.Children.Add(nsNode);
            }
        }
    }

    private static TypeNodeViewModel BuildTypeNode(TypeModel type)
    {
        // Types with >30 members: defer child group construction to first expand (ASM-02-D).
        var memberCount = type.Methods.Count + type.Fields.Count
                        + type.Properties.Count + type.Events.Count;

        if (memberCount > 30)
            return new TypeNodeViewModel(type, () => BuildMemberGroups(type));

        // Small type: eagerly build children.
        var typeNode = new TypeNodeViewModel(type);
        foreach (var group in BuildMemberGroups(type))
            typeNode.Children.Add(group);
        return typeNode;
    }

    private static IReadOnlyList<AssemblyNodeViewModel> BuildMemberGroups(TypeModel type)
    {
        var groups = new List<AssemblyNodeViewModel>(5);

        // "Inherits From" group — base type + interfaces
        var hasBase       = !string.IsNullOrEmpty(type.BaseTypeName) && type.BaseTypeName != "System.Object";
        var hasInterfaces = type.InterfaceNames.Count > 0;
        if (hasBase || hasInterfaces)
        {
            var inheritsGroup = new NamespaceNodeViewModel("Inherits From");
            if (hasBase)
                inheritsGroup.Children.Add(new MetadataTableNodeViewModel($"\u21B3 {type.BaseTypeName}", 0));
            foreach (var iface in type.InterfaceNames)
                inheritsGroup.Children.Add(new MetadataTableNodeViewModel($"\u21AA {iface}", 0));
            groups.Add(inheritsGroup);
        }

        if (type.Methods.Count > 0)
        {
            var methodsGroup = new NamespaceNodeViewModel("Methods");
            foreach (var m in type.Methods)
                methodsGroup.Children.Add(new MethodNodeViewModel(m));
            groups.Add(methodsGroup);
        }

        if (type.Fields.Count > 0)
        {
            var fieldsGroup = new NamespaceNodeViewModel("Fields");
            foreach (var f in type.Fields)
                fieldsGroup.Children.Add(new FieldNodeViewModel(f));
            groups.Add(fieldsGroup);
        }

        if (type.Properties.Count > 0)
        {
            var propsGroup = new NamespaceNodeViewModel("Properties");
            foreach (var p in type.Properties)
                propsGroup.Children.Add(new PropertyNodeViewModel(p));
            groups.Add(propsGroup);
        }

        if (type.Events.Count > 0)
        {
            var eventsGroup = new NamespaceNodeViewModel("Events");
            foreach (var e in type.Events)
                eventsGroup.Children.Add(new EventNodeViewModel(e));
            groups.Add(eventsGroup);
        }

        return groups;
    }

    private static void AddTypeForwardersGroup(AssemblyRootNodeViewModel root, AssemblyModel model)
    {
        var fwdNode = new NamespaceNodeViewModel($"Type Forwarders ({model.TypeForwarders.Count})");
        foreach (var fwd in model.TypeForwarders.OrderBy(f => f.FullName, StringComparer.OrdinalIgnoreCase))
            fwdNode.Children.Add(new MetadataTableNodeViewModel(fwd.FullName, 0));
        root.Children.Add(fwdNode);
    }

    private static void AddReferencesGroup(AssemblyRootNodeViewModel root, AssemblyModel model)
    {
        var refsNode = new NamespaceNodeViewModel("References");
        foreach (var r in model.References.OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase))
            refsNode.Children.Add(new ReferenceNodeViewModel(r));
        root.Children.Add(refsNode);
    }

    private static void AddResourcesGroup(AssemblyRootNodeViewModel root, AssemblyModel model)
    {
        var resNode = new NamespaceNodeViewModel("Resources");
        foreach (var r in model.Resources)
            resNode.Children.Add(new ResourceNodeViewModel(r));
        root.Children.Add(resNode);
    }

    private static void AddMetadataGroup(AssemblyRootNodeViewModel root, AssemblyModel model)
    {
        var metaNode = new NamespaceNodeViewModel("Metadata Tables");
        metaNode.Children.Add(new MetadataTableNodeViewModel("TypeDef",     model.Types.Count));
        metaNode.Children.Add(new MetadataTableNodeViewModel("MethodDef",   model.Types.Sum(t => t.Methods.Count)));
        metaNode.Children.Add(new MetadataTableNodeViewModel("FieldDef",    model.Types.Sum(t => t.Fields.Count)));
        metaNode.Children.Add(new MetadataTableNodeViewModel("AssemblyRef", model.References.Count));
        root.Children.Add(metaNode);
    }

    private static void AddSectionsGroup(AssemblyRootNodeViewModel root, AssemblyModel model)
    {
        var secNode = new NamespaceNodeViewModel("PE Sections");
        foreach (var s in model.Sections)
            secNode.Children.Add(new MetadataTableNodeViewModel(s.Name, 0, s.RawOffset));
        root.Children.Add(secNode);
    }

    /// <summary>
    /// Recursively sets OwnerFilePath + ByteLength on every descendant node
    /// so that detail pane and hex editor integration can resolve the file without
    /// traversing the workspace dictionary on every click.
    /// </summary>
    private static void PropagateOwnerFilePath(AssemblyNodeViewModel node, string filePath)
    {
        node.OwnerFilePath = filePath;
        foreach (var child in node.Children)
            PropagateOwnerFilePath(child, filePath);
    }

    // ── Filter ────────────────────────────────────────────────────────────────

    private void ApplyFilter(string text)
    {
        foreach (var root in RootNodes)
            SetNodeVisibility(root, text);
    }

    /// <summary>
    /// Deep recursive filter: a node is visible when empty filter, its own name
    /// matches, or any descendant matches. Parents of matching children stay
    /// visible to preserve tree structure. Matched parents are auto-expanded.
    /// Returns true if this node or any descendant should be shown.
    /// </summary>
    private static bool SetNodeVisibility(AssemblyNodeViewModel node, string text)
    {
        var empty      = string.IsNullOrEmpty(text);
        var selfMatch  = empty || node.DisplayName.Contains(text, StringComparison.OrdinalIgnoreCase);
        var childMatch = false;

        foreach (var child in node.Children)
            childMatch |= SetNodeVisibility(child, text);

        node.IsMatch   = !empty && selfMatch;
        node.IsVisible = empty || selfMatch || childMatch;

        if (!empty && childMatch)
            node.IsExpanded = true;

        return node.IsVisible;
    }

    // ── Rebuild ───────────────────────────────────────────────────────────────

    /// <summary>Rebuilds children for all workspace entries (called when toggles change).</summary>
    private void RebuildAllTrees()
    {
        foreach (var entry in _workspace.Values)
        {
            BuildTreeChildren(entry.Root, entry.Model);
            PropagateOwnerFilePath(entry.Root, entry.Model.FilePath);
        }
    }

    // ── HexEditor sync ────────────────────────────────────────────────────────

    private void NavigateHexEditorToNode(AssemblyNodeViewModel node, bool force = false)
    {
        if (!force && !_syncWithHexEditor) return;
        if (node.PeOffset <= 0) return;
        if (!_hexEditor.IsActive)
        {
            if (force)
                _output.Write("Plugin System", "[Assembly Explorer] Open the assembly in the HexEditor first.");
            return;
        }

        var hexFile      = _hexEditor.CurrentFilePath;
        var assemblyFile = node.OwnerFilePath;
        if (force
            && !string.IsNullOrEmpty(hexFile)
            && !string.IsNullOrEmpty(assemblyFile)
            && !string.Equals(hexFile, assemblyFile, StringComparison.OrdinalIgnoreCase))
        {
            _output.Write("Plugin System",
                $"[Assembly Explorer] HexEditor has '{Path.GetFileName(hexFile)}' open, " +
                $"but the explorer loaded '{Path.GetFileName(assemblyFile)}'. " +
                $"Navigating anyway — offsets may not match.");
        }

        try { _hexEditor.NavigateTo(node.PeOffset); }
        catch (Exception ex)
        {
            _output.Write("Plugin System", $"[Assembly Explorer] HexEditor navigation failed: {ex.Message}");
        }
    }

    /// <summary>Explicit "Open in HexEditor" from context menu — bypasses SyncWithHexEditor toggle.</summary>
    public void NavigateToNodeExplicit(AssemblyNodeViewModel node)
    {
        StartDecompileAsync(node);
        NavigateHexEditorToNode(node, force: true);
    }

    /// <summary>
    /// Opens a local source file in the IDE text editor and navigates to the specified line.
    /// Used by the Source tab "Go to Source" command.
    /// </summary>
    public void OpenSourceFileInTextEditor(string filePath, int line)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return;
        try   { _documentHost?.ActivateAndNavigateTo(filePath, line, column: 1); }
        catch (Exception ex)
        { _output.Write("Plugin System", $"[Assembly Explorer] Failed to open source '{Path.GetFileName(filePath)}': {ex.Message}"); }
    }

    /// <summary>Opens the assembly file in the hex editor at offset 0 — no member navigation.</summary>
    public void OpenAssemblyFileInHexEditor(string filePath)
    {
        if (string.IsNullOrEmpty(filePath)) return;
        try   { _documentHost?.OpenDocument(filePath, preferredEditorId: "hex-editor"); }
        catch (Exception ex)
        { _output.Write("Plugin System", $"[Assembly Explorer] Failed to open '{Path.GetFileName(filePath)}': {ex.Message}"); }
    }

    // ── Phase 2: Deep Hex Editor Integration ──────────────────────────────────

    /// <summary>
    /// Opens the assembly file in the hex editor, scrolls to the member's PE offset,
    /// and highlights the member's byte range (if ByteLength > 0).
    /// </summary>
    public async Task OpenMemberInHexEditorAsync(AssemblyNodeViewModel node)
    {
        if (node.PeOffset <= 0) return;
        var filePath = node.OwnerFilePath;
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return;

        try
        {
            // Open / activate the .dll in the document host (hex editor).
            _documentHost?.OpenDocument(filePath, preferredEditorId: "hex-editor");

            // Clear previous highlight for this member.
            var tag = $"AsmExplorer.{node.MetadataToken}";
            _hexEditor.ClearCustomBackgroundBlockByTag(tag);

            // Navigate to offset (scrolls + selects 1 byte at minimum).
            _hexEditor.NavigateTo(node.PeOffset);

            // Add highlight if we know the byte range.
            // Description is used as the tag by ClearCustomBackgroundBlockByTag.
            if (node.ByteLength > 0)
            {
                var brush = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(79, 193, 255));
                var block = new WpfHexEditor.Core.CustomBackgroundBlock
                {
                    StartOffset = node.PeOffset,
                    Length      = node.ByteLength,
                    Color       = brush,
                    Opacity     = 0.25,
                    Description = tag   // used as tag by ClearCustomBackgroundBlockByTag
                };
                _hexEditor.AddCustomBackgroundBlock(block);
            }

            _output.Write("Plugin System",
                $"[Assembly Explorer] Navigated hex editor to '{node.DisplayName}'" +
                $" offset 0x{node.PeOffset:X}" +
                (node.ByteLength > 0 ? $" ({node.ByteLength} bytes)" : string.Empty));
        }
        catch (Exception ex)
        {
            _output.Write("Plugin System", $"[Assembly Explorer] Hex editor navigation failed: {ex.Message}");
        }

        await Task.CompletedTask;
    }

    // ── Phase 6: Cross-assembly reference navigation ──────────────────────────

    private void TryNavigateToReference(ReferenceNodeViewModel refNode)
    {
        var refName = refNode.Reference.Name;

        // Find the workspace entry whose assembly name matches this reference.
        var targetEntry = _workspace.Values
            .FirstOrDefault(e => string.Equals(e.Model.Name, refName, StringComparison.OrdinalIgnoreCase));

        if (targetEntry is not null)
        {
            // Jump to the root in the tree.
            targetEntry.Root.IsSelected = true;
            targetEntry.Root.IsExpanded = true;
            StatusText = $"Jumped to '{targetEntry.Model.Name}' in workspace.";
        }
        else
        {
            StatusText = $"Assembly '{refName}' is not in the workspace. Use Open Assembly to load it.";
        }
    }

    // ── EventBus publishing ───────────────────────────────────────────────────

    private void PublishMemberSelected(AssemblyNodeViewModel node)
    {
        MemberSelected?.Invoke(this, new AssemblyMemberSelectedEvent
        {
            NodeDisplayName = node.DisplayName,
            MetadataToken   = node.MetadataToken,
            PeOffset        = node.PeOffset,
            NodeKind        = node.GetType().Name.Replace("NodeViewModel", string.Empty)
        });
    }

    // ── Open in Code Editor ───────────────────────────────────────────────────

    private async Task OpenSelectedNodeInEditorAsync()
    {
        if (_selectedNode is null) return;

        var node     = _selectedNode;
        var filePath = node.OwnerFilePath ?? string.Empty;

        // Resolve the active output language from the registry.
        var langId   = _decompilerBackend.Options.TargetLanguageId ?? "CSharp";
        var language = DecompilationLanguageRegistry.Get(langId)
                    ?? CSharpDecompilationLanguage.Instance;

        // Include language ID in the tab UI ID to avoid collisions between C# and VB.NET tabs.
        var token = node.MetadataToken;
        var hash  = token != 0 ? token.ToString("X8") : node.DisplayName.GetHashCode().ToString("X8");
        var uiId  = $"doc-plugin-{_pluginId}-decompiled-{hash}-{langId}";
        var title = $"{node.DisplayName} ({language.DisplayName})";

        if (_uiRegistry.Exists(uiId))
        {
            // Close the existing tab so we can re-open it with fresh content.
            // Silently re-using the old tab is problematic when it was registered with
            // empty/stale content (e.g. the ApplicationIdle callback was never fired).
            _uiRegistry.UnregisterDocumentTab(uiId);
        }

        // IL Disassembly — produced directly by GetIlText; no C#→IL transform.
        bool isIlOutput = string.Equals(language.Id, "IL", StringComparison.OrdinalIgnoreCase);

        // Decompile on a background thread.
        string rawText;
        try
        {
            if (isIlOutput)
            {
                rawText = await Task.Run(() =>
                {
                    switch (node)
                    {
                        case MethodNodeViewModel meth:
                            var single = _decompiler.GetIlText(meth.Model, filePath);
                            return string.IsNullOrEmpty(single)
                                ? "// No IL body.\n// Possible causes: abstract, extern, interface, " +
                                  "delegate stub, or reference assembly."
                                : single;

                        case TypeNodeViewModel type:
                            // Filter out methods with no IL body (delegates, abstract, reference asm).
                            var parts = type.Model.Methods
                                .Select(m => _decompiler.GetIlText(m, filePath))
                                .Where(s => !string.IsNullOrEmpty(s))
                                .ToList();
                            return parts.Count > 0
                                ? string.Join(Environment.NewLine + Environment.NewLine, parts)
                                : "// No IL disassembly available for this type.\n" +
                                  "// Possible causes:\n" +
                                  "//   1. Reference assembly — load the runtime .dll instead.\n" +
                                  "//   2. Delegate type — Invoke/BeginInvoke/EndInvoke are CLR stubs.\n" +
                                  "//   3. All methods are abstract, extern, or interface declarations.";

                        default:
                            return _decompiler.GetStubText(node.DisplayName);
                    }
                });
            }
            else
            {
                rawText = await Task.Run(() => node switch
                {
                    AssemblyRootNodeViewModel root => _decompilerBackend.DecompileAssembly(root.Model, filePath),
                    TypeNodeViewModel         type => _decompilerBackend.DecompileType(type.Model, filePath),
                    MethodNodeViewModel       meth => _decompilerBackend.DecompileMethod(meth.Model, filePath),
                    _                              => _decompiler.GetStubText(node.DisplayName)
                });
            }
        }
        catch (Exception ex)
        {
            rawText = $"// Decompilation failed: {ex.Message}";
        }

        // Apply language transform when backend output is C#-only and target is not C# or IL.
        // IL text was already produced directly — skip the transform entirely.
        string text;
        if (!isIlOutput && _decompilerBackend.OutputIsCSharpOnly && language.Id != "CSharp")
        {
            try
            {
                var (transformed, _) = await language.TransformFromCSharpAsync(rawText, CancellationToken.None);
                text = transformed;
            }
            catch (Exception ex)
            {
                text = $"// {language.DisplayName} transform failed: {ex.Message}\n\n{rawText}";
            }
        }
        else
        {
            text = rawText;
        }

        // TextLinks (goto-def) are only meaningful for C# output; IL has no links.
        var isCSharpOutput = language.Id == "CSharp" && !isIlOutput;
        var assemblyModel  = string.IsNullOrEmpty(filePath)
            ? null
            : _workspace.TryGetValue(filePath, out var entry) ? entry.Model : null;

        var content = BuildDecompiledCodeEditor(text, isCSharpOutput, language.EditorLanguageName, assemblyModel);

        _uiRegistry.RegisterDocumentTab(uiId, content, _pluginId, new DocumentDescriptor
        {
            Title     = title,
            ContentId = uiId,
            ToolTip   = $"Decompiled: {node.DisplayName}",
            CanClose  = true
        });
    }

    /// <summary>
    /// Builds a full <see cref="CodeEditorSplitHost"/> tab for the decompiled code.
    /// Syntax highlighting, folding, and search are provided by the host code editor.
    /// The <paramref name="installLinks"/> and <paramref name="assembly"/> parameters are
    /// retained for signature compatibility but are no longer needed — the CodeEditor
    /// handles navigation natively via its LSP/symbol infrastructure.
    /// </summary>
    private UIElement BuildDecompiledCodeEditor(
        string         text,
        bool           installLinks,
        string?        editorLanguageName,
        AssemblyModel? assembly = null)
    {
        var host = new CodeEditorSplitHost();

        // Map decompilation display names → LanguageRegistry IDs.
        // Decompilation is already complete at this point (called after await Task.Run),
        // so we load the text synchronously on the UI thread.
        var langId = editorLanguageName switch
        {
            "C#"     => "csharp",
            "VB.NET" => "vbnet",
            "F#"     => "fsharp",
            { } s    => s,
            null     => "csharp"
        };
        var lang = LanguageRegistry.Instance.FindById(langId);
        if (lang is not null) host.SetLanguage(lang);
        host.IsReadOnly = true;
        host.PrimaryEditor.LoadText(text);

        return host;
    }

    /// <summary>
    /// Builds <see cref="TextLink"/> objects for every PascalCase identifier in
    /// <paramref name="decompiledText"/> that matches a type name in
    /// <paramref name="assembly"/>.  Ctrl+Clicking a link navigates to that type.
    /// </summary>
    private IReadOnlyList<TextLink> BuildTextLinks(string decompiledText, AssemblyModel assembly)
    {
        var spans = DecompiledTextLinker.ExtractTypeNames(decompiledText);
        if (spans.Count == 0) return [];

        // Build a fast lookup: type simple-name → type full name.
        var typeNameLookup = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var type in assembly.Types)
        {
            var simpleName = type.FullName.Contains('.')
                ? type.FullName[(type.FullName.LastIndexOf('.') + 1)..]
                : type.FullName;

            // Remove generic arity suffix `1, `2 etc.
            var backtick = simpleName.IndexOf('`');
            if (backtick >= 0) simpleName = simpleName[..backtick];

            typeNameLookup.TryAdd(simpleName, type.FullName);
        }

        var links = new List<TextLink>(spans.Count);
        foreach (var span in spans)
        {
            if (!typeNameLookup.TryGetValue(span.Text, out var fullName)) continue;

            var capturedFullName = fullName;
            links.Add(new TextLink(
                StartOffset: span.Start,
                EndOffset:   span.Start + span.Length,
                DisplayText: span.Text,
                OnClick:     () => NavigateToTypeName(capturedFullName)));
        }

        return links;
    }

    /// <summary>Navigates the explorer tree to the type with the given full name.</summary>
    private void NavigateToTypeName(string fullName)
    {
        foreach (var root in RootNodes)
        {
            foreach (var nsNode in root.Children)
            {
                foreach (var typeNode in nsNode.Children)
                {
                    if (typeNode is TypeNodeViewModel tn && tn.Model.FullName == fullName)
                    {
                        SelectedNode = tn;
                        return;
                    }
                }
            }
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static void SetAllExpanded(IEnumerable<AssemblyNodeViewModel> nodes, bool expanded)
    {
        foreach (var node in nodes)
        {
            node.IsExpanded = expanded;
            SetAllExpanded(node.Children, expanded);
        }
    }

    private AssemblyWorkspaceEntry? FindEntryForNode(AssemblyNodeViewModel node)
    {
        // Fast path: root nodes are direct children of RootNodes.
        if (node is AssemblyRootNodeViewModel root)
            return _workspace.Values.FirstOrDefault(e => ReferenceEquals(e.Root, root));

        // General path: use the OwnerFilePath tag.
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

        // Clear detail pane if the selected node belonged to this entry.
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

        // Fire AssemblyCleared only when workspace becomes fully empty.
        if (_workspace.Count == 0)
            AssemblyCleared?.Invoke(this, EventArgs.Empty);
        else
            AssemblyUnloaded?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateStatusText()
    {
        if (_workspace.Count == 0)
        {
            StatusText = "No assembly loaded";
            return;
        }

        if (_workspace.Count == 1)
        {
            var entry      = _workspace.Values.First();
            var typeCount  = entry.Model.Types.Count;
            var methCount  = entry.Model.Types.Sum(t => t.Methods.Count);
            StatusText = entry.Model.IsManaged
                ? $"{typeCount} types | {methCount} methods"
                : $"Native PE — {entry.Model.Sections.Count} sections";
        }
        else
        {
            StatusText = $"{_workspace.Count} assemblies loaded";
        }
    }

    private void RaiseWorkspaceStatsChanged()
    {
        TotalLoadedAssemblies = _workspace.Count;
        TotalLoadedTypes      = _workspace.Values.Sum(e => e.Model.Types.Count);
        WorkspaceStatsChanged?.Invoke(this, EventArgs.Empty);
    }

    // ── Extract to project ────────────────────────────────────────────────────

    /// <summary>
    /// Writes an informational message to the IDE output and updates the status bar text.
    /// Used by the panel layer after extract/save operations.
    /// </summary>
    public void ReportInfo(string message)
    {
        StatusText = message;
        _output.Write("Plugin System", $"[Assembly Explorer] {message}");
    }

    /// <summary>
    /// Returns the decompiled C# text for the given node.
    /// Returns (<see langword="true"/>, text) for decompilable nodes;
    /// (<see langword="false"/>, stub) for group/namespace nodes with no C# output.
    /// </summary>
    public (bool isCSharp, string text) GetDecompiledText(AssemblyNodeViewModel node)
    {
        var filePath = node.OwnerFilePath ?? string.Empty;
        return node switch
        {
            AssemblyRootNodeViewModel root => (true,  _decompilerBackend.DecompileAssembly(root.Model, filePath)),
            TypeNodeViewModel         type => (true,  _decompilerBackend.DecompileType(type.Model, filePath)),
            MethodNodeViewModel       meth => (true,  _decompilerBackend.DecompileMethod(meth.Model, filePath)),
            _                              => (false, _decompiler.GetStubText(node.DisplayName))
        };
    }

    // ── Metadata table navigation (ASM-02-WIRE) ───────────────────────────────

    /// <summary>
    /// Navigates the tree to the <see cref="MetadataTableNodeViewModel"/> with
    /// the given <paramref name="tableName"/> inside the assembly that owns
    /// <paramref name="ownerFilePath"/>. Expands the "Metadata Tables" group and
    /// sets <see cref="SelectedNode"/>. No-op when the node cannot be found.
    /// </summary>
    public void NavigateToMetadataTable(string tableName, string ownerFilePath)
    {
        var root = RootNodes.OfType<AssemblyRootNodeViewModel>()
                            .FirstOrDefault(r => r.OwnerFilePath == ownerFilePath
                                             || r.Model.FilePath == ownerFilePath);
        if (root is null) return;

        var metaGroup = root.Children.OfType<NamespaceNodeViewModel>()
                            .FirstOrDefault(n => n.DisplayName == "Metadata Tables");
        if (metaGroup is null) return;

        metaGroup.IsExpanded = true;

        var tableNode = metaGroup.Children.OfType<MetadataTableNodeViewModel>()
                                 .FirstOrDefault(t => t.TableName == tableName);
        if (tableNode is null) return;

        SelectedNode = tableNode;
    }

    // ── Reverse Hex → Tree navigation (ASM-02-A) ──────────────────────────────

    /// <summary>
    /// Selects the tree node whose <see cref="AssemblyNodeViewModel.MetadataToken"/> matches
    /// <paramref name="token"/> and sets <see cref="AssemblyNodeViewModel.IsReverseHighlighted"/>.
    /// No-op when <paramref name="token"/> is null or no matching node is found.
    /// Must be called on the UI thread.
    /// </summary>
    public void SelectNode(int? token)
    {
        if (token is null) return;
        var tokenValue = token.Value;
        if (tokenValue == 0) return;

        // Clear previous reverse-highlight across all roots.
        ClearReverseHighlight(RootNodes);

        foreach (var root in RootNodes)
        {
            var found = FindNodeByTokenRecursive(root.Children, tokenValue);
            if (found is null) continue;

            found.IsReverseHighlighted = true;
            found.IsSelected           = true;

            // Ensure path to the found node is expanded so it is visible.
            EnsureAncestorsExpanded(root, found);
            return;
        }
    }

    /// <summary>
    /// Navigates the hex editor to the PE byte offset corresponding to
    /// the given <paramref name="token"/> in the currently active assembly.
    /// No-op when no matching node is found or the offset is 0.
    /// </summary>
    public void NavigateToOffset(int? token)
    {
        if (token is null) return;
        foreach (var root in RootNodes)
        {
            var node = FindNodeByTokenRecursive(root.Children, token.Value);
            if (node is not null && node.PeOffset > 0)
            {
                NavigateHexEditorToNode(node, force: true);
                return;
            }
        }
    }

    private static void ClearReverseHighlight(IEnumerable<AssemblyNodeViewModel> nodes)
    {
        foreach (var node in nodes)
        {
            if (node.IsReverseHighlighted) node.IsReverseHighlighted = false;
            ClearReverseHighlight(node.Children);
        }
    }

    /// <summary>
    /// Expands all ancestors of <paramref name="target"/> within the subtree rooted at
    /// <paramref name="current"/>. Returns true when target is found in the subtree.
    /// </summary>
    private static bool EnsureAncestorsExpanded(AssemblyNodeViewModel current, AssemblyNodeViewModel target)
    {
        if (ReferenceEquals(current, target)) return true;

        foreach (var child in current.Children)
        {
            if (EnsureAncestorsExpanded(child, target))
            {
                current.IsExpanded = true;
                return true;
            }
        }

        return false;
    }

    // ── Search / Diff support ─────────────────────────────────────────────────

    /// <summary>Returns all AssemblyModels currently in the workspace for search/diff.</summary>
    public IReadOnlyList<WpfHexEditor.Core.AssemblyAnalysis.Models.AssemblyModel> GetLoadedAssemblyModels()
        => _workspace.Values.Select(e => e.Model).ToList();

    /// <summary>
    /// Finds the first tree node whose MetadataToken matches and whose OwnerFilePath
    /// matches the given assembly file path. Used by search result navigation.
    /// </summary>
    public AssemblyNodeViewModel? FindNodeByToken(int token, string filePath)
    {
        if (!_workspace.TryGetValue(filePath, out var entry)) return null;
        return FindNodeByTokenRecursive(entry.Root.Children, token);
    }

    private static AssemblyNodeViewModel? FindNodeByTokenRecursive(
        IEnumerable<AssemblyNodeViewModel> nodes, int token)
    {
        foreach (var node in nodes)
        {
            if (node.MetadataToken == token) return node;
            var found = FindNodeByTokenRecursive(node.Children, token);
            if (found is not null) return found;
        }
        return null;
    }
}
