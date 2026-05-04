// ==========================================================
// Project: WpfHexEditor.Plugins.Debugger
// File: ViewModels/ParallelWatchViewModel.cs
// Description:
//     VM for the Parallel Watch panel — evaluate an expression across all active threads.
//     Rows = expressions, columns = threads (dynamic GridView via code-behind).
// Architecture:
//     Uses IDebuggerService.GetThreadsAsync + EvaluateAsync per thread.
//     ThreadColumn list drives dynamic GridView column generation in code-behind.
// ==========================================================

using System.Collections.ObjectModel;
using System.Windows.Input;
using WpfHexEditor.Core.ViewModels;
using WpfHexEditor.SDK.Commands;
using WpfHexEditor.SDK.Contracts.Services;

namespace WpfHexEditor.Plugins.Debugger.ViewModels;

/// <summary>One expression row; values are keyed by thread ID.</summary>
public sealed class ParallelWatchRow : ViewModelBase
{
    private string _expression = string.Empty;

    public string Expression
    {
        get => _expression;
        set { _expression = value; OnPropertyChanged(); }
    }

    // Thread ID → evaluated value string
    public Dictionary<int, string> Values { get; } = [];

    public string GetValue(int threadId)
        => Values.TryGetValue(threadId, out var v) ? v : "—";
}

/// <summary>Column descriptor for dynamic GridView generation.</summary>
public sealed record ThreadColumn(int Id, string Name);

public sealed class ParallelWatchViewModel : ViewModelBase
{
    private readonly IDebuggerService _debugger;

    public ObservableCollection<ParallelWatchRow>  Rows    { get; } = [new() { Expression = "" }];
    public ObservableCollection<ThreadColumn>       Columns { get; } = [];

    public ICommand AddRowCommand    { get; }
    public ICommand RemoveRowCommand { get; }
    public ICommand RefreshCommand   { get; }

    public ParallelWatchViewModel(IDebuggerService debugger)
    {
        _debugger       = debugger;
        AddRowCommand    = new RelayCommand(_ => Rows.Add(new ParallelWatchRow { Expression = "" }));
        RemoveRowCommand = new RelayCommand(p => { if (p is ParallelWatchRow r) Rows.Remove(r); });
        RefreshCommand   = new RelayCommand(_ => _ = RefreshAsync());
    }

    public async Task RefreshAsync()
    {
        if (!_debugger.IsPaused) return;

        var threads = await _debugger.GetThreadsAsync();

        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            Columns.Clear();
            foreach (var t in threads) Columns.Add(new ThreadColumn(t.Id, t.Name));
        });

        foreach (var row in Rows.ToList())
        {
            if (string.IsNullOrWhiteSpace(row.Expression)) continue;
            row.Values.Clear();
            foreach (var t in threads)
            {
                try
                {
                    var val = await _debugger.EvaluateAsync(row.Expression);
                    row.Values[t.Id] = val;
                }
                catch
                {
                    row.Values[t.Id] = "<error>";
                }
            }
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                row.OnPropertyChanged(nameof(row.Values)));
        }
    }

    public void Clear()
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            Columns.Clear();
            foreach (var r in Rows) r.Values.Clear();
        });
    }
}
