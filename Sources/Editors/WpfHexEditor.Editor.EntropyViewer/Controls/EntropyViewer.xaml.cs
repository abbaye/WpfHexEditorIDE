//////////////////////////////////////////////
// Project: WpfHexEditor.Editor.EntropyViewer
// File: Controls/EntropyViewer.xaml.cs
// Description:
//     Read-only entropy and byte-distribution analyser.
//     Implements IDocumentEditor + IOpenableDocument.
//     Fires NavigateToOffsetRequested when the user clicks a block so the
//     host IDE can sync the HexEditor to that offset.
// Architecture:
//     Thin controller — all heavy lifting is in EntropyDrawingCanvas.cs
//     (EntropyBarCanvas / ByteFreqCanvas).  Analysis runs on Task.Run.
//////////////////////////////////////////////

using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfHexEditor.Core.BinaryAnalysis.Services;
using WpfHexEditor.Editor.Core;
using WpfHexEditor.Editor.EntropyViewer.Properties;

namespace WpfHexEditor.Editor.EntropyViewer.Controls;

/// <summary>
/// Read-only entropy and byte-distribution analyser.
/// Implements <see cref="IDocumentEditor"/> and <see cref="IOpenableDocument"/>.
/// </summary>
public sealed partial class EntropyViewer : UserControl, IDocumentEditor, IOpenableDocument
{
    // ── Fields ────────────────────────────────────────────────────────────────

    private string  _filePath   = string.Empty;
    private byte[]? _data;
    private int     _windowSize = 1024;

    // Cached analysis results
    private double[]? _blockEntropy;
    private long[]?   _byteFrequency;

    // ── Constructor ───────────────────────────────────────────────────────────

    public EntropyViewer()
    {
        InitializeComponent();

        UndoCommand      = new RelayCommand(() => { }, () => false);
        RedoCommand      = new RelayCommand(() => { }, () => false);
        SaveCommand      = new RelayCommand(() => { }, () => false);
        CopyCommand      = new RelayCommand(() => { }, () => false);
        CutCommand       = new RelayCommand(() => { }, () => false);
        PasteCommand     = new RelayCommand(() => { }, () => false);
        DeleteCommand    = new RelayCommand(() => { }, () => false);
        SelectAllCommand = new RelayCommand(() => { }, () => false);

        SizeChanged += (_, _) => PushDataToCanvas();
    }

    // ── IDocumentEditor — State ───────────────────────────────────────────────

    public bool   IsDirty    => false;
    public bool   CanUndo    => false;
    public bool   CanRedo    => false;
    public bool   IsReadOnly { get => true; set { } }
    public string Title      { get; private set; } = "Entropy";
    public bool   IsBusy     { get; private set; }

    // ── IDocumentEditor — Commands ────────────────────────────────────────────

    public ICommand UndoCommand      { get; }
    public ICommand RedoCommand      { get; }
    public ICommand SaveCommand      { get; }
    public ICommand CopyCommand      { get; }
    public ICommand CutCommand       { get; }
    public ICommand PasteCommand     { get; }
    public ICommand DeleteCommand    { get; }
    public ICommand SelectAllCommand { get; }

    // ── IDocumentEditor — Events ──────────────────────────────────────────────

#pragma warning disable CS0067
    public event EventHandler?         ModifiedChanged;
    public event EventHandler?         CanUndoChanged;
    public event EventHandler?         CanRedoChanged;
    public event EventHandler<string>? TitleChanged;
    public event EventHandler<string>? StatusMessage;
    public event EventHandler<string>? OutputMessage;
    public event EventHandler?         SelectionChanged;
    public event EventHandler<DocumentOperationEventArgs>?          OperationStarted;
    public event EventHandler<DocumentOperationEventArgs>?          OperationProgress;
    public event EventHandler<DocumentOperationCompletedEventArgs>? OperationCompleted;
#pragma warning restore CS0067

    /// <summary>
    /// Raised when the user clicks an entropy block.
    /// The argument is the file offset of the start of that block.
    /// The host IDE should sync the HexEditor to this offset.
    /// </summary>
    public event EventHandler<long>? NavigateToOffsetRequested;

    // ── IDocumentEditor — Methods ─────────────────────────────────────────────

    public void Undo()  { }
    public void Redo()  { }
    public void Save()  { }
    public Task SaveAsync  (CancellationToken ct = default) => Task.CompletedTask;
    public Task SaveAsAsync(string filePath, CancellationToken ct = default) => Task.CompletedTask;
    public void Copy()      { }
    public void Cut()       { }
    public void Paste()     { }
    public void Delete()    { }
    public void SelectAll() { }
    public void CancelOperation() { }

    public void Close()
    {
        _data          = null;
        _blockEntropy  = null;
        _byteFrequency = null;
        EntropyCanvas.SetData(null, _windowSize);
        FreqCanvas.SetData(null);
    }

    // ── IOpenableDocument ─────────────────────────────────────────────────────

    public async Task OpenAsync(string filePath, CancellationToken ct = default)
    {
        IsBusy = true;
        OperationStarted?.Invoke(this, new DocumentOperationEventArgs
            { Title = EntropyViewerResources.EntropyViewer_Analysing, IsIndeterminate = true });

        try
        {
            _filePath = filePath;
            Title     = Path.GetFileName(filePath);

            _data = await Task.Run(() => File.ReadAllBytes(filePath), ct);
            await AnalyseAsync(ct);

            TitleChanged?.Invoke(this, Title);
            StatusMessage?.Invoke(this, $"Analysed: {Path.GetFileName(filePath)}");
            OperationCompleted?.Invoke(this, new DocumentOperationCompletedEventArgs { Success = true });
        }
        catch (OperationCanceledException)
        {
            OperationCompleted?.Invoke(this,
                new DocumentOperationCompletedEventArgs { WasCancelled = true, ErrorMessage = "Cancelled" });
        }
        catch (Exception ex)
        {
            StatusMessage?.Invoke(this, $"Error: {ex.Message}");
            OperationCompleted?.Invoke(this,
                new DocumentOperationCompletedEventArgs { Success = false, ErrorMessage = ex.Message });
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ── Analysis ──────────────────────────────────────────────────────────────

    private async Task AnalyseAsync(CancellationToken ct)
    {
        if (_data is null) return;

        var data       = _data;
        var windowSize = _windowSize;

        var (blockEnt, byteFreq, stats) = await Task.Run(() =>
        {
            // Per-block entropy
            var blocks = new List<double>();
            for (int offset = 0; offset < data.Length; offset += windowSize)
            {
                int   len  = Math.Min(windowSize, data.Length - offset);
                var   freq = new long[256];
                for (int i = offset; i < offset + len; i++) freq[data[i]]++;
                blocks.Add(Shannon(freq, len));
            }

            // Overall byte frequency
            var freq256 = new long[256];
            foreach (var b in data) freq256[b]++;

            var svc = new DataStatisticsService();
            var st  = svc.CalculateStatistics(data);

            return (blocks.ToArray(), freq256, st);
        }, ct);

        _blockEntropy  = blockEnt;
        _byteFrequency = byteFreq;

        Dispatcher.Invoke(() =>
        {
            EntropyText.Text =
                $"Overall entropy: {stats.Entropy:F3}  |  Type: {stats.EstimatedDataType}  |  Size: {FormatSize(data.Length)}";
            StatusText.Text =
                $"{data.Length:N0} bytes  |  {blockEnt.Length} blocks of {windowSize} B  |  Entropy: {stats.Entropy:F3} / 8.0";

            StatsText.Text =
                $"File size      : {data.Length:N0} bytes ({FormatSize(data.Length)})\n" +
                $"Entropy        : {stats.Entropy:F4}  (0=uniform, 8=random)\n" +
                $"Estimated type : {stats.EstimatedDataType}\n" +
                $"Unique bytes   : {stats.UniqueBytesCount} / 256  ({stats.UniqueBytesCount / 2.56:F1}%)\n" +
                $"Null bytes     : {stats.NullBytePercentage:F2}%\n" +
                $"Printable ASCII: {stats.PrintableAsciiPercentage:F2}%\n" +
                $"Most common    : 0x{stats.MostCommonByte:X2}  ({stats.MostCommonByteCount:N0} × {stats.GetBytePercentage(stats.MostCommonByte):F2}%)";

            PushDataToCanvas();
        });
    }

    /// <summary>Pushes cached data to both DrawingContext canvases.</summary>
    private void PushDataToCanvas()
    {
        EntropyCanvas.SetData(_blockEntropy, _windowSize);
        FreqCanvas.SetData(_byteFrequency);
    }

    // ── Event handlers ────────────────────────────────────────────────────────

    private void OnWindowSizeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (WindowSizeCombo.SelectedItem is ComboBoxItem item
            && int.TryParse(item.Tag as string, out var size))
        {
            _windowSize = size;
            if (_data is not null)
                _ = AnalyseAsync(CancellationToken.None);
        }
    }

    private void OnShowRegionsChanged(object sender, RoutedEventArgs e)
        => EntropyCanvas.ShowRegions = ShowRegionsCheck.IsChecked == true;

    private void OnShowThresholdChanged(object sender, RoutedEventArgs e)
        => EntropyCanvas.ShowThresholdLine = ShowThresholdCheck.IsChecked == true;

    private void OnEntropyHoverChanged(object? sender, EntropyHoverEventArgs e)
    {
        if (e.BlockIndex < 0)
        {
            // Restore default status when mouse leaves
            if (_data is not null && _blockEntropy is not null)
                StatusText.Text =
                    $"{_data.Length:N0} bytes  |  {_blockEntropy.Length} blocks of {_windowSize} B";
            return;
        }
        StatusText.Text =
            $"Block {e.BlockIndex}  |  Offset 0x{e.Offset:X8}  |  Entropy {e.Entropy:F4}  " +
            $"[click to navigate]";
    }

    private void OnEntropyOffsetRequested(object? sender, long offset)
        => NavigateToOffsetRequested?.Invoke(this, offset);

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static double Shannon(long[] freq, long total)
    {
        if (total == 0) return 0;
        double h = 0;
        foreach (var f in freq)
        {
            if (f <= 0) continue;
            double p = f / (double)total;
            h -= p * Math.Log(p, 2);
        }
        return h;
    }

    private static string FormatSize(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        double   size  = bytes;
        int      unit  = 0;
        while (size >= 1024 && unit < units.Length - 1) { size /= 1024; unit++; }
        return $"{size:F1} {units[unit]}";
    }
}

// ── Minimal RelayCommand (no external dep) ───────────────────────────────────

internal sealed class RelayCommand(Action execute, Func<bool>? canExecute = null) : ICommand
{
    public event EventHandler? CanExecuteChanged
    {
        add    => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter) => canExecute?.Invoke() ?? true;
    public void Execute(object? parameter)    => execute();
}
