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

public sealed class CallStackPanelViewModel : ViewModelBase
{
    private readonly IIDEHostContext _context;
    private DebugFrameInfo? _selectedFrame;

    public ObservableCollection<DebugFrameInfo> Frames { get; } = [];

    public DebugFrameInfo? SelectedFrame
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
        NavigateCommand = new RelayCommand(async p => await NavigateToFrameAsync(p as DebugFrameInfo));
    }

    public void SetFrames(IReadOnlyList<DebugFrameInfo> frames)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            Frames.Clear();
            foreach (var f in frames) Frames.Add(f);
        });
    }

    private async Task NavigateToFrameAsync(DebugFrameInfo? frame)
    {
        if (frame?.FilePath is null || frame.Line <= 0) return;
        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            _context.DocumentHost.OpenDocument(frame.FilePath);
            // NavigateTo will be handled by the document open event + EditorFocused
        });
    }

}
