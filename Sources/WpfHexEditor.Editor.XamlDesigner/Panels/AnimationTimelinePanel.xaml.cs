// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: AnimationTimelinePanel.xaml.cs
// Author: Derek Tremblay
// Created: 2026-03-17
// Description:
//     Code-behind for the Animation Timeline dockable panel.
//     Renders keyframe dots on per-track Canvas lanes and synchronises
//     the scrolling between the track-header ListBox and the lane area.
//
// Architecture Notes:
//     VS-Like dockable panel with ToolbarOverflowManager.
//     Lifecycle rule: OnUnloaded must NOT null _vm.
//     Keyframe rendering is code-behind driven (lane Canvas) to avoid
//     the performance cost of virtualised DataTemplate item layout.
// ==========================================================

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using WpfHexEditor.Editor.XamlDesigner.ViewModels;

namespace WpfHexEditor.Editor.XamlDesigner.Panels;

/// <summary>
/// Animation timeline panel — shows storyboard tracks with keyframe dots.
/// </summary>
public partial class AnimationTimelinePanel : UserControl
{
    private AnimationTimelinePanelViewModel? _vm;

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
        _vm          = vm;
        DataContext  = vm;
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_vm is null) return;
        _vm.PropertyChanged += OnVmPropertyChanged;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        // IMPORTANT: do NOT null _vm here — panel may be re-loaded after dock/float.
        if (_vm is not null)
            _vm.PropertyChanged -= OnVmPropertyChanged;
    }

    // ── Event handlers ────────────────────────────────────────────────────────

    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AnimationTimelinePanelViewModel.Tracks))
            RefreshAllLanes();
    }

    /// <summary>
    /// Called when the TimelineRuler fires a seek via mouse drag.
    /// </summary>
    private void OnRulerSeekRequested(object sender, TimeSpan time)
    {
        _vm?.SeekCommand.Execute(time);
    }

    /// <summary>
    /// Called when each lane Canvas is loaded — populates keyframe dots.
    /// </summary>
    private void OnLaneLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is Canvas canvas && canvas.Tag is AnimationTrackViewModel track)
            RenderLane(canvas, track);
    }

    // ── Lane rendering ────────────────────────────────────────────────────────

    private void RefreshAllLanes()
    {
        // Re-rendering happens lazily via OnLaneLoaded when ItemsControl rebuilds.
        // Force a layout pass by resetting the ItemsSource binding.
        KeyframeLanes.ItemsSource = null;
        KeyframeLanes.ItemsSource = _vm?.Tracks;
    }

    /// <summary>
    /// Populates a lane Canvas with keyframe diamonds (Ellipse) positioned
    /// proportionally to their time within the total duration.
    /// </summary>
    private void RenderLane(Canvas canvas, AnimationTrackViewModel track)
    {
        canvas.Children.Clear();

        if (_vm is null || _vm.Duration.TotalMilliseconds <= 0) return;

        double totalMs = _vm.Duration.TotalMilliseconds;
        var fill   = TryFindResource("XD_KeyframeThumbBrush") as Brush
                     ?? Brushes.DodgerBlue;
        var stroke = TryFindResource("DockBorderBrush") as Brush
                     ?? Brushes.Gray;

        canvas.SizeChanged += (_, _) => RenderLane(canvas, track);

        foreach (var kf in track.Keyframes)
        {
            double ratio = kf.Time.TotalMilliseconds / totalMs;
            double x     = ratio * canvas.ActualWidth;

            var dot = new Ellipse
            {
                Width           = 8,
                Height          = 8,
                Fill            = fill,
                Stroke          = stroke,
                StrokeThickness = 1,
                ToolTip         = $"{kf.Time.TotalMilliseconds:F0}ms = {kf.Value}",
                Cursor          = Cursors.Hand
            };

            Canvas.SetLeft(dot, x - 4);
            Canvas.SetTop(dot, 8);
            canvas.Children.Add(dot);
        }
    }
}
