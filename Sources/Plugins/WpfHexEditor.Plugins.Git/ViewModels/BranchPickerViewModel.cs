// ==========================================================
// Project: WpfHexEditor.Plugins.Git
// File: ViewModels/BranchPickerViewModel.cs
// Description:
//     ViewModel for BranchPickerPopup.
//     Loads all branches, filters by search text, supports
//     switch, create, and delete operations.
// ==========================================================

using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using WpfHexEditor.Core.ViewModels;
using WpfHexEditor.Editor.Core.LSP;
using WpfHexEditor.SDK.Commands;

namespace WpfHexEditor.Plugins.Git.ViewModels;

public sealed class BranchRow : ViewModelBase
{
    public BranchInfo Info      { get; }
    public string     Name      => Info.Name;
    public bool       IsCurrent => Info.IsCurrent;
    public bool       IsRemote  => Info.IsRemote;
    public string     Icon      => Info.IsCurrent ? "✓" : (Info.IsRemote ? "⬆" : "⬦");

    public BranchRow(BranchInfo info) => Info = info;
}

public sealed class BranchPickerViewModel : ViewModelBase
{
    private readonly IVersionControlService _vcs;
    private string _searchText       = string.Empty;
    private string _newBranchName    = string.Empty;
    private bool   _isCreatingBranch;
    private bool   _isLoading;

    public ObservableCollection<BranchRow> AllBranches      { get; } = [];
    public ObservableCollection<BranchRow> FilteredBranches { get; } = [];

    public string SearchText
    {
        get => _searchText;
        set { _searchText = value; OnPropChanged(); ApplyFilter(); }
    }

    public string NewBranchName
    {
        get => _newBranchName;
        set { _newBranchName = value; OnPropChanged(); CommandManager.InvalidateRequerySuggested(); }
    }

    public bool IsCreatingBranch
    {
        get => _isCreatingBranch;
        set { _isCreatingBranch = value; OnPropChanged(); }
    }

    public bool IsLoading { get => _isLoading; private set { _isLoading = value; OnPropChanged(); } }

    public System.Windows.Input.ICommand SwitchCommand      { get; }
    public System.Windows.Input.ICommand DeleteCommand      { get; }
    public System.Windows.Input.ICommand CreateCommand      { get; }
    public System.Windows.Input.ICommand BeginCreateCommand { get; }
    public System.Windows.Input.ICommand CancelCreateCommand{ get; }

    public event EventHandler? RequestClose;

    public BranchPickerViewModel(IVersionControlService vcs)
    {
        _vcs = vcs;

        SwitchCommand       = new RelayCommand(
            p => { if (p is BranchRow r && !r.IsCurrent) SwitchAsync(r.Name); },
            p => p is BranchRow r && !r.IsCurrent && !IsLoading);
        DeleteCommand       = new RelayCommand(
            p => { if (p is BranchRow r) DeleteAsync(r.Name); },
            p => p is BranchRow r && !r.IsCurrent && !r.IsRemote && !IsLoading);
        CreateCommand       = new RelayCommand(
            _ => CreateAsync(),
            _ => !string.IsNullOrWhiteSpace(NewBranchName) && !IsLoading);
        BeginCreateCommand  = new RelayCommand(_ => IsCreatingBranch = true);
        CancelCreateCommand = new RelayCommand(_ => { IsCreatingBranch = false; NewBranchName = string.Empty; });
    }

    public async void LoadAsync()
    {
        IsLoading = true;
        try
        {
            var branches = await _vcs.GetBranchesAsync();
            AllBranches.Clear();
            foreach (var b in branches) AllBranches.Add(new BranchRow(b));
            ApplyFilter();
        }
        finally { IsLoading = false; }
    }

    private void ApplyFilter()
    {
        FilteredBranches.Clear();
        var q = _searchText.Trim();
        foreach (var b in AllBranches)
            if (string.IsNullOrEmpty(q) || b.Name.Contains(q, StringComparison.OrdinalIgnoreCase))
                FilteredBranches.Add(b);
    }

    private async void SwitchAsync(string name)
    {
        IsLoading = true;
        try
        {
            await _vcs.SwitchBranchAsync(name);
            RequestClose?.Invoke(this, EventArgs.Empty);
        }
        finally { IsLoading = false; }
    }

    private async void DeleteAsync(string name)
    {
        IsLoading = true;
        try { await _vcs.DeleteBranchAsync(name); LoadAsync(); }
        finally { IsLoading = false; }
    }

    private async void CreateAsync()
    {
        if (string.IsNullOrWhiteSpace(NewBranchName)) return;
        IsLoading = true;
        try
        {
            await _vcs.CreateBranchAsync(NewBranchName.Trim(), checkout: true);
            NewBranchName    = string.Empty;
            IsCreatingBranch = false;
            RequestClose?.Invoke(this, EventArgs.Empty);
        }
        finally { IsLoading = false; }
    }

    private void OnPropChanged([CallerMemberName] string? n = null) => OnPropertyChanged(n);
}
