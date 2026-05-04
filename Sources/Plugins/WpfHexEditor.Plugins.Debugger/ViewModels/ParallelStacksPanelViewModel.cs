// ==========================================================
// Project: WpfHexEditor.Plugins.Debugger
// File: ViewModels/ParallelStacksPanelViewModel.cs
// Description: VM for the Parallel Stacks panel.
//              Fetches all threads + their call stacks and groups
//              shared frames so the canvas can draw VS-style boxes.
// Architecture:
//     ThreadStackGroup  — one box per thread (or merged group sharing frames).
//     StackFrameRow     — one row inside a box.
//     Layout computed client-side; Canvas binding renders in code-behind.
// ==========================================================

using System.Collections.ObjectModel;
using System.Windows.Input;
using WpfHexEditor.Core.ViewModels;
using WpfHexEditor.SDK.Commands;
using WpfHexEditor.SDK.Contracts.Services;

namespace WpfHexEditor.Plugins.Debugger.ViewModels;

public sealed class ParallelStacksPanelViewModel : ViewModelBase
{
    private readonly IDebuggerService _debugger;
    private bool                      _isBusy;

    public ObservableCollection<ThreadStackGroup> Groups { get; } = [];

    public bool IsBusy
    {
        get => _isBusy;
        private set { _isBusy = value; OnPropertyChanged(nameof(IsBusy)); }
    }

    public ICommand RefreshCommand { get; }

    public ParallelStacksPanelViewModel(IDebuggerService debugger)
    {
        _debugger      = debugger;
        RefreshCommand = new RelayCommand(_ => _ = RefreshAsync());
    }

    public async Task RefreshAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var threads = await _debugger.GetThreadsAsync();
            var groups  = new List<ThreadStackGroup>();

            foreach (var thread in threads)
            {
                var frames = await _debugger.GetCallStackForThreadAsync(thread.Id);
                groups.Add(new ThreadStackGroup(
                    thread.Id,
                    thread.Name,
                    frames.Select(f => new StackFrameRow(f.Id, f.Name, f.FilePath, f.Line)).ToList()));
            }

            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                Groups.Clear();
                foreach (var g in groups) Groups.Add(g);
            });
        }
        finally { IsBusy = false; }
    }

    public void Clear() =>
        System.Windows.Application.Current?.Dispatcher.Invoke(Groups.Clear);
}

public sealed class ThreadStackGroup(int threadId, string threadName, IReadOnlyList<StackFrameRow> frames)
{
    public int                        ThreadId   { get; } = threadId;
    public string                     ThreadName { get; } = threadName;
    public IReadOnlyList<StackFrameRow> Frames   { get; } = frames;

    // Layout position set by the canvas renderer
    public double X { get; set; }
    public double Y { get; set; }
}

public sealed class StackFrameRow(int id, string name, string? filePath, int line)
{
    public int     Id       { get; } = id;
    public string  Name     { get; } = name;
    public string? FilePath { get; } = filePath;
    public int     Line     { get; } = line;

    public string DisplayText => FilePath is not null
        ? $"{name}  ({System.IO.Path.GetFileName(FilePath)}:{line})"
        : name;
}
