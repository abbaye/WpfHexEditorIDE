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

        var timeline = FindParentTimeline();
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            vm.IsSelected = !vm.IsSelected;
        else if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            SelectRange(vm, timeline);
        else
        {
            ClearOtherSelections(vm, timeline);
            vm.IsSelected = true;
        }

        timeline?.OnFrameCardSelected(vm);
    }

    private bool _dragPending;

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (e.LeftButton != MouseButtonState.Pressed || _dragPending) return;

        var delta = e.GetPosition(this) - _dragStart;
        if (Math.Abs(delta.X) > SystemParameters.MinimumHorizontalDragDistance)
        {
            _dragPending = true;
            // Defer DoDragDrop out of the MouseMove handler to avoid
            // Dispatcher re-entrancy (InvalidOperationException).
            Dispatcher.BeginInvoke(BeginDrag);
        }
    }

    private void BeginDrag()
    {
        _dragPending = false;
        if (DataContext is FrameCardViewModel vm)
            DragDrop.DoDragDrop(this, vm, DragDropEffects.Move);
    }

    private TimelineStrip? FindParentTimeline()
    {
        DependencyObject? p = this;
        while ((p = System.Windows.Media.VisualTreeHelper.GetParent(p)) is not null)
            if (p is TimelineStrip ts) return ts;
        return null;
    }

    private static void ClearOtherSelections(FrameCardViewModel current, TimelineStrip? timeline)
    {
        if (timeline?.DataContext is not TimelineViewModel tvm) return;
        foreach (var f in tvm.Frames)
            if (f != current) f.IsSelected = false;
    }

    private static void SelectRange(FrameCardViewModel current, TimelineStrip? timeline)
    {
        if (timeline?.DataContext is not TimelineViewModel tvm) return;
        var anchor = tvm.Frames.FirstOrDefault(f => f.IsSelected);
        if (anchor is null) { current.IsSelected = true; return; }

        var ai   = tvm.Frames.IndexOf(anchor);
        var ci   = tvm.Frames.IndexOf(current);
        var from = Math.Min(ai, ci);
        var to   = Math.Max(ai, ci);
        for (var i = from; i <= to; i++) tvm.Frames[i].IsSelected = true;
    }
}
