// ==========================================================
// Project: WpfHexEditor.Plugins.AssemblyExplorer
// File: ViewModels/AssemblySearchViewModel.cs
// Author: Derek Tremblay
// Created: 2026-03-16
// Description:
//     ViewModel for the cross-assembly search panel (Phase 3).
//     Exposes a debounced search (300ms) across all loaded assemblies
//     using AssemblySearchService. Results link to tree navigation
using WpfHexEditor.Core.ViewModels;
//     and hex editor offset jumping.
//
// Architecture Notes:
//     Pattern: MVVM with DispatcherTimer debounce.
//     Search runs on Task.Run; results marshalled to UI thread via
//     Application.Current.Dispatcher.
//     AssemblyExplorerViewModel is referenced for workspace access
//     and OpenMemberInHexEditorAsync delegation.
// ==========================================================

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using WpfHexEditor.Core.AssemblyAnalysis.Models;
using WpfHexEditor.Core.AssemblyAnalysis.Services;
using WpfHexEditor.SDK.Commands;

namespace WpfHexEditor.Plugins.AssemblyExplorer.ViewModels;

/// <summary>
/// Represents a single result row in the cross-assembly search panel.
/// </summary>
public sealed class AssemblySearchResultViewModel : ViewModelBase
{
    private readonly AssemblyExplorerViewModel _explorerVm;

    public AssemblySearchResultViewModel(
        AssemblySearchResult       result,
        AssemblyExplorerViewModel  explorerVm)
    {
        Result     = result;
        _explorerVm = explorerVm;

        NavigateCommand = new RelayCommand(_ => Navigate());
    }

    public AssemblySearchResult Result { get; }

    public string AssemblyName   => Result.Assembly.Name ?? string.Empty;
    public string Namespace      => ExtractNamespace(Result.TypeFullName);
    public string TypeName       => ExtractTypeName(Result.TypeFullName);
    public string MemberName     => Result.MemberName ?? string.Empty;
    public string Kind           => Result.MemberKind.HasValue ? Result.MemberKind.Value.ToString() : "Type";
    public string Token          => Result.MetadataToken != 0 ? $"0x{Result.MetadataToken:X8}" : string.Empty;
    public string Offset         => Result.PeOffset > 0 ? $"0x{Result.PeOffset:X}" : string.Empty;
    public string DisplayName    => Result.DisplayName ?? Result.TypeFullName;

    public ICommand NavigateCommand { get; }


    private void Navigate()
    {
        // Find the matching node in the tree and open in hex editor.
        var filePath = Result.Assembly.FilePath;
        if (string.IsNullOrEmpty(filePath)) return;

        // Locate tree node by MetadataToken + OwnerFilePath.
        var node = _explorerVm.FindNodeByToken(Result.MetadataToken, filePath);
        if (node is not null)
        {
            _explorerVm.SelectedNode = node;
            _ = _explorerVm.OpenMemberInHexEditorAsync(node);
        }
    }

    private static string ExtractNamespace(string? fullName)
    {
        if (string.IsNullOrEmpty(fullName)) return string.Empty;
        var dot = fullName.LastIndexOf('.');
        return dot > 0 ? fullName[..dot] : string.Empty;
    }

    private static string ExtractTypeName(string? fullName)
    {
        if (string.IsNullOrEmpty(fullName)) return fullName ?? string.Empty;
        var dot = fullName.LastIndexOf('.');
        return dot >= 0 ? fullName[(dot + 1)..] : fullName;
    }
}

/// <summary>
/// ViewModel for the cross-assembly search panel.
/// Searches all loaded assemblies simultaneously with a debounced query.
/// </summary>
public sealed class AssemblySearchViewModel : ViewModelBase
{
    private readonly AssemblyExplorerViewModel _explorerVm;
    private readonly DispatcherTimer           _debounceTimer;
    private bool                               _isSearching;
    private string                             _searchText       = string.Empty;
    private string                             _selectedKind     = "All";
    private bool                               _publicOnly       = false;
    private string                             _statusText       = string.Empty;
    private bool                               _includeMembers   = true;

    // ── Constructor ───────────────────────────────────────────────────────────

    public AssemblySearchViewModel(AssemblyExplorerViewModel explorerVm)
    {
        _explorerVm = explorerVm;

        Results = [];

        _debounceTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(300)
        };
        _debounceTimer.Tick += (_, _) =>
        {
            _debounceTimer.Stop();
            _ = RunSearchAsync();
        };

        SearchCommand = new RelayCommand(_ => _ = RunSearchAsync());
        ClearCommand  = new RelayCommand(_ => Clear());
    }

    // ── INotifyPropertyChanged ────────────────────────────────────────────────



    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }

    // ── Bindable properties ───────────────────────────────────────────────────

    public ObservableCollection<AssemblySearchResultViewModel> Results { get; }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (!SetField(ref _searchText, value)) return;
            // Restart debounce on every keystroke.
            _debounceTimer.Stop();
            _debounceTimer.Start();
        }
    }

    public string SelectedKind
    {
        get => _selectedKind;
        set => SetField(ref _selectedKind, value);
    }

    public bool PublicOnly
    {
        get => _publicOnly;
        set => SetField(ref _publicOnly, value);
    }

    public bool IncludeMembers
    {
        get => _includeMembers;
        set => SetField(ref _includeMembers, value);
    }

    public bool IsSearching
    {
        get => _isSearching;
        private set => SetField(ref _isSearching, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetField(ref _statusText, value);
    }

    /// <summary>Available kind filter values for the ComboBox.</summary>
    public IReadOnlyList<string> KindOptions { get; } =
        ["All", "Class", "Interface", "Struct", "Enum", "Delegate",
         "Method", "Field", "Property", "Event"];

    // ── Commands ──────────────────────────────────────────────────────────────

    public ICommand SearchCommand { get; }
    public ICommand ClearCommand  { get; }

    // ── Search logic ──────────────────────────────────────────────────────────

    private async Task RunSearchAsync()
    {
        if (string.IsNullOrWhiteSpace(_searchText))
        {
            Clear();
            return;
        }

        IsSearching = true;
        StatusText  = "Searchingâ€¦";

        var assemblies = _explorerVm.GetLoadedAssemblyModels();
        if (assemblies.Count == 0)
        {
            Clear();
            StatusText  = "No assemblies loaded.";
            IsSearching = false;
            return;
        }

        var query = BuildQuery();

        IReadOnlyList<AssemblySearchResult> results;
        try
        {
            results = await Task.Run(() => AssemblySearchService.Search(assemblies, query));
        }
        catch (Exception ex)
        {
            StatusText  = $"Search failed: {ex.Message}";
            IsSearching = false;
            return;
        }

        // Marshal to UI thread.
        Application.Current.Dispatcher.Invoke(() =>
        {
            Results.Clear();
            foreach (var r in results)
                Results.Add(new AssemblySearchResultViewModel(r, _explorerVm));

            StatusText = results.Count == 0
                ? "No results found."
                : $"{results.Count} result{(results.Count == 1 ? "" : "s")} across {assemblies.Count} assembl{(assemblies.Count == 1 ? "y" : "ies")}.";

            IsSearching = false;
        });
    }

    private AssemblySearchQuery BuildQuery()
    {
        TypeKind? kind = null;
        if (_selectedKind != "All" && Enum.TryParse<TypeKind>(_selectedKind, out var tk))
            kind = tk;

        return new AssemblySearchQuery
        {
            NameContains   = _searchText,
            IncludeMembers = _includeMembers,
            IsPublic       = _publicOnly ? true : null,
            Kind           = kind
        };
    }

    private void Clear()
    {
        _debounceTimer.Stop();
        Results.Clear();
        StatusText = string.Empty;
    }
}
