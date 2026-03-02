//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Media;
using WpfHexEditor.Editor.Core;
using WpfHexEditor.Core.Bytes;
using WpfHexEditor.Core.Services;
using WpfHexEditor.Core.Models.Comparison;

namespace WpfHexEditor.Editor.DiffViewer.Controls;

/// <summary>
/// Side-by-side binary comparison viewer.
/// Uses <see cref="FileDiffService"/> for structured diff regions with
/// Modified / AddedInSecond / DeletedInSecond semantics.
/// Open via <see cref="CompareAsync(string,string,System.Threading.CancellationToken)"/>.
/// Implements <see cref="IDocumentEditor"/> and <see cref="IOpenableDocument"/>.
/// </summary>
public sealed partial class DiffViewer : UserControl, IDocumentEditor, IOpenableDocument
{
    // -----------------------------------------------------------------------
    // Constants
    // -----------------------------------------------------------------------

    private const int BytesPerRow = 16;

    // -----------------------------------------------------------------------
    // Fields
    // -----------------------------------------------------------------------

    private byte[]? _left;
    private byte[]? _right;
    private string  _leftPath  = string.Empty;
    private string  _rightPath = string.Empty;

    // Structured diff regions from FileDiffService
    private List<FileDifference> _diffRegions = [];
    private int _currentDiff = -1;

    // Flat offset list derived from regions (first offset of each region for navigation)
    private List<long> _regionOffsets = [];

    // Canvas brush lookups
    private static readonly Brush BrushModified = new SolidColorBrush(Color.FromArgb(80, 204, 68, 68));
    private static readonly Brush BrushAdded    = new SolidColorBrush(Color.FromArgb(80, 68, 170, 68));
    private static readonly Brush BrushDeleted  = new SolidColorBrush(Color.FromArgb(80, 204, 136, 0));

    // -----------------------------------------------------------------------
    // Constructor
    // -----------------------------------------------------------------------

    /// <summary>Creates a new <see cref="DiffViewer"/>.</summary>
    public DiffViewer()
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
    }

    // -----------------------------------------------------------------------
    // Public API
    // -----------------------------------------------------------------------

    /// <summary>
    /// Loads two files and computes their byte-level differences.
    /// This is the primary entry point for this viewer.
    /// </summary>
    public async Task CompareAsync(string leftPath, string rightPath, CancellationToken ct = default)
    {
        IsBusy = true;
        OperationStarted?.Invoke(this, new DocumentOperationEventArgs { Title = "Comparing…", IsIndeterminate = true });
        try
        {
            _leftPath  = leftPath;
            _rightPath = rightPath;
            Title      = $"{Path.GetFileName(leftPath)} ↔ {Path.GetFileName(rightPath)}";

            _left  = await Task.Run(() => File.ReadAllBytes(leftPath),  ct);
            _right = await Task.Run(() => File.ReadAllBytes(rightPath), ct);

            await ComputeDiffAsync(ct);
            TitleChanged?.Invoke(this, Title);
            OperationCompleted?.Invoke(this, new DocumentOperationCompletedEventArgs { Success = true });
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error: {ex.Message}";
            OperationCompleted?.Invoke(this, new DocumentOperationCompletedEventArgs { Success = false, ErrorMessage = ex.Message });
        }
        finally { IsBusy = false; }
    }

    // -----------------------------------------------------------------------
    // IOpenableDocument
    // -----------------------------------------------------------------------

    /// <summary>Loads the left-side file only (compares with itself for initial view).</summary>
    public async Task OpenAsync(string filePath, CancellationToken ct = default)
        => await CompareAsync(filePath, filePath, ct);

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
    public bool IsReadOnly { get => true; set { } }
    /// <inheritdoc/>
    public string Title { get; private set; } = "Diff";
    /// <inheritdoc/>
    public bool IsBusy { get; private set; }

    // -----------------------------------------------------------------------
    // IDocumentEditor — Commands (all disabled — read-only viewer)
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
    public void Close()     { _left = null; _right = null; _diffRegions = []; }
    /// <inheritdoc/>
    public void CancelOperation() { }

    // -----------------------------------------------------------------------
    // Diff computation — uses FileDiffService for structured regions
    // -----------------------------------------------------------------------

    private async Task ComputeDiffAsync(CancellationToken ct)
    {
        if (_left is null || _right is null) return;

        var left  = _left;
        var right = _right;

        // Use FileDiffService which returns grouped regions (Modified/Added/Deleted)
        var regions = await Task.Run(() =>
        {
            using var p1 = new ByteProvider();
            using var p2 = new ByteProvider();
            p1.OpenMemory(left,  readOnly: true);
            p2.OpenMemory(right, readOnly: true);
            return new FileDiffService().CompareFiles(p1, p2);
        }, ct);

        _diffRegions  = regions;
        _regionOffsets = regions.Select(r => r.Offset).ToList();
        _currentDiff  = regions.Count > 0 ? 0 : -1;

        Dispatcher.Invoke(() =>
        {
            LeftHeader.Text  = Path.GetFileName(_leftPath);
            RightHeader.Text = Path.GetFileName(_rightPath);

            // Statistics
            var svc  = new FileDiffService();
            var stats = svc.GetStatistics(regions);

            long totalDiffBytes = stats.TotalModifiedBytes + stats.TotalAddedBytes + stats.TotalDeletedBytes;
            long maxLen = Math.Max(left.Length, right.Length);
            double simPct = maxLen > 0 ? (1.0 - (double)totalDiffBytes / maxLen) * 100.0 : 100;
            simPct = Math.Max(0, simPct);

            DiffCountText.Text = $"{regions.Count:N0} diff regions  |  {simPct:F1}% similar";
            StatusText.Text    = $"Left: {left.Length:N0} B  |  Right: {right.Length:N0} B  |  Regions: {regions.Count:N0}";

            // Statistics chips
            ShowChip(ChipModified, ModifiedText, "Modified",  stats.ModifiedCount);
            ShowChip(ChipAdded,    AddedText,    "Added",     stats.AddedCount);
            ShowChip(ChipDeleted,  DeletedText,  "Deleted",   stats.DeletedCount);

            // Diff list DataGrid
            DiffListGrid.ItemsSource = regions.Select((r, i) => new DiffRowVm(r, i, left, right)).ToList();

            BtnPrevDiff.IsEnabled = regions.Count > 0;
            BtnNextDiff.IsEnabled = regions.Count > 0;

            RenderHex();
        });
    }

    private static void ShowChip(Border chip, TextBlock label, string prefix, int count)
    {
        if (count > 0)
        {
            label.Text = $"{prefix}: {count}";
            chip.Visibility = Visibility.Visible;
        }
        else
        {
            chip.Visibility = Visibility.Collapsed;
        }
    }

    // -----------------------------------------------------------------------
    // Canvas rendering
    // -----------------------------------------------------------------------

    private void RenderHex()
    {
        if (_left is null || _right is null) return;

        LeftCanvas.Children.Clear();
        RightCanvas.Children.Clear();

        var fontFamily = new FontFamily("Consolas, Courier New");
        const double fontSize   = 12;
        double lineHeight       = fontSize + 4;

        long maxLen     = Math.Max(_left.Length, _right.Length);
        long rows       = (maxLen + BytesPerRow - 1) / BytesPerRow;
        long renderRows = Math.Min(rows, 2000);

        // Build a map: row → brush (for regions that span that row)
        var rowBrush = new Dictionary<long, Brush>();
        foreach (var region in _diffRegions)
        {
            var brush = region.Type switch
            {
                DifferenceType.Modified        => BrushModified,
                DifferenceType.AddedInSecond   => BrushAdded,
                DifferenceType.DeletedInSecond => BrushDeleted,
                _                              => BrushModified
            };
            long firstRow = region.Offset / BytesPerRow;
            int regionLen = region.BytesFile1?.Length > 0 ? region.BytesFile1.Length : (region.BytesFile2?.Length ?? 1);
            long lastRow  = (region.Offset + regionLen - 1) / BytesPerRow;
            for (long r = firstRow; r <= lastRow && r < renderRows; r++)
                rowBrush[r] = brush; // last-writer wins if regions overlap (shouldn't happen)
        }

        for (long row = 0; row < renderRows; row++)
        {
            double y   = row * lineHeight;
            long   off = row * BytesPerRow;

            var leftSb  = new System.Text.StringBuilder();
            var rightSb = new System.Text.StringBuilder();

            leftSb.Append($"{off:X8}  ");
            rightSb.Append($"{off:X8}  ");

            rowBrush.TryGetValue(row, out var rowHighlight);

            for (int col = 0; col < BytesPerRow; col++)
            {
                long idx = off + col;
                byte lb  = idx < _left.Length  ? _left[idx]  : (byte)0;
                byte rb  = idx < _right.Length ? _right[idx] : (byte)0;

                leftSb.Append($" {lb:X2}");
                rightSb.Append($" {rb:X2}");
            }

            AddTextRow(LeftCanvas,  y, leftSb.ToString(),  fontFamily, fontSize, lineHeight, rowHighlight);
            AddTextRow(RightCanvas, y, rightSb.ToString(), fontFamily, fontSize, lineHeight, rowHighlight);
        }

        double canvasH = renderRows * lineHeight;
        LeftCanvas.Height  = canvasH;
        RightCanvas.Height = canvasH;
    }

    private static void AddTextRow(
        System.Windows.Controls.Canvas canvas,
        double y, string text,
        FontFamily fontFamily, double fontSize, double lineHeight,
        Brush? highlight)
    {
        if (highlight is not null)
        {
            var bg = new System.Windows.Shapes.Rectangle
            {
                Width  = 10000,
                Height = lineHeight,
                Fill   = highlight
            };
            System.Windows.Controls.Canvas.SetLeft(bg, 0);
            System.Windows.Controls.Canvas.SetTop(bg, y);
            canvas.Children.Add(bg);
        }

        var tb = new TextBlock
        {
            Text         = text,
            FontFamily   = fontFamily,
            FontSize     = fontSize,
            Foreground   = SystemColors.ControlTextBrush,
            Background   = Brushes.Transparent,
            TextTrimming = TextTrimming.None,
        };
        System.Windows.Controls.Canvas.SetLeft(tb, 4);
        System.Windows.Controls.Canvas.SetTop(tb, y);
        canvas.Children.Add(tb);
    }

    // -----------------------------------------------------------------------
    // Navigation
    // -----------------------------------------------------------------------

    private void BtnPrevDiff_Click(object sender, RoutedEventArgs e)
    {
        if (_regionOffsets.Count == 0) return;
        _currentDiff = (_currentDiff - 1 + _regionOffsets.Count) % _regionOffsets.Count;
        ScrollToDiff(_currentDiff);
        SyncDiffListSelection(_currentDiff);
    }

    private void BtnNextDiff_Click(object sender, RoutedEventArgs e)
    {
        if (_regionOffsets.Count == 0) return;
        _currentDiff = (_currentDiff + 1) % _regionOffsets.Count;
        ScrollToDiff(_currentDiff);
        SyncDiffListSelection(_currentDiff);
    }

    private void ScrollToDiff(int idx)
    {
        if (idx < 0 || idx >= _regionOffsets.Count) return;
        long offset = _regionOffsets[idx];
        long row    = offset / BytesPerRow;
        var  region = _diffRegions[idx];

        StatusText.Text = $"Region {idx + 1}/{_regionOffsets.Count}  |  "
                        + $"Type: {region.Type}  |  Offset: 0x{offset:X}  |  "
                        + $"Length: {(region.BytesFile1?.Length > 0 ? region.BytesFile1.Length : region.BytesFile2?.Length ?? 0)} B";

        double y = row * 16.0;
        LeftScroll.ScrollToVerticalOffset(Math.Max(0, y - 60));
        RightScroll.ScrollToVerticalOffset(Math.Max(0, y - 60));
    }

    private void SyncDiffListSelection(int idx)
    {
        if (DiffListGrid.Items.Count > idx && idx >= 0)
        {
            DiffListGrid.SelectedIndex = idx;
            DiffListGrid.ScrollIntoView(DiffListGrid.SelectedItem);
        }
    }

    // -----------------------------------------------------------------------
    // Diff list panel toggle / DataGrid selection
    // -----------------------------------------------------------------------

    private void OnDiffListToggled(object sender, RoutedEventArgs e)
    {
        DiffListPanel.Visibility = BtnDiffList.IsChecked == true
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void OnDiffListSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DiffListGrid.SelectedIndex < 0) return;
        _currentDiff = DiffListGrid.SelectedIndex;
        ScrollToDiff(_currentDiff);
    }
}

// ---------------------------------------------------------------------------
// DataGrid row view-model
// ---------------------------------------------------------------------------

internal sealed class DiffRowVm
{
    public DiffRowVm(FileDifference r, int index, byte[] left, byte[] right)
    {
        TypeLabel    = r.Type.ToString();
        OffsetHex    = $"0x{r.Offset:X8}";
        int len      = r.BytesFile1?.Length > 0 ? r.BytesFile1.Length : (r.BytesFile2?.Length ?? 0);
        LengthStr    = $"{len} B";
        LeftPreview  = PreviewBytes(r.BytesFile1);
        RightPreview = PreviewBytes(r.BytesFile2);
    }

    public string TypeLabel    { get; }
    public string OffsetHex    { get; }
    public string LengthStr    { get; }
    public string LeftPreview  { get; }
    public string RightPreview { get; }

    private static string PreviewBytes(byte[]? bytes)
    {
        if (bytes is null || bytes.Length == 0) return "—";
        var sb = new System.Text.StringBuilder();
        int max = Math.Min(bytes.Length, 8);
        for (int i = 0; i < max; i++) sb.Append($"{bytes[i]:X2} ");
        if (bytes.Length > max) sb.Append("…");
        return sb.ToString().TrimEnd();
    }
}

// ---------------------------------------------------------------------------
// Minimal RelayCommand
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
