// ==========================================================
// Project: WpfHexEditor.Plugins.Git
// File: ViewModels/GitHistoryPanelViewModel.cs
// Description:
//     ViewModel for GitHistoryPanel.
//     Loads commit log with lazy pagination (100 commits at a time).
//     Supports file-scoped and repo-wide views. Subscribes to
//     GitBlameNavigateEvent to scroll to a specific commit.
// Architecture Notes:
//     Pattern: INPC + ObservableCollection.
//     IIDEEventBus used for GitBlameNavigateEvent.
// ==========================================================

using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using WpfHexEditor.Core.Events;
using WpfHexEditor.Core.Events.IDEEvents;
using WpfHexEditor.Core.ViewModels;
using WpfHexEditor.Editor.Core.LSP;
using WpfHexEditor.SDK.Commands;

namespace WpfHexEditor.Plugins.Git.ViewModels;

/// <summary>Row in the history list.</summary>
public sealed class CommitRow : ViewModelBase
{
    public CommitInfo Info       { get; }
    public string     ShortHash  => Info.ShortHash;
    public string     Message    => Info.Message;
    public string     AuthorName => Info.AuthorName;
    public string     DateText   => Info.Date == default ? "" : FormatDate(Info.Date);
    public string     Hash       => Info.Hash;

    private static string FormatDate(DateTime d)
    {
        var diff = DateTime.Now - d;
        if (diff.TotalDays < 1)  return diff.TotalHours < 1  ? "just now" : $"{(int)diff.TotalHours}h ago";
        if (diff.TotalDays < 7)  return $"{(int)diff.TotalDays}d ago";
        if (diff.TotalDays < 30) return $"{(int)(diff.TotalDays / 7)}w ago";
        return d.ToString("MMM d, yyyy");
    }

    public CommitRow(CommitInfo info) => Info = info;
}

/// <summary>ViewModel for the Git History dockable panel.</summary>
public sealed class GitHistoryPanelViewModel : ViewModelBase, IDisposable
{
    private readonly IVersionControlService  _vcs;
    private readonly List<IDisposable>       _subs = [];

    private bool   _isLoading;
    private string _fileFilter   = string.Empty;
    private string _authorFilter = string.Empty;
    private int    _maxCount     = 100;

    private CommitRow? _selectedCommit;
    private string     _diffText = string.Empty;

    public ObservableCollection<CommitRow> Commits { get; } = [];

    public bool   IsLoading    { get => _isLoading;    private set { _isLoading    = value; OnPropChanged(); } }
    public string FileFilter   { get => _fileFilter;   set         { _fileFilter   = value; OnPropChanged(); } }
    public string AuthorFilter { get => _authorFilter; set         { _authorFilter = value; OnPropChanged(); } }
    public int    MaxCount     { get => _maxCount;     set         { _maxCount     = value; OnPropChanged(); } }
    public string DiffText     { get => _diffText;     private set { _diffText     = value; OnPropChanged(); } }

    public CommitRow? SelectedCommit
    {
        get => _selectedCommit;
        set
        {
            _selectedCommit = value;
            OnPropChanged();
            if (value is not null) LoadDiffAsync(value);
        }
    }

    public ICommand RefreshCommand { get; }
    public ICommand ScrollToHashCommand { get; }

    public GitHistoryPanelViewModel(IVersionControlService vcs, IIDEEventBus? bus = null)
    {
        _vcs = vcs;

        RefreshCommand      = new RelayCommand(_ => LoadHistoryAsync(), _ => !IsLoading);
        ScrollToHashCommand = new RelayCommand(
            p => { if (p is string h) ScrollToHash(h); },
            p => p is string);

        if (bus is not null)
            _subs.Add(bus.Subscribe<GitBlameNavigateEvent>(OnBlameNavigate));
    }

    // ── Public ────────────────────────────────────────────────────────────────

    public async void LoadHistoryAsync()
    {
        IsLoading = true;
        Commits.Clear();
        try
        {
            var filePath = string.IsNullOrWhiteSpace(FileFilter) ? null : FileFilter.Trim();
            var log = await _vcs.GetLogAsync(MaxCount, filePath);
            foreach (var c in log)
            {
                var row = new CommitRow(c);
                if (!string.IsNullOrWhiteSpace(AuthorFilter) &&
                    !c.AuthorName.Contains(AuthorFilter, StringComparison.OrdinalIgnoreCase))
                    continue;
                Commits.Add(row);
            }
        }
        finally { IsLoading = false; }
    }

    public void Dispose()
    {
        foreach (var s in _subs) s.Dispose();
        _subs.Clear();
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private async void LoadDiffAsync(CommitRow row)
    {
        DiffText = "Loading…";
        try
        {
            DiffText = await _vcs.ShowCommitAsync(row.Hash);
        }
        catch (Exception ex)
        {
            DiffText = $"Error: {ex.Message}";
        }
    }

    private void ScrollToHash(string hash)
    {
        var row = Commits.FirstOrDefault(c => c.Hash.StartsWith(hash, StringComparison.OrdinalIgnoreCase)
                                           || c.ShortHash == hash);
        if (row is not null) SelectedCommit = row;
    }

    private System.Threading.Tasks.Task OnBlameNavigate(GitBlameNavigateEvent e)
    {
        Dispatch(() => ScrollToHash(e.CommitHash));
        return System.Threading.Tasks.Task.CompletedTask;
    }

    private static void Dispatch(Action action)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null) return;
        if (dispatcher.CheckAccess()) action();
        else dispatcher.InvokeAsync(action);
    }

    private void OnPropChanged([CallerMemberName] string? n = null) => OnPropertyChanged(n);
}
