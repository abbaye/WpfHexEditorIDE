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
using WpfHexEditor.SDK.Commands;
using WpfHexEditor.SDK.Contracts;
using WpfHexEditor.SDK.Contracts.Services;

namespace WpfHexEditor.Plugins.Debugger.ViewModels;

public enum GroupByMode { None, File, Type, EnabledState }

public sealed class BreakpointExplorerViewModel : INotifyPropertyChanged
{
    private readonly IDebuggerService _debugger;
    private readonly IIDEHostContext? _context;

    private string     _searchFilter = string.Empty;
    private GroupByMode _groupBy = GroupByMode.None;
    private string     _summaryText = string.Empty;
    private BreakpointRowEx? _selectedBreakpoint;

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

    // ── Commands ─────────────────────────────────────────────────────────────

    public ICommand EnableAllCommand   { get; }
    public ICommand DisableAllCommand  { get; }
    public ICommand DeleteAllCommand   { get; }
    public ICommand DeleteCommand      { get; }
    public ICommand ToggleEnabledCommand { get; }
    public ICommand GoToSourceCommand  { get; }
    public ICommand CopyLocationCommand { get; }

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

        Refresh();
    }

    // ── Refresh ──────────────────────────────────────────────────────────────

    private void Refresh()
    {
        var allBps = _debugger.Breakpoints;
        var rows = allBps.Select(bp => new BreakpointRowEx
        {
            FilePath   = bp.FilePath,
            FileName   = Path.GetFileName(bp.FilePath),
            Line       = bp.Line,
            Condition  = bp.Condition,
            IsEnabled  = bp.IsEnabled,
            IsVerified = bp.IsVerified,
            HitCount   = bp.HitCount,
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

    // ── INPC ─────────────────────────────────────────────────────────────────

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? p = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
}
