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
using System.Windows;
using System.Windows.Input;
using WpfHexEditor.Plugins.ScreenRecorder.ViewModels;

namespace WpfHexEditor.Plugins.ScreenRecorder.Views;

public partial class TimelineStrip : System.Windows.Controls.UserControl
{
    public TimelineStrip()
    {
        InitializeComponent();
        KeyDown      += OnKeyDown;
        SizeChanged  += (_, _) => UpdateScrubber();
        DataContextChanged += OnDataContextChanged;
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
        if (e.OldValue is TimelineViewModel old)
            old.PropertyChanged -= OnTimelinePropertyChanged;
        if (e.NewValue is TimelineViewModel tvm)
            tvm.PropertyChanged += OnTimelinePropertyChanged;
        UpdateScrubber();
    }

    private void OnTimelinePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(TimelineViewModel.SelectedIndex) or nameof(TimelineViewModel.Frames))
            UpdateScrubber();
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

    private void OnFrameDrop(object sender, DragEventArgs e)
    {
        if (DataContext is not TimelineViewModel tvm) return;
        if (!e.Data.GetDataPresent(typeof(FrameCardViewModel))) return;

        var dragged   = (FrameCardViewModel)e.Data.GetData(typeof(FrameCardViewModel));
        var toIndex   = ComputeDropIndex(e.GetPosition(FrameList));
        var fromIndex = tvm.Frames.IndexOf(dragged);
        if (fromIndex >= 0 && toIndex >= 0) tvm.MoveFrame(fromIndex, toIndex);
    }

    private void OnFrameDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(typeof(FrameCardViewModel))
            ? DragDropEffects.Move
            : DragDropEffects.None;
        e.Handled = true;
    }

    private int ComputeDropIndex(Point position)
    {
        if (DataContext is not TimelineViewModel tvm || tvm.Frames.Count == 0) return 0;
        var cardWidth = 104.0; // FrameCard Width(100) + Margin(2+2)
        return Math.Clamp((int)(position.X / cardWidth), 0, tvm.Frames.Count - 1);
    }
}
