// ==========================================================
// Project: WpfHexEditor.App.Debug
// File: ViewModels/CallStackPanelViewModel.cs
// Description: VM for the Call Stack panel — frame list + navigate to source.
// ==========================================================

using System.Collections.ObjectModel;
using System.Windows.Input;
using WpfHexEditor.SDK.Commands;
using WpfHexEditor.SDK.Contracts;
using WpfHexEditor.SDK.Contracts.Services;
using WpfHexEditor.Core.ViewModels;

namespace WpfHexEditor.App.Debug.ViewModels;

/// <summary>
/// Wrapper around a DAP stack frame with async grouping annotation.
/// </summary>
public sealed class CallStackFrameItem
{
    public DebugFrameInfo Frame { get; init; } = null!;

    public int     Id       => Frame.Id;
    public string  Name     => Frame.Name;
    public string? FilePath => Frame.FilePath;
    public int     Line     => Frame.Line;

    /// <summary>
    /// True when this frame is the start of an async continuation group.
    /// </summary>
    public bool IsAsyncBoundary { get; init; }

    /// <summary>
    /// True when this frame is compiler-generated / external code.
    /// </summary>
    public bool IsExternal => Name.Contains("[External Code]", StringComparison.OrdinalIgnoreCase)
                           || Name.Contains("MoveNext",      StringComparison.Ordinal)
                           || Name.Contains("<>c__DisplayClass", StringComparison.Ordinal);
}

public sealed class CallStackPanelViewModel : ViewModelBase
{
    private readonly IIDEHostContext _context;
    private CallStackFrameItem? _selectedFrame;
    private string  _searchText        = string.Empty;
    private bool    _showExternalCode  = true;
    private bool    _showAllThreads    = false;
    private int     _navIndex          = -1;
    private IReadOnlyList<DebugFrameInfo> _rawFrames = [];

    // ── Observable collections ──────────────────────────────────────────
    public ObservableCollection<CallStackFrameItem> Frames { get; } = [];

    // ── Toolbar bindings ─────────────────────────────────────────────────
    public string SearchText
    {
        get => _searchText;
        set { _searchText = value; OnPropertyChanged(nameof(SearchText)); ApplyFilter(); }
    }

    public bool ShowExternalCode
    {
        get => _showExternalCode;
        set { _showExternalCode = value; OnPropertyChanged(nameof(ShowExternalCode)); ApplyFilter(); }
    }

    public bool ShowAllThreads
    {
        get => _showAllThreads;
        set { _showAllThreads = value; OnPropertyChanged(nameof(ShowAllThreads)); }
    }

    public bool CanNavBack    => _navIndex > 0;
    public bool CanNavForward => _navIndex >= 0 && _navIndex < Frames.Count - 1;

    // ── Selected frame ───────────────────────────────────────────────────
    public CallStackFrameItem? SelectedFrame
    {
        get => _selectedFrame;
        set
        {
            _selectedFrame = value;
            OnPropertyChanged(nameof(SelectedFrame));
            if (value is not null)
            {
                _navIndex = Frames.IndexOf(value);
                OnPropertyChanged(nameof(CanNavBack));
                OnPropertyChanged(nameof(CanNavForward));
            }
            NavigateToFrameAsync(value).ConfigureAwait(false);
        }
    }

    // ── Commands ─────────────────────────────────────────────────────────
    public ICommand NavigateCommand        { get; }
    public ICommand CopyFrameCommand       { get; }
    public ICommand CopyAllCommand         { get; }
    public ICommand GoToSourceCommand      { get; }
    public ICommand NavBackCommand         { get; }
    public ICommand NavForwardCommand      { get; }
    public ICommand RunToFrameCommand      { get; }
    public ICommand SwitchToFrameCommand   { get; }

    public CallStackPanelViewModel(IDebuggerService debugger, IIDEHostContext context)
    {
        _context = context;

        NavigateCommand      = new RelayCommand(async p => await NavigateToFrameAsync(p as CallStackFrameItem));
        GoToSourceCommand    = new RelayCommand(async p => await NavigateToFrameAsync(p as CallStackFrameItem));
        RunToFrameCommand    = new RelayCommand(_ => { }, _ => false); // placeholder — requires DAP support
        SwitchToFrameCommand = new RelayCommand(async p => await NavigateToFrameAsync(p as CallStackFrameItem));

        CopyFrameCommand = new RelayCommand(p =>
        {
            if (p is CallStackFrameItem f)
                System.Windows.Clipboard.SetText(FormatFrame(f));
        });

        CopyAllCommand = new RelayCommand(_ =>
        {
            if (Frames.Count == 0) return;
            System.Windows.Clipboard.SetText(string.Join(Environment.NewLine, Frames.Select(FormatFrame)));
        });

        NavBackCommand = new RelayCommand(_ =>
        {
            if (_navIndex > 0)
                SelectedFrame = Frames[--_navIndex];
        }, _ => CanNavBack);

        NavForwardCommand = new RelayCommand(_ =>
        {
            if (_navIndex < Frames.Count - 1)
                SelectedFrame = Frames[++_navIndex];
        }, _ => CanNavForward);
    }

    // ── Data ─────────────────────────────────────────────────────────────
    public void SetFrames(IReadOnlyList<DebugFrameInfo> frames)
    {
        _rawFrames = frames;
        System.Windows.Application.Current?.Dispatcher.Invoke(ApplyFilter);
    }

    private void ApplyFilter()
    {
        Frames.Clear();
        _navIndex = -1;

        for (int i = 0; i < _rawFrames.Count; i++)
        {
            var f = _rawFrames[i];
            var item = new CallStackFrameItem
            {
                Frame = f,
                IsAsyncBoundary = i > 0 && IsAsyncBoundaryBetween(_rawFrames[i - 1], f)
            };

            if (!_showExternalCode && item.IsExternal) continue;
            if (!string.IsNullOrWhiteSpace(_searchText)
                && !item.Name.Contains(_searchText, StringComparison.OrdinalIgnoreCase))
                continue;

            Frames.Add(item);
        }

        OnPropertyChanged(nameof(CanNavBack));
        OnPropertyChanged(nameof(CanNavForward));
    }

    private static bool IsAsyncBoundaryBetween(DebugFrameInfo prev, DebugFrameInfo _)
    {
        var n = prev.Name;
        return n.Contains("MoveNext",          StringComparison.Ordinal)
            || n.Contains("<>c__DisplayClass",  StringComparison.Ordinal)
            || n.Contains("__StateMachine",     StringComparison.Ordinal);
    }

    private static string FormatFrame(CallStackFrameItem f)
        => $"{f.Id}\t{f.Name}\t{f.FilePath}:{f.Line}";

    private async Task NavigateToFrameAsync(CallStackFrameItem? item)
    {
        if (item?.FilePath is null || item.Line <= 0) return;
        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            _context.DocumentHost.OpenDocument(item.FilePath));
    }
}
