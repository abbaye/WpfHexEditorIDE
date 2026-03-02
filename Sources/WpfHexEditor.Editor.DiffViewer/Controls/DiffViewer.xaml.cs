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

namespace WpfHexEditor.Editor.DiffViewer.Controls;

/// <summary>
/// Side-by-side binary comparison viewer.
/// Open via <see cref="CompareAsync(string,string,System.Threading.CancellationToken)"/> rather than
/// <see cref="IOpenableDocument.OpenAsync"/> (which loads only the left-side file).
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

    // Diff results: indices in _left/_right that differ
    private List<long> _diffOffsets = [];
    private int        _currentDiff = -1;

    // -----------------------------------------------------------------------
    // Constructor
    // -----------------------------------------------------------------------

    /// <summary>
    /// Creates a new <see cref="DiffViewer"/>.
    /// </summary>
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
    /// Loads two files for comparison.
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
    // IOpenableDocument — loads left-side file only
    // -----------------------------------------------------------------------

    /// <summary>
    /// Loads the left-side file. Call <see cref="CompareAsync"/> for full comparison.
    /// </summary>
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
    public void Close()     { _left = null; _right = null; }
    /// <inheritdoc/>
    public void CancelOperation() { }

    // -----------------------------------------------------------------------
    // Diff computation
    // -----------------------------------------------------------------------

    private async Task ComputeDiffAsync(CancellationToken ct)
    {
        if (_left is null || _right is null) return;

        var left  = _left;
        var right = _right;

        var diffs = await Task.Run(() =>
        {
            var result = new List<long>();
            long maxLen = Math.Max(left.Length, right.Length);
            for (long i = 0; i < maxLen; i++)
            {
                byte l = i < left.Length  ? left[i]  : (byte)0;
                byte r = i < right.Length ? right[i] : (byte)0;
                if (l != r) result.Add(i);
            }
            return result;
        }, ct);

        _diffOffsets = diffs;
        _currentDiff = diffs.Count > 0 ? 0 : -1;

        Dispatcher.Invoke(() =>
        {
            LeftHeader.Text  = Path.GetFileName(_leftPath);
            RightHeader.Text = Path.GetFileName(_rightPath);

            long same    = Math.Min(left.Length, right.Length) - diffs.Count;
            long maxLen  = Math.Max(left.Length, right.Length);
            double pct   = maxLen > 0 ? (double)same / maxLen * 100 : 100;

            DiffCountText.Text = $"{diffs.Count:N0} differences  |  {pct:F1}% similar";
            StatusText.Text    = $"Left: {left.Length:N0} B  |  Right: {right.Length:N0} B  |  Differences: {diffs.Count:N0}";

            BtnPrevDiff.IsEnabled = diffs.Count > 0;
            BtnNextDiff.IsEnabled = diffs.Count > 0;

            RenderHex();
        });
    }

    private void RenderHex()
    {
        if (_left is null || _right is null) return;

        LeftCanvas.Children.Clear();
        RightCanvas.Children.Clear();

        var fontFamily  = new FontFamily("Consolas, Courier New");
        const double fontSize = 12;
        double lineHeight = fontSize + 4;
        double charW      = 7.2; // approx for Consolas 12px

        long maxLen = Math.Max(_left.Length, _right.Length);
        long rows   = (maxLen + BytesPerRow - 1) / BytesPerRow;

        // Limit rendered rows for performance
        long renderRows = Math.Min(rows, 2000);

        var diffSet = new HashSet<long>(_diffOffsets);

        for (long row = 0; row < renderRows; row++)
        {
            double y     = row * lineHeight;
            long   off   = row * BytesPerRow;
            var    leftSb  = new System.Text.StringBuilder();
            var    rightSb = new System.Text.StringBuilder();

            leftSb.Append($"{off:X8}  ");
            rightSb.Append($"{off:X8}  ");

            for (int col = 0; col < BytesPerRow; col++)
            {
                long idx  = off + col;
                byte lb   = idx < _left.Length  ? _left[idx]  : (byte)0;
                byte rb   = idx < _right.Length ? _right[idx] : (byte)0;
                bool diff = idx < maxLen && diffSet.Contains(idx);

                leftSb.Append(diff ? $"[{lb:X2}]" : $" {lb:X2} ");
                rightSb.Append(diff ? $"[{rb:X2}]" : $" {rb:X2} ");
            }

            AddTextRow(LeftCanvas,  y, leftSb.ToString(),  fontFamily, fontSize);
            AddTextRow(RightCanvas, y, rightSb.ToString(), fontFamily, fontSize);
        }

        double canvasH = renderRows * lineHeight;
        LeftCanvas.Height  = canvasH;
        RightCanvas.Height = canvasH;
    }

    private static void AddTextRow(Canvas canvas, double y, string text,
        FontFamily fontFamily, double fontSize)
    {
        var tb = new TextBlock
        {
            Text              = text,
            FontFamily        = fontFamily,
            FontSize          = fontSize,
            Foreground        = SystemColors.ControlTextBrush,
            Background        = Brushes.Transparent,
            TextTrimming      = TextTrimming.None,
        };
        Canvas.SetLeft(tb, 4);
        Canvas.SetTop(tb,  y);
        canvas.Children.Add(tb);
    }

    // -----------------------------------------------------------------------
    // Navigation
    // -----------------------------------------------------------------------

    private void BtnPrevDiff_Click(object sender, RoutedEventArgs e)
    {
        if (_diffOffsets.Count == 0) return;
        _currentDiff = (_currentDiff - 1 + _diffOffsets.Count) % _diffOffsets.Count;
        ScrollToDiff(_currentDiff);
    }

    private void BtnNextDiff_Click(object sender, RoutedEventArgs e)
    {
        if (_diffOffsets.Count == 0) return;
        _currentDiff = (_currentDiff + 1) % _diffOffsets.Count;
        ScrollToDiff(_currentDiff);
    }

    private void ScrollToDiff(int idx)
    {
        if (idx < 0 || idx >= _diffOffsets.Count) return;
        long row = _diffOffsets[idx] / BytesPerRow;
        StatusText.Text = $"Difference {idx + 1}/{_diffOffsets.Count}  at offset 0x{_diffOffsets[idx]:X}";
        // Simple scroll approximation
        double y = row * 16.0;
        LeftScroll.ScrollToVerticalOffset(Math.Max(0, y - 60));
        RightScroll.ScrollToVerticalOffset(Math.Max(0, y - 60));
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
