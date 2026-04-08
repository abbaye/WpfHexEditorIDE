// ==========================================================
// Project: WpfHexEditor.Plugins.Git
// File: ViewModels/GitChangesPanelViewModel.cs
// Description:
//     ViewModel for GitChangesPanel. Groups changed files by kind.
//     Exposes Stage/Unstage/Discard/Refresh/Commit/Push/Pull/Stash commands.
// Architecture Notes:
//     IIDEEventBus subscription for GitAheadBehindChangedEvent /
//     GitOperationStartedEvent / GitOperationCompletedEvent.
//     All bus callbacks marshal to UI thread before mutating ObservableCollections.
// ==========================================================

using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using WpfHexEditor.Core.Events.IDEEvents;
using WpfHexEditor.Core.ViewModels;
using WpfHexEditor.Editor.Core.LSP;
using WpfHexEditor.Core.Events;
using WpfHexEditor.SDK.Commands;
using WpfHexEditor.SDK.Contracts.Services;

namespace WpfHexEditor.Plugins.Git.ViewModels;

/// <summary>Row in the changes list.</summary>
public sealed class GitChangeRow : ViewModelBase
{
    public string        FilePath  { get; }
    public string        FileName  { get; }
    public GitChangeKind Kind      { get; }
    public string        KindLabel => Kind.ToString();

    public GitChangeRow(GitChangeEntry entry)
    {
        FilePath = entry.FilePath;
        FileName = System.IO.Path.GetFileName(entry.FilePath);
        Kind     = entry.Kind;
    }
}

/// <summary>Row in the stash list.</summary>
public sealed class StashEntryRow : ViewModelBase
{
    public int      Index   { get; }
    public string   Message { get; }
    public DateTime Date    { get; }
    public string   DateText => Date == default ? "" : Date.ToString("MMM d, yyyy");

    public StashEntryRow(StashEntry entry)
    {
        Index   = entry.Index;
        Message = entry.Message;
        Date    = entry.Date;
    }
}

/// <summary>ViewModel for the Git Changes panel.</summary>
public sealed class GitChangesPanelViewModel : ViewModelBase, IDisposable
{
    private readonly IVersionControlService _vcs;
    private readonly IOutputService         _output;
    private readonly List<IDisposable>      _busSubscriptions = [];

    private bool   _isLoading;
    private bool   _isRemoteOp;
    private string _statusText    = "Initializing…";
    private string _commitMessage = string.Empty;
    private bool   _isAmend;
    private int    _ahead;
    private int    _behind;

    // ── Collections ───────────────────────────────────────────────────────────

    public ObservableCollection<GitChangeRow>   StagedFiles    { get; } = [];
    public ObservableCollection<GitChangeRow>   ModifiedFiles  { get; } = [];
    public ObservableCollection<GitChangeRow>   UntrackedFiles { get; } = [];
    public ObservableCollection<GitChangeRow>   ConflictFiles  { get; } = [];
    public ObservableCollection<StashEntryRow>  StashEntries   { get; } = [];

    // ── Properties ────────────────────────────────────────────────────────────

    public bool   IsLoading     { get => _isLoading;     private set { _isLoading     = value; OnPropChanged(); OnPropChanged(nameof(IsIdle)); } }
    public bool   IsRemoteOp    { get => _isRemoteOp;    private set { _isRemoteOp    = value; OnPropChanged(); OnPropChanged(nameof(IsIdle)); } }
    public bool   IsIdle        => !_isLoading && !_isRemoteOp;
    public string StatusText    { get => _statusText;    private set { _statusText    = value; OnPropChanged(); } }
    public string CommitMessage { get => _commitMessage; set         { _commitMessage = value; OnPropChanged(); CommandManager.InvalidateRequerySuggested(); } }
    public bool   IsAmend       { get => _isAmend;       set         { _isAmend       = value; OnPropChanged(); } }
    public int    Ahead         { get => _ahead;         private set { _ahead         = value; OnPropChanged(); OnPropChanged(nameof(AheadBehindText)); } }
    public int    Behind        { get => _behind;        private set { _behind        = value; OnPropChanged(); OnPropChanged(nameof(AheadBehindText)); } }
    public string AheadBehindText => $"↑{_ahead} ↓{_behind}";
    public bool   HasStash      => StashEntries.Count > 0;

    // ── Commands ──────────────────────────────────────────────────────────────

    public System.Windows.Input.ICommand RefreshCommand     { get; }
    public System.Windows.Input.ICommand StageAllCommand    { get; }
    public System.Windows.Input.ICommand UnstageAllCommand  { get; }
    public System.Windows.Input.ICommand DiscardAllCommand  { get; }
    public System.Windows.Input.ICommand StageCommand       { get; }
    public System.Windows.Input.ICommand UnstageCommand     { get; }
    public System.Windows.Input.ICommand DiscardCommand     { get; }
    public System.Windows.Input.ICommand CommitCommand      { get; }
    public System.Windows.Input.ICommand FetchCommand       { get; }
    public System.Windows.Input.ICommand PullCommand        { get; }
    public System.Windows.Input.ICommand PushCommand        { get; }
    public System.Windows.Input.ICommand SyncCommand        { get; }
    public System.Windows.Input.ICommand StashCommand       { get; }
    public System.Windows.Input.ICommand StashPopCommand    { get; }
    public System.Windows.Input.ICommand StashDropCommand   { get; }

    public GitChangesPanelViewModel(IVersionControlService vcs, IOutputService output,
        IIDEEventBus? bus = null)
    {
        _vcs    = vcs;
        _output = output;

        RefreshCommand     = new RelayCommand(_ => RefreshAsync());
        StageAllCommand    = new RelayCommand(_ => StageAllAsync(),
            _ => IsIdle && (ModifiedFiles.Count > 0 || UntrackedFiles.Count > 0));
        UnstageAllCommand  = new RelayCommand(_ => UnstageAllAsync(),
            _ => IsIdle && StagedFiles.Count > 0);
        DiscardAllCommand  = new RelayCommand(_ => DiscardAllAsync(),
            _ => IsIdle && ModifiedFiles.Count > 0);
        StageCommand       = new RelayCommand(
            p => { if (p is GitChangeRow r) StageOneAsync(r); },
            p => IsIdle && p is GitChangeRow);
        UnstageCommand     = new RelayCommand(
            p => { if (p is GitChangeRow r) UnstageOneAsync(r); },
            p => IsIdle && p is GitChangeRow);
        DiscardCommand     = new RelayCommand(
            p => { if (p is GitChangeRow r) DiscardOneAsync(r); },
            p => IsIdle && p is GitChangeRow r && r.Kind != GitChangeKind.Untracked);
        CommitCommand      = new RelayCommand(
            _ => CommitAsync(),
            _ => IsIdle && !string.IsNullOrWhiteSpace(CommitMessage));
        FetchCommand       = new RelayCommand(_ => FetchAsync(), _ => IsIdle && _vcs.IsRepo);
        PullCommand        = new RelayCommand(_ => PullAsync(),  _ => IsIdle && _vcs.IsRepo);
        PushCommand        = new RelayCommand(_ => PushAsync(),  _ => IsIdle && _vcs.IsRepo);
        SyncCommand        = new RelayCommand(_ => SyncAsync(),  _ => IsIdle && _vcs.IsRepo);
        StashCommand       = new RelayCommand(_ => StashAsync(), _ => IsIdle && _vcs.IsDirty);
        StashPopCommand    = new RelayCommand(
            p => { if (p is StashEntryRow r) StashPopAsync(r.Index); },
            p => IsIdle && p is StashEntryRow);
        StashDropCommand   = new RelayCommand(
            p => { if (p is StashEntryRow r) StashDropAsync(r.Index); },
            p => IsIdle && p is StashEntryRow);

        // Subscribe to event bus
        if (bus is not null)
        {
            _busSubscriptions.Add(bus.Subscribe<GitAheadBehindChangedEvent>(OnAheadBehindChanged));
            _busSubscriptions.Add(bus.Subscribe<GitOperationStartedEvent>(OnOperationStarted));
            _busSubscriptions.Add(bus.Subscribe<GitOperationCompletedEvent>(OnOperationCompleted));
        }
    }

    // ── Public ────────────────────────────────────────────────────────────────

    public async void RefreshAsync()
    {
        bool first = TryFirstLoad();
        if (first) IsLoading = true;
        try
        {
            var changes = await _vcs.GetChangedFilesAsync();
            PopulateGroups(changes);

            var stash = await _vcs.GetStashListAsync();
            PopulateStash(stash);

            StatusText = _vcs.IsRepo
                ? $"{_vcs.BranchName ?? "detached"}{(_vcs.IsDirty ? " ●" : "")}"
                : "No repository";
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
            _output.Write("Git", $"[Git Changes] Refresh failed: {ex.Message}");
        }
        finally
        {
            if (first) IsLoading = false;
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public void Dispose()
    {
        foreach (var sub in _busSubscriptions) sub.Dispose();
        _busSubscriptions.Clear();
    }

    // ── Commit ────────────────────────────────────────────────────────────────

    private async void CommitAsync()
    {
        if (string.IsNullOrWhiteSpace(CommitMessage)) return;
        IsRemoteOp = true;
        try
        {
            await _vcs.CommitAsync(CommitMessage, IsAmend);
            CommitMessage = string.Empty;
            IsAmend       = false;
            _output.Write("Git", "Commit created.");
            RefreshAsync();
        }
        catch (Exception ex)
        {
            StatusText = $"Commit failed: {ex.Message}";
            _output.Write("Git", $"[Git Commit] {ex.Message}");
        }
        finally { IsRemoteOp = false; }
    }

    // ── Remote ────────────────────────────────────────────────────────────────

    private async void FetchAsync()
    {
        try { await _vcs.FetchAsync(); }
        catch (Exception ex) { _output.Write("Git", $"[Git Fetch] {ex.Message}"); }
    }

    private async void PullAsync()
    {
        try
        {
            await _vcs.PullAsync();
            RefreshAsync();
        }
        catch (Exception ex) { _output.Write("Git", $"[Git Pull] {ex.Message}"); }
    }

    private async void PushAsync()
    {
        try { await _vcs.PushAsync(); }
        catch (Exception ex) { _output.Write("Git", $"[Git Push] {ex.Message}"); }
    }

    private async void SyncAsync()
    {
        try
        {
            await _vcs.PullAsync();
            await _vcs.PushAsync();
            RefreshAsync();
        }
        catch (Exception ex) { _output.Write("Git", $"[Git Sync] {ex.Message}"); }
    }

    // ── Stash ─────────────────────────────────────────────────────────────────

    private async void StashAsync()
    {
        IsRemoteOp = true;
        try
        {
            await _vcs.StashAsync();
            RefreshAsync();
        }
        catch (Exception ex) { _output.Write("Git", $"[Git Stash] {ex.Message}"); }
        finally { IsRemoteOp = false; }
    }

    private async void StashPopAsync(int index)
    {
        IsRemoteOp = true;
        try
        {
            await _vcs.StashPopAsync(index);
            RefreshAsync();
        }
        catch (Exception ex) { _output.Write("Git", $"[Git Stash Pop] {ex.Message}"); }
        finally { IsRemoteOp = false; }
    }

    private async void StashDropAsync(int index)
    {
        IsRemoteOp = true;
        try
        {
            await _vcs.StashDropAsync(index);
            var stash = await _vcs.GetStashListAsync();
            PopulateStash(stash);
            RefreshAsync();
        }
        catch (Exception ex) { _output.Write("Git", $"[Git Stash Drop] {ex.Message}"); }
        finally { IsRemoteOp = false; }
    }

    // ── File operations ───────────────────────────────────────────────────────

    private void PopulateGroups(IReadOnlyList<GitChangeEntry> changes)
    {
        StagedFiles.Clear();
        ModifiedFiles.Clear();
        UntrackedFiles.Clear();
        ConflictFiles.Clear();

        foreach (var c in changes)
        {
            var row = new GitChangeRow(c);
            switch (c.Kind)
            {
                case GitChangeKind.Staged:
                    StagedFiles.Add(row); break;
                case GitChangeKind.Untracked:
                    UntrackedFiles.Add(row); break;
                case GitChangeKind.Conflicted:
                    ConflictFiles.Add(row); break;
                default:
                    ModifiedFiles.Add(row); break;
            }
        }
        OnPropertyChanged(nameof(StagedFiles));
        OnPropertyChanged(nameof(ModifiedFiles));
        OnPropertyChanged(nameof(UntrackedFiles));
        OnPropertyChanged(nameof(ConflictFiles));
    }

    private void PopulateStash(IReadOnlyList<StashEntry> entries)
    {
        StashEntries.Clear();
        foreach (var e in entries) StashEntries.Add(new StashEntryRow(e));
        OnPropertyChanged(nameof(HasStash));
        OnPropertyChanged(nameof(StashEntries));
    }

    private async void StageAllAsync()
    {
        foreach (var r in ModifiedFiles.Concat(UntrackedFiles).ToList())
            await _vcs.StageAsync(r.FilePath);
        RefreshAsync();
    }

    private async void UnstageAllAsync()
    {
        foreach (var r in StagedFiles.ToList())
            await _vcs.UnstageAsync(r.FilePath);
        RefreshAsync();
    }

    private async void DiscardAllAsync()
    {
        foreach (var r in ModifiedFiles.ToList())
            await _vcs.DiscardAsync(r.FilePath);
        RefreshAsync();
    }

    private async void StageOneAsync(GitChangeRow row)
    {
        await _vcs.StageAsync(row.FilePath);
        RefreshAsync();
    }

    private async void UnstageOneAsync(GitChangeRow row)
    {
        await _vcs.UnstageAsync(row.FilePath);
        RefreshAsync();
    }

    private async void DiscardOneAsync(GitChangeRow row)
    {
        await _vcs.DiscardAsync(row.FilePath);
        RefreshAsync();
    }

    // ── Event bus handlers ────────────────────────────────────────────────────

    private System.Threading.Tasks.Task OnAheadBehindChanged(GitAheadBehindChangedEvent e)
    {
        Dispatch(() => { Ahead = e.Ahead; Behind = e.Behind; });
        return System.Threading.Tasks.Task.CompletedTask;
    }

    private System.Threading.Tasks.Task OnOperationStarted(GitOperationStartedEvent e)
    {
        Dispatch(() =>
        {
            IsRemoteOp = true;
            StatusText = $"{e.OperationName}…";
        });
        return System.Threading.Tasks.Task.CompletedTask;
    }

    private System.Threading.Tasks.Task OnOperationCompleted(GitOperationCompletedEvent e)
    {
        Dispatch(() =>
        {
            IsRemoteOp = false;
            if (!e.Success)
            {
                StatusText = $"{e.OperationName} failed";
                _output.Write("Git", $"[{e.OperationName}] {e.ErrorMessage}");
            }
            CommandManager.InvalidateRequerySuggested();
        });
        return System.Threading.Tasks.Task.CompletedTask;
    }

    private static void Dispatch(Action action)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null) return;
        if (dispatcher.CheckAccess()) action();
        else dispatcher.InvokeAsync(action);
    }

    private void OnPropChanged([CallerMemberName] string? n = null)
        => OnPropertyChanged(n);
}
