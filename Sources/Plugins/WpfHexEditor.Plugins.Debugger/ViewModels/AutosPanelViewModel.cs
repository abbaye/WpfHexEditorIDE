// ==========================================================
// Project: WpfHexEditor.Plugins.Debugger
// File: ViewModels/AutosPanelViewModel.cs
// Description:
//     VM for the Autos panel — shows variables relevant to the current execution line
//     (locals from scope 0, same as Locals but semantically scoped to "autos").
// Architecture: reuses VariableNode from LocalsPanelViewModel, no duplication.
// ==========================================================

using System.Collections.ObjectModel;
using WpfHexEditor.Core.ViewModels;
using WpfHexEditor.SDK.Contracts.Services;

namespace WpfHexEditor.Plugins.Debugger.ViewModels;

public sealed class AutosPanelViewModel : ViewModelBase
{
    private readonly IDebuggerService _debugger;

    public ObservableCollection<VariableNode> Variables { get; } = [];

    public AutosPanelViewModel(IDebuggerService debugger)
    {
        _debugger = debugger;
    }

    public void SetVariables(IReadOnlyList<DebugVariableInfo> vars, int scopeRef = 0)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            var prev = Variables.ToDictionary(n => n.Name, n => n.Value);
            Variables.Clear();
            foreach (var v in vars)
            {
                var changed = prev.TryGetValue(v.Name, out var old) && old != v.Value;
                Variables.Add(new VariableNode
                {
                    Name               = v.Name,
                    Value              = v.Value,
                    Type               = v.Type,
                    VariablesReference = v.VariablesReference,
                    ScopeReference     = scopeRef,
                    IsChanged          = changed,
                });
            }
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
                    VariablesReference = c.VariablesReference,
                    ScopeReference     = node.VariablesReference,
                });
        });
    }

    public async Task SetValueAsync(VariableNode node, string newValue)
    {
        var result = await _debugger.SetVariableAsync(node.ScopeReference, node.Name, newValue);
        if (result is not null)
            System.Windows.Application.Current?.Dispatcher.Invoke(() => node.Value = result);
    }
}
