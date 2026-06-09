// ==========================================================
// Project: WpfHexEditor.Plugins.ScreenRecorder
// File: Views/TimelineStrip.xaml.cs
// Description: Code-behind for TimelineStrip — handles drag-drop reorder,
//              frame selection, scrubber click-to-seek, and keyboard shortcuts.
// Architecture Notes:
//     Drag data is the FrameCardViewModel itself (Move effect).
//     DragOver uses mouse position to compute the target insert index.
//     Scrubber fill width is updated via SizeChanged + PropertyChanged on SelectedIndex.
// ==========================================================

using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using WpfHexEditor.Plugins.ScreenRecorder.ViewModels;
using WpfHexEditor.SDK.Commands;

namespace WpfHexEditor.Plugins.ScreenRecorder.Views;

public partial class TimelineStrip : System.Windows.Controls.UserControl
{
    private const double FrameCardWidth = 104.0; // FrameCard Width(100) + Margin(2+2)
    private TimelineViewModel? _currentTimeline;

    public TimelineStrip()
    {
        InitializeComponent();
        KeyDown            += OnKeyDown;
        SizeChanged        += (_, _) => UpdateScrubber();
        DataContextChanged += OnDataContextChanged;
        Loaded             += (_, _) => SubscribeTimeline(DataContext as TimelineViewModel);
        Unloaded           += (_, _) => UnsubscribeTimeline();
        Focusable = true;
    }

    public void OnFrameCardSelected(FrameCardViewModel vm)
    {
        if (DataContext is TimelineViewModel tvm)
            tvm.SelectedFrame = vm;
    }

    // ── Scrubber ──────────────────────────────────────────────────────────────

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        UnsubscribeTimeline();
        SubscribeTimeline(e.NewValue as TimelineViewModel);
        UpdateScrubber();
    }

    private void SubscribeTimeline(TimelineViewModel? tvm)
    {
        if (tvm is null || ReferenceEquals(tvm, _currentTimeline)) return;
        _currentTimeline = tvm;
        tvm.PropertyChanged += OnTimelinePropertyChanged;
        tvm.InsertImageCommand ??= new RelayCommand(_ => InsertImageFromDialog());
    }

    private void UnsubscribeTimeline()
    {
        if (_currentTimeline is null) return;
        _currentTimeline.PropertyChanged -= OnTimelinePropertyChanged;
        _currentTimeline = null;
    }

    private void OnTimelinePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(TimelineViewModel.SelectedIndex) or nameof(TimelineViewModel.Frames))
        {
            UpdateScrubber();
            UpdateMarker();
            if (e.PropertyName == nameof(TimelineViewModel.SelectedIndex)
                && DataContext is TimelineViewModel { IsPlaying: true } tvm2)
                ScrollToFrame(tvm2.SelectedIndex);
        }
        else if (e.PropertyName is nameof(TimelineViewModel.IsPlaying))
        {
            UpdateMarker();
        }
    }

    private void ScrollToFrame(int idx)
    {
        var offset = idx * FrameCardWidth;
        var visible = FrameScroll.ViewportWidth;
        if (offset < FrameScroll.HorizontalOffset || offset + FrameCardWidth > FrameScroll.HorizontalOffset + visible)
            FrameScroll.ScrollToHorizontalOffset(Math.Max(0, offset - visible / 2));
    }

    private void UpdateMarker()
    {
        if (DataContext is not TimelineViewModel tvm || !tvm.IsPlaying || tvm.Frames.Count == 0)
        {
            MarkerLine.Height = 0;
            return;
        }
        var idx    = Math.Max(0, tvm.SelectedIndex);
        var center = idx * FrameCardWidth + FrameCardWidth / 2 - FrameScroll.HorizontalOffset;
        Canvas.SetLeft(MarkerLine,     center - 1);
        Canvas.SetLeft(MarkerTriangle, center);
        MarkerLine.Height = MarkerCanvas.ActualHeight;
    }

    private void UpdateScrubber()
    {
        if (DataContext is not TimelineViewModel tvm || tvm.Frames.Count == 0)
        {
            ScrubberFill.Width = 0;
            return;
        }
        var trackWidth = ScrubberTrack.ActualWidth;
        if (trackWidth <= 0) return;

        var idx   = Math.Max(0, tvm.SelectedIndex);
        var ratio = (double)(idx + 1) / tvm.Frames.Count;
        ScrubberFill.Width = trackWidth * ratio;
    }

    private void OnScrubberClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not TimelineViewModel tvm || tvm.Frames.Count == 0) return;
        var x     = e.GetPosition(ScrubberTrack).X;
        var ratio = Math.Clamp(x / ScrubberTrack.ActualWidth, 0, 1);
        var idx   = (int)(ratio * tvm.Frames.Count);
        idx = Math.Clamp(idx, 0, tvm.Frames.Count - 1);
        tvm.SelectedFrame = tvm.Frames[idx];
        e.Handled = true;
    }

    // ── Keyboard shortcuts ────────────────────────────────────────────────────

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not TimelineViewModel tvm) return;

        if (e.Key == Key.Delete)
        {
            tvm.DeleteSelectedCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.D && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            tvm.DuplicateFrameCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.Left && tvm.SelectedIndex > 0)
        {
            tvm.SelectedFrame = tvm.Frames[tvm.SelectedIndex - 1];
            e.Handled = true;
        }
        else if (e.Key == Key.Right && tvm.SelectedIndex < tvm.Frames.Count - 1)
        {
            tvm.SelectedFrame = tvm.Frames[tvm.SelectedIndex + 1];
            e.Handled = true;
        }
    }

    // ── Drag-drop reorder ─────────────────────────────────────────────────────

    private void InsertImageFromDialog()
    {
        if (DataContext is not TimelineViewModel tvm) return;

        var dlg = new OpenFileDialog
        {
            Title      = Properties.ScreenRecorderResources.ScreenRecorder_InsertImage,
            Filter     = "Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tiff;*.tif;*.webp|All files|*.*",
            Multiselect = true
        };
        if (dlg.ShowDialog() != true) return;

        var frames = dlg.FileNames
            .Select(p => TryLoadImageFrame(p, tvm.GlobalDelay))
            .OfType<FrameCardViewModel>()
            .ToList();

        if (frames.Count > 0)
        {
            var insertAt = tvm.SelectedFrame is null ? tvm.Frames.Count
                           : tvm.Frames.IndexOf(tvm.SelectedFrame) + 1;
            tvm.InsertFramesAt(frames, insertAt);
        }
    }

    private static readonly HashSet<string> _imageExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tiff", ".tif", ".webp" };

    private void OnFrameDrop(object sender, DragEventArgs e)
    {
        if (DataContext is not TimelineViewModel tvm) return;

        if (e.Data.GetDataPresent(typeof(FrameCardViewModel)))
        {
            var dragged   = (FrameCardViewModel)e.Data.GetData(typeof(FrameCardViewModel));
            var toIndex   = ComputeDropIndex(e.GetPosition(FrameList));
            var fromIndex = tvm.Frames.IndexOf(dragged);
            if (fromIndex >= 0 && toIndex >= 0) tvm.MoveFrame(fromIndex, toIndex);
            return;
        }

        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var paths  = (string[])e.Data.GetData(DataFormats.FileDrop);
            var frames = paths
                .Where(p => _imageExtensions.Contains(Path.GetExtension(p)))
                .OrderBy(p => p)
                .Select(p => TryLoadImageFrame(p, tvm.GlobalDelay))
                .OfType<FrameCardViewModel>()
                .ToList();

            if (frames.Count > 0)
                tvm.InsertFramesAt(frames, ComputeDropIndex(e.GetPosition(FrameList)));
        }
    }

    private void OnFrameDragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(typeof(FrameCardViewModel)))
            e.Effects = DragDropEffects.Move;
        else if (e.Data.GetDataPresent(DataFormats.FileDrop) &&
                 e.Data.GetData(DataFormats.FileDrop) is string[] paths &&
                 paths.Any(p => _imageExtensions.Contains(Path.GetExtension(p))))
            e.Effects = DragDropEffects.Copy;
        else
            e.Effects = DragDropEffects.None;
        e.Handled = true;
    }

    private static FrameCardViewModel? TryLoadImageFrame(string path, int delay)
    {
        try
        {
            var full = new BitmapImage();
            full.BeginInit();
            full.UriSource       = new Uri(path, UriKind.Absolute);
            full.CacheOption     = BitmapCacheOption.OnLoad;
            full.EndInit();
            full.Freeze();

            var thumb = new TransformedBitmap(full,
                new System.Windows.Media.ScaleTransform(
                    Math.Min(1.0, 120.0 / full.PixelWidth),
                    Math.Min(1.0, 120.0 / full.PixelHeight)));
            thumb.Freeze();

            return new FrameCardViewModel(0, thumb, delay, full) { SourcePath = path };
        }
        catch
        {
            return null;
        }
    }

    private int ComputeDropIndex(Point position)
    {
        if (DataContext is not TimelineViewModel tvm || tvm.Frames.Count == 0) return 0;
        return Math.Clamp((int)(position.X / FrameCardWidth), 0, tvm.Frames.Count - 1);
    }
}
