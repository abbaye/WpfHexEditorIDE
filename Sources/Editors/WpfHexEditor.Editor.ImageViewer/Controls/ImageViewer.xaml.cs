// ==========================================================
// Project: WpfHexEditor.Editor.ImageViewer
// File: Controls/ImageViewer.xaml.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-06
// Description:
//     Code-behind for the ImageViewer control. Hosts a non-destructive
//     transform pipeline (rotate, flip, crop, resize) on top of a
//     read-only BitmapSource. Contributes toolbar items, status bar
//     items, and an Office-like context menu to the App shell.
//
// Architecture Notes:
//     Pattern: Pipeline (ImageTransformPipeline) + Strategy (IImageTransform)
//     _bitmap        = original frozen BitmapSource (never mutated)
//     _displayBitmap = pipeline result shown in ImageDisplay
//     Transforms persisted via EditorConfigDto.Extra["Transforms"] (JSON).
//     No .whchg usage — changeset system is for byte-level binary edits only.
//
// ==========================================================

using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WpfHexEditor.Editor.Core;
using Rectangle = System.Windows.Shapes.Rectangle;
using WpfHexEditor.Editor.ImageViewer.Dialogs;
using WpfHexEditor.Editor.ImageViewer.Properties;
using WpfHexEditor.Editor.ImageViewer.Transforms;

namespace WpfHexEditor.Editor.ImageViewer.Controls;

/// <summary>
/// Image viewer with non-destructive transform pipeline, zoom, pan, and pixel inspection.
/// </summary>
public sealed partial class ImageViewer : UserControl,
    IDocumentEditor, IOpenableDocument, IStatusBarContributor, IEditorToolbarContributor, IEditorPersistable
{
    // -----------------------------------------------------------------------
    // Fields
    // -----------------------------------------------------------------------

    private string _filePath = string.Empty;

    // Original bitmap — never modified
    private BitmapSource? _bitmap;

    // Displayed bitmap = pipeline applied to _bitmap
    private BitmapSource? _displayBitmap;

    // Non-destructive transform pipeline + redo stack
    private readonly ImageTransformPipeline      _pipeline   = new();
    private readonly Stack<IImageTransform>      _redoStack  = new();

    // Zoom
    private double _zoom = 1.0;

    // Pan state
    private bool  _isPanning;
    private Point _panStart;
    private double _panScrollH;
    private double _panScrollV;

    // Crop selection state
    private bool  _isCropMode;
    private bool  _isDraggingCrop;
    private Point _cropDragStart;   // in CropOverlay canvas coordinates
    private Rect  _cropRect = Rect.Empty;

    // Crop handle drag state
    private string? _activeCropHandle;
    private Point   _cropHandleDragStart;
    private Rect    _cropRectAtHandleDragStart;

    // Persistence
    private double _restoredZoom;   // 0 = "fit to window"
    private string _restoredTransforms = string.Empty;

    // -----------------------------------------------------------------------
    // Toolbar + Status bar collections
    // -----------------------------------------------------------------------

    public ObservableCollection<EditorToolbarItem> ToolbarItems { get; } = new();

    private readonly StatusBarItem _zoomItem       = new() { Label = "Zoom",       Value = "100%" };
    private readonly StatusBarItem _dimensionsItem = new() { Label = "Size",       Value = "—" };
    private readonly StatusBarItem _colorModeItem  = new() { Label = "Format",     Value = "—" };
    private readonly StatusBarItem _fileSizeItem   = new() { Label = "File",       Value = "—" };
    private readonly StatusBarItem _dpiItem        = new() { Label = "DPI",        Value = "—" };
    private readonly StatusBarItem _pixelItem      = new() { Label = "Pixel",      Value = "—" };
    private readonly StatusBarItem _transformsItem = new() { Label = "Transforms", Value = "None" };

    // -----------------------------------------------------------------------
    // Constructor
    // -----------------------------------------------------------------------

    public ImageViewer()
    {
        InitializeComponent();

        UndoCommand      = new RelayCommand(Undo, () => CanUndo);
        RedoCommand      = new RelayCommand(Redo, () => CanRedo);
        SaveCommand      = new RelayCommand(Save,
            () => _pipeline.Count > 0 && _displayBitmap is not null && !string.IsNullOrEmpty(_filePath));
        CopyCommand      = new RelayCommand(CopyImage,        () => _displayBitmap is not null);
        CutCommand       = new RelayCommand(() => { },        () => false);
        PasteCommand     = new RelayCommand(() => { },        () => false);
        DeleteCommand    = new RelayCommand(() => { },        () => false);
        SelectAllCommand = new RelayCommand(() => { },        () => false);

        BuildToolbarItems();
        BuildStatusBarItems();
    }

    // -----------------------------------------------------------------------
    // Toolbar builder
    // -----------------------------------------------------------------------

    private void BuildToolbarItems()
    {
        // Zoom pod (existing)
        ToolbarItems.Add(new EditorToolbarItem { Icon = "\uE8A3", Tooltip = "Zoom In (Ctrl+=)",  Command = new RelayCommand(() => ApplyZoom(_zoom * 1.25)) });
        ToolbarItems.Add(new EditorToolbarItem { Icon = "\uE71F", Tooltip = "Zoom Out (Ctrl+-)", Command = new RelayCommand(() => ApplyZoom(_zoom / 1.25)) });
        ToolbarItems.Add(new EditorToolbarItem { IsSeparator = true });
        ToolbarItems.Add(new EditorToolbarItem { Label = "1:1",   Tooltip = "Actual Size",       Command = new RelayCommand(() => ApplyZoom(1.0)) });
        ToolbarItems.Add(new EditorToolbarItem { Icon = "\uE799", Tooltip = "Fit to Window",     Command = new RelayCommand(FitToWindow) });

        // Transform pod
        ToolbarItems.Add(new EditorToolbarItem { IsSeparator = true });
        ToolbarItems.Add(new EditorToolbarItem { Icon = "\uE7AD", Tooltip = "Rotate Right 90° (Ctrl+R)",       Command = new RelayCommand(RotateRight,   () => _bitmap is not null) });
        ToolbarItems.Add(new EditorToolbarItem { Icon = "\uE7AE", Tooltip = "Rotate Left 90° (Ctrl+Shift+R)",  Command = new RelayCommand(RotateLeft,    () => _bitmap is not null) });
        ToolbarItems.Add(new EditorToolbarItem { Icon = "\uE8B1", Tooltip = "Flip Horizontal (Ctrl+H)",        Command = new RelayCommand(FlipH,         () => _bitmap is not null) });
        ToolbarItems.Add(new EditorToolbarItem { Icon = "\uE8B0", Tooltip = "Flip Vertical (Ctrl+Shift+H)",    Command = new RelayCommand(FlipV,         () => _bitmap is not null) });

        // Crop & Resize pod
        ToolbarItems.Add(new EditorToolbarItem { IsSeparator = true });
        ToolbarItems.Add(new EditorToolbarItem { Icon = "\uE7A8", Tooltip = "Crop Mode (Ctrl+K)",              Command = new RelayCommand(ToggleCropMode, () => _bitmap is not null) });
        ToolbarItems.Add(new EditorToolbarItem { Icon = "\uE8D4", Tooltip = "Resize... (Ctrl+Shift+Z)",        Command = new RelayCommand(OpenResizeDialog, () => _bitmap is not null) });

        // Export pod
        ToolbarItems.Add(new EditorToolbarItem { IsSeparator = true });
        ToolbarItems.Add(new EditorToolbarItem { Icon = "\uE792", Tooltip = "Export... (Ctrl+Shift+S)",        Command = new RelayCommand(() => SaveAs(), () => _displayBitmap is not null) });
    }

    // -----------------------------------------------------------------------
    // Status bar builder
    // -----------------------------------------------------------------------

    private void BuildStatusBarItems()
    {
        // Zoom presets
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
        StatusBarItems.Add(_dimensionsItem);
        StatusBarItems.Add(_colorModeItem);
        StatusBarItems.Add(_fileSizeItem);
        StatusBarItems.Add(_dpiItem);
        StatusBarItems.Add(_pixelItem);
        StatusBarItems.Add(_transformsItem);
    }

    // -----------------------------------------------------------------------
    // IDocumentEditor — State
    // -----------------------------------------------------------------------

    public bool IsDirty => _pipeline.Count > 0;
    public bool CanUndo => _pipeline.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;

    public bool IsReadOnly
    {
        get => true;
        set { /* always read-only on disk; transforms are non-destructive */ }
    }

    public string Title { get; private set; } = "Image";
    public bool   IsBusy { get; private set; }

    // -----------------------------------------------------------------------
    // IDocumentEditor — Commands
    // -----------------------------------------------------------------------

    public ICommand UndoCommand      { get; }
    public ICommand RedoCommand      { get; }
    public ICommand SaveCommand      { get; }
    public ICommand CopyCommand      { get; }
    public ICommand CutCommand       { get; }
    public ICommand PasteCommand     { get; }
    public ICommand DeleteCommand    { get; }
    public ICommand SelectAllCommand { get; }

    // -----------------------------------------------------------------------
    // IStatusBarContributor
    // -----------------------------------------------------------------------

    public ObservableCollection<StatusBarItem> StatusBarItems { get; } = new();

    void IStatusBarContributor.RefreshStatusBarItems() => UpdateStatusBar();

    // -----------------------------------------------------------------------
    // IDocumentEditor — Events
    // -----------------------------------------------------------------------

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

    // -----------------------------------------------------------------------
    // IDocumentEditor — Methods
    // -----------------------------------------------------------------------

    public void Undo()
    {
        if (_pipeline.Count == 0) return;
        var step = _pipeline.Steps[^1];
        _pipeline.RemoveAt(_pipeline.Count - 1);
        _redoStack.Push(step);
        UpdateDisplayBitmap();
        UpdateTransformsStatusItem();
        NotifyUndoRedoChanged();
        ModifiedChanged?.Invoke(this, EventArgs.Empty);
        StatusMessage?.Invoke(this, $"Undo: {step.Name}");
    }

    public void Redo()
    {
        if (_redoStack.Count == 0) return;
        var step = _redoStack.Pop();
        _pipeline.Add(step);
        UpdateDisplayBitmap();
        UpdateTransformsStatusItem();
        NotifyUndoRedoChanged();
        ModifiedChanged?.Invoke(this, EventArgs.Empty);
        StatusMessage?.Invoke(this, $"Redo: {step.Name}");
    }

    private void NotifyUndoRedoChanged()
    {
        CanUndoChanged?.Invoke(this, EventArgs.Empty);
        CanRedoChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Save()
    {
        if (_pipeline.Count == 0 || _displayBitmap is null || string.IsNullOrEmpty(_filePath)) return;

        try
        {
            var encoder = GetEncoder(Path.GetExtension(_filePath));
            encoder.Frames.Add(BitmapFrame.Create(_displayBitmap));
            using var stream = new FileStream(_filePath, FileMode.Create, FileAccess.Write, FileShare.None);
            encoder.Save(stream);

            // The saved result becomes the new base; pipeline and redo history are cleared.
            _bitmap = _displayBitmap;
            _pipeline.Clear();
            _redoStack.Clear();
            UpdateDisplayBitmap();
            UpdateTransformsStatusItem();
            NotifyUndoRedoChanged();
            ModifiedChanged?.Invoke(this, EventArgs.Empty);
            TitleChanged?.Invoke(this, Title);

            var msg = $"Saved: {Path.GetFileName(_filePath)}";
            StatusMessage?.Invoke(this, msg);
            OutputMessage?.Invoke(this, msg);
        }
        catch (Exception ex)
        {
            var msg = $"Save failed '{Path.GetFileName(_filePath)}': {ex.Message}";
            StatusMessage?.Invoke(this, msg);
            OutputMessage?.Invoke(this, msg);
        }
    }

    public async Task SaveAsync(CancellationToken ct = default)
    {
        if (_pipeline.Count == 0 || _displayBitmap is null || string.IsNullOrEmpty(_filePath)) return;

        // Capture frozen refs — BitmapSource.Freeze() makes them safe on background threads.
        var bitmap  = _displayBitmap;
        var path    = _filePath;
        var encoder = GetEncoder(Path.GetExtension(path));
        encoder.Frames.Add(BitmapFrame.Create(bitmap));

        try
        {
            await Task.Run(() =>
            {
                ct.ThrowIfCancellationRequested();
                using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
                encoder.Save(stream);
            }, ct);

            // Back on the UI thread after await — safe to update WPF state.
            _bitmap = _displayBitmap;
            _pipeline.Clear();
            _redoStack.Clear();
            UpdateDisplayBitmap();
            UpdateTransformsStatusItem();
            NotifyUndoRedoChanged();
            ModifiedChanged?.Invoke(this, EventArgs.Empty);
            TitleChanged?.Invoke(this, Title);

            var msg = $"Saved: {Path.GetFileName(_filePath)}";
            StatusMessage?.Invoke(this, msg);
            OutputMessage?.Invoke(this, msg);
        }
        catch (OperationCanceledException) { /* silent — user cancelled */ }
        catch (Exception ex)
        {
            var msg = $"Save failed '{Path.GetFileName(path)}': {ex.Message}";
            StatusMessage?.Invoke(this, msg);
            OutputMessage?.Invoke(this, msg);
        }
    }

    public Task SaveAsAsync(string filePath, CancellationToken ct = default)
    {
        SaveAs(filePath);
        return Task.CompletedTask;
    }

    public void Copy() => CopyImage();
    public void Cut()  { }
    public void Paste(){ }
    public void Delete(){ }
    public void SelectAll(){ }

    public void Close()
    {
        _bitmap        = null;
        _displayBitmap = null;
        ImageDisplay.Source = null;
        _pipeline.Clear();
        _redoStack.Clear();
    }

    public void CancelOperation() { }

    // -----------------------------------------------------------------------
    // IEditorPersistable
    // -----------------------------------------------------------------------

    public EditorConfigDto GetEditorConfig() => new()
    {
        Extra = new Dictionary<string, string>
        {
            ["ZoomFactor"] = _zoom.ToString(CultureInfo.InvariantCulture),
            ["Transforms"] = _pipeline.Count > 0 ? _pipeline.ToJson() : string.Empty
        }
    };

    public void ApplyEditorConfig(EditorConfigDto config)
    {
        if (config?.Extra is null) return;

        if (config.Extra.TryGetValue("ZoomFactor", out var s) &&
            double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var z) && z > 0)
            _restoredZoom = z;

        if (config.Extra.TryGetValue("Transforms", out var t) && !string.IsNullOrWhiteSpace(t))
            _restoredTransforms = t;
    }

    public byte[]? GetUnsavedModifications()      => null;
    public void ApplyUnsavedModifications(byte[] data) { }
    public IReadOnlyList<BookmarkDto>? GetBookmarks() => null;
    public void ApplyBookmarks(IReadOnlyList<BookmarkDto> bookmarks) { }

    // Image viewers have no byte-level edits — always return empty snapshot.
    public ChangesetSnapshot GetChangesetSnapshot() => ChangesetSnapshot.Empty;
    public void ApplyChangeset(ChangesetDto changeset) { }
    public void MarkChangesetSaved() { }

    // -----------------------------------------------------------------------
    // IOpenableDocument
    // -----------------------------------------------------------------------

    public async Task OpenAsync(string filePath, CancellationToken ct = default)
    {
        IsBusy = true;
        OperationStarted?.Invoke(this, new DocumentOperationEventArgs { Title = ImageViewerResources.ImageViewer_LoadingImage, IsIndeterminate = true });

        try
        {
            _filePath = filePath;
            Title = Path.GetFileName(filePath);

            var bitmap = await Task.Run(() =>
            {
                // Load via FileStream so the file handle is released immediately after
                // EndInit() — BitmapImage.UriSource can hold a lock even with OnLoad.
                // FileShare.ReadWrite: allows the HexEditor (or any other consumer) to
                // have the same file open concurrently without causing an IOException.
                using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                var img = new BitmapImage();
                img.BeginInit();
                img.StreamSource = stream;
                img.CacheOption  = BitmapCacheOption.OnLoad;
                img.EndInit();
                img.Freeze();
                return (BitmapSource)img;
            }, ct);

            _bitmap = bitmap;

            // Restore transforms from persistence before displaying
            if (!string.IsNullOrWhiteSpace(_restoredTransforms))
            {
                var restored = ImageTransformPipeline.FromJson(_restoredTransforms);
                foreach (var step in restored.Steps)
                    _pipeline.Add(step);
                _restoredTransforms = string.Empty;
            }

            UpdateDisplayBitmap();

            if (_restoredZoom > 0)
                ApplyZoom(_restoredZoom);
            else
            {
                Loaded += OnLoadedFitToWindow;
                if (IsLoaded) FitToWindow();
            }

            UpdateStatusBar();
            UpdateTransformsStatusItem();

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
            var msg = $"Cannot open image '{Path.GetFileName(filePath)}': {ex.Message}";
            StatusMessage?.Invoke(this, msg);
            // ErrorMessage carries the full contextual message; MainWindow logs it as Error and closes the tab.
            OperationCompleted?.Invoke(this, new DocumentOperationCompletedEventArgs
            {
                Success           = false,
                ErrorMessage      = msg,
                CloseTabOnFailure = true
            });
        }
        finally
        {
            IsBusy = false;
        }
    }

    // -----------------------------------------------------------------------
    // Transform pipeline helpers
    // -----------------------------------------------------------------------

    private void UpdateDisplayBitmap()
    {
        if (_bitmap is null) return;
        _displayBitmap = _pipeline.Count > 0 ? _pipeline.Apply(_bitmap) : _bitmap;
        ImageDisplay.Source = _displayBitmap;
        // Update dimensions status to reflect the transformed image
        _dimensionsItem.Value = $"{_displayBitmap.PixelWidth} × {_displayBitmap.PixelHeight} px";
    }

    private void ApplyTransform(IImageTransform transform)
    {
        if (_bitmap is null) return;
        _redoStack.Clear(); // A new action clears the redo history.
        _pipeline.Add(transform);
        UpdateDisplayBitmap();
        UpdateTransformsStatusItem();
        NotifyUndoRedoChanged();
        ModifiedChanged?.Invoke(this, EventArgs.Empty);
        StatusMessage?.Invoke(this, $"Applied: {transform.Name}");
    }

    private void UpdateTransformsStatusItem()
    {
        var count = _pipeline.Count;
        _transformsItem.Value = count == 0 ? "None" : $"{count} applied";

        _transformsItem.Choices.Clear();
        for (int i = 0; i < _pipeline.Steps.Count; i++)
        {
            var step = _pipeline.Steps[i];
            var idx  = i;
            _transformsItem.Choices.Add(new StatusBarChoice
            {
                DisplayName = step.Name,
                IsActive    = false,
                Command     = new RelayCommand(() =>
                {
                    _pipeline.RemoveAt(idx);
                    UpdateDisplayBitmap();
                    UpdateTransformsStatusItem();
                    ModifiedChanged?.Invoke(this, EventArgs.Empty);
                })
            });
        }

        if (count > 0)
        {
            _transformsItem.Choices.Add(new StatusBarChoice
            {
                DisplayName = "Reset All",
                IsActive    = false,
                Command     = new RelayCommand(ResetAllTransforms)
            });
        }
    }

    // -----------------------------------------------------------------------
    // Transform actions (called from toolbar, context menu, keyboard)
    // -----------------------------------------------------------------------

    public void RotateRight() => ApplyTransform(new RotateImageTransform(90));
    public void RotateLeft()  => ApplyTransform(new RotateImageTransform(270));
    public void FlipH()       => ApplyTransform(new FlipImageTransform(FlipAxis.Horizontal));
    public void FlipV()       => ApplyTransform(new FlipImageTransform(FlipAxis.Vertical));

    public void ResetAllTransforms()
    {
        if (_pipeline.Count == 0 && _redoStack.Count == 0) return;
        _pipeline.Clear();
        _redoStack.Clear();
        UpdateDisplayBitmap();
        UpdateTransformsStatusItem();
        NotifyUndoRedoChanged();
        ModifiedChanged?.Invoke(this, EventArgs.Empty);
        StatusMessage?.Invoke(this, "All transforms reset");
    }

    public void OpenResizeDialog()
    {
        if (_displayBitmap is null) return;

        var dlg = new ResizeImageDialog(_displayBitmap.PixelWidth, _displayBitmap.PixelHeight)
        {
            Owner = Window.GetWindow(this)
        };

        if (dlg.ShowDialog() != true) return;

        ApplyTransform(new ResizeImageTransform(dlg.TargetWidth, dlg.TargetHeight, dlg.Algorithm));
    }

    // -----------------------------------------------------------------------
    // Crop mode
    // -----------------------------------------------------------------------

    public void ToggleCropMode()
    {
        _isCropMode = !_isCropMode;
        CropOverlay.Visibility           = _isCropMode ? Visibility.Visible : Visibility.Collapsed;
        CropInstructionBanner.Visibility = _isCropMode ? Visibility.Visible : Visibility.Collapsed;
        _isDraggingCrop                  = false;
        _cropRect                        = Rect.Empty;
        UpdateCropUI();

        // Grab keyboard focus so Enter/Escape are handled by this control, not by toolbar buttons.
        if (_isCropMode) Focus();

        StatusMessage?.Invoke(this, _isCropMode ? "Crop Mode: draw a selection rectangle" : "Crop Mode off");
    }

    private void ApplyCrop()
    {
        if (!_isCropMode || _displayBitmap is null || _cropRect.IsEmpty) return;

        // Map canvas coordinates → image pixel coordinates
        var imageTopLeft = ImageDisplay.TranslatePoint(new Point(0, 0), CropOverlay);
        double x = (_cropRect.X - imageTopLeft.X) / _zoom;
        double y = (_cropRect.Y - imageTopLeft.Y) / _zoom;
        double w = _cropRect.Width  / _zoom;
        double h = _cropRect.Height / _zoom;

        // Clamp to image bounds
        int ix = Math.Max(0, (int)x);
        int iy = Math.Max(0, (int)y);
        int iw = Math.Min(_displayBitmap.PixelWidth  - ix, (int)w);
        int ih = Math.Min(_displayBitmap.PixelHeight - iy, (int)h);

        if (iw <= 0 || ih <= 0)
        {
            StatusMessage?.Invoke(this, "Crop selection is empty — no crop applied");
            ToggleCropMode();
            return;
        }

        ToggleCropMode();
        ApplyTransform(new CropImageTransform(new Int32Rect(ix, iy, iw, ih)));
    }

    // Updates the CropSelection rectangle, 4 veil regions, and 8 handles from _cropRect.
    private void UpdateCropUI()
    {
        if (_cropRect.IsEmpty)
        {
            CropSelection.Visibility = Visibility.Collapsed;
        }
        else
        {
            Canvas.SetLeft(CropSelection, _cropRect.X);
            Canvas.SetTop(CropSelection,  _cropRect.Y);
            CropSelection.Width      = _cropRect.Width;
            CropSelection.Height     = _cropRect.Height;
            CropSelection.Visibility = Visibility.Visible;
        }

        UpdateCropVeil();
        UpdateCropHandles();
    }

    private void UpdateCropVeil()
    {
        if (_cropRect.IsEmpty)
        {
            VeilTop.Width = VeilTop.Height = VeilBottom.Width = VeilBottom.Height = 0;
            VeilLeft.Width = VeilLeft.Height = VeilRight.Width = VeilRight.Height = 0;
            return;
        }

        double cw = CropOverlay.ActualWidth;
        double ch = CropOverlay.ActualHeight;
        double x = _cropRect.X, y = _cropRect.Y, w = _cropRect.Width, h = _cropRect.Height;

        // Top: full width, from canvas top to crop top
        Canvas.SetLeft(VeilTop, 0);  Canvas.SetTop(VeilTop, 0);
        VeilTop.Width  = cw;         VeilTop.Height = y;

        // Bottom: full width, from crop bottom to canvas bottom
        Canvas.SetLeft(VeilBottom, 0);  Canvas.SetTop(VeilBottom, y + h);
        VeilBottom.Width  = cw;         VeilBottom.Height = Math.Max(0, ch - y - h);

        // Left: left of crop rect, between top and bottom veils
        Canvas.SetLeft(VeilLeft, 0);  Canvas.SetTop(VeilLeft, y);
        VeilLeft.Width  = x;          VeilLeft.Height = h;

        // Right: right of crop rect, between top and bottom veils
        Canvas.SetLeft(VeilRight, x + w);  Canvas.SetTop(VeilRight, y);
        VeilRight.Width  = Math.Max(0, cw - x - w);  VeilRight.Height = h;
    }

    private void UpdateCropHandles()
    {
        var handles = new[]
        {
            CropHandleTL, CropHandleTC, CropHandleTR,
            CropHandleML, CropHandleMR,
            CropHandleBL, CropHandleBC, CropHandleBR
        };

        if (_cropRect.IsEmpty)
        {
            foreach (var h in handles) h.Visibility = Visibility.Collapsed;
            return;
        }

        double x = _cropRect.X, y = _cropRect.Y, w = _cropRect.Width, rh = _cropRect.Height;
        double mx = x + w / 2, my = y + rh / 2;
        const double ho = 4.5; // half of handle size (9px)

        void Place(Rectangle r, double cx, double cy)
        {
            Canvas.SetLeft(r, cx - ho);
            Canvas.SetTop(r,  cy - ho);
            r.Visibility = Visibility.Visible;
        }

        Place(CropHandleTL, x,       y);
        Place(CropHandleTC, mx,      y);
        Place(CropHandleTR, x + w,   y);
        Place(CropHandleML, x,       my);
        Place(CropHandleMR, x + w,   my);
        Place(CropHandleBL, x,       y + rh);
        Place(CropHandleBC, mx,      y + rh);
        Place(CropHandleBR, x + w,   y + rh);
    }

    private void CropOverlay_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!_isCropMode) return;
        Focus(); // Reclaim keyboard focus so Enter/Escape reach ImageViewer_KeyDown.
        _cropDragStart  = e.GetPosition(CropOverlay);
        _isDraggingCrop = true;
        _cropRect       = Rect.Empty;
        UpdateCropUI();
        CropOverlay.CaptureMouse();
        e.Handled = true;
    }

    private void CropOverlay_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDraggingCrop) return;

        var current = e.GetPosition(CropOverlay);
        double x = Math.Min(_cropDragStart.X, current.X);
        double y = Math.Min(_cropDragStart.Y, current.Y);
        double w = Math.Abs(current.X - _cropDragStart.X);
        double h = Math.Abs(current.Y - _cropDragStart.Y);

        if (w < 2 || h < 2) return;

        _cropRect = new Rect(x, y, w, h);
        UpdateCropUI();
        e.Handled = true;
    }

    private void CropOverlay_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDraggingCrop) return;
        _isDraggingCrop = false;
        CropOverlay.ReleaseMouseCapture();
        e.Handled = true;
        StatusMessage?.Invoke(this, "Press Enter to apply crop or Escape to cancel");
    }

    private void CropHandle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _activeCropHandle          = ((FrameworkElement)sender).Name;
        _cropHandleDragStart       = e.GetPosition(CropOverlay);
        _cropRectAtHandleDragStart = _cropRect;
        ((FrameworkElement)sender).CaptureMouse();
        e.Handled = true;
    }

    private void CropHandle_MouseMove(object sender, MouseEventArgs e)
    {
        if (_activeCropHandle is null) return;

        var pos = e.GetPosition(CropOverlay);
        double dx = pos.X - _cropHandleDragStart.X;
        double dy = pos.Y - _cropHandleDragStart.Y;

        _cropRect = ComputeNewCropRect(_activeCropHandle, _cropRectAtHandleDragStart, dx, dy);
        UpdateCropUI();
        e.Handled = true;
    }

    private void CropHandle_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _activeCropHandle = null;
        ((FrameworkElement)sender).ReleaseMouseCapture();
        e.Handled = true;
    }

    private Rect ComputeNewCropRect(string handle, Rect original, double dx, double dy)
    {
        const double minSize = 10.0;
        double cw = CropOverlay.ActualWidth;
        double ch = CropOverlay.ActualHeight;

        double left   = original.Left;
        double top    = original.Top;
        double right  = original.Right;
        double bottom = original.Bottom;

        switch (handle)
        {
            case nameof(CropHandleTL): left  += dx; top    += dy; break;
            case nameof(CropHandleTC):              top    += dy; break;
            case nameof(CropHandleTR): right += dx; top    += dy; break;
            case nameof(CropHandleML): left  += dx;               break;
            case nameof(CropHandleMR): right += dx;               break;
            case nameof(CropHandleBL): left  += dx; bottom += dy; break;
            case nameof(CropHandleBC):              bottom += dy; break;
            case nameof(CropHandleBR): right += dx; bottom += dy; break;
        }

        // Enforce minimum size (prevent inversion)
        if (right - left < minSize)
        {
            if (handle is nameof(CropHandleTL) or nameof(CropHandleML) or nameof(CropHandleBL))
                left  = right - minSize;
            else
                right = left  + minSize;
        }
        if (bottom - top < minSize)
        {
            if (handle is nameof(CropHandleTL) or nameof(CropHandleTC) or nameof(CropHandleTR))
                top    = bottom - minSize;
            else
                bottom = top    + minSize;
        }

        // Clamp within canvas bounds
        left   = Math.Max(0, left);
        top    = Math.Max(0, top);
        right  = Math.Min(cw, right);
        bottom = Math.Min(ch, bottom);

        return new Rect(left, top, right - left, bottom - top);
    }

    // -----------------------------------------------------------------------
    // Save As
    // -----------------------------------------------------------------------

    public void SaveAs(string? suggestedPath = null)
    {
        if (_displayBitmap is null) return;

        var dlg = new SaveFileDialog
        {
            Title  = ImageViewerResources.ImageViewer_SaveImageAs,
            Filter = "PNG Image (*.png)|*.png|JPEG Image (*.jpg;*.jpeg)|*.jpg;*.jpeg|BMP Image (*.bmp)|*.bmp|TIFF Image (*.tiff;*.tif)|*.tiff;*.tif",
            FilterIndex = 1,
            FileName = suggestedPath is not null
                ? Path.GetFileNameWithoutExtension(suggestedPath)
                : Path.GetFileNameWithoutExtension(_filePath)
        };

        if (dlg.ShowDialog(Window.GetWindow(this)) != true) return;

        try
        {
            var encoder = GetEncoder(Path.GetExtension(dlg.FileName));
            encoder.Frames.Add(BitmapFrame.Create(_displayBitmap));

            using var stream = File.Create(dlg.FileName);
            encoder.Save(stream);

            StatusMessage?.Invoke(this, $"Saved: {Path.GetFileName(dlg.FileName)}");
        }
        catch (Exception ex)
        {
            StatusMessage?.Invoke(this, $"Save As failed: {ex.Message}");
        }
    }

    private static BitmapEncoder GetEncoder(string extension) =>
        extension.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => new JpegBitmapEncoder { QualityLevel = 92 },
            ".bmp"            => new BmpBitmapEncoder(),
            ".tiff" or ".tif" => new TiffBitmapEncoder(),
            _                 => new PngBitmapEncoder()
        };

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

        _zoomItem.Value = $"{_zoom * 100:F0}%";
        foreach (var c in _zoomItem.Choices)
            c.IsActive = c.DisplayName != "Fit" &&
                         double.TryParse(c.DisplayName.TrimEnd('%'), out var pct) &&
                         Math.Abs(pct / 100 - _zoom) < 0.01;

        StatusMessage?.Invoke(this, $"Zoom: {_zoom * 100:F0}%");
    }

    private void FitToWindow()
    {
        if (_displayBitmap is null) return;
        if (ImageScroll.ActualWidth <= 0 || ImageScroll.ActualHeight <= 0) return;

        double zoomX = (ImageScroll.ActualWidth  - 32) / _displayBitmap.PixelWidth;
        double zoomY = (ImageScroll.ActualHeight - 32) / _displayBitmap.PixelHeight;
        ApplyZoom(Math.Min(zoomX, zoomY));
    }

    // -----------------------------------------------------------------------
    // Event handlers — keyboard
    // -----------------------------------------------------------------------

    private void ImageViewer_KeyDown(object sender, KeyEventArgs e)
    {
        bool ctrl  = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
        bool shift = (Keyboard.Modifiers & ModifierKeys.Shift)   != 0;

        // Crop mode — Enter/Escape
        if (_isCropMode)
        {
            if (e.Key == Key.Return) { ApplyCrop();       e.Handled = true; return; }
            if (e.Key == Key.Escape) { ToggleCropMode();  e.Handled = true; return; }
        }

        if (!ctrl) return;

        switch (e.Key)
        {
            // Zoom
            case Key.OemPlus:
            case Key.Add:        ApplyZoom(_zoom * 1.25); e.Handled = true; break;
            case Key.OemMinus:
            case Key.Subtract:   ApplyZoom(_zoom / 1.25); e.Handled = true; break;
            case Key.D0:
            case Key.NumPad0:    ApplyZoom(1.0);           e.Handled = true; break;

            // Rotate
            case Key.R when  shift: RotateLeft();          e.Handled = true; break;
            case Key.R:             RotateRight();          e.Handled = true; break;

            // Flip
            case Key.H when  shift: FlipV();               e.Handled = true; break;
            case Key.H:             FlipH();                e.Handled = true; break;

            // Undo / Redo
            case Key.Z when !shift: Undo();                 e.Handled = true; break;
            case Key.Y:             Redo();                 e.Handled = true; break;

            // Crop mode toggle
            case Key.K:             ToggleCropMode();       e.Handled = true; break;

            // Resize
            case Key.Z when  shift: OpenResizeDialog();     e.Handled = true; break;

            // Save As
            case Key.S when  shift: SaveAs();               e.Handled = true; break;
        }
    }

    // -----------------------------------------------------------------------
    // Event handlers — mouse scroll and pan
    // -----------------------------------------------------------------------

    private void ImageScroll_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            ApplyZoom(e.Delta > 0 ? _zoom * 1.15 : _zoom / 1.15);
            e.Handled = true;
        }
    }

    private void ImageScroll_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_isCropMode) return;  // pan disabled in crop mode

        if (e.MiddleButton == MouseButtonState.Pressed ||
            (e.LeftButton  == MouseButtonState.Pressed && (Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt))
        {
            _isPanning   = true;
            _panStart    = e.GetPosition(this);
            _panScrollH  = ImageScroll.HorizontalOffset;
            _panScrollV  = ImageScroll.VerticalOffset;
            Mouse.Capture(ImageScroll);
            e.Handled    = true;
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
            return;
        }

        // Pixel inspection on display bitmap
        if (_displayBitmap is null) return;
        var posOnImage = e.GetPosition(ImageDisplay);
        int px = (int)(posOnImage.X / _zoom);
        int py = (int)(posOnImage.Y / _zoom);

        if (px >= 0 && py >= 0 && px < _displayBitmap.PixelWidth && py < _displayBitmap.PixelHeight)
        {
            var color = GetPixelColor(_displayBitmap, px, py);
            _pixelItem.Value = $"({px}, {py})  R:{color.R} G:{color.G} B:{color.B} A:{color.A}  #{color.R:X2}{color.G:X2}{color.B:X2}";
        }
        else
        {
            _pixelItem.Value = "—";
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
    // Context menu click handlers (delegate to public transform methods)
    // -----------------------------------------------------------------------

    private void OnContextRotateRight(object sender, RoutedEventArgs e) => RotateRight();
    private void OnContextRotateLeft (object sender, RoutedEventArgs e) => RotateLeft();
    private void OnContextFlipH      (object sender, RoutedEventArgs e) => FlipH();
    private void OnContextFlipV      (object sender, RoutedEventArgs e) => FlipV();
    private void OnContextCropMode   (object sender, RoutedEventArgs e) => ToggleCropMode();
    private void OnContextResize     (object sender, RoutedEventArgs e) => OpenResizeDialog();
    private void OnContextSaveAs     (object sender, RoutedEventArgs e) => SaveAs();
    private void OnContextCopy       (object sender, RoutedEventArgs e) => CopyImage();
    private void OnContextResetTransforms(object sender, RoutedEventArgs e) => ResetAllTransforms();

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private void UpdateStatusBar()
    {
        if (_bitmap is null)
        {
            _dimensionsItem.Value = "—";
            _colorModeItem.Value  = "—";
            _fileSizeItem.Value   = "—";
            _dpiItem.Value        = "—";
            return;
        }

        var display = _displayBitmap ?? _bitmap;
        _dimensionsItem.Value = $"{display.PixelWidth} × {display.PixelHeight} px";
        _colorModeItem.Value  = FormatPixelFormat(_bitmap.Format);
        _fileSizeItem.Value   = File.Exists(_filePath) ? FormatSize(new FileInfo(_filePath).Length) : "—";
        _dpiItem.Value        = $"{_bitmap.DpiX:F0} × {_bitmap.DpiY:F0} DPI";
    }

    private static string FormatPixelFormat(PixelFormat fmt)
    {
        if (fmt == PixelFormats.Bgra32 || fmt == PixelFormats.Pbgra32) return "ARGB32";
        if (fmt == PixelFormats.Bgr32  || fmt == PixelFormats.Bgr24)   return "RGB";
        if (fmt == PixelFormats.Gray8)                                   return "Grayscale";
        if (fmt == PixelFormats.Indexed8 || fmt == PixelFormats.Indexed4) return "Indexed";
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
        if (_displayBitmap is not null)
            Clipboard.SetImage(_displayBitmap);
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
