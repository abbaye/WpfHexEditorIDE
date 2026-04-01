//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.IO;
using System.Windows.Controls;
using System.Windows.Input;
using WpfHexEditor.Editor.Core;

namespace WpfHexEditor.Editor.TileEditor.Controls;

/// <summary>
/// Stub tile editor — planned for a future sprint (NES/GBA/GB tile graphics).
/// Implements <see cref="IDocumentEditor"/> and <see cref="IOpenableDocument"/>.
/// </summary>
public sealed partial class TileEditor : UserControl, IDocumentEditor, IOpenableDocument
{
    private string _filePath = string.Empty;

    /// <summary>
    /// Creates a new <see cref="TileEditor"/>.
    /// </summary>
    public TileEditor()
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

    // -- IDocumentEditor — State ------------------------------------------

    /// <inheritdoc/>
    public bool IsDirty    => false;

    /// <inheritdoc/>
    public bool CanUndo    => false;

    /// <inheritdoc/>
    public bool CanRedo    => false;

    /// <inheritdoc/>
    public bool IsReadOnly { get => true; set { } }

    /// <inheritdoc/>
    public string Title { get; private set; } = "";

    /// <inheritdoc/>
    public bool IsBusy { get; private set; }

    // -- IDocumentEditor — Commands ---------------------------------------

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

    // -- IDocumentEditor — Events -----------------------------------------

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

    // -- IDocumentEditor — Methods (no-ops for stub) ----------------------

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
    public void Copy() { }

    /// <inheritdoc/>
    public void Cut() { }

    /// <inheritdoc/>
    public void Paste() { }

    /// <inheritdoc/>
    public void Delete() { }

    /// <inheritdoc/>
    public void SelectAll() { }

    /// <inheritdoc/>
    public void Close() { }

    /// <inheritdoc/>
    public void CancelOperation() { }

    // -- IOpenableDocument ------------------------------------------------

    private byte[] _data = [];
    private int _tileSize = 8;
    private int _bpp = 2;

    /// <inheritdoc/>
    public async Task OpenAsync(string filePath, CancellationToken ct = default)
    {
        _filePath = filePath;
        Title = Path.GetFileName(filePath);
        TitleChanged?.Invoke(this, Title);

        _data = await Task.Run(() => File.ReadAllBytes(filePath), ct);
        RenderTiles();

        StatusText.Text = $"{filePath} | {_data.Length:N0} bytes";
        OperationCompleted?.Invoke(this, new DocumentOperationCompletedEventArgs { Success = true });
    }

    private void OnTileSizeChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        _tileSize = TileSizeCombo.SelectedIndex == 0 ? 8 : 16;
        RenderTiles();
    }

    private void OnBppChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        _bpp = BppCombo.SelectedIndex switch { 0 => 2, 1 => 4, 2 => 8, _ => 2 };
        RenderTiles();
    }

    private void RenderTiles()
    {
        TileCanvas.Children.Clear();
        if (_data.Length == 0) return;

        int bytesPerTile = _tileSize * _tileSize * _bpp / 8;
        if (bytesPerTile == 0) return;

        int tileCount = _data.Length / bytesPerTile;
        int tilesPerRow = 16;
        int scale = _tileSize == 8 ? 3 : 2; // pixel scale
        int tileW = _tileSize * scale;
        int tileH = _tileSize * scale;

        TileCount.Text = $"{tileCount} tiles";
        TileCanvas.Width  = tilesPerRow * (tileW + 1);
        TileCanvas.Height = ((tileCount + tilesPerRow - 1) / tilesPerRow) * (tileH + 1);

        // Grayscale palette (2bpp = 4 shades, 4bpp = 16, 8bpp = 256)
        int colors = 1 << _bpp;
        var palette = new System.Windows.Media.Color[colors];
        for (int i = 0; i < colors; i++)
        {
            byte v = (byte)(255 - (i * 255 / Math.Max(1, colors - 1)));
            palette[i] = System.Windows.Media.Color.FromRgb(v, v, v);
        }

        for (int t = 0; t < tileCount && t < 4096; t++) // cap at 4096 tiles
        {
            int col = t % tilesPerRow;
            int row = t / tilesPerRow;
            double x = col * (tileW + 1);
            double y = row * (tileH + 1);

            var wb = new System.Windows.Media.Imaging.WriteableBitmap(
                _tileSize, _tileSize, 96, 96,
                System.Windows.Media.PixelFormats.Bgra32, null);

            var pixels = new byte[_tileSize * _tileSize * 4];
            int baseOffset = t * bytesPerTile;

            for (int py = 0; py < _tileSize; py++)
            {
                for (int px = 0; px < _tileSize; px++)
                {
                    int pixelIndex = py * _tileSize + px;
                    int bitOffset = pixelIndex * _bpp;
                    int byteIdx = baseOffset + bitOffset / 8;
                    int bitShift = (8 - _bpp) - (bitOffset % 8);

                    int colorIdx = 0;
                    if (byteIdx < _data.Length)
                    {
                        colorIdx = (_data[byteIdx] >> Math.Max(0, bitShift)) & ((1 << _bpp) - 1);
                        colorIdx = Math.Min(colorIdx, colors - 1);
                    }

                    var c = palette[colorIdx];
                    int pos = (py * _tileSize + px) * 4;
                    pixels[pos + 0] = c.B;
                    pixels[pos + 1] = c.G;
                    pixels[pos + 2] = c.R;
                    pixels[pos + 3] = 255;
                }
            }
            wb.WritePixels(new System.Windows.Int32Rect(0, 0, _tileSize, _tileSize),
                pixels, _tileSize * 4, 0);

            var img = new System.Windows.Controls.Image
            {
                Source = wb,
                Width  = tileW,
                Height = tileH,
                SnapsToDevicePixels = true,
            };
            System.Windows.Media.RenderOptions.SetBitmapScalingMode(img, System.Windows.Media.BitmapScalingMode.NearestNeighbor);
            System.Windows.Controls.Canvas.SetLeft(img, x);
            System.Windows.Controls.Canvas.SetTop(img, y);
            TileCanvas.Children.Add(img);
        }
    }
}

// -- Minimal RelayCommand (no external dep) -----------------------------------

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
