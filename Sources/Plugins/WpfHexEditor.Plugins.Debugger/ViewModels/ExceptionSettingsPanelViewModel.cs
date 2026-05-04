// ==========================================================
// Project: WpfHexEditor.Plugins.Debugger
// File: ViewModels/ExceptionSettingsPanelViewModel.cs
// Description:
//     VM for the Exception Settings panel — shows a list of exception filters
//     with checkboxes (same as VS "Exception Settings" window).
// ==========================================================

using System.Collections.ObjectModel;
using System.Windows.Input;
using WpfHexEditor.Core.ViewModels;
using WpfHexEditor.SDK.Commands;
using WpfHexEditor.SDK.Contracts.Services;

namespace WpfHexEditor.Plugins.Debugger.ViewModels;

public sealed class ExceptionFilterItem : ViewModelBase
{
    private bool   _isEnabled;
    private string _condition = string.Empty;

    public string Filter    { get; init; } = string.Empty;
    public string Label     { get; init; } = string.Empty;

    public bool IsEnabled
    {
        get => _isEnabled;
        set { _isEnabled = value; OnPropertyChanged(); }
    }

    public string Condition
    {
        get => _condition;
        set { _condition = value; OnPropertyChanged(); }
    }
}

public sealed class ExceptionSettingsPanelViewModel : ViewModelBase
{
    private readonly IDebuggerService _debugger;

    public ObservableCollection<ExceptionFilterItem> Filters { get; } = [];

    public ICommand ApplyCommand { get; }

    public ExceptionSettingsPanelViewModel(IDebuggerService debugger)
    {
        _debugger    = debugger;
        ApplyCommand = new RelayCommand(_ => _ = ApplyAsync());
        LoadFilters();
    }

    private void LoadFilters()
    {
        Filters.Clear();
        foreach (var f in _debugger.ExceptionFilters)
            Filters.Add(new ExceptionFilterItem
            {
                Filter    = f.Filter,
                Label     = f.Label,
                IsEnabled = f.IsEnabled,
                Condition = f.Condition ?? string.Empty,
            });
    }

    public async Task ApplyAsync()
    {
        var updated = Filters.Select(f => new ExceptionFilterInfo(
            f.Filter, f.Label, f.IsEnabled,
            string.IsNullOrWhiteSpace(f.Condition) ? null : f.Condition)).ToList();
        await _debugger.SetExceptionFiltersAsync(updated);
    }

    public void Refresh() => LoadFilters();
}
