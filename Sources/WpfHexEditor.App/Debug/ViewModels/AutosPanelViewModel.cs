// ==========================================================
// Project: WpfHexEditor.App.Debug
// File: ViewModels/AutosPanelViewModel.cs
// Description:
//     VM for the Autos panel — shows variables relevant to the current execution line
//     (locals from scope 0, same as Locals but semantically scoped to "autos").
// Architecture: reuses VariableNode from LocalsPanelViewModel, no duplication.
// ==========================================================

using System.Collections.ObjectModel;
using System.Windows.Input;
using WpfHexEditor.Core.Events;
using WpfHexEditor.Core.Events.IDEEvents;
using WpfHexEditor.Core.ViewModels;
using WpfHexEditor.SDK.Commands;
using WpfHexEditor.SDK.Contracts.Services;

namespace WpfHexEditor.App.Debug.ViewModels;

public sealed class AutosPanelViewModel : ViewModelBase
{
    private readonly IDebuggerService _debugger;
    private readonly IIDEEventBus?    _events;

    public ObservableCollection<VariableNode> Variables { get; } = [];

    public ICommand CopyValueCommand      { get; }
    public ICommand CopyExpressionCommand { get; }
    public ICommand AddToWatchCommand     { get; }
    public ICommand EditValueCommand      { get; }

    public AutosPanelViewModel(IDebuggerService debugger, IIDEEventBus? events = null)
    {
        _debugger = debugger;
        _events   = events;

        CopyValueCommand      = new RelayCommand(p => { if (p is VariableNode n && !string.IsNullOrEmpty(n.Value)) System.Windows.Clipboard.SetText(n.Value); });
        CopyExpressionCommand = new RelayCommand(p => { if (p is VariableNode n && !string.IsNullOrEmpty(n.Name))  System.Windows.Clipboard.SetText(n.Name); });
        EditValueCommand      = new RelayCommand(p => { if (p is VariableNode n) n.IsEditing = true; });
        AddToWatchCommand     = new RelayCommand(p =>
        {
            if (p is VariableNode n && _events is not null)
                _events.Publish(new AddWatchRequestedEvent { Expression = n.Name });
        });
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
