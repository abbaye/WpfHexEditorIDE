//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WpfHexEditor.Core.BinaryAnalysis.Services;
using WpfHexEditor.Editor.Core;

namespace WpfHexEditor.Editor.EntropyViewer.Controls;

/// <summary>
/// Read-only entropy and byte-distribution analyser.
/// Implements <see cref="IDocumentEditor"/> and <see cref="IOpenableDocument"/>.
/// </summary>
public sealed partial class EntropyViewer : UserControl, IDocumentEditor, IOpenableDocument
{
    // -----------------------------------------------------------------------
    // Fields
    // -----------------------------------------------------------------------

    private string  _filePath    = string.Empty;
    private byte[]? _data;
    private int     _windowSize  = 1024;

    private readonly DataStatisticsService _statsService = new();

    // Cached analysis results
    private double[]? _blockEntropy;     // per-window entropy values
    private long[]?   _byteFrequency;    // 256 values

    // -----------------------------------------------------------------------
    // Constructor
    // -----------------------------------------------------------------------

    /// <summary>
    /// Creates a new <see cref="EntropyViewer"/>.
    /// </summary>
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

        SizeChanged += (_, _) => RenderCharts();
    }

    // -----------------------------------------------------------------------
    // IDocumentEditor — State
    // -----------------------------------------------------------------------

    /// <inheritdoc/>
    public bool IsDirty    => false;

    /// <inheritdoc/>
    public bool CanUndo    => false;

    /// <inheritdoc/>
    public bool CanRedo    => false;

    /// <inheritdoc/>
    public bool IsReadOnly
    {
        get => true;
        set { /* always read-only */ }
    }

    /// <inheritdoc/>
    public string Title { get; private set; } = "Entropy";

    /// <inheritdoc/>
    public bool IsBusy { get; private set; }

    // -----------------------------------------------------------------------
    // IDocumentEditor — Commands
    // -----------------------------------------------------------------------

    /// <inheritdoc/>
    public ICommand UndoCommand      { get; }
    /// <inheritdoc/>
    public ICommand RedoCommand      { get; }
    /// <inheritdoc/>
    public ICommand SaveCommand      { get; }
    /// <inheritdoc/>
    public ICommand CopyCommand      { get; }
    /// <inheritdoc/>
    public ICommand CutCommand       { get; }
    /// <inheritdoc/>
    public ICommand PasteCommand     { get; }
    /// <inheritdoc/>
    public ICommand DeleteCommand    { get; }
    /// <inheritdoc/>
    public ICommand SelectAllCommand { get; }

    // -----------------------------------------------------------------------
    // IDocumentEditor — Events
    // -----------------------------------------------------------------------

#pragma warning disable CS0067
    /// <inheritdoc/>
    public event EventHandler?         ModifiedChanged;
    /// <inheritdoc/>
    public event EventHandler?         CanUndoChanged;
    /// <inheritdoc/>
    public event EventHandler?         CanRedoChanged;
    /// <inheritdoc/>
    public event EventHandler<string>? TitleChanged;
    /// <inheritdoc/>
    public event EventHandler<string>? StatusMessage;
    /// <inheritdoc/>
    public event EventHandler<string>? OutputMessage;
    /// <inheritdoc/>
    public event EventHandler?         SelectionChanged;
    /// <inheritdoc/>
    public event EventHandler<DocumentOperationEventArgs>?          OperationStarted;
    /// <inheritdoc/>
    public event EventHandler<DocumentOperationEventArgs>?          OperationProgress;
    /// <inheritdoc/>
    public event EventHandler<DocumentOperationCompletedEventArgs>? OperationCompleted;
#pragma warning restore CS0067

    // -----------------------------------------------------------------------
    // IDocumentEditor — Methods
    // -----------------------------------------------------------------------

    /// <inheritdoc/>
    public void Undo() { }
    /// <inheritdoc/>
    public void Redo() { }
    /// <inheritdoc/>
    public void Save() { }
    /// <inheritdoc/>
    public Task SaveAsync(CancellationToken ct = default) => Task.CompletedTask;
    /// <inheritdoc/>
    public Task SaveAsAsync(string filePath, CancellationToken ct = default) => Task.CompletedTask;
    /// <inheritdoc/>
    public void Copy()      { }
    /// <inheritdoc/>
    public void Cut()       { }
    /// <inheritdoc/>
    public void Paste()     { }
    /// <inheritdoc/>
    public void Delete()    { }
    /// <inheritdoc/>
    public void SelectAll() { }
    /// <inheritdoc/>
    public void Close()     { _data = null; _blockEntropy = null; _byteFrequency = null; }
    /// <inheritdoc/>
    public void CancelOperation() { }

    // -----------------------------------------------------------------------
    // IOpenableDocument
    // -----------------------------------------------------------------------

    /// <summary>
    /// Loads a binary file and computes entropy + byte distribution.
    /// </summary>
    public async Task OpenAsync(string filePath, CancellationToken ct = default)
    {
        IsBusy = true;
        OperationStarted?.Invoke(this, new DocumentOperationEventArgs { Title = "Analysing…", IsIndeterminate = true });

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
            OperationCompleted?.Invoke(this, new DocumentOperationCompletedEventArgs { WasCancelled = true, ErrorMessage = "Cancelled" });
        }
        catch (Exception ex)
        {
            StatusMessage?.Invoke(this, $"Error: {ex.Message}");
            OperationCompleted?.Invoke(this, new DocumentOperationCompletedEventArgs { Success = false, ErrorMessage = ex.Message });
        }
        finally
        {
            IsBusy = false;
        }
    }

    // -----------------------------------------------------------------------
    // Analysis
    // -----------------------------------------------------------------------

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
                var len  = Math.Min(windowSize, data.Length - offset);
                var freq = new long[256];
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

        // Update UI on dispatcher thread
        Dispatcher.Invoke(() =>
        {
            EntropyText.Text = $"Overall entropy: {stats.Entropy:F3}  |  Type: {stats.EstimatedDataType}  |  Size: {FormatSize(data.Length)}";
            StatusText.Text  = $"{data.Length:N0} bytes  |  {blockEnt.Length} blocks of {windowSize} B  |  Entropy: {stats.Entropy:F3} / 8.0";

            // Stats text
            StatsText.Text =
                $"File size      : {data.Length:N0} bytes ({FormatSize(data.Length)})\n" +
                $"Entropy        : {stats.Entropy:F4}  (0=uniform, 8=random)\n" +
                $"Estimated type : {stats.EstimatedDataType}\n" +
                $"Unique bytes   : {stats.UniqueBytesCount} / 256  ({stats.UniqueBytesCount / 2.56:F1}%)\n" +
                $"Null bytes     : {stats.NullBytePercentage:F2}%\n" +
                $"Printable ASCII: {stats.PrintableAsciiPercentage:F2}%\n" +
                $"Most common    : 0x{stats.MostCommonByte:X2}  ({stats.MostCommonByteCount:N0} × {stats.GetBytePercentage(stats.MostCommonByte):F2}%)";

            RenderCharts();
        });
    }

    // -----------------------------------------------------------------------
    // Rendering
    // -----------------------------------------------------------------------

    private void RenderCharts()
    {
        if (_blockEntropy  is not null) RenderEntropyChart();
        if (_byteFrequency is not null) RenderFreqChart();
    }

    private void RenderEntropyChart()
    {
        EntropyCanvas.Children.Clear();
        if (_blockEntropy is null || _blockEntropy.Length == 0) return;

        double w = Math.Max(1, EntropyCanvas.ActualWidth);
        double h = EntropyCanvas.ActualHeight;
        if (w <= 1 || h <= 1) return;

        double barW = w / _blockEntropy.Length;

        for (int i = 0; i < _blockEntropy.Length; i++)
        {
            double ent   = _blockEntropy[i];
            double barH  = (ent / 8.0) * (h - 4);
            var    rect  = new System.Windows.Shapes.Rectangle
            {
                Width           = Math.Max(1, barW - 1),
                Height          = Math.Max(1, barH),
                Fill            = EntropyColor(ent),
                VerticalAlignment   = VerticalAlignment.Bottom,
                HorizontalAlignment = HorizontalAlignment.Left,
            };
            System.Windows.Controls.Canvas.SetLeft(rect, i * barW);
            System.Windows.Controls.Canvas.SetTop (rect, h - barH - 2);
            EntropyCanvas.Children.Add(rect);
        }
    }

    private void RenderFreqChart()
    {
        FreqCanvas.Children.Clear();
        if (_byteFrequency is null) return;

        double w    = Math.Max(1, FreqCanvas.ActualWidth);
        double h    = FreqCanvas.ActualHeight;
        if (w <= 1 || h <= 1) return;

        double barW = w / 256.0;
        long   max  = _byteFrequency.Max();
        if (max == 0) return;

        var fill = TryGetBrush("AccentColor") ?? Brushes.DodgerBlue;

        for (int i = 0; i < 256; i++)
        {
            if (_byteFrequency[i] == 0) continue;
            double barH = (_byteFrequency[i] / (double)max) * (h - 4);
            var    rect = new System.Windows.Shapes.Rectangle
            {
                Width  = Math.Max(1, barW),
                Height = Math.Max(1, barH),
                Fill   = fill,
            };
            System.Windows.Controls.Canvas.SetLeft(rect, i * barW);
            System.Windows.Controls.Canvas.SetTop (rect, h - barH - 2);
            FreqCanvas.Children.Add(rect);
        }
    }

    // -----------------------------------------------------------------------
    // Event handlers
    // -----------------------------------------------------------------------

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

    private void EntropyCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (_blockEntropy is null || _data is null) return;
        var pos   = e.GetPosition(EntropyCanvas);
        int idx   = (int)(pos.X / Math.Max(1, EntropyCanvas.ActualWidth) * _blockEntropy.Length);
        if (idx >= 0 && idx < _blockEntropy.Length)
        {
            long offset = (long)idx * _windowSize;
            StatusText.Text = $"Block {idx}  offset 0x{offset:X}  entropy {_blockEntropy[idx]:F3}";
        }
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

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

    private static Brush EntropyColor(double ent)
    {
        // 0 → green (structured/compressible), 8 → red (compressed/encrypted)
        double t = ent / 8.0;
        byte r = (byte)(t * 220);
        byte g = (byte)((1 - t) * 180);
        return new SolidColorBrush(Color.FromRgb(r, g, 60)) { Opacity = 0.9 };
    }

    private Brush? TryGetBrush(string key)
    {
        try { return TryFindResource(key) as Brush; }
        catch { return null; }
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

// ---------------------------------------------------------------------------
// Minimal RelayCommand (no external dep)
// ---------------------------------------------------------------------------

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
