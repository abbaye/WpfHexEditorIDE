// ==========================================================
// Project: WpfHexEditor.Plugins.ScreenRecorder
// File: Views/TimelineStrip.xaml.cs
// Description: Code-behind for TimelineStrip — handles drag-drop reorder and
//              frame selection notification to PreviewViewModel.
// Architecture Notes:
//     Drag data is the FrameCardViewModel itself (Move effect).
//     DragOver uses mouse position to compute the target insert index.
//     ScrollViewer does not participate in drag — only the ItemsControl panel does.
// ==========================================================

using System.Windows;
using System.Windows.Input;
using WpfHexEditor.Plugins.ScreenRecorder.ViewModels;

namespace WpfHexEditor.Plugins.ScreenRecorder.Views;

public partial class TimelineStrip : System.Windows.Controls.UserControl
{
    public TimelineStrip()
    {
        InitializeComponent();
        KeyDown += OnKeyDown;
        Focusable = true;
    }

    public void OnFrameCardSelected(FrameCardViewModel vm)
    {
        if (DataContext is TimelineViewModel tvm)
            tvm.SelectedFrame = vm;
    }

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
    }

    private void OnFrameDrop(object sender, DragEventArgs e)
    {
        if (DataContext is not TimelineViewModel tvm) return;
        if (!e.Data.GetDataPresent(typeof(FrameCardViewModel))) return;

        var dragged = (FrameCardViewModel)e.Data.GetData(typeof(FrameCardViewModel));
        var toIndex = ComputeDropIndex(e.GetPosition(FrameList));
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
