// ==========================================================
// Project: WpfHexEditor.Plugins.Debugger
// File: ViewModels/LocalsPanelViewModel.cs
// Description: VM for the Locals panel — variable tree.
// ==========================================================

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using WpfHexEditor.SDK.Contracts.Services;

namespace WpfHexEditor.Plugins.Debugger.ViewModels;

public sealed class VariableNode : INotifyPropertyChanged
{
    private bool _isExpanded;

    public string  Name               { get; init; } = string.Empty;
    public string  Value              { get; init; } = string.Empty;
    public string? Type               { get; init; }
    public int     VariablesReference { get; init; }
    public bool    HasChildren        => VariablesReference > 0;

    public ObservableCollection<VariableNode> Children { get; } = [];

    public bool IsExpanded
    {
        get => _isExpanded;
        set { _isExpanded = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? p = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
}

public sealed class LocalsPanelViewModel : INotifyPropertyChanged
{
    private readonly IDebuggerService _debugger;

    public ObservableCollection<VariableNode> Variables { get; } = [];

    public LocalsPanelViewModel(IDebuggerService debugger)
    {
        _debugger = debugger;
    }

    public void SetVariables(IReadOnlyList<DebugVariableInfo> vars)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            Variables.Clear();
            foreach (var v in vars)
                Variables.Add(new VariableNode
                {
                    Name               = v.Name,
                    Value              = v.Value,
                    Type               = v.Type,
                    VariablesReference = v.VariablesReference,
                });
        });
    }

    public async Task ExpandAsync(VariableNode node)
    {
        if (!node.HasChildren || node.Children.Count > 0) return;
        var children = await _debugger.GetVariablesAsync(node.VariablesReference);
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            foreach (var c in children)
                node.Children.Add(new VariableNode
                {
                    Name = c.Name, Value = c.Value, Type = c.Type,
                    VariablesReference = c.VariablesReference
                });
        });
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
