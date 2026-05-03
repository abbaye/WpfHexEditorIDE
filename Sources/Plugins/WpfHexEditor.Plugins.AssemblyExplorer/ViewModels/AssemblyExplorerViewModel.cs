// ==========================================================
// Project: WpfHexEditor.Plugins.AssemblyExplorer
// File: ViewModels/AssemblyExplorerViewModel.cs
// Description:
//     Root orchestrator ViewModel for the Assembly Explorer panel.
//     Split into 5 partial classes:
//       - AssemblyExplorerViewModel.cs          (this file â€” fields, ctor, INPC, commands, events)
//       - AssemblyExplorerViewModel.TreeState.cs (tree properties, toolbar toggles, workspace stats)
//       - AssemblyExplorerViewModel.Loading.cs   (LoadAssemblyAsync, Clear, CloseEntry, workspace management)
//       - AssemblyExplorerViewModel.TreeBuilding.cs (BuildTreeChildren, filter, rebuild)
//       - AssemblyExplorerViewModel.Navigation.cs   (hex nav, decompilation, editor integration, search)
// Architecture Notes:
//     Pattern: MVVM orchestrator. Analysis runs on Task.Run; tree mutations on UI thread.
// ==========================================================

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using WpfHexEditor.Core.AssemblyAnalysis.Services;
using WpfHexEditor.Plugins.AssemblyExplorer.Events;
using WpfHexEditor.Plugins.AssemblyExplorer.Services;
using WpfHexEditor.SDK.Commands;
using WpfHexEditor.SDK.Contracts;
using WpfHexEditor.SDK.Contracts.Services;
using IAssemblyAnalysisEngine = WpfHexEditor.Core.AssemblyAnalysis.Services.IAssemblyAnalysisEngine;
using WpfHexEditor.Core.ViewModels;

namespace WpfHexEditor.Plugins.AssemblyExplorer.ViewModels;

/// <summary>
/// Root ViewModel for the Assembly Explorer panel.
/// Loaded once and kept alive for the plugin lifetime.
/// Supports simultaneous loading of multiple assemblies (multi-assembly workspace).
/// </summary>
public sealed partial class AssemblyExplorerViewModel : ViewModelBase
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

        OpenInEditorCommand = new RelayCommand(
            _ => _ = OpenSelectedNodeInEditorAsync(),
            _ => SelectedNode is not null);

        OpenInHexEditorCommand = new RelayCommand(
            p => { if (p is AssemblyNodeViewModel n) _ = OpenMemberInHexEditorAsync(n); },
            p => p is AssemblyNodeViewModel node && node.PeOffset > 0);
    }

    // ── INotifyPropertyChanged ────────────────────────────────────────────────



    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
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

    public event EventHandler<AssemblyLoadedEvent>?           AssemblyLoaded;
    public event EventHandler?                                AssemblyCleared;
    public event EventHandler?                                AssemblyUnloaded;
    public event EventHandler<AssemblyMemberSelectedEvent>?   MemberSelected;
    public event EventHandler?                                WorkspaceStatsChanged;

    // ── Stats (shown in status bar) ───────────────────────────────────────────

    private string _statsText = "No assemblies loaded.";

    /// <summary>Human-readable summary shown in the panel's status bar.</summary>
    public string StatsText
    {
        get => _statsText;
        private set { _statsText = value; OnPropertyChanged(); }
    }

    /// <summary>Updates <see cref="StatsText"/> from the already-computed workspace counts.</summary>
    internal void RefreshStats()
    {
        int asmCount    = TotalLoadedAssemblies;
        int typeCount   = TotalLoadedTypes;

        StatsText = asmCount == 0
            ? "No assemblies loaded."
            : $"{asmCount} {(asmCount == 1 ? "assembly" : "assemblies")} | {typeCount} types";
    }
}
