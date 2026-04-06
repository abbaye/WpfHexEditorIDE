// ==========================================================
// Project: WpfHexEditor.Plugins.SynalysisGrammar
// File: ViewModels/GrammarSelectorViewModel.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     ViewModel for GrammarSelectorPanel. Manages the list of available grammars,
//     selection state, search filter, and triggers grammar application.
//     Exposes ClearGrammarCommand, RefreshCommand, RemoveGrammarCommand and
//     the StructureNodes tree used by the Structure tab TreeView.
//
// Architecture Notes:
//     Pattern: MVVM
//     - Raises PropertyChanged for all observable properties.
//     - ApplyGrammarCommand delegates to the injected apply callback.
//     - ClearGrammarCommand calls the injected clearOverlay callback.
//     - StructureNodes are rebuilt via RefreshStructureNodes() whenever
//       SelectedGrammar changes.
// ==========================================================

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using WpfHexEditor.Core.SynalysisGrammar;
using WpfHexEditor.SDK.Commands;
using WpfHexEditor.Core.ViewModels;

namespace WpfHexEditor.Plugins.SynalysisGrammar.ViewModels;

/// <summary>
/// ViewModel for the Grammar Explorer dockable panel.
/// </summary>
public sealed class GrammarSelectorViewModel : ViewModelBase
{
    private readonly SynalysisGrammarRepository       _repository;
    private readonly Action<GrammarEntryViewModel>    _applyCallback;
    private readonly Action                            _loadFromDiskCallback;
    private readonly Action                            _clearOverlayCallback;

    private string                _searchText      = string.Empty;
    private GrammarEntryViewModel? _selectedGrammar;
    private bool                  _isAutoApply     = true;
    private string                _statusText      = string.Empty;
    private bool                  _isStructureExpanded = true;

    // -- Constructor -------------------------------------------------------

    public GrammarSelectorViewModel(
        SynalysisGrammarRepository repository,
        Action<GrammarEntryViewModel> applyCallback,
        Action loadFromDiskCallback,
        Action clearOverlayCallback)
    {
        _repository           = repository          ?? throw new ArgumentNullException(nameof(repository));
        _applyCallback        = applyCallback       ?? throw new ArgumentNullException(nameof(applyCallback));
        _loadFromDiskCallback = loadFromDiskCallback ?? throw new ArgumentNullException(nameof(loadFromDiskCallback));
        _clearOverlayCallback = clearOverlayCallback ?? throw new ArgumentNullException(nameof(clearOverlayCallback));

        ApplyGrammarCommand   = new RelayCommand(_ => ApplySelected(),     _ => SelectedGrammar is not null);
        LoadFromDiskCommand   = new RelayCommand(_ => _loadFromDiskCallback());
        ClearSelectionCommand = new RelayCommand(_ => SelectedGrammar = null);
        ClearGrammarCommand   = new RelayCommand(_ => _clearOverlayCallback());
        RefreshCommand        = new RelayCommand(_ => Reload());
        RemoveGrammarCommand  = new RelayCommand(_ => RemoveSelected(),    _ => SelectedGrammar?.Source == GrammarSource.Disk);
    }

    // -- Observable properties ---------------------------------------------

    /// <summary>Flat list of all grammar entries (after search filter).</summary>
    public ObservableCollection<GrammarEntryViewModel> FilteredGrammars { get; } = [];

    /// <summary>Tree nodes for the Structure tab, rebuilt on each selection change.</summary>
    public ObservableCollection<GrammarStructureNodeViewModel> StructureNodes { get; } = [];

    /// <summary>Currently selected grammar entry, or null.</summary>
    public GrammarEntryViewModel? SelectedGrammar
    {
        get => _selectedGrammar;
        set
        {
            _selectedGrammar = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSelection));
            OnPropertyChanged(nameof(SelectedDescription));
            OnPropertyChanged(nameof(SelectedInfo));
            RefreshStructureNodes();
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        }
    }

    /// <summary>Text filter applied to the grammar list.</summary>
    public string SearchText
    {
        get => _searchText;
        set
        {
            _searchText = value;
            OnPropertyChanged();
            RefreshFilter();
        }
    }

    /// <summary>When true the plugin applies grammars automatically on file open.</summary>
    public bool IsAutoApply
    {
        get => _isAutoApply;
        set { _isAutoApply = value; OnPropertyChanged(); }
    }

    /// <summary>Status message shown in the panel footer.</summary>
    public string StatusText
    {
        get => _statusText;
        set { _statusText = value; OnPropertyChanged(); }
    }

    /// <summary>Controls the default expanded state of the structure TreeView.</summary>
    public bool IsStructureExpanded
    {
        get => _isStructureExpanded;
        set { _isStructureExpanded = value; OnPropertyChanged(); }
    }

    public bool HasSelection => _selectedGrammar is not null;

    public string SelectedDescription =>
        _selectedGrammar?.Description ?? "Select a grammar to see its description.";

    public string SelectedInfo =>
        _selectedGrammar is null
            ? string.Empty
            : $"Author: {_selectedGrammar.Author}  |  Extensions: {_selectedGrammar.ExtensionsDisplay}  |  Source: {_selectedGrammar.SourceLabel}";

    // -- Commands ----------------------------------------------------------

    public ICommand ApplyGrammarCommand   { get; }
    public ICommand LoadFromDiskCommand   { get; }
    public ICommand ClearSelectionCommand { get; }
    public ICommand ClearGrammarCommand   { get; }
    public ICommand RefreshCommand        { get; }
    public ICommand RemoveGrammarCommand  { get; }

    // -- Public methods ----------------------------------------------------

    /// <summary>
    /// Rebuilds the full grammar list from the repository and applies the current filter.
    /// Call this after registering new grammars (embedded or from disk/plugin).
    /// </summary>
    public void Reload()
    {
        _allEntries.Clear();

        foreach (var key in _repository.GetAllKeys())
        {
            // GetByKey triggers lazy load; GetParsedGrammar reads cache only.
            var grammar = _repository.GetByKey(key);
            if (grammar is null) continue;

            var source = key.Contains("FormatDefinitions")                               ? GrammarSource.Embedded
                       : key.StartsWith("plugin:", StringComparison.OrdinalIgnoreCase)   ? GrammarSource.Plugin
                       : GrammarSource.Disk;

            _allEntries.Add(new GrammarEntryViewModel
            {
                Key               = key,
                Name              = grammar.Grammar.Name,
                Author            = grammar.Grammar.Author,
                ExtensionsDisplay = string.Join(", ", grammar.Grammar.FileExtensions),
                Description       = grammar.Grammar.Description,
                Source            = source,
                ParsedGrammar     = grammar,
            });
        }

        RefreshFilter();
        StatusText = $"{_allEntries.Count} grammar(s) available.";
    }

    // -- Internal ----------------------------------------------------------

    private readonly List<GrammarEntryViewModel> _allEntries = [];

    private void RefreshFilter()
    {
        FilteredGrammars.Clear();

        var query = _allEntries.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(_searchText))
        {
            var lower = _searchText.Trim().ToLowerInvariant();
            query = query.Where(e =>
                e.Name.Contains(lower, StringComparison.OrdinalIgnoreCase) ||
                e.ExtensionsDisplay.Contains(lower, StringComparison.OrdinalIgnoreCase) ||
                e.Description.Contains(lower, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var entry in query.OrderBy(e => e.Source).ThenBy(e => e.Name))
            FilteredGrammars.Add(entry);
    }

    private void RefreshStructureNodes()
    {
        StructureNodes.Clear();

        if (_selectedGrammar?.ParsedGrammar is not { } root) return;

        foreach (var node in GrammarStructureNodeViewModel.FromGrammar(root.Grammar))
            StructureNodes.Add(node);
    }

    private void ApplySelected()
    {
        if (_selectedGrammar is null) return;
        _applyCallback(_selectedGrammar);
    }

    private void RemoveSelected()
    {
        if (_selectedGrammar is null || _selectedGrammar.Source != GrammarSource.Disk) return;

        // Remove from the internal list and re-filter.
        _allEntries.RemoveAll(e => e.Key == _selectedGrammar.Key);
        SelectedGrammar = null;
        RefreshFilter();
        StatusText = $"{_allEntries.Count} grammar(s) available.";
    }

    // -- INotifyPropertyChanged --------------------------------------------


}
