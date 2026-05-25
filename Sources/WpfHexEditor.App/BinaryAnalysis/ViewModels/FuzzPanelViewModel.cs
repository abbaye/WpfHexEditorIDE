// ==========================================================
// Project: WpfHexEditor.App
// File: BinaryAnalysis/ViewModels/FuzzPanelViewModel.cs
// Description: P15 — ViewModel for the FuzzPanel document tab.
//              Exposes start/stop controls, generation parameters, a live
//              variant stream (ObservableCollection), and per-variant open-in-editor.
// Architecture: MVVM. FuzzRunnerService runs on background thread; results are
//              marshalled back via dispatcher-aware Progress<FuzzVariant>.
// ==========================================================

using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using WhfmtFuzz;
using WpfHexEditor.Core.Contracts;
using WpfHexEditor.Core.ViewModels;
using WpfHexEditor.App.BinaryAnalysis.Services;

namespace WpfHexEditor.App.BinaryAnalysis.ViewModels;

/// <summary>Row item in the FuzzPanel variants DataGrid.</summary>
public sealed class FuzzVariantItem : ViewModelBase
{
    private bool _isAnomaly;

    public int    Index         { get; }
    public string Strategy      { get; }
    public string Field         { get; }
    public string Description   { get; }
    public int    DataLength    { get; }
    public bool   IsError       { get; }
    public string? Error        { get; }
    public byte[] Data          { get; }

    /// <summary>True when the variant triggered an anomaly detection heuristic.</summary>
    public bool IsAnomaly
    {
        get => _isAnomaly;
        set => SetField(ref _isAnomaly, value);
    }

    public string AnomalyIcon => IsAnomaly ? "⚠" : string.Empty;

    public FuzzVariantItem(FuzzVariant v)
    {
        Index       = v.Index;
        Strategy    = v.Strategy;
        Field       = v.Field;
        Description = v.Description;
        DataLength  = v.Data.Length;
        IsError     = v.IsError;
        Error       = v.Error;
        Data        = v.Data;
    }
}

/// <summary>
/// P15 — ViewModel for the FuzzPanel IDE tab.
/// </summary>
public sealed class FuzzPanelViewModel : ViewModelBase
{
    private readonly FuzzRunnerService _runner = new();
    private CancellationTokenSource?   _cts;

    // ── Parameters ──────────────────────────────────────────────────────────
    private int     _iterations       = 20;
    private int?    _seed;
    private string? _forcedFormat;
    private int     _compoundMutations = 1;
    private string  _sourceFileName    = string.Empty;
    private byte[]  _sourceBytes       = [];

    public int     Iterations        { get => _iterations;        set => SetField(ref _iterations, value); }
    public int?    Seed              { get => _seed;              set => SetField(ref _seed, value); }
    public string? ForcedFormat      { get => _forcedFormat;      set => SetField(ref _forcedFormat, value); }
    public int     CompoundMutations { get => _compoundMutations; set => SetField(ref _compoundMutations, value); }

    // ── State ────────────────────────────────────────────────────────────────
    private bool   _isRunning;
    private string _statusMessage = "Ready. Load a file to start fuzzing.";
    private int    _progress;
    private int    _total;

    public bool   IsRunning      { get => _isRunning;      private set { SetField(ref _isRunning, value); RaiseCanExecute(); } }
    public string StatusMessage  { get => _statusMessage;  private set => SetField(ref _statusMessage, value); }
    public int    Progress       { get => _progress;       private set => SetField(ref _progress, value); }
    public int    Total          { get => _total;          private set => SetField(ref _total, value); }
    public bool   HasSource      => _sourceBytes.Length > 0;

    // ── Results ──────────────────────────────────────────────────────────────
    public ObservableCollection<FuzzVariantItem> Variants { get; } = [];

    // ── Commands ──────────────────────────────────────────────────────────────
    public ICommand StartCommand     { get; }
    public ICommand StopCommand      { get; }
    public ICommand ClearCommand     { get; }
    public ICommand OpenVariantCommand { get; }

    /// <summary>
    /// Fired when the user wants to open a variant's bytes in a new HexEditor tab.
    /// Subscribers receive (byte[] data, string suggestedName).
    /// </summary>
    public event EventHandler<(byte[] Data, string Name)>? OpenVariantRequested;

    public FuzzPanelViewModel()
    {
        StartCommand       = new FuzzRelayCommand(StartAsync, () => HasSource && !IsRunning);
        StopCommand        = new FuzzRelayCommand(Stop,       () => IsRunning);
        ClearCommand       = new FuzzRelayCommand(Clear,      () => !IsRunning);
        OpenVariantCommand = new FuzzRelayCommand<FuzzVariantItem>(OpenVariant);
    }

    // ── Load source ──────────────────────────────────────────────────────────

    /// <summary>Sets the file to fuzz. Call before starting.</summary>
    public void LoadSource(string fileName, byte[] bytes)
    {
        _sourceFileName = fileName;
        _sourceBytes    = bytes;
        Variants.Clear();
        StatusMessage   = $"Ready. Source: {fileName} ({bytes.Length:N0} bytes).";
        OnPropertyChanged(nameof(HasSource));
        RaiseCanExecute();
    }

    // ── Run ──────────────────────────────────────────────────────────────────

    private async void StartAsync()
    {
        if (!HasSource || IsRunning) return;

        _cts           = new CancellationTokenSource();
        IsRunning      = true;
        Progress       = 0;
        Total          = Iterations;
        StatusMessage  = $"Running {Iterations} iterations…";
        Variants.Clear();

        var catalog  = GetCatalog();
        if (catalog is null) { Stop(); StatusMessage = "Error: format catalog not available."; return; }

        var progressHandler = new Progress<FuzzVariant>(variant =>
        {
            var item = new FuzzVariantItem(variant);
            Variants.Add(item);
            Progress++;
            StatusMessage = $"Generating… {Progress}/{Total} variants";
        });

        try
        {
            await _runner.RunAsync(
                catalog:           catalog,
                fileBytes:         _sourceBytes,
                fileName:          _sourceFileName,
                totalIterations:   Iterations,
                seed:              Seed,
                forcedFormat:      ForcedFormat,
                compoundMutations: CompoundMutations,
                progress:          progressHandler,
                ct:                _cts.Token);

            int errors = Variants.Count(v => v.IsError);
            StatusMessage = $"Complete — {Variants.Count} variants, {errors} error(s).";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = $"Stopped — {Variants.Count} variant(s) generated.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsRunning = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    private void Stop()
    {
        _cts?.Cancel();
    }

    private void Clear()
    {
        if (IsRunning) return;
        Variants.Clear();
        Progress      = 0;
        StatusMessage = "Cleared.";
    }

    private void OpenVariant(FuzzVariantItem? item)
    {
        if (item is null || item.Data.Length == 0) return;
        string name = $"fuzz_{item.Index:D4}_{item.Strategy}.bin";
        OpenVariantRequested?.Invoke(this, (item.Data, name));
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private void RaiseCanExecute() =>
        System.Windows.Input.CommandManager.InvalidateRequerySuggested();

    /// <summary>
    /// Returns the embedded format catalog. Can be replaced via <see cref="CatalogOverride"/>
    /// for testing or IDE-host injection.
    /// </summary>
    private IEmbeddedFormatCatalog? GetCatalog() =>
        CatalogOverride ?? WpfHexEditor.Core.Definitions.EmbeddedFormatCatalog.Instance;

    /// <summary>Optional catalog override injected by the IDE host or tests.</summary>
    public IEmbeddedFormatCatalog? CatalogOverride { get; set; }
}

// ── Minimal relay command helpers ────────────────────────────────────────────

file sealed class FuzzRelayCommand(Action execute, Func<bool>? canExecute = null) : ICommand
{
    public event EventHandler? CanExecuteChanged
    {
        add    => System.Windows.Input.CommandManager.RequerySuggested += value;
        remove => System.Windows.Input.CommandManager.RequerySuggested -= value;
    }
    public bool CanExecute(object? _) => canExecute?.Invoke() ?? true;
    public void Execute(object? _)    => execute();
}

file sealed class FuzzRelayCommand<T>(Action<T?> execute) : ICommand
{
    public event EventHandler? CanExecuteChanged { add { } remove { } }
    public bool CanExecute(object? _) => true;
    public void Execute(object? p)    => execute(p is T t ? t : default);
}
