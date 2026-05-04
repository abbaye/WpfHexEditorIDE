// ==========================================================
// Project: WpfHexEditor.Plugins.Debugger
// File: ViewModels/CallStackPanelViewModel.cs
// Description: VM for the Call Stack panel â€” frame list + navigate to source.
// ==========================================================

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using WpfHexEditor.SDK.Commands;
using WpfHexEditor.SDK.Contracts;
using WpfHexEditor.SDK.Contracts.Services;
using WpfHexEditor.Core.ViewModels;

namespace WpfHexEditor.Plugins.Debugger.ViewModels;

/// <summary>
/// Wrapper around a DAP stack frame with async grouping annotation.
/// </summary>
public sealed class CallStackFrameItem
{
    public DebugFrameInfo Frame { get; init; } = null!;

    // Forwarded shortcuts for XAML bindings
    public int     Id       => Frame.Id;
    public string  Name     => Frame.Name;
    public string? FilePath => Frame.FilePath;
    public int     Line     => Frame.Line;

    /// <summary>
    /// True when this frame is the start of an async continuation group
    /// (preceded by a compiler-generated MoveNext / DisplayClass boundary).
    /// The XAML inserts a visual separator above it.
    /// </summary>
    public bool IsAsyncBoundary { get; init; }
}

public sealed class CallStackPanelViewModel : ViewModelBase
{
    private readonly IIDEHostContext _context;
    private CallStackFrameItem? _selectedFrame;

    public ObservableCollection<CallStackFrameItem> Frames { get; } = [];

    public CallStackFrameItem? SelectedFrame
    {
        get => _selectedFrame;
        set
        {
            _selectedFrame = value;
            OnPropertyChanged(nameof(SelectedFrame));
            NavigateToFrameAsync(value).ConfigureAwait(false);
        }
    }

    public ICommand NavigateCommand { get; }

    public CallStackPanelViewModel(IDebuggerService debugger, IIDEHostContext context)
    {
        _context        = context;
        NavigateCommand = new RelayCommand(async p => await NavigateToFrameAsync(p as CallStackFrameItem));
    }

    public void SetFrames(IReadOnlyList<DebugFrameInfo> frames)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            Frames.Clear();
            for (int i = 0; i < frames.Count; i++)
            {
                var boundary = i > 0 && IsAsyncBoundaryBetween(frames[i - 1], frames[i]);
                Frames.Add(new CallStackFrameItem { Frame = frames[i], IsAsyncBoundary = boundary });
            }
        });
    }

    private static bool IsAsyncBoundaryBetween(DebugFrameInfo prev, DebugFrameInfo _)
    {
        // Compiler-generated frame names signal async/await boundaries
        var n = prev.Name;
        return n.Contains("MoveNext",      StringComparison.Ordinal)
            || n.Contains("<>c__DisplayClass", StringComparison.Ordinal)
            || n.Contains("__StateMachine",    StringComparison.Ordinal);
    }

    private async Task NavigateToFrameAsync(CallStackFrameItem? item)
    {
        if (item?.FilePath is null || item.Line <= 0) return;
        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            _context.DocumentHost.OpenDocument(item.FilePath);
        });
    }

}
