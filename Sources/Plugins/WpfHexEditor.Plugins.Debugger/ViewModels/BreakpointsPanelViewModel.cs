// ==========================================================
// Project: WpfHexEditor.Plugins.Debugger
// File: ViewModels/BreakpointsPanelViewModel.cs
// Description: VM for the Breakpoints panel — list + enable/disable/delete.
// ==========================================================

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using WpfHexEditor.SDK.Commands;
using WpfHexEditor.SDK.Contracts.Services;

namespace WpfHexEditor.Plugins.Debugger.ViewModels;

public sealed class BreakpointRow : INotifyPropertyChanged
{
    private bool _isEnabled;

    public string  FilePath  { get; init; } = string.Empty;
    public int     Line      { get; init; }
    public string? Condition { get; init; }
    public bool    IsVerified { get; init; }

    public bool IsEnabled
    {
        get => _isEnabled;
        set { _isEnabled = value; OnPropertyChanged(); }
    }

    public string DisplayName => $"{System.IO.Path.GetFileName(FilePath)}:{Line}";

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? p = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
}

public sealed class BreakpointsPanelViewModel : INotifyPropertyChanged
{
    private readonly IDebuggerService _debugger;

    public ObservableCollection<BreakpointRow> Rows { get; } = [];

    public ICommand DeleteCommand      { get; }
    public ICommand ClearAllCommand    { get; }
    public ICommand EnableAllCommand   { get; }
    public ICommand DisableAllCommand  { get; }

    public BreakpointsPanelViewModel(IDebuggerService debugger)
    {
        _debugger = debugger;
        _debugger.BreakpointsChanged += (_, _) => Refresh();

        DeleteCommand     = new RelayCommand(async p => await DeleteAsync(p as BreakpointRow));
        ClearAllCommand   = new RelayCommand(async _ => await _debugger.ClearAllBreakpointsAsync());
        EnableAllCommand  = new RelayCommand(_ => SetAllEnabled(true));
        DisableAllCommand = new RelayCommand(_ => SetAllEnabled(false));

        Refresh();
    }

    private void Refresh()
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            Rows.Clear();
            foreach (var bp in _debugger.Breakpoints)
                Rows.Add(new BreakpointRow
                {
                    FilePath  = bp.FilePath,
                    Line      = bp.Line,
                    Condition = bp.Condition,
                    IsEnabled = bp.IsEnabled,
                    IsVerified = bp.IsVerified,
                });
        });
    }

    private async Task DeleteAsync(BreakpointRow? row)
    {
        if (row is null) return;
        await _debugger.ToggleBreakpointAsync(row.FilePath, row.Line);
    }

    private void SetAllEnabled(bool enabled)
    {
        foreach (var r in Rows) r.IsEnabled = enabled;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
