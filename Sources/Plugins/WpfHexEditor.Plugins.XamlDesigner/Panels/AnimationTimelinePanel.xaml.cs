// ==========================================================
// Project: WpfHexEditor.Plugins.XamlDesigner
//          2026-03-22 — Moved to plugin project (WpfHexEditor.Plugins.XamlDesigner.Panels).
// File: AnimationTimelinePanel.xaml.cs
// Author: Derek Tremblay
// Created: 2026-03-17
// Updated: 2026-03-19
// Description:
//     Code-behind for the Animation Timeline dockable panel.
//     Renders keyframe dots on per-track Canvas lanes, synchronises
//     scroll between track-header ListBox and lane area.
//     Added: AddKeyframe/DeleteKeyframe handlers, keyframe drag support
//            (PreviewMouseDown/Move/Up on lane Canvas → MoveKeyframeCommand).
//
// Architecture Notes:
//     VS-Like dockable panel.
//     Lifecycle rule: OnUnloaded must NOT null _vm.
//     Keyframe drag state per Canvas is stored in a Dictionary keyed by Canvas instance.
//     8px hit-radius for keyframe dot capture.
// ==========================================================

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using WpfHexEditor.Editor.XamlDesigner.Models;
using WpfHexEditor.Plugins.XamlDesigner.ViewModels;

namespace WpfHexEditor.Plugins.XamlDesigner.Panels;

/// <summary>
/// Animation timeline panel — shows storyboard tracks with keyframe dots.
/// </summary>
public partial class AnimationTimelinePanel : UserControl
{
    private const double KeyframeDotRadius   = 4.0;
    private const double KeyframeHitRadius   = 8.0;

    private AnimationTimelinePanelViewModel? _vm;

    // Per-lane drag state keyed by Canvas instance.
    private readonly Dictionary<Canvas, LaneDragState> _dragStates = new();

    // ── Constructor ───────────────────────────────────────────────────────────

    public AnimationTimelinePanel()
    {
        InitializeComponent();
        Loaded   += OnLoaded;
        Unloaded += OnUnloaded;
    }

    // ── ViewModel injection ───────────────────────────────────────────────────

    /// <summary>Called by the plugin host to inject the shared ViewModel.</summary>
    public void SetViewModel(AnimationTimelinePanelViewModel vm)
    {
        _vm         = vm;
        DataContext = vm;
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_vm is null) return;
        _vm.PropertyChanged -= OnVmPropertyChanged;
        _vm.PropertyChanged += OnVmPropertyChanged;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        // IMPORTANT: do NOT null _vm — panel may be re-loaded after dock/float.
        if (_vm is not null)
            _vm.PropertyChanged -= OnVmPropertyChanged;
    }

    // ── VM property change ────────────────────────────────────────────────────

    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AnimationTimelinePanelViewModel.Tracks))
            RefreshAllLanes();
    }

    // ── Toolbar handlers ──────────────────────────────────────────────────────

    /// <summary>Add keyframe button click — delegates to VM command.</summary>
    private void OnAddKeyframeClick(object sender, RoutedEventArgs e)
        => _vm?.AddKeyframeCommand.Execute(null);

    /// <summary>Delete keyframe button click — passes the selected keyframe.</summary>
    private void OnDeleteKeyframeClick(object sender, RoutedEventArgs e)
    {
        var kf = _vm?.SelectedTrack?.Keyframes.FirstOrDefault(k => k.IsSelected);
        if (kf is not null)
            _vm?.DeleteKeyframeCommand.Execute(kf);
    }

    // ── Ruler ─────────────────────────────────────────────────────────────────

    private void OnRulerSeekRequested(object sender, TimeSpan time)
        => _vm?.SeekCommand.Execute(time);

    // ── Lane loaded ───────────────────────────────────────────────────────────

    private void OnLaneLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is Canvas canvas && canvas.Tag is AnimationTrackViewModel track)
            RenderLane(canvas, track);
    }

    // ── Keyframe drag ─────────────────────────────────────────────────────────

    private void OnLanePreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Canvas canvas || _vm is null) return;
        if (canvas.Tag is not AnimationTrackViewModel track) return;

        var pos = e.GetPosition(canvas);
        var kf  = FindNearestKeyframe(canvas, track, pos.X);
        if (kf is null) return;

        _dragStates[canvas] = new LaneDragState(kf);
        canvas.CaptureMouse();
        e.Handled = true;
    }

    private void OnLanePreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (sender is not Canvas canvas) return;
        if (!_dragStates.TryGetValue(canvas, out var state)) return;
        if (e.LeftButton != MouseButtonState.Pressed || _vm is null) return;

        double x     = Math.Clamp(e.GetPosition(canvas).X, 0, canvas.ActualWidth);
        double ratio = canvas.ActualWidth > 0 ? x / canvas.ActualWidth : 0;
        double ms    = ratio * _vm.Duration.TotalMilliseconds;

        state.PreviewTime = TimeSpan.FromMilliseconds(ms);
        UpdateDotPosition(canvas, state.Keyframe, x);
        e.Handled = true;
    }

    private void OnLanePreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Canvas canvas) return;
        if (!_dragStates.TryGetValue(canvas, out var state)) return;

        canvas.ReleaseMouseCapture();
        _dragStates.Remove(canvas);

        if (_vm is not null)
            _vm.MoveKeyframeCommand.Execute((state.Keyframe, state.PreviewTime));

        if (canvas.Tag is AnimationTrackViewModel track)
            RenderLane(canvas, track);
    }

    // ── Lane rendering ────────────────────────────────────────────────────────

    private void RefreshAllLanes()
    {
        KeyframeLanes.ItemsSource = null;
        KeyframeLanes.ItemsSource = _vm?.Tracks;
    }

    private void RenderLane(Canvas canvas, AnimationTrackViewModel track)
    {
        canvas.Children.Clear();
        if (_vm is null || _vm.Duration.TotalMilliseconds <= 0) return;

        double totalMs = _vm.Duration.TotalMilliseconds;
        var fill   = TryFindResource("XD_KeyframeThumbBrush") as Brush ?? Brushes.DodgerBlue;
        var stroke = TryFindResource("DockBorderBrush")       as Brush ?? Brushes.Gray;

        canvas.SizeChanged += (_, _) => RenderLane(canvas, track);

        foreach (var kf in track.Keyframes)
        {
            double ratio = kf.Time.TotalMilliseconds / totalMs;
            double x     = ratio * canvas.ActualWidth;
            var dot = BuildKeyframeDot(kf, x, fill, stroke);
            canvas.Children.Add(dot);
        }
    }

    private static Ellipse BuildKeyframeDot(
        KeyframeViewModel kf, double x, Brush fill, Brush stroke)
    {
        var dot = new Ellipse
        {
            Width           = KeyframeDotRadius * 2,
            Height          = KeyframeDotRadius * 2,
            Fill            = fill,
            Stroke          = stroke,
            StrokeThickness = 1,
            ToolTip         = $"{kf.Time.TotalMilliseconds:F0}ms = {kf.Value}",
            Cursor          = Cursors.Hand,
            Tag             = kf
        };

        Canvas.SetLeft(dot, x - KeyframeDotRadius);
        Canvas.SetTop(dot, 8);
        return dot;
    }

    private static void UpdateDotPosition(Canvas canvas, KeyframeViewModel kf, double x)
    {
        foreach (UIElement child in canvas.Children)
        {
            if (child is Ellipse dot && dot.Tag == kf)
            {
                Canvas.SetLeft(dot, x - KeyframeDotRadius);
                return;
            }
        }
    }

    private static KeyframeViewModel? FindNearestKeyframe(
        Canvas canvas, AnimationTrackViewModel track, double mouseX)
    {
        if (track.Keyframes.Count == 0) return null;

        KeyframeViewModel? best  = null;
        double             bestD = double.MaxValue;

        foreach (UIElement child in canvas.Children)
        {
            if (child is not Ellipse { Tag: KeyframeViewModel kf }) continue;
            double cx = Canvas.GetLeft(child) + KeyframeDotRadius;
            double d  = Math.Abs(cx - mouseX);
            if (d < bestD && d <= KeyframeHitRadius) { bestD = d; best = kf; }
        }

        return best;
    }

    // ── Inner type ────────────────────────────────────────────────────────────

    private sealed class LaneDragState(KeyframeViewModel keyframe)
    {
        public KeyframeViewModel Keyframe    { get; } = keyframe;
        public TimeSpan          PreviewTime { get; set; }
    }
}
