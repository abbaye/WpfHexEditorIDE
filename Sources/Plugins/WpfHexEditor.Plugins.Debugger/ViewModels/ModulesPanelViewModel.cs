// ==========================================================
// Project: WpfHexEditor.Plugins.Debugger
// File: ViewModels/ModulesPanelViewModel.cs
// Description:
//     VM for the Modules panel — shows loaded DLLs and EXEs from the active debug session.
// ==========================================================

using System.Collections.ObjectModel;
using WpfHexEditor.Core.ViewModels;
using WpfHexEditor.SDK.Contracts.Services;

namespace WpfHexEditor.Plugins.Debugger.ViewModels;

public sealed class ModuleItem
{
    public string  Name         { get; init; } = string.Empty;
    public string  Path         { get; init; } = string.Empty;
    public string  Version      { get; init; } = string.Empty;
    public string  SymbolStatus { get; init; } = string.Empty;
    public bool    IsOptimized  { get; init; }
    public bool    IsUserCode   { get; init; }
}

public sealed class ModulesPanelViewModel : ViewModelBase
{
    private readonly IDebuggerService _debugger;
    private string _filterText = string.Empty;

    public ObservableCollection<ModuleItem> Modules { get; } = [];

    public string FilterText
    {
        get => _filterText;
        set { _filterText = value; OnPropertyChanged(); ApplyFilter(); }
    }

    private IReadOnlyList<ModuleItem> _allModules = [];

    public ModulesPanelViewModel(IDebuggerService debugger)
    {
        _debugger = debugger;
    }

    public async Task RefreshAsync()
    {
        var items = await _debugger.GetModulesAsync();
        _allModules = items.Select(m => new ModuleItem
        {
            Name         = m.Name,
            Path         = m.Path         ?? string.Empty,
            Version      = m.Version      ?? string.Empty,
            SymbolStatus = m.SymbolStatus ?? string.Empty,
            IsOptimized  = m.IsOptimized,
            IsUserCode   = m.IsUserCode,
        }).ToList();
        ApplyFilter();
    }

    public void Clear()
    {
        _allModules = [];
        System.Windows.Application.Current?.Dispatcher.Invoke(Modules.Clear);
    }

    private void ApplyFilter()
    {
        var filter = _filterText.Trim();
        var filtered = string.IsNullOrEmpty(filter)
            ? _allModules
            : _allModules.Where(m =>
                m.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                m.Path.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();

        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            Modules.Clear();
            foreach (var m in filtered) Modules.Add(m);
        });
    }
}
