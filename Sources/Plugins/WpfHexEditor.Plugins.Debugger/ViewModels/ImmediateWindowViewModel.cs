// ==========================================================
// Project: WpfHexEditor.Plugins.Debugger
// File: ViewModels/ImmediateWindowViewModel.cs
// Description:
//     VM for the Immediate / Command window — evaluates expressions via DAP evaluate.
//     Maintains a scrollable transcript and command history (Up/Down arrows).
// ==========================================================

using System.Collections.ObjectModel;
using System.Windows.Input;
using WpfHexEditor.Core.ViewModels;
using WpfHexEditor.SDK.Commands;
using WpfHexEditor.SDK.Contracts.Services;

namespace WpfHexEditor.Plugins.Debugger.ViewModels;

public sealed class ImmediateWindowViewModel : ViewModelBase
{
    private readonly IDebuggerService _debugger;
    private string _input = string.Empty;
    private readonly List<string> _history = [];
    private int _historyIndex = -1;

    public ObservableCollection<string> Lines { get; } = [];

    public string Input
    {
        get => _input;
        set { _input = value; OnPropertyChanged(); }
    }

    public ICommand ExecuteCommand { get; }
    public ICommand ClearCommand   { get; }

    public ImmediateWindowViewModel(IDebuggerService debugger)
    {
        _debugger      = debugger;
        ExecuteCommand = new RelayCommand(_ => _ = ExecuteAsync());
        ClearCommand   = new RelayCommand(_ => Clear());
    }

    public async Task ExecuteAsync()
    {
        var expr = Input.Trim();
        if (string.IsNullOrEmpty(expr)) return;

        _history.Insert(0, expr);
        _historyIndex = -1;
        Input = string.Empty;

        Lines.Add($"> {expr}");

        if (!_debugger.IsPaused)
        {
            Lines.Add("<not paused>");
            return;
        }

        try
        {
            var result = await _debugger.EvaluateAsync(expr);
            Lines.Add(result);
        }
        catch (Exception ex)
        {
            Lines.Add($"Error: {ex.Message}");
        }
    }

    public void HistoryUp()
    {
        if (_history.Count == 0) return;
        _historyIndex = Math.Min(_historyIndex + 1, _history.Count - 1);
        Input = _history[_historyIndex];
    }

    public void HistoryDown()
    {
        if (_historyIndex <= 0) { _historyIndex = -1; Input = string.Empty; return; }
        _historyIndex--;
        Input = _history[_historyIndex];
    }

    public void Clear()
    {
        Lines.Clear();
    }
}
