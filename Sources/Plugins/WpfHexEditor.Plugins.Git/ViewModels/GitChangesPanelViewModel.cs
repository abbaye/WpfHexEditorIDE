// ==========================================================
// Project: WpfHexEditor.Plugins.Git
// File: ViewModels/GitChangesPanelViewModel.cs
// Description:
//     ViewModel for GitChangesPanel. Groups changed files by kind.
//     Exposes Stage/Unstage/Discard/Refresh commands.
// ==========================================================

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using WpfHexEditor.Editor.Core.LSP;
using WpfHexEditor.SDK.Commands;
using WpfHexEditor.SDK.Contracts.Services;
using WpfHexEditor.Core.ViewModels;

namespace WpfHexEditor.Plugins.Git.ViewModels;

/// <summary>Row in the changes list.</summary>
public sealed class GitChangeRow : ViewModelBase
{
    public string       FilePath { get; }
    public string       FileName { get; }
    public GitChangeKind Kind    { get; }
    public string       KindLabel => Kind.ToString();

    public GitChangeRow(GitChangeEntry entry)
    {
        FilePath = entry.FilePath;
        FileName = System.IO.Path.GetFileName(entry.FilePath);
        Kind     = entry.Kind;
    }

}

/// <summary>ViewModel for the Git Changes panel.</summary>
public sealed class GitChangesPanelViewModel : ViewModelBase
{
    private readonly IVersionControlService _vcs;
    private readonly IOutputService         _output;
    private bool   _isLoading;
    private string _statusText = "Initializingâ€¦";

    public ObservableCollection<GitChangeRow> StagedFiles   { get; } = [];
    public ObservableCollection<GitChangeRow> ModifiedFiles { get; } = [];
    public ObservableCollection<GitChangeRow> UntrackedFiles{ get; } = [];

    public bool   IsLoading  { get => _isLoading;  private set { _isLoading  = value; OnPropChanged(); } }
    public string StatusText { get => _statusText; private set { _statusText = value; OnPropChanged(); } }

    public System.Windows.Input.ICommand RefreshCommand    { get; }
    public System.Windows.Input.ICommand StageAllCommand   { get; }
    public System.Windows.Input.ICommand UnstageAllCommand { get; }
    public System.Windows.Input.ICommand DiscardAllCommand { get; }
    public System.Windows.Input.ICommand StageCommand      { get; }
    public System.Windows.Input.ICommand UnstageCommand    { get; }
    public System.Windows.Input.ICommand DiscardCommand    { get; }

    public GitChangesPanelViewModel(IVersionControlService vcs, IOutputService output)
    {
        _vcs    = vcs;
        _output = output;

        RefreshCommand    = new RelayCommand(_ => RefreshAsync());
        StageAllCommand   = new RelayCommand(_ => StageAllAsync(),   _ => ModifiedFiles.Count > 0 || UntrackedFiles.Count > 0);
        UnstageAllCommand = new RelayCommand(_ => UnstageAllAsync(), _ => StagedFiles.Count > 0);
        DiscardAllCommand = new RelayCommand(_ => DiscardAllAsync(), _ => ModifiedFiles.Count > 0);
        StageCommand      = new RelayCommand(p => { if (p is GitChangeRow r) StageOneAsync(r); }, p => p is GitChangeRow);
        UnstageCommand    = new RelayCommand(p => { if (p is GitChangeRow r) UnstageOneAsync(r); }, p => p is GitChangeRow);
        DiscardCommand    = new RelayCommand(p => { if (p is GitChangeRow r) DiscardOneAsync(r); }, p => p is GitChangeRow r && r.Kind != GitChangeKind.Untracked);
    }

    // ── Public ────────────────────────────────────────────────────────────────

    public async void RefreshAsync()
    {
        IsLoading = true;
        try
        {
            var changes = await _vcs.GetChangedFilesAsync();
            PopulateGroups(changes);
            StatusText = _vcs.IsRepo
                ? $"{_vcs.BranchName ?? "detached"}{(_vcs.IsDirty ? " â—" : "")}"
                : "No repository";
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
            _output.Write("Git", $"[Git Changes] Refresh failed: {ex.Message}");
        }
        finally { IsLoading = false; }
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private void PopulateGroups(IReadOnlyList<GitChangeEntry> changes)
    {
        StagedFiles.Clear();
        ModifiedFiles.Clear();
        UntrackedFiles.Clear();

        foreach (var c in changes)
        {
            var row = new GitChangeRow(c);
            switch (c.Kind)
            {
                case GitChangeKind.Staged:
                    StagedFiles.Add(row);
                    break;
                case GitChangeKind.Untracked:
                    UntrackedFiles.Add(row);
                    break;
                default:
                    ModifiedFiles.Add(row);
                    break;
            }
        }
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

    private void OnPropChanged([CallerMemberName] string? n = null)
        => OnPropertyChanged(n);
}
