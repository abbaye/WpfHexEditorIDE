// ==========================================================
// Project: WpfHexEditor.App.Debug
// File: ViewModels/RegistersPanelViewModel.cs
// Description:
//     VM for the Registers window panel.
//     Queries the "Registers" scope from the active frame's scopes list, then loads variables.
//     Highlights registers whose value changed since the last pause.
// Architecture: reads from IDebuggerService.GetVariablesAsync(scopeRef).
// ==========================================================

using System.Collections.ObjectModel;
using System.Windows.Input;
using WpfHexEditor.Core.ViewModels;
using WpfHexEditor.SDK.Commands;
using WpfHexEditor.SDK.Contracts.Services;

namespace WpfHexEditor.App.Debug.ViewModels;

public sealed class RegisterItem : ViewModelBase
{
    private string _value = string.Empty;
    private bool   _isChanged;

    public string Name     { get; init; } = string.Empty;
    public string Type     { get; init; } = string.Empty;

    public string Value
    {
        get => _value;
        set { _value = value; OnPropertyChanged(); }
    }

    public bool IsChanged
    {
        get => _isChanged;
        set { _isChanged = value; OnPropertyChanged(); }
    }
}

public sealed class RegistersPanelViewModel : ViewModelBase
{
    private readonly IDebuggerService _debugger;
    private bool _hexDisplay = true;

    public ObservableCollection<RegisterItem> Registers { get; } = [];

    public bool HexDisplay
    {
        get => _hexDisplay;
        set { _hexDisplay = value; OnPropertyChanged(); }
    }

    public ICommand RefreshCommand     { get; }
    public ICommand CopyValueCommand   { get; }
    public ICommand CopyNameCommand    { get; }
    public ICommand ToggleHexCommand   { get; }

    public RegistersPanelViewModel(IDebuggerService debugger)
    {
        _debugger = debugger;

        RefreshCommand   = new RelayCommand(async _ => await RefreshAsync());
        CopyValueCommand = new RelayCommand(p => { if (p is RegisterItem r && !string.IsNullOrEmpty(r.Value)) System.Windows.Clipboard.SetText(r.Value); });
        CopyNameCommand  = new RelayCommand(p => { if (p is RegisterItem r && !string.IsNullOrEmpty(r.Name))  System.Windows.Clipboard.SetText(r.Name); });
        ToggleHexCommand = new RelayCommand(_ => HexDisplay = !HexDisplay);
    }

    public async Task RefreshAsync()
    {
        if (!_debugger.IsPaused) return;
        try
        {
            var regVars = await _debugger.GetRegistersAsync();
            await UpdateRegisters(regVars);
        }
        catch { /* session may have ended */ }
    }

    public void Clear()
        => System.Windows.Application.Current?.Dispatcher.Invoke(Registers.Clear);

    private Task UpdateRegisters(IReadOnlyList<DebugVariableInfo> vars)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            var prev = Registers.ToDictionary(r => r.Name, r => r.Value);
            Registers.Clear();
            foreach (var v in vars)
            {
                var changed = prev.TryGetValue(v.Name, out var old) && old != v.Value;
                Registers.Add(new RegisterItem
                {
                    Name      = v.Name,
                    Value     = v.Value,
                    Type      = v.Type ?? string.Empty,
                    IsChanged = changed,
                });
            }
        });
        return Task.CompletedTask;
    }
}
