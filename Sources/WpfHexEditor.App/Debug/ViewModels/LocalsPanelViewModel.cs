// ==========================================================
// Project: WpfHexEditor.App.Debug
// File: ViewModels/LocalsPanelViewModel.cs
// Description: VM for the Locals panel â€” variable tree.
// ==========================================================

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using WpfHexEditor.Core.Events;
using WpfHexEditor.Core.Events.IDEEvents;
using WpfHexEditor.Core.ViewModels;
using WpfHexEditor.SDK.Commands;
using WpfHexEditor.SDK.Contracts.Services;

namespace WpfHexEditor.App.Debug.ViewModels;

public sealed class VariableNode : ViewModelBase
{
    private bool   _isExpanded;
    private string _value = string.Empty;
    private bool   _isEditing;
    private bool   _isChanged;

    public string  Name               { get; init; } = string.Empty;
    public string? Type               { get; init; }
    public int     VariablesReference { get; init; }
    public int     ScopeReference     { get; init; }
    public bool    HasChildren        => VariablesReference > 0;

    public string Value
    {
        get => _value;
        set { _value = value; OnPropertyChanged(); }
    }

    /// <summary>True when the value TextBox is in edit mode.</summary>
    public bool IsEditing
    {
        get => _isEditing;
        set { _isEditing = value; OnPropertyChanged(); }
    }

    /// <summary>True when the value changed since the last pause (highlights in red like VS).</summary>
    public bool IsChanged
    {
        get => _isChanged;
        set { _isChanged = value; OnPropertyChanged(); }
    }

    public ObservableCollection<VariableNode> Children { get; } = [];

    public bool IsExpanded
    {
        get => _isExpanded;
        set { _isExpanded = value; OnPropertyChanged(); }
    }
}

public sealed class LocalsPanelViewModel : ViewModelBase
{
    private readonly IDebuggerService _debugger;
    private readonly IIDEEventBus?    _events;
    private int _scopeReference;

    public ObservableCollection<VariableNode> Variables { get; } = [];

    public ICommand CopyValueCommand    { get; }
    public ICommand CopyExpressionCommand { get; }
    public ICommand AddToWatchCommand   { get; }
    public ICommand EditValueCommand    { get; }

    public LocalsPanelViewModel(IDebuggerService debugger, IIDEEventBus? events = null)
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
        _scopeReference = scopeRef;
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
