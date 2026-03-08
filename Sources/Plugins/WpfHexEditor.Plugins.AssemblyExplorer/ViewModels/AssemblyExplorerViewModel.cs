// ==========================================================
// Project: WpfHexEditor.Plugins.AssemblyExplorer
// File: ViewModels/AssemblyExplorerViewModel.cs
// Author: Derek Tremblay
// Created: 2026-03-08
// Description:
//     Root orchestrator ViewModel for the Assembly Explorer panel.
//     Coordinates assembly loading (background), tree construction (UI),
//     node selection, HexEditor sync, filter, and EventBus publishing.
//
// Architecture Notes:
//     Pattern: MVVM orchestrator.
//     Analysis runs on Task.Run background thread; all tree mutations
//     occur on the UI thread (Dispatcher.InvokeAsync or direct call after await).
//     EventBus publishing is done here — the plugin entry point wires
//     AssemblyLoaded to update status bar items.
// ==========================================================

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using WpfHexEditor.Core.AssemblyAnalysis.Models;
using WpfHexEditor.Core.AssemblyAnalysis.Services;
using IAssemblyAnalysisEngine = WpfHexEditor.Core.AssemblyAnalysis.Services.IAssemblyAnalysisEngine;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WpfHexEditor.Plugins.AssemblyExplorer.Events;
using WpfHexEditor.Plugins.AssemblyExplorer.Services;
using WpfHexEditor.SDK.Commands;
using WpfHexEditor.SDK.Contracts;
using WpfHexEditor.SDK.Contracts.Services;
using WpfHexEditor.SDK.Descriptors;

namespace WpfHexEditor.Plugins.AssemblyExplorer.ViewModels;

/// <summary>
/// Root ViewModel for the Assembly Explorer panel.
/// Loaded once and kept alive for the plugin lifetime.
/// </summary>
public sealed class AssemblyExplorerViewModel : INotifyPropertyChanged
{
    private readonly IAssemblyAnalysisEngine _analysisService;
    private readonly DecompilerService       _decompiler;
    private readonly IHexEditorService       _hexEditor;
    private readonly IOutputService          _output;
    private readonly IUIRegistry             _uiRegistry;
    private readonly string                  _pluginId;

    private CancellationTokenSource? _loadCts;

    public AssemblyExplorerViewModel(
        IAssemblyAnalysisEngine analysisService,
        DecompilerService       decompiler,
        IHexEditorService       hexEditor,
        IOutputService          output,
        IUIRegistry             uiRegistry,
        string                  pluginId)
    {
        _analysisService = analysisService;
        _decompiler      = decompiler;
        _hexEditor       = hexEditor;
        _output          = output;
        _uiRegistry      = uiRegistry;
        _pluginId        = pluginId;

        DetailViewModel = new AssemblyDetailViewModel(decompiler);

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

        // Phase 5: opens decompiled text in a read-only styled TextBox document tab
        OpenInEditorCommand = new RelayCommand(
            _ => OpenSelectedNodeInEditor(),
            _ => SelectedNode is not null);
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

    // ── Toolbar toggles ───────────────────────────────────────────────────────

    private bool _sortAlphabetical = true;
    public bool SortAlphabetical
    {
        get => _sortAlphabetical;
        set { if (SetField(ref _sortAlphabetical, value)) RebuildTree(); }
    }

    private bool _showReferences = true;
    public bool ShowReferences
    {
        get => _showReferences;
        set { if (SetField(ref _showReferences, value)) RebuildTree(); }
    }

    private bool _showResources = true;
    public bool ShowResources
    {
        get => _showResources;
        set { if (SetField(ref _showResources, value)) RebuildTree(); }
    }

    private bool _showMetadata;
    public bool ShowMetadata
    {
        get => _showMetadata;
        set { if (SetField(ref _showMetadata, value)) RebuildTree(); }
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

    public ICommand LoadCurrentFileCommand { get; }
    public ICommand CollapseAllCommand     { get; }
    public ICommand ExpandAllCommand       { get; }
    public ICommand ClearCommand           { get; }
    public ICommand OpenInEditorCommand    { get; }

    // ── Events (consumed by plugin entry point for status bar) ────────────────

    public event EventHandler<AssemblyLoadedEvent>? AssemblyLoaded;

    // ── Last loaded model (for rebuild) ───────────────────────────────────────

    private AssemblyModel? _lastModel;

    /// <summary>
    /// File path of the currently loaded assembly, or null when no assembly is loaded.
    /// Exposed for code-behind/detail pane consumers that need on-demand file access (IL, hex).
    /// </summary>
    public string? CurrentAssemblyFilePath => _lastModel?.FilePath;

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Loads and analyzes the assembly at <paramref name="filePath"/>.
    /// Runs analysis on a background thread; populates the tree on the UI thread.
    /// Safe to call from any thread (cancels any in-progress load first).
    /// </summary>
    public async Task LoadAssemblyAsync(string filePath, CancellationToken externalCt = default)
    {
        if (string.IsNullOrEmpty(filePath) || !_analysisService.CanAnalyze(filePath))
        {
            StatusText = "No assembly loaded";
            return;
        }

        // Cancel previous load.
        _loadCts?.Cancel();
        _loadCts = CancellationTokenSource.CreateLinkedTokenSource(externalCt);
        var ct = _loadCts.Token;

        IsLoading  = true;
        StatusText = $"Analyzing {Path.GetFileName(filePath)}…";

        try
        {
            var model = await Task.Run(() => _analysisService.AnalyzeAsync(filePath, ct), ct);
            ct.ThrowIfCancellationRequested();

            _lastModel = model;
            PopulateTree(model);
        }
        catch (OperationCanceledException)
        {
            StatusText = "Analysis cancelled.";
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
            _output.Error($"[Assembly Explorer] Failed to analyze '{filePath}': {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private Task LoadCurrentFileAsync()
        => LoadAssemblyAsync(_hexEditor.CurrentFilePath ?? string.Empty);

    /// <summary>Clears the tree and resets all state.</summary>
    public void Clear()
    {
        RootNodes.Clear();
        DetailViewModel.Clear();
        StatusText  = "No assembly loaded";
        FilterText  = string.Empty;
        _lastModel  = null;
        _selectedNode = null;
        AssemblyCleared?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Raised when the panel is explicitly cleared by the user.
    /// The plugin entry point subscribes to erase the persisted session path.
    /// </summary>
    public event EventHandler? AssemblyCleared;

    /// <summary>
    /// Called when the user selects a tree node.
    /// Updates the detail pane and optionally syncs the HexEditor.
    /// </summary>
    public void OnNodeSelected(AssemblyNodeViewModel node)
    {
        DetailViewModel.ShowNode(node, _lastModel?.FilePath ?? string.Empty);
        NavigateHexEditorToNode(node);
        PublishMemberSelected(node);
    }

    // ── Tree construction ─────────────────────────────────────────────────────

    private void PopulateTree(AssemblyModel model)
    {
        RootNodes.Clear();

        var root = new AssemblyRootNodeViewModel(model);
        BuildTreeChildren(root, model);
        RootNodes.Add(root);

        var typeCount   = model.Types.Count;
        var methodCount = model.Types.Sum(t => t.Methods.Count);
        StatusText = model.IsManaged
            ? $"{typeCount} types | {methodCount} methods"
            : $"Native PE — {model.Sections.Count} sections";

        AssemblyLoaded?.Invoke(this, new AssemblyLoadedEvent
        {
            FilePath    = model.FilePath,
            Name        = model.Name,
            Version     = model.Version,
            IsManaged   = model.IsManaged,
            TypeCount   = typeCount,
            MethodCount = methodCount
        });
    }

    private void BuildTreeChildren(AssemblyRootNodeViewModel root, AssemblyModel model)
    {
        if (model.IsManaged)
        {
            AddNamespaceGroups(root, model);

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
            var nsNode = new NamespaceNodeViewModel(group.Key);
            var types  = _sortAlphabetical
                ? group.OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
                : group.OrderBy(t => t.PeOffset);

            foreach (var type in types)
                nsNode.Children.Add(BuildTypeNode(type));

            root.Children.Add(nsNode);
        }
    }

    private static TypeNodeViewModel BuildTypeNode(TypeModel type)
    {
        var typeNode = new TypeNodeViewModel(type);

        // "Inherits From" group — base type + interfaces (first child for easy discovery)
        var hasBase       = !string.IsNullOrEmpty(type.BaseTypeName) && type.BaseTypeName != "System.Object";
        var hasInterfaces = type.InterfaceNames.Count > 0;
        if (hasBase || hasInterfaces)
        {
            var inheritsGroup = new NamespaceNodeViewModel("Inherits From");
            if (hasBase)
                inheritsGroup.Children.Add(
                    new MetadataTableNodeViewModel($"\u21B3 {type.BaseTypeName}", 0));  // ↳
            foreach (var iface in type.InterfaceNames)
                inheritsGroup.Children.Add(
                    new MetadataTableNodeViewModel($"\u21AA {iface}", 0));              // ↪
            typeNode.Children.Add(inheritsGroup);
        }

        if (type.Methods.Count > 0)
        {
            var methodsGroup = new NamespaceNodeViewModel("Methods");
            foreach (var m in type.Methods)
                methodsGroup.Children.Add(new MethodNodeViewModel(m));
            typeNode.Children.Add(methodsGroup);
        }

        if (type.Fields.Count > 0)
        {
            var fieldsGroup = new NamespaceNodeViewModel("Fields");
            foreach (var f in type.Fields)
                fieldsGroup.Children.Add(new FieldNodeViewModel(f));
            typeNode.Children.Add(fieldsGroup);
        }

        if (type.Properties.Count > 0)
        {
            var propsGroup = new NamespaceNodeViewModel("Properties");
            foreach (var p in type.Properties)
                propsGroup.Children.Add(new PropertyNodeViewModel(p));
            typeNode.Children.Add(propsGroup);
        }

        if (type.Events.Count > 0)
        {
            var eventsGroup = new NamespaceNodeViewModel("Events");
            foreach (var e in type.Events)
                eventsGroup.Children.Add(new EventNodeViewModel(e));
            typeNode.Children.Add(eventsGroup);
        }

        return typeNode;
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
        metaNode.Children.Add(new MetadataTableNodeViewModel("TypeDef",   model.Types.Count));
        metaNode.Children.Add(new MetadataTableNodeViewModel("MethodDef", model.Types.Sum(t => t.Methods.Count)));
        metaNode.Children.Add(new MetadataTableNodeViewModel("FieldDef",  model.Types.Sum(t => t.Fields.Count)));
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

        // Auto-expand parents that have matching descendants.
        if (!empty && childMatch)
            node.IsExpanded = true;

        return node.IsVisible;
    }

    // ── Rebuild ───────────────────────────────────────────────────────────────

    private void RebuildTree()
    {
        if (_lastModel is null) return;
        PopulateTree(_lastModel);
    }

    // ── HexEditor sync ────────────────────────────────────────────────────────

    private void NavigateHexEditorToNode(AssemblyNodeViewModel node)
    {
        if (!_syncWithHexEditor || node.PeOffset <= 0) return;
        if (!_hexEditor.IsActive) return;

        try
        {
            _hexEditor.SetSelection(node.PeOffset, node.PeOffset + 1);
        }
        catch (Exception ex)
        {
            _output.Warning($"[Assembly Explorer] HexEditor sync failed: {ex.Message}");
        }
    }

    // ── EventBus publishing ───────────────────────────────────────────────────

    private void PublishMemberSelected(AssemblyNodeViewModel node)
    {
        // EventBus publish is done from the plugin entry point (has IPluginEventBus reference).
        // ViewModel raises a lightweight internal event instead of coupling to the SDK EventBus.
        MemberSelected?.Invoke(this, new AssemblyMemberSelectedEvent
        {
            NodeDisplayName = node.DisplayName,
            MetadataToken   = node.MetadataToken,
            PeOffset        = node.PeOffset,
            NodeKind        = node.GetType().Name.Replace("NodeViewModel", string.Empty)
        });
    }

    public event EventHandler<AssemblyMemberSelectedEvent>? MemberSelected;

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static void SetAllExpanded(
        IEnumerable<AssemblyNodeViewModel> nodes,
        bool expanded)
    {
        foreach (var node in nodes)
        {
            node.IsExpanded = expanded;
            SetAllExpanded(node.Children, expanded);
        }
    }

    // ── Open in Code Editor ───────────────────────────────────────────────────

    private void OpenSelectedNodeInEditor()
    {
        if (_selectedNode is null) return;

        var text = _selectedNode switch
        {
            AssemblyRootNodeViewModel root => _decompiler.DecompileAssembly(root.Model),
            TypeNodeViewModel         type => _decompiler.DecompileType(type.Model),
            MethodNodeViewModel       meth => _decompiler.DecompileMethod(meth.Model),
            _                              => _decompiler.GetStubText(_selectedNode.DisplayName)
        };

        var token  = _selectedNode.MetadataToken;
        var uiId   = $"doc-plugin-{_pluginId}-decompiled-{(token != 0 ? token.ToString("X8") : _selectedNode.DisplayName.GetHashCode().ToString("X8"))}";
        var title  = $"{_selectedNode.DisplayName} (decompiled)";

        // If the tab is already open, just show it; otherwise create it.
        if (_uiRegistry.Exists(uiId))
        {
            _output.Info($"[Assembly Explorer] '{_selectedNode.DisplayName}' is already open in the editor.");
            return;
        }

        var content = BuildDecompiledContent(text);

        _uiRegistry.RegisterDocumentTab(uiId, content, _pluginId, new DocumentDescriptor
        {
            Title     = title,
            ContentId = uiId,
            ToolTip   = $"Decompiled: {_selectedNode.DisplayName}",
            CanClose  = true
        });
    }

    /// <summary>
    /// Builds a read-only, theme-aware WPF control to display decompiled text as a document tab.
    /// Uses DynamicResource so the content adapts automatically to IDE theme changes.
    /// No TextEditor project reference required — SDK-only approach.
    /// </summary>
    private static UIElement BuildDecompiledContent(string text)
    {
        var textBox = new TextBox
        {
            Text              = text,
            IsReadOnly        = true,
            FontFamily        = new FontFamily("Consolas"),
            FontSize          = 12,
            BorderThickness   = new Thickness(0),
            TextWrapping      = TextWrapping.NoWrap,
            CaretBrush        = Brushes.Transparent,
            Padding           = new Thickness(8, 4, 8, 4),
            VerticalScrollBarVisibility   = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto
        };

        // DynamicResource resolves once the control enters the visual tree inside the IDE.
        textBox.SetResourceReference(TextBox.ForegroundProperty, "PFP_SubTextBrush");
        textBox.SetResourceReference(TextBox.BackgroundProperty, "PFP_SectionBackgroundBrush");

        return textBox;
    }
}
