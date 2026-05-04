// ==========================================================
// Project: WpfHexEditor.Plugins.Debugger
// File: ViewModels/ThreadsPanelViewModel.cs
// Description: VM for the Threads panel – list all active threads,
//              switch active thread and show its call stack.
// ==========================================================

using System.Collections.ObjectModel;
using System.Windows.Input;
using WpfHexEditor.Core.ViewModels;
using WpfHexEditor.SDK.Commands;
using WpfHexEditor.SDK.Contracts.Services;

namespace WpfHexEditor.Plugins.Debugger.ViewModels;

public sealed class ThreadsPanelViewModel : ViewModelBase
{
    private readonly IDebuggerService _debugger;
    private ThreadItem?               _selectedThread;

    public ObservableCollection<ThreadItem> Threads { get; } = [];

    public ThreadItem? SelectedThread
    {
        get => _selectedThread;
        set
        {
            _selectedThread = value;
            OnPropertyChanged(nameof(SelectedThread));
        }
    }

    public ICommand RefreshCommand { get; }
    public ICommand FreezeCommand  { get; }
    public ICommand ThawCommand    { get; }

    public ThreadsPanelViewModel(IDebuggerService debugger)
    {
        _debugger      = debugger;
        RefreshCommand = new RelayCommand(_ => _ = RefreshAsync());
        FreezeCommand  = new RelayCommand(p => { if (p is ThreadItem t) _ = FreezeAsync(t); });
        ThawCommand    = new RelayCommand(p => { if (p is ThreadItem t) _ = ThawAsync(t); });
    }

    public async Task RefreshAsync()
    {
        var threads = await _debugger.GetThreadsAsync();
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            Threads.Clear();
            foreach (var t in threads)
                Threads.Add(new ThreadItem(t.Id, t.Name, _debugger.IsThreadFrozen(t.Id)));
        });
    }

    public void Clear() =>
        System.Windows.Application.Current?.Dispatcher.Invoke(Threads.Clear);

    private async Task FreezeAsync(ThreadItem item)
    {
        await _debugger.FreezeThreadAsync(item.Id);
        item.IsFrozen = true;
    }

    private async Task ThawAsync(ThreadItem item)
    {
        await _debugger.ThawThreadAsync(item.Id);
        item.IsFrozen = false;
    }
}

public sealed class ThreadItem(int id, string name, bool isFrozen = false) : WpfHexEditor.Core.ViewModels.ViewModelBase
{
    private bool _isFrozen = isFrozen;

    public int    Id   { get; } = id;
    public string Name { get; } = name;

    public bool IsFrozen
    {
        get => _isFrozen;
        set { _isFrozen = value; OnPropertyChanged(); }
    }
}
