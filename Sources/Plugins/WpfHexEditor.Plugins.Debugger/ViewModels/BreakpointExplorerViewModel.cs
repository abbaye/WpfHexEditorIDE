// ==========================================================
// Project: WpfHexEditor.Plugins.Debugger
// File: ViewModels/BreakpointExplorerViewModel.cs
// Description:
//     Full-featured VM for the VS-style Breakpoint Explorer panel.
//     Supports flat/grouped views, search filter, hit counts, and
//     all CRUD operations on breakpoints.
// Architecture:
//     Subscribes to IDebuggerService.BreakpointsChanged for live updates.
//     Uses IIDEHostContext for file navigation.
// ==========================================================

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using WpfHexEditor.Core.Debugger.Models;
using WpfHexEditor.Plugins.Debugger.Dialogs;
using WpfHexEditor.Plugins.Debugger.Properties;
using WpfHexEditor.Plugins.Debugger.Services;
using WpfHexEditor.SDK.Commands;
using WpfHexEditor.SDK.Contracts;
using WpfHexEditor.SDK.Contracts.Services;
using WpfHexEditor.Core.ViewModels;
using WpfHexEditor.Editor.Core.Dialogs;

namespace WpfHexEditor.Plugins.Debugger.ViewModels;

public enum GroupByMode { None, File, Type, EnabledState, Project }

/// <summary>Position of the detail panel relative to the breakpoint list.</summary>
public enum DetailPanelLayout { Right, Bottom, Hidden }

public sealed class BreakpointExplorerViewModel : ViewModelBase
{
    private readonly IDebuggerService _debugger;
    private readonly IIDEHostContext? _context;

    private string     _searchFilter = string.Empty;
    private GroupByMode _groupBy = GroupByMode.File;
    private string     _summaryText = string.Empty;
    private BreakpointRowEx? _selectedBreakpoint;
    private DetailPanelLayout _detailLayout = DetailPanelLayout.Right;

    // ── Internal services (for code-behind popup wiring) ─────────────────────

    internal IDebuggerService DebuggerService => _debugger;

    // ── Collections ──────────────────────────────────────────────────────────

    public ObservableCollection<BreakpointRowEx>   FlatBreakpoints    { get; } = [];
    public ObservableCollection<BreakpointGroupNode> GroupedBreakpoints { get; } = [];

    // ── Properties ───────────────────────────────────────────────────────────

    public string SearchFilter
    {
        get => _searchFilter;
        set { if (_searchFilter == value) return; _searchFilter = value; OnPropertyChanged(); Refresh(); }
    }

    public GroupByMode GroupBy
    {
        get => _groupBy;
        set { if (_groupBy == value) return; _groupBy = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsGrouped)); Refresh(); }
    }

    public bool IsGrouped => _groupBy != GroupByMode.None;

    public string SummaryText
    {
        get => _summaryText;
        private set { if (_summaryText == value) return; _summaryText = value; OnPropertyChanged(); }
    }

    public BreakpointRowEx? SelectedBreakpoint
    {
        get => _selectedBreakpoint;
        set { if (_selectedBreakpoint == value) return; _selectedBreakpoint = value; OnPropertyChanged(); }
    }

    public DetailPanelLayout DetailLayout
    {
        get => _detailLayout;
        set
        {
            if (_detailLayout == value) return;
            _detailLayout = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsDetailRight));
            OnPropertyChanged(nameof(IsDetailBottom));
            OnPropertyChanged(nameof(IsDetailVisible));
        }
    }

    public bool IsDetailRight   => _detailLayout == DetailPanelLayout.Right;
    public bool IsDetailBottom  => _detailLayout == DetailPanelLayout.Bottom;
    public bool IsDetailVisible => _detailLayout != DetailPanelLayout.Hidden;

    // ── Commands ─────────────────────────────────────────────────────────────

    public ICommand EnableAllCommand     { get; }
    public ICommand DisableAllCommand    { get; }
    public ICommand DeleteAllCommand     { get; }
    public ICommand DeleteCommand        { get; }
    public ICommand ToggleEnabledCommand { get; }
    public ICommand GoToSourceCommand    { get; }
    public ICommand CopyLocationCommand  { get; }
    public ICommand ImportCommand        { get; }
    public ICommand ExportCommand        { get; }
    public ICommand EditConditionCommand { get; }

    // ── Constructor ──────────────────────────────────────────────────────────

    public BreakpointExplorerViewModel(IDebuggerService debugger, IIDEHostContext? context = null)
    {
        _debugger = debugger;
        _context  = context;
        _debugger.BreakpointsChanged += (_, _) => Application.Current?.Dispatcher.InvokeAsync(Refresh);

        EnableAllCommand     = new RelayCommand(_ => SetAllEnabled(true));
        DisableAllCommand    = new RelayCommand(_ => SetAllEnabled(false));
        DeleteAllCommand     = new RelayCommand(async _ => await _debugger.ClearAllBreakpointsAsync());
        DeleteCommand        = new RelayCommand(async p => { if (p is BreakpointRowEx r) await _debugger.DeleteBreakpointAsync(r.FilePath, r.Line); });
        ToggleEnabledCommand = new RelayCommand(async p => { if (p is BreakpointRowEx r) await _debugger.UpdateBreakpointAsync(r.FilePath, r.Line, r.Condition, !r.IsEnabled); });
        GoToSourceCommand    = new RelayCommand(p => NavigateToBreakpoint(p as BreakpointRowEx));
        CopyLocationCommand  = new RelayCommand(p => { if (p is BreakpointRowEx r) Clipboard.SetText(r.DisplayLocation); });
        ImportCommand        = new RelayCommand(_ => ImportFromVsXml());
        ExportCommand        = new RelayCommand(_ => ExportToVsXml(), _ => _debugger.Breakpoints.Count > 0);
        EditConditionCommand = new RelayCommand(p => EditCondition(p as BreakpointRowEx),
                                                p => p is BreakpointRowEx);

        Refresh();
    }

    // ── Refresh ──────────────────────────────────────────────────────────────

    private void Refresh()
    {
        // Preserve selection identity before rebuilding row instances.
        var prevFile = _selectedBreakpoint?.FilePath;
        var prevLine = _selectedBreakpoint?.Line;

        var allBps = _debugger.Breakpoints;
        var solution = _context?.SolutionManager?.CurrentSolution;
        var rows = allBps.Select(bp => new BreakpointRowEx
        {
            FilePath       = bp.FilePath,
            FileName       = Path.GetFileName(bp.FilePath),
            Line           = bp.Line,
            Condition      = bp.Condition,
            IsEnabled      = bp.IsEnabled,
            IsVerified     = bp.IsVerified,
            HitCount       = bp.HitCount,
            ProjectName    = solution?.Projects
                                 .FirstOrDefault(p => p.FindItemByPath(bp.FilePath) is not null)
                                 ?.Name
                             ?? Path.GetFileName(Path.GetDirectoryName(bp.FilePath))
                             ?? "Unknown",
            // Extended settings (round-trip from BreakpointLocation via DebugBreakpointInfo)
            ConditionKind     = bp.ConditionKind,
            ConditionMode     = bp.ConditionMode,
            HitCountOp        = bp.HitCountOp,
            HitCountTarget    = bp.HitCountTarget,
            FilterExpr        = bp.FilterExpr,
            HasAction         = bp.HasAction,
            LogMessage        = bp.LogMessage,
            ContinueExecution = bp.ContinueExecution,
            DisableOnceHit    = bp.DisableOnceHit,
            DependsOnBpKey    = bp.DependsOnBpKey,
        }).ToList();

        // Apply search filter.
        if (!string.IsNullOrWhiteSpace(_searchFilter))
        {
            var filter = _searchFilter.Trim();
            rows = rows.Where(r =>
                r.FileName.Contains(filter, StringComparison.OrdinalIgnoreCase)
                || r.Line.ToString().Contains(filter)
                || (r.Condition?.Contains(filter, StringComparison.OrdinalIgnoreCase) == true)
            ).ToList();
        }

        // Flat view.
        FlatBreakpoints.Clear();
        foreach (var r in rows) FlatBreakpoints.Add(r);

        // Grouped view.
        GroupedBreakpoints.Clear();
        if (_groupBy != GroupByMode.None)
        {
            var groups = _groupBy switch
            {
                GroupByMode.File         => rows.GroupBy(r => r.FileName),
                GroupByMode.Type         => rows.GroupBy(r => r.TypeLabel),
                GroupByMode.EnabledState => rows.GroupBy(r => r.IsEnabled ? "Enabled" : "Disabled"),
                GroupByMode.Project      => rows.GroupBy(r => r.ProjectName),
                _                        => rows.GroupBy(r => r.FileName),
            };

            foreach (var g in groups.OrderBy(x => x.Key))
            {
                var node = new BreakpointGroupNode
                {
                    GroupKey  = g.Key,
                    GroupIcon = _groupBy == GroupByMode.File ? "\uE8A5" : "\uE71D", // File / Filter glyph
                };
                foreach (var r in g.OrderBy(x => x.Line)) node.Children.Add(r);
                GroupedBreakpoints.Add(node);
            }
        }

        // Restore selection to the same breakpoint after rows are rebuilt.
        if (prevFile is not null && prevLine is not null)
            SelectedBreakpoint = FlatBreakpoints.FirstOrDefault(r =>
                string.Equals(r.FilePath, prevFile, StringComparison.OrdinalIgnoreCase) &&
                r.Line == prevLine);

        // Summary.
        int total    = allBps.Count;
        int enabled  = allBps.Count(b => b.IsEnabled);
        int cond     = allBps.Count(b => !string.IsNullOrEmpty(b.Condition));
        int disabled = total - enabled;
        SummaryText = $"{total} breakpoint{(total != 1 ? "s" : "")} ({enabled} enabled, {cond} conditional, {disabled} disabled)";
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private void SetAllEnabled(bool enabled)
    {
        foreach (var bp in _debugger.Breakpoints)
            _ = _debugger.UpdateBreakpointAsync(bp.FilePath, bp.Line, bp.Condition, enabled);
    }

    private void NavigateToBreakpoint(BreakpointRowEx? row)
    {
        if (row is null || _context is null) return;
        _context.DocumentHost.ActivateAndNavigateTo(row.FilePath, row.Line, 0);
    }


    // ── Edit condition dialog ─────────────────────────────────────────────────

    internal void EditCondition(BreakpointRowEx? row)
    {
        if (row is null) return;

        var owner = Application.Current?.MainWindow;
        if (owner is null) return;

        // Build BreakpointLocation from the row for dialog population.
        var loc = new BreakpointLocation
        {
            FilePath          = row.FilePath,
            Line              = row.Line,
            Condition         = row.Condition ?? string.Empty,
            IsEnabled         = row.IsEnabled,
            ConditionKind     = row.ConditionKind,
            ConditionMode     = row.ConditionMode,
            HitCountOp        = row.HitCountOp,
            HitCountTarget    = row.HitCountTarget,
            FilterExpr        = row.FilterExpr,
            HasAction         = row.HasAction,
            LogMessage        = row.LogMessage,
            ContinueExecution = row.ContinueExecution,
            DisableOnceHit    = row.DisableOnceHit,
            DependsOnBpKey    = row.DependsOnBpKey,
        };

        // All other breakpoints for the depends-on dropdown.
        var allLocs = _debugger.Breakpoints.Select(b => new BreakpointLocation
        {
            FilePath  = b.FilePath,
            Line      = b.Line,
            Condition = b.Condition ?? string.Empty,
            IsEnabled = b.IsEnabled,
        }).ToList();

        var result = BreakpointConditionDialog.Show(owner, loc, allLocs);
        if (result is null) return;

        _ = _debugger.UpdateBreakpointSettingsAsync(row.FilePath, row.Line, result);
    }

    // ── VS XML Import / Export ──────────────────────────────────────────

    private async void ImportFromVsXml()
    {
        var dlg = new OpenFileDialog
        {
            Title  = DebuggerResources.Debugger_ImportBreakpoints,
            Filter = "VS Breakpoint XML (*.xml)|*.xml|All Files (*.*)|*.*",
        };
        if (dlg.ShowDialog() != true) return;

        var imported = VsBreakpointXmlService.ImportFromXml(dlg.FileName);
        if (imported.Count == 0)
        {
            IdeMessageBox.Show(DebuggerResources.Debugger_NoValidBreakpoints, "Import", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var existing = _debugger.Breakpoints;
        int added = 0, skipped = 0;

        foreach (var bp in imported)
        {
            bool duplicate = existing.Any(e =>
                string.Equals(e.FilePath, bp.FilePath, StringComparison.OrdinalIgnoreCase)
                && e.Line == bp.Line);

            if (duplicate) { skipped++; continue; }

            await _debugger.ToggleBreakpointAsync(bp.FilePath, bp.Line, bp.Condition);
            if (!bp.IsEnabled)
                await _debugger.UpdateBreakpointAsync(bp.FilePath, bp.Line, bp.Condition, false);
            added++;
        }

        IdeMessageBox.Show(
            $"Imported {added} breakpoint{(added != 1 ? "s" : "")}" +
            (skipped > 0 ? $" ({skipped} duplicate{(skipped != 1 ? "s" : "")} skipped)" : "") + ".",
            "Import Complete", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ExportToVsXml()
    {
        var dlg = new SaveFileDialog
        {
            Title    = DebuggerResources.Debugger_ExportBreakpoints,
            Filter   = "VS Breakpoint XML (*.xml)|*.xml",
            FileName = "breakpoints.xml",
        };
        if (dlg.ShowDialog() != true) return;

        VsBreakpointXmlService.ExportToXml(dlg.FileName, _debugger.Breakpoints);
        IdeMessageBox.Show(
            $"Exported {_debugger.Breakpoints.Count} breakpoint{(_debugger.Breakpoints.Count != 1 ? "s" : "")}.",
            "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    // ── INPC ─────────────────────────────────────────────────────────────────

}
