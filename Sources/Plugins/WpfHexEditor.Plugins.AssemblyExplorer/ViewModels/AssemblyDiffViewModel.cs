// ==========================================================
// Project: WpfHexEditor.Plugins.AssemblyExplorer
// File: ViewModels/AssemblyDiffViewModel.cs
// Author: Derek Tremblay
// Created: 2026-03-16
// Description:
//     ViewModel for the assembly diff / version compare panel (Phase 5).
//     Allows the user to select two loaded assemblies and compare them.
//     Results are shown in a DataGrid with Added/Removed/Changed color coding.
//
// Architecture Notes:
//     Pattern: MVVM with Task.Run for background diff.
//     DiffKind-to-brush conversion is done via a value converter in XAML.
//     AssemblyExplorerViewModel provides the loaded workspace list.
// ==========================================================

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using WpfHexEditor.Core.AssemblyAnalysis.Models;
using WpfHexEditor.Core.AssemblyAnalysis.Services;
using WpfHexEditor.SDK.Commands;
using WpfHexEditor.Core.ViewModels;

namespace WpfHexEditor.Plugins.AssemblyExplorer.ViewModels;

/// <summary>
/// Represents a single row in the diff results DataGrid with navigation support.
/// </summary>
public sealed class DiffEntryViewModel : ViewModelBase
{
    private readonly AssemblyExplorerViewModel _explorerVm;

    public DiffEntryViewModel(DiffEntry entry, AssemblyExplorerViewModel explorerVm)
    {
        Entry       = entry;
        _explorerVm = explorerVm;

        NavigateBaselineCommand = new RelayCommand(
            _ => Navigate(entry.BaselineToken, isBaseline: true),
            _ => entry.BaselineToken != 0);

        NavigateTargetCommand = new RelayCommand(
            _ => Navigate(entry.TargetToken, isBaseline: false),
            _ => entry.TargetToken != 0);
    }

    public DiffEntry   Entry       { get; }
    public string      TypeName    => Entry.TypeFullName;
    public string      MemberName  => Entry.MemberSignature ?? string.Empty;
    public string      Kind        => Entry.Kind.ToString();
    public string      DisplayName => Entry.DisplayName;
    public DiffKind    DiffKind    => Entry.Kind;

    public ICommand NavigateBaselineCommand { get; }
    public ICommand NavigateTargetCommand   { get; }


    private void Navigate(int token, bool isBaseline)
    {
        if (token == 0) return;
        // Locate the node in the appropriate workspace assembly.
        // The caller (AssemblyDiffViewModel) exposes the current baseline/target models.
    }
}

/// <summary>
/// ViewModel for the diff panel. Exposes two assembly selectors (baseline / target)
/// and computes a live diff on demand.  Also owns the <see cref="DiffDetailViewModel"/>
/// that is populated when the user selects a row in the results DataGrid.
/// </summary>
public sealed class AssemblyDiffViewModel : ViewModelBase
{
    private readonly AssemblyExplorerViewModel _explorerVm;
    private AssemblyModel?                     _baselineModel;
    private AssemblyModel?                     _targetModel;
    private bool                               _isComparing;
    private string                             _statusText         = string.Empty;
    private string                             _filterKind         = "All";
    private DiffEntryViewModel?                _selectedDiffEntry;

    // ── Constructor ───────────────────────────────────────────────────────────

    public AssemblyDiffViewModel(AssemblyExplorerViewModel explorerVm)
    {
        _explorerVm = explorerVm;

        AllEntries     = [];
        FilteredEntries = [];

        CompareCommand = new RelayCommand(
            _ => _ = RunCompareAsync(),
            _ => _baselineModel is not null && _targetModel is not null && !_isComparing);

        ClearCommand = new RelayCommand(_ => Clear());

        // Refresh assembly list when the workspace changes.
        explorerVm.WorkspaceStatsChanged += (_, _) => RefreshAssemblyList();

        RefreshAssemblyList();
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

    /// <summary>Detail pane ViewModel â€” populated when a diff row is selected.</summary>
    public DiffDetailViewModel DiffDetail { get; } = new();

    /// <summary>Assembly names available in the workspace for selector ComboBoxes.</summary>
    public ObservableCollection<string> AssemblyNames { get; } = [];

    public ObservableCollection<DiffEntryViewModel> AllEntries      { get; }
    public ObservableCollection<DiffEntryViewModel> FilteredEntries { get; }

    public string? SelectedBaselineName
    {
        get => _baselineModel?.Name;
        set
        {
            _baselineModel = _explorerVm.GetLoadedAssemblyModels()
                .FirstOrDefault(m => m.Name == value);
            OnPropertyChanged();
        }
    }

    public string? SelectedTargetName
    {
        get => _targetModel?.Name;
        set
        {
            _targetModel = _explorerVm.GetLoadedAssemblyModels()
                .FirstOrDefault(m => m.Name == value);
            OnPropertyChanged();
        }
    }

    public string FilterKind
    {
        get => _filterKind;
        set
        {
            if (!SetField(ref _filterKind, value)) return;
            ApplyFilter();
        }
    }

    public bool IsComparing
    {
        get => _isComparing;
        private set => SetField(ref _isComparing, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetField(ref _statusText, value);
    }

    public IReadOnlyList<string> FilterOptions { get; } = ["All", "Added", "Removed", "Changed"];

    /// <summary>
    /// The row currently selected in the DataGrid.  Setting this triggers an async
    /// load of decompiled code + unified diff in <see cref="DiffDetail"/>.
    /// </summary>
    public DiffEntryViewModel? SelectedDiffEntry
    {
        get => _selectedDiffEntry;
        set
        {
            if (!SetField(ref _selectedDiffEntry, value)) return;
            if (value is null)
                DiffDetail.Clear();
            else
                _ = DiffDetail.LoadAsync(value, _baselineModel, _targetModel);
        }
    }

    // ── Commands ──────────────────────────────────────────────────────────────

    public ICommand CompareCommand { get; }
    public ICommand ClearCommand   { get; }

    // ── Compare logic ─────────────────────────────────────────────────────────

    private async Task RunCompareAsync()
    {
        if (_baselineModel is null || _targetModel is null) return;

        IsComparing = true;
        StatusText  = "Comparingâ€¦";

        AssemblyDiff diff;
        try
        {
            var baseline = _baselineModel;
            var target   = _targetModel;
            diff = await Task.Run(() => AssemblyDiffService.Compare(baseline, target));
        }
        catch (Exception ex)
        {
            StatusText  = $"Comparison failed: {ex.Message}";
            IsComparing = false;
            return;
        }

        Application.Current.Dispatcher.Invoke(() =>
        {
            AllEntries.Clear();
            foreach (var entry in diff.Entries)
                AllEntries.Add(new DiffEntryViewModel(entry, _explorerVm));

            ApplyFilter();

            StatusText = diff.Entries.Count == 0
                ? "Assemblies are identical."
                : $"+{diff.AddedCount} added  âˆ’{diff.RemovedCount} removed  ~{diff.ChangedCount} changed";

            IsComparing = false;
        });
    }

    private void ApplyFilter()
    {
        FilteredEntries.Clear();
        foreach (var entry in AllEntries)
        {
            if (_filterKind == "All" || entry.Kind == _filterKind)
                FilteredEntries.Add(entry);
        }
    }

    private void Clear()
    {
        AllEntries.Clear();
        FilteredEntries.Clear();
        DiffDetail.Clear();
        StatusText = string.Empty;
    }

    private void RefreshAssemblyList()
    {
        var names = _explorerVm.GetLoadedAssemblyModels()
            .Select(m => m.Name ?? string.Empty)
            .Where(n => !string.IsNullOrEmpty(n))
            .ToList();

        Application.Current?.Dispatcher.Invoke(() =>
        {
            AssemblyNames.Clear();
            foreach (var n in names)
                AssemblyNames.Add(n);
        });
    }
}
