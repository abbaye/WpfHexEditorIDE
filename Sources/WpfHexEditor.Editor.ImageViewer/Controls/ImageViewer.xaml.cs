//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WpfHexEditor.Editor.Core;

namespace WpfHexEditor.Editor.ImageViewer.Controls;

/// <summary>
/// Read-only image viewer with zoom, pan, and pixel inspection.
/// Implements <see cref="IDocumentEditor"/> and <see cref="IOpenableDocument"/>.
/// </summary>
public sealed partial class ImageViewer : UserControl,
    IDocumentEditor, IOpenableDocument, IStatusBarContributor, IEditorPersistable
{
    // -----------------------------------------------------------------------
    // Fields
    // -----------------------------------------------------------------------

    private string _filePath = string.Empty;
    private BitmapSource? _bitmap;
    private double _zoom = 1.0;

    // Pan state
    private bool _isPanning;
    private Point _panStart;
    private double _panScrollH;
    private double _panScrollV;

    // Persistence
    private double _restoredZoom;   // 0 = "fit to window"

    // Status bar
    private readonly StatusBarItem _zoomItem = new() { Label = "Zoom", Value = "100%" };

    // -----------------------------------------------------------------------
    // Constructor
    // -----------------------------------------------------------------------

    /// <summary>
    /// Creates a new <see cref="ImageViewer"/>.
    /// </summary>
    public ImageViewer()
    {
        InitializeComponent();

        UndoCommand      = new RelayCommand(() => { }, () => false);
        RedoCommand      = new RelayCommand(() => { }, () => false);
        SaveCommand      = new RelayCommand(() => { }, () => false);
        CopyCommand      = new RelayCommand(CopyImage,  () => _bitmap is not null);
        CutCommand       = new RelayCommand(() => { }, () => false);
        PasteCommand     = new RelayCommand(() => { }, () => false);
        DeleteCommand    = new RelayCommand(() => { }, () => false);
        SelectAllCommand = new RelayCommand(() => { }, () => false);

        // Keyboard shortcuts
        KeyDown += OnKeyDown;

        // Status bar — zoom presets
        foreach (var (name, factor) in new (string, double)[]
            { ("Fit", 0), ("25%", .25), ("50%", .5), ("75%", .75),
              ("100%", 1), ("150%", 1.5), ("200%", 2), ("400%", 4) })
        {
            var f = factor;
            _zoomItem.Choices.Add(new StatusBarChoice
            {
                DisplayName = name,
                IsActive    = false,
                Command     = new RelayCommand(() => { if (f == 0) FitToWindow(); else ApplyZoom(f); })
            });
        }
        StatusBarItems.Add(_zoomItem);
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
    public string Title { get; private set; } = "Image";

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
    // IStatusBarContributor
    // -----------------------------------------------------------------------

    /// <inheritdoc/>
    public ObservableCollection<StatusBarItem> StatusBarItems { get; } = new();

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
    // IDocumentEditor — Methods (no-ops for read-only viewer)
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
    public void Copy() => CopyImage();

    /// <inheritdoc/>
    public void Cut() { }

    /// <inheritdoc/>
    public void Paste() { }

    /// <inheritdoc/>
    public void Delete() { }

    /// <inheritdoc/>
    public void SelectAll() { }

    /// <inheritdoc/>
    public void Close() { _bitmap = null; ImageDisplay.Source = null; }

    /// <inheritdoc/>
    public void CancelOperation() { }

    // -----------------------------------------------------------------------
    // IEditorPersistable
    // -----------------------------------------------------------------------

    /// <inheritdoc/>
    public EditorConfigDto GetEditorConfig() => new()
    {
        Extra = new Dictionary<string, string>
        {
            ["ZoomFactor"] = _zoom.ToString(CultureInfo.InvariantCulture)
        }
    };

    /// <inheritdoc/>
    public void ApplyEditorConfig(EditorConfigDto config)
    {
        if (config?.Extra?.TryGetValue("ZoomFactor", out var s) == true &&
            double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var z) && z > 0)
            _restoredZoom = z;
    }

    /// <inheritdoc/>
    public byte[]? GetUnsavedModifications() => null;

    /// <inheritdoc/>
    public void ApplyUnsavedModifications(byte[] data) { }

    /// <inheritdoc/>
    public IReadOnlyList<BookmarkDto>? GetBookmarks() => null;

    /// <inheritdoc/>
    public void ApplyBookmarks(IReadOnlyList<BookmarkDto> bookmarks) { }

    /// <inheritdoc/>
    /// <remarks>Image viewers do not have byte-level edits.</remarks>
    public ChangesetSnapshot GetChangesetSnapshot() => ChangesetSnapshot.Empty;

    /// <inheritdoc/>
    /// <remarks>Not applicable for image viewers.</remarks>
    public void ApplyChangeset(ChangesetDto changeset) { }

    // -----------------------------------------------------------------------
    // IOpenableDocument
    // -----------------------------------------------------------------------

    /// <summary>
    /// Loads an image file asynchronously and updates the view.
    /// Supported: PNG, BMP, JPG, GIF, TGA, ICO, TIFF, WEBP.
    /// </summary>
    public async Task OpenAsync(string filePath, CancellationToken ct = default)
    {
        IsBusy = true;
        OperationStarted?.Invoke(this, new DocumentOperationEventArgs { Title = "Loading image…", IsIndeterminate = true });

        try
        {
            _filePath = filePath;
            Title = Path.GetFileName(filePath);

            var bitmap = await Task.Run(() =>
            {
                var img = new BitmapImage();
                img.BeginInit();
                img.UriSource = new Uri(filePath, UriKind.Absolute);
                img.CacheOption = BitmapCacheOption.OnLoad;
                img.EndInit();
                img.Freeze();
                return (BitmapSource)img;
            }, ct);

            _bitmap = bitmap;
            ImageDisplay.Source = bitmap;

            if (_restoredZoom > 0)
                ApplyZoom(_restoredZoom);
            else
            {
                Loaded += OnLoadedFitToWindow;
                if (IsLoaded) FitToWindow();
            }

            UpdateStatusBar();
            TitleChanged?.Invoke(this, Title);
            StatusMessage?.Invoke(this, $"Loaded: {Path.GetFileName(filePath)}");
            OperationCompleted?.Invoke(this, new DocumentOperationCompletedEventArgs { Success = true });
        }
        catch (OperationCanceledException)
        {
            OperationCompleted?.Invoke(this, new DocumentOperationCompletedEventArgs { WasCancelled = true, ErrorMessage = "Cancelled" });
        }
        catch (Exception ex)
        {
            StatusMessage?.Invoke(this, $"Error loading image: {ex.Message}");
            OperationCompleted?.Invoke(this, new DocumentOperationCompletedEventArgs { Success = false, ErrorMessage = ex.Message });
        }
        finally
        {
            IsBusy = false;
        }
    }

    // -----------------------------------------------------------------------
    // Zoom
    // -----------------------------------------------------------------------

    private void OnLoadedFitToWindow(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoadedFitToWindow;
        FitToWindow();
    }

    private void ApplyZoom(double newZoom)
    {
        newZoom = Math.Max(0.05, Math.Min(32.0, newZoom));
        _zoom = newZoom;

        ImageDisplay.LayoutTransform = new ScaleTransform(_zoom, _zoom);
        ZoomText.Text = $"{_zoom * 100:F0}%";

        _zoomItem.Value = $"{_zoom * 100:F0}%";
        foreach (var c in _zoomItem.Choices)
            c.IsActive = c.DisplayName != "Fit" &&
                         double.TryParse(c.DisplayName.TrimEnd('%'), out var pct) &&
                         Math.Abs(pct / 100 - _zoom) < 0.01;

        StatusMessage?.Invoke(this, $"Zoom: {_zoom * 100:F0}%");
    }

    private void FitToWindow()
    {
        if (_bitmap is null) return;
        if (ImageScroll.ActualWidth <= 0 || ImageScroll.ActualHeight <= 0) return;

        double zoomX = (ImageScroll.ActualWidth  - 32) / _bitmap.PixelWidth;
        double zoomY = (ImageScroll.ActualHeight - 32) / _bitmap.PixelHeight;
        ApplyZoom(Math.Min(zoomX, zoomY));
    }

    // -----------------------------------------------------------------------
    // Event handlers — toolbar
    // -----------------------------------------------------------------------

    private void BtnZoomIn_Click(object sender, RoutedEventArgs e)  => ApplyZoom(_zoom * 1.25);
    private void BtnZoomOut_Click(object sender, RoutedEventArgs e) => ApplyZoom(_zoom / 1.25);
    private void BtnZoom100_Click(object sender, RoutedEventArgs e) => ApplyZoom(1.0);
    private void BtnZoomFit_Click(object sender, RoutedEventArgs e) => FitToWindow();

    // -----------------------------------------------------------------------
    // Event handlers — scroll / pan / mouse
    // -----------------------------------------------------------------------

    private void ImageScroll_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            // Ctrl+Wheel → zoom
            ApplyZoom(e.Delta > 0 ? _zoom * 1.15 : _zoom / 1.15);
            e.Handled = true;
        }
        // else: default scroll behaviour
    }

    private void ImageScroll_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.MiddleButton == MouseButtonState.Pressed ||
            (e.LeftButton  == MouseButtonState.Pressed && (Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt))
        {
            _isPanning    = true;
            _panStart     = e.GetPosition(this);
            _panScrollH   = ImageScroll.HorizontalOffset;
            _panScrollV   = ImageScroll.VerticalOffset;
            Mouse.Capture(ImageScroll);
            e.Handled     = true;
        }
    }

    private void ImageScroll_MouseMove(object sender, MouseEventArgs e)
    {
        if (_isPanning)
        {
            var pos = e.GetPosition(this);
            ImageScroll.ScrollToHorizontalOffset(_panScrollH - (pos.X - _panStart.X));
            ImageScroll.ScrollToVerticalOffset  (_panScrollV - (pos.Y - _panStart.Y));
            e.Handled = true;
        }
        else if (_bitmap is not null)
        {
            // Pixel inspection
            var posOnImage = e.GetPosition(ImageDisplay);
            int px = (int)(posOnImage.X / _zoom);
            int py = (int)(posOnImage.Y / _zoom);

            if (px >= 0 && py >= 0 && px < _bitmap.PixelWidth && py < _bitmap.PixelHeight)
            {
                var color = GetPixelColor(_bitmap, px, py);
                PixelText.Text = $"({px}, {py})  R:{color.R} G:{color.G} B:{color.B} A:{color.A}  #{color.R:X2}{color.G:X2}{color.B:X2}";
            }
            else
            {
                PixelText.Text = string.Empty;
            }
        }
    }

    private void ImageScroll_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_isPanning)
        {
            _isPanning = false;
            Mouse.Capture(null);
            e.Handled  = true;
        }
    }

    // -----------------------------------------------------------------------
    // Keyboard shortcuts
    // -----------------------------------------------------------------------

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if ((e.KeyboardDevice.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            switch (e.Key)
            {
                case Key.OemPlus:
                case Key.Add:    ApplyZoom(_zoom * 1.25); e.Handled = true; break;
                case Key.OemMinus:
                case Key.Subtract: ApplyZoom(_zoom / 1.25); e.Handled = true; break;
                case Key.D0:
                case Key.NumPad0: ApplyZoom(1.0); e.Handled = true; break;
            }
        }
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private void UpdateStatusBar()
    {
        if (_bitmap is null) { DimensionsText.Text = "—"; ColorModeText.Text = "—"; FileSizeText.Text = "—"; DpiText.Text = "—"; return; }

        DimensionsText.Text = $"{_bitmap.PixelWidth} × {_bitmap.PixelHeight} px";
        ColorModeText.Text  = FormatPixelFormat(_bitmap.Format);
        FileSizeText.Text   = File.Exists(_filePath) ? FormatSize(new FileInfo(_filePath).Length) : "—";
        DpiText.Text        = $"{_bitmap.DpiX:F0} × {_bitmap.DpiY:F0} DPI";
    }

    private static string FormatPixelFormat(PixelFormat fmt)
    {
        if (fmt == PixelFormats.Bgra32 || fmt == PixelFormats.Pbgra32) return "ARGB32";
        if (fmt == PixelFormats.Bgr32  || fmt == PixelFormats.Bgr24)   return "RGB";
        if (fmt == PixelFormats.Gray8)                                   return "Grayscale";
        if (fmt == PixelFormats.Indexed8|| fmt == PixelFormats.Indexed4) return "Indexed";
        return fmt.ToString();
    }

    private static string FormatSize(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        double size = bytes;
        int unit = 0;
        while (size >= 1024 && unit < units.Length - 1) { size /= 1024; unit++; }
        return $"{size:F1} {units[unit]}";
    }

    private static Color GetPixelColor(BitmapSource bmp, int x, int y)
    {
        try
        {
            var converted = new FormatConvertedBitmap(bmp, PixelFormats.Bgra32, null, 0);
            var pixels    = new byte[4];
            converted.CopyPixels(new Int32Rect(x, y, 1, 1), pixels, 4, 0);
            return Color.FromArgb(pixels[3], pixels[2], pixels[1], pixels[0]);
        }
        catch
        {
            return Colors.Transparent;
        }
    }

    private void CopyImage()
    {
        if (_bitmap is not null)
            Clipboard.SetImage(_bitmap);
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
