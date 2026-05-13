// Project: WpfHexEditor.Plugins.ScreenRecorder
// File: Views/FrameCard.xaml.cs
// Description: Code-behind for FrameCard — handles click selection and drag initiation.

using System.Windows;
using System.Windows.Input;
using WpfHexEditor.Plugins.ScreenRecorder.ViewModels;

namespace WpfHexEditor.Plugins.ScreenRecorder.Views;

public partial class FrameCard : System.Windows.Controls.UserControl
{
    private Point _dragStart;

    public FrameCard() => InitializeComponent();

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        _dragStart = e.GetPosition(this);

        if (DataContext is not FrameCardViewModel vm) return;

        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            vm.IsSelected = !vm.IsSelected;
        else if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            SelectRange(vm);
        else
        {
            ClearOtherSelections(vm);
            vm.IsSelected = true;
        }

        NotifyTimelineOfSelection(vm);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (e.LeftButton != MouseButtonState.Pressed) return;

        var delta = e.GetPosition(this) - _dragStart;
        if (Math.Abs(delta.X) > SystemParameters.MinimumHorizontalDragDistance)
            BeginDrag();
    }

    private void BeginDrag()
    {
        if (DataContext is FrameCardViewModel vm)
            DragDrop.DoDragDrop(this, vm, DragDropEffects.Move);
    }

    private void NotifyTimelineOfSelection(FrameCardViewModel vm)
    {
        if (FindParentTimeline() is TimelineStrip strip)
            strip.OnFrameCardSelected(vm);
    }

    private TimelineStrip? FindParentTimeline()
    {
        var p = Parent as DependencyObject;
        while (p is not null)
        {
            if (p is TimelineStrip ts) return ts;
            p = System.Windows.Media.VisualTreeHelper.GetParent(p);
        }
        return null;
    }

    private void ClearOtherSelections(FrameCardViewModel current)
    {
        if (FindParentTimeline()?.DataContext is not TimelineViewModel tvm) return;
        foreach (var f in tvm.Frames)
            if (f != current) f.IsSelected = false;
    }

    private void SelectRange(FrameCardViewModel current)
    {
        if (FindParentTimeline()?.DataContext is not TimelineViewModel tvm) return;
        var anchor = tvm.Frames.FirstOrDefault(f => f.IsSelected);
        if (anchor is null) { current.IsSelected = true; return; }

        var from = Math.Min(tvm.Frames.IndexOf(anchor), tvm.Frames.IndexOf(current));
        var to   = Math.Max(tvm.Frames.IndexOf(anchor), tvm.Frames.IndexOf(current));
        for (var i = from; i <= to; i++) tvm.Frames[i].IsSelected = true;
    }
}
