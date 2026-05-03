// ==========================================================
// Project: WpfHexEditor.Plugins.AssemblyExplorer
// File: ViewModels/AssemblyExplorerViewModel.TreeState.cs
// Description:
//     Tree properties, toolbar toggles, workspace stats, language management.
// ==========================================================

using System.Collections.ObjectModel;
using WpfHexEditor.Core.AssemblyAnalysis.Services;
using WpfHexEditor.Plugins.AssemblyExplorer.Services;
using WpfHexEditor.Plugins.AssemblyExplorer.Properties;

namespace WpfHexEditor.Plugins.AssemblyExplorer.ViewModels;

public sealed partial class AssemblyExplorerViewModel
{
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

    // ── Loading state ─────────────────────────────────────────────────────────

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        private set => SetField(ref _isLoading, value);
    }

    private string _statusText = AssemblyExplorerResources.AsmExplorer_NoAssemblyLoaded;
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

    // ── Language management ───────────────────────────────────────────────────

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
        SetDefaultLanguage(savedId ?? "CSharp");
    }

    /// <summary>
    /// Persists <paramref name="languageId"/> as the target decompilation language.
    /// </summary>
    public void SetDefaultLanguage(string languageId)
    {
        var opts = _decompilerBackend.Options with { TargetLanguageId = languageId };
        _decompilerBackend.Options = opts;
    }

    // ── Status helpers ────────────────────────────────────────────────────────

    private void UpdateStatusText()
    {
        if (_workspace.Count == 0)
        {
            StatusText = AssemblyExplorerResources.AsmExplorer_NoAssemblyLoaded;
            return;
        }

        if (_workspace.Count == 1)
        {
            var entry     = _workspace.Values.First();
            var typeCount = entry.Model.Types.Count;
            var methCount = entry.Model.Types.Sum(t => t.Methods.Count);
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

        // Refresh the status bar stats text
        RefreshStats();

        WorkspaceStatsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Writes an informational message to the IDE output and updates the status bar text.
    /// Used by the panel layer after extract/save operations.
    /// </summary>
    public void ReportInfo(string message)
    {
        StatusText = message;
        _output.Write("Plugin System", $"[Assembly Explorer] {message}");
    }
}
