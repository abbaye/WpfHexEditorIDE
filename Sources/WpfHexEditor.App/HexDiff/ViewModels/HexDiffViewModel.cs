// Project      : WpfHexEditor.App
// File         : HexDiff/ViewModels/HexDiffViewModel.cs
// Description  : VM for the Hex Diff panel.
// Architecture : Thin VM; delegates diff logic to FileDiffService.

using System.Collections.ObjectModel;
using System.IO;
using WpfHexEditor.App.BinaryAnalysis.Services;
using WpfHexEditor.App.HexDiff.Models;
using WpfHexEditor.App.HexDiff.Services;
using WpfHexEditor.Core.ViewModels;
using WpfHexEditor.SDK.Contracts;

namespace WpfHexEditor.App.HexDiff.ViewModels;

public sealed class HexDiffViewModel : ViewModelBase
{
    private IIDEHostContext? _context;
    private string  _fileAPath   = string.Empty;
    private string  _fileBPath   = string.Empty;
    private string  _statusText  = string.Empty;
    private bool    _isBusy;
    private int     _cursorIndex = -1;
    private IReadOnlyList<DiffRecord> _diffs = [];

    public ObservableCollection<DiffRecord> Results { get; } = [];

    public string FileAPath
    {
        get => _fileAPath;
        set { _fileAPath = value; OnPropertyChanged(); }
    }

    public string FileBPath
    {
        get => _fileBPath;
        set { _fileBPath = value; OnPropertyChanged(); }
    }

    public string StatusText
    {
        get => _statusText;
        private set { _statusText = value; OnPropertyChanged(); }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set { _isBusy = value; OnPropertyChanged(); }
    }

    public bool HasDiffs => _diffs.Count > 0;
    public bool HasPrev  => _cursorIndex > 0;
    public bool HasNext  => _cursorIndex < _diffs.Count - 1;

    public DiffRecord? CurrentDiff
        => _cursorIndex >= 0 && _cursorIndex < _diffs.Count ? _diffs[_cursorIndex] : null;

    public void SetContext(IIDEHostContext context) => _context = context;

    public void UseActiveEditorAsFileA()
    {
        if (_context?.HexEditor.IsActive == true)
            FileAPath = _context.HexEditor.CurrentFilePath ?? string.Empty;
    }

    public async Task RunDiffAsync()
    {
        if (string.IsNullOrWhiteSpace(_fileAPath) || string.IsNullOrWhiteSpace(_fileBPath))
        {
            StatusText = "Select both files before comparing.";
            return;
        }

        IsBusy = true;
        StatusText = "Reading files…";
        Results.Clear();
        _diffs = [];
        _cursorIndex = -1;

        try
        {
            var taskA = Task.Run(() => ReadGuarded(_fileAPath));
            var taskB = Task.Run(() => ReadGuarded(_fileBPath));
            await Task.WhenAll(taskA, taskB);
            var bytesA = taskA.Result;
            var bytesB = taskB.Result;

            if (bytesA is null || bytesB is null) return;

            StatusText = "Comparing…";
            _diffs = await Task.Run(() => FileDiffService.Diff(bytesA, bytesB));

            foreach (var d in _diffs) Results.Add(d);

            StatusText = _diffs.Count == 0
                ? "Files are identical."
                : $"{_diffs.Count:N0} difference(s) found.";

            OnPropertyChanged(nameof(HasDiffs));
            OnPropertyChanged(nameof(HasPrev));
            OnPropertyChanged(nameof(HasNext));
        }
        finally { IsBusy = false; }
    }

    public void NavigatePrev()
    {
        if (!HasPrev) return;
        _cursorIndex--;
        NotifyNavigation();
    }

    public void NavigateNext()
    {
        if (!HasNext) return;
        _cursorIndex++;
        NotifyNavigation();
    }

    public void ExportPatch(string path)
    {
        if (_diffs.Count == 0) return;
        if (path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            PatchExportService.ExportJson(_diffs, path);
        else
            PatchExportService.ExportText(_diffs, path);
    }

    private byte[]? ReadGuarded(string path)
    {
        try
        {
            // Check size before reading to avoid loading multi-GB files into memory.
            if (new FileInfo(path).Length > FileDiffService.MaxFileSizeBytes)
            {
                StatusText = "File exceeds 256 MB limit.";
                return null;
            }
            return File.ReadAllBytes(path);
        }
        catch (Exception ex)
        {
            StatusText = $"Error reading file: {ex.Message}";
            return null;
        }
    }

    private void NotifyNavigation()
    {
        OnPropertyChanged(nameof(CurrentDiff));
        OnPropertyChanged(nameof(HasPrev));
        OnPropertyChanged(nameof(HasNext));
    }
}
