//////////////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Project: WpfHexEditor.Editor.StructureEditor
// File: ViewModels/TestTabViewModel.cs
// Description: VM for the Test Panel tab — drives file selection, interpreter
//              invocation, result binding, and status-filter toggling.
//////////////////////////////////////////////////////

using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using Microsoft.Win32;
using WpfHexEditor.Core.ViewModels;
using WpfHexEditor.Editor.StructureEditor.Properties;
using WpfHexEditor.Editor.StructureEditor.Services;

namespace WpfHexEditor.Editor.StructureEditor.ViewModels;

internal sealed class TestTabViewModel : ViewModelBase
{
    // ── State ─────────────────────────────────────────────────────────────────

    private string _filePath   = "";
    private bool   _isBusy;
    private string _summary    = "";
    private int    _okCount;
    private int    _warnCount;
    private int    _errorCount;
    private int    _skipCount;

    // Filter toggles
    private bool _showOk      = true;
    private bool _showWarning = true;
    private bool _showError   = true;
    private bool _showSkipped = true;

    public string FilePath
    {
        get => _filePath;
        set => SetField(ref _filePath, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetField(ref _isBusy, value);
    }

    public string Summary
    {
        get => _summary;
        private set => SetField(ref _summary, value);
    }

    public int OkCount    { get => _okCount;    private set => SetField(ref _okCount, value); }
    public int WarnCount  { get => _warnCount;  private set => SetField(ref _warnCount, value); }
    public int ErrorCount { get => _errorCount; private set => SetField(ref _errorCount, value); }
    public int SkipCount  { get => _skipCount;  private set => SetField(ref _skipCount, value); }

    // ── Filter properties ─────────────────────────────────────────────────────

    public bool ShowOk
    {
        get => _showOk;
        set { if (SetField(ref _showOk, value)) ApplyFilter(); }
    }

    public bool ShowWarning
    {
        get => _showWarning;
        set { if (SetField(ref _showWarning, value)) ApplyFilter(); }
    }

    public bool ShowError
    {
        get => _showError;
        set { if (SetField(ref _showError, value)) ApplyFilter(); }
    }

    public bool ShowSkipped
    {
        get => _showSkipped;
        set { if (SetField(ref _showSkipped, value)) ApplyFilter(); }
    }

    // ── Collections ───────────────────────────────────────────────────────────

    /// <summary>Raw results from the last run (unfiltered source of truth).</summary>
    private List<BlockTestResult> _allResults = [];

    /// <summary>Filtered view bound to the DataGrid.</summary>
    public ObservableCollection<BlockTestResult> Results { get; } = [];

    // ── Commands ──────────────────────────────────────────────────────────────

    public ICommand BrowseCommand { get; }
    public ICommand RunCommand    { get; }

    // ── Constructor ───────────────────────────────────────────────────────────

    public TestTabViewModel()
    {
        BrowseCommand = new RelayCommand(BrowseFile);
        RunCommand    = new RelayCommand(
            () => { /* invoked via code-behind */ },
            () => !string.IsNullOrEmpty(FilePath) && !IsBusy);
    }

    // ── File browse ───────────────────────────────────────────────────────────

    private void BrowseFile()
    {
        var dlg = new OpenFileDialog
        {
            Title       = StructureEditorResources.StructureEditor_TestBrowseDialogTitle,
            Filter      = "All files (*.*)|*.*",
            Multiselect = false,
        };
        if (dlg.ShowDialog() == true)
            FilePath = dlg.FileName;
    }

    // ── Run test ──────────────────────────────────────────────────────────────

    internal async Task RunAsync(WpfHexEditor.Core.FormatDetection.FormatDefinition def)
    {
        if (string.IsNullOrEmpty(FilePath) || IsBusy) return;

        IsBusy  = true;
        Summary = "Running…";
        Results.Clear();
        _allResults = [];
        OkCount = WarnCount = ErrorCount = SkipCount = 0;

        try
        {
            var bytes   = await File.ReadAllBytesAsync(FilePath);
            var interp  = new SimpleBlockInterpreter(bytes);
            var results = await Task.Run(() => interp.Run(def));

            _allResults = results;

            OkCount    = results.Count(r => r.Status == "OK");
            WarnCount  = results.Count(r => r.Status == "Warning");
            ErrorCount = results.Count(r => r.Status == "Error");
            SkipCount  = results.Count(r => r.Status == "Skipped");

            ApplyFilter();

            Summary = ErrorCount > 0
                ? $"{ErrorCount} error(s), {WarnCount} warning(s), {SkipCount} skipped — {OkCount} OK"
                : WarnCount > 0
                    ? $"Passed with {WarnCount} warning(s) — {OkCount} OK, {SkipCount} skipped"
                    : $"All {OkCount} block(s) OK — {SkipCount} complex block(s) skipped";
        }
        catch (Exception ex)
        {
            Summary = $"Error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ── Filter ────────────────────────────────────────────────────────────────

    private void ApplyFilter()
    {
        Results.Clear();
        foreach (var r in _allResults)
        {
            if (r.Status == "OK"      && !_showOk)      continue;
            if (r.Status == "Warning" && !_showWarning) continue;
            if (r.Status == "Error"   && !_showError)   continue;
            if (r.Status == "Skipped" && !_showSkipped) continue;
            Results.Add(r);
        }
    }
}
