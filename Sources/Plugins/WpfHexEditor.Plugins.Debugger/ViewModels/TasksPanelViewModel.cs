// ==========================================================
// Project: WpfHexEditor.Plugins.Debugger
// File: ViewModels/TasksPanelViewModel.cs
// Description:
//     VM for the Tasks (TPL) panel — lists active Task / Task<T> objects from the current locals scope.
//     Filters debug variables whose type name starts with "Task" (covers Task, Task<T>, ValueTask<T>).
// Architecture: best-effort panel; adapters that expose task info populate it, others show empty.
// ==========================================================

using System.Collections.ObjectModel;
using WpfHexEditor.Core.ViewModels;
using WpfHexEditor.SDK.Contracts.Services;

namespace WpfHexEditor.Plugins.Debugger.ViewModels;

public sealed class TaskItem
{
    public string Id     { get; init; } = string.Empty;
    public string Name   { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string Type   { get; init; } = string.Empty;
}

public sealed class TasksPanelViewModel : ViewModelBase
{
    private readonly IDebuggerService _debugger;

    public ObservableCollection<TaskItem> Tasks { get; } = [];

    public TasksPanelViewModel(IDebuggerService debugger)
    {
        _debugger = debugger;
    }

    public async Task RefreshAsync()
    {
        if (!_debugger.IsPaused) return;

        // Get locals from all threads' active frame and filter task-like variables
        var items = new List<TaskItem>();
        try
        {
            var threads = await _debugger.GetThreadsAsync();
            foreach (var thread in threads)
            {
                var frames = await _debugger.GetCallStackForThreadAsync(thread.Id);
                if (frames.Count == 0) continue;

                // scope 0 = locals of the top frame
                var vars = await _debugger.GetVariablesAsync(0);
                foreach (var v in vars)
                {
                    if (v.Type is null) continue;
                    if (!v.Type.StartsWith("Task", StringComparison.OrdinalIgnoreCase) &&
                        !v.Type.StartsWith("System.Threading.Tasks.Task", StringComparison.OrdinalIgnoreCase) &&
                        !v.Type.StartsWith("ValueTask", StringComparison.OrdinalIgnoreCase))
                        continue;

                    // Extract status from value string (e.g. "Running", "WaitingForActivation")
                    var status = ExtractStatus(v.Value);
                    items.Add(new TaskItem
                    {
                        Id     = $"{thread.Id}:{v.Name}",
                        Name   = $"[Thread {thread.Id}] {v.Name}",
                        Status = status,
                        Type   = v.Type,
                    });
                }
            }
        }
        catch { /* session may have ended */ }

        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            Tasks.Clear();
            foreach (var t in items) Tasks.Add(t);
        });
    }

    public void Clear()
        => System.Windows.Application.Current?.Dispatcher.Invoke(Tasks.Clear);

    private static string ExtractStatus(string value)
    {
        // Value may look like: "{Status = RanToCompletion, ...}" or just the value
        var idx = value.IndexOf("Status", StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return value.Length > 40 ? value[..40] : value;
        var eq = value.IndexOf('=', idx);
        if (eq < 0) return string.Empty;
        var comma = value.IndexOf(',', eq);
        var end   = comma < 0 ? value.IndexOf('}', eq) : comma;
        if (end < 0) end = value.Length;
        return value[(eq + 1)..end].Trim();
    }
}
