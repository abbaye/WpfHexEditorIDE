//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Collections.ObjectModel;
using System.Windows.Input;
using WpfHexEditor.App.BinaryAnalysis.Services;
using WpfHexEditor.Core.ViewModels;
using WpfHexEditor.SDK.Contracts;

namespace WpfHexEditor.App.BinaryAnalysis.ViewModels;

/// <summary>
/// View-model for the PE Import/Export Table Analyzer panel (#114).
/// Drives four tabs: Imports, Exports, Sections, Headers.
/// </summary>
public sealed class PeAnalyzerViewModel : ViewModelBase
{
    private IIDEHostContext?  _context;
    private CancellationTokenSource? _cts;

    private bool   _isBusy;
    private string _statusText    = string.Empty;
    private string _filterText    = string.Empty;
    private string _architecture  = string.Empty;
    private string _fileName      = string.Empty;
    private bool   _isPeFile;
    private PeHeader? _header;

    // -- Observable collections bound to tabs --------------------------------

    public ObservableCollection<ImportModule>  Imports  { get; } = [];
    public ObservableCollection<ExportEntry>   Exports  { get; } = [];
    public ObservableCollection<PeSection>     Sections { get; } = [];
    public ObservableCollection<HeaderRow>     Headers  { get; } = [];

    // Filtered views updated by FilterText
    public ObservableCollection<ImportModule>  FilteredImports { get; } = [];
    public ObservableCollection<ExportEntry>   FilteredExports { get; } = [];

    // -- Bindable properties -------------------------------------------------

    public bool IsBusy
    {
        get => _isBusy;
        private set { _isBusy = value; OnPropertyChanged(); }
    }

    public string StatusText
    {
        get => _statusText;
        private set { _statusText = value; OnPropertyChanged(); }
    }

    public string FilterText
    {
        get => _filterText;
        set { _filterText = value; OnPropertyChanged(); ApplyFilter(); }
    }

    public string Architecture
    {
        get => _architecture;
        private set { _architecture = value; OnPropertyChanged(); }
    }

    public string FileName
    {
        get => _fileName;
        private set { _fileName = value; OnPropertyChanged(); }
    }

    public bool IsPeFile
    {
        get => _isPeFile;
        private set { _isPeFile = value; OnPropertyChanged(); }
    }

    // -- Commands ------------------------------------------------------------

    public ICommand ScanCommand   { get; }
    public ICommand CancelCommand { get; }

    // Jump-to-offset is wired directly from the panel (needs UI element reference).
    public Action<long>? RequestJumpToOffset { get; set; }

    // -- Construction --------------------------------------------------------

    public PeAnalyzerViewModel()
    {
        ScanCommand   = new RelayCommand(async () => await ScanAsync(), () => !IsBusy);
        CancelCommand = new RelayCommand(() => { _cts?.Cancel(); return Task.CompletedTask; }, () => IsBusy);
    }

    public void SetContext(IIDEHostContext ctx) => _context = ctx;

    // -- Scan ----------------------------------------------------------------

    public async Task ScanAsync()
    {
        if (_context is null || IsBusy || !_context.HexEditor.IsActive) return;

        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        IsBusy = true;
        ClearAll();

        FileName = System.IO.Path.GetFileName(_context.HexEditor.CurrentFilePath ?? "");
        StatusText = "Analysing PE structure…";

        try
        {
            var result = await Task.Run(() =>
            {
                using var stream = new HexEditorStream(_context.HexEditor);
                return PeFileAnalyzer.TryAnalyze(stream);
            }, _cts.Token);

            if (result is null)
            {
                IsPeFile   = false;
                StatusText = "Not a PE file.";
                return;
            }

            IsPeFile      = true;
            Architecture  = result.Is64Bit ? "PE64 (x64)" : "PE32 (x86)";

            PopulateImports(result.Imports);
            PopulateExports(result.Exports);
            PopulateSections(result.Sections);
            PopulateHeaders(result.Header);
            ApplyFilter();

            StatusText = $"{result.Imports.Sum(m => m.Functions.Count)} imports · " +
                         $"{result.Exports.Count} exports · " +
                         $"{result.Sections.Count} sections";
        }
        catch (OperationCanceledException) { StatusText = "Cancelled."; }
        catch (Exception ex)               { StatusText = $"Error: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    public void Cancel() => _cts?.Cancel();

    // -- Population ----------------------------------------------------------

    private void PopulateImports(IReadOnlyList<ImportModule> modules)
    {
        foreach (var m in modules)
            Imports.Add(m);
    }

    private void PopulateExports(IReadOnlyList<ExportEntry> entries)
    {
        foreach (var e in entries)
            Exports.Add(e);
    }

    private void PopulateSections(IReadOnlyList<PeSection> sections)
    {
        foreach (var s in sections)
            Sections.Add(s);
    }

    private void PopulateHeaders(PeHeader h)
    {
        Headers.Add(new HeaderRow("Architecture",       h.Is64Bit ? "PE64" : "PE32"));
        Headers.Add(new HeaderRow("Machine",            h.Machine));
        Headers.Add(new HeaderRow("Entry Point RVA",    $"0x{h.EntryPointRva:X8}"));
        Headers.Add(new HeaderRow("Image Base",         $"0x{h.ImageBase:X16}"));
        Headers.Add(new HeaderRow("Size of Image",      $"0x{h.SizeOfImage:X8}  ({h.SizeOfImage:N0} bytes)"));
        Headers.Add(new HeaderRow("Size of Headers",    $"0x{h.SizeOfHeaders:X8}"));
        Headers.Add(new HeaderRow("Subsystem",          h.Subsystem));
        Headers.Add(new HeaderRow("Compile Timestamp",  h.TimeDateStamp.ToString("yyyy-MM-dd HH:mm:ss UTC")));
        Headers.Add(new HeaderRow("Number of Sections", h.NumberOfSections.ToString()));
        Headers.Add(new HeaderRow("Data Directories",   h.NumberOfRvaAndSizes.ToString()));
    }

    // -- Filter --------------------------------------------------------------

    private void ApplyFilter()
    {
        FilteredImports.Clear();
        FilteredExports.Clear();

        bool noFilter = string.IsNullOrWhiteSpace(_filterText);

        foreach (var m in Imports)
        {
            if (noFilter || m.Dll.Contains(_filterText, StringComparison.OrdinalIgnoreCase) ||
                m.Functions.Any(f => f.Name.Contains(_filterText, StringComparison.OrdinalIgnoreCase)))
                FilteredImports.Add(m);
        }

        foreach (var e in Exports)
        {
            if (noFilter || e.Name.Contains(_filterText, StringComparison.OrdinalIgnoreCase))
                FilteredExports.Add(e);
        }
    }

    // -- Helpers -------------------------------------------------------------

    private void ClearAll()
    {
        Imports.Clear(); Exports.Clear(); Sections.Clear();
        Headers.Clear(); FilteredImports.Clear(); FilteredExports.Clear();
        Architecture = string.Empty;
    }

    public void OnFileOpened()
    {
        ClearAll();
        StatusText = string.Empty;
        IsPeFile   = false;
    }
}

/// <summary>Key/value row for the Headers tab.</summary>
public sealed record HeaderRow(string Key, string Value);

/// <summary>Minimal relay command implementation.</summary>
file sealed class RelayCommand(Func<Task> executeAsync, Func<bool>? canExecute = null) : ICommand
{
    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? p) => canExecute?.Invoke() ?? true;
    public async void Execute(object? p) => await executeAsync();
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
