// ==========================================================
// Project: WpfHexEditor.ProgressBar
// File: Controls/ProgressBarBase.cs
// Description:
//     Abstract base class for all progress bar controls.
//     Provides common DPs (Progress, State, brushes, animation),
//     a smooth animation engine, and theme brush resolution.
//     Subclasses only need to override OnRender.
// ==========================================================

using System.Globalization;
using System.Windows;
using System.Windows.Media;
using WpfHexEditor.ProgressBar.Helpers;

namespace WpfHexEditor.ProgressBar.Controls;

/// <summary>
/// Abstract base for all progress controls. Lightweight <see cref="FrameworkElement"/>
/// with smooth animated progress, indeterminate offset cycling, and theme-aware brushes.
/// </summary>
public abstract class ProgressBarBase : FrameworkElement
{
    // ── Animation state ───────────────────────────────────────────────────────

    private double      _animatedProgress;
    private double      _indeterminateOffset;
    private IDisposable? _animSub;
    private bool        _needsAnimation;

    // ── Fallback colors ───────────────────────────────────────────────────────

    private static readonly Color DefaultAccent    = (Color)ColorConverter.ConvertFromString("#0078D4");
    private static readonly Color DefaultTrack     = (Color)ColorConverter.ConvertFromString("#3E3E42");
    private static readonly Color DefaultError     = (Color)ColorConverter.ConvertFromString("#E51400");
    private static readonly Color DefaultSuccess   = (Color)ColorConverter.ConvertFromString("#60A917");
    private static readonly Color DefaultPaused    = (Color)ColorConverter.ConvertFromString("#F0A30A");
    private static readonly Color DefaultForeground = (Color)ColorConverter.ConvertFromString("#F1F1F1");

    // ── Dependency Properties ─────────────────────────────────────────────────

    public static readonly DependencyProperty ProgressProperty =
        DependencyProperty.Register(nameof(Progress), typeof(double), typeof(ProgressBarBase),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender, OnProgressChanged, CoerceProgress));

    public static readonly DependencyProperty IsIndeterminateProperty =
        DependencyProperty.Register(nameof(IsIndeterminate), typeof(bool), typeof(ProgressBarBase),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender, OnAnimationStateChanged));

    public static readonly DependencyProperty StateProperty =
        DependencyProperty.Register(nameof(State), typeof(ProgressState), typeof(ProgressBarBase),
            new FrameworkPropertyMetadata(ProgressState.Normal, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ShowPercentageProperty =
        DependencyProperty.Register(nameof(ShowPercentage), typeof(bool), typeof(ProgressBarBase),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty AnimationDurationProperty =
        DependencyProperty.Register(nameof(AnimationDuration), typeof(TimeSpan), typeof(ProgressBarBase),
            new PropertyMetadata(TimeSpan.FromMilliseconds(300)));

    public static readonly DependencyProperty TrackBrushProperty =
        DependencyProperty.Register(nameof(TrackBrush), typeof(Brush), typeof(ProgressBarBase),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ProgressBrushProperty =
        DependencyProperty.Register(nameof(ProgressBrush), typeof(Brush), typeof(ProgressBarBase),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty IndeterminateBrushProperty =
        DependencyProperty.Register(nameof(IndeterminateBrush), typeof(Brush), typeof(ProgressBarBase),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ErrorBrushProperty =
        DependencyProperty.Register(nameof(ErrorBrush), typeof(Brush), typeof(ProgressBarBase),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SuccessBrushProperty =
        DependencyProperty.Register(nameof(SuccessBrush), typeof(Brush), typeof(ProgressBarBase),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty PausedBrushProperty =
        DependencyProperty.Register(nameof(PausedBrush), typeof(Brush), typeof(ProgressBarBase),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty PercentageForegroundProperty =
        DependencyProperty.Register(nameof(PercentageForeground), typeof(Brush), typeof(ProgressBarBase),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    // ── CLR wrappers ──────────────────────────────────────────────────────────

    /// <summary>Progress value, clamped 0–1.</summary>
    public double Progress
    {
        get => (double)GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    /// <summary>When true, shows a marquee/spin animation instead of determinate progress.</summary>
    public bool IsIndeterminate
    {
        get => (bool)GetValue(IsIndeterminateProperty);
        set => SetValue(IsIndeterminateProperty, value);
    }

    /// <summary>Visual state that determines the active fill brush color.</summary>
    public ProgressState State
    {
        get => (ProgressState)GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    /// <summary>When true, renders a "45%" overlay text.</summary>
    public bool ShowPercentage
    {
        get => (bool)GetValue(ShowPercentageProperty);
        set => SetValue(ShowPercentageProperty, value);
    }

    /// <summary>Duration of the smooth easing transition when Progress changes.</summary>
    public TimeSpan AnimationDuration
    {
        get => (TimeSpan)GetValue(AnimationDurationProperty);
        set => SetValue(AnimationDurationProperty, value);
    }

    /// <summary>Track background brush. Falls back to DockBorderBrush → #3E3E42.</summary>
    public Brush? TrackBrush
    {
        get => (Brush?)GetValue(TrackBrushProperty);
        set => SetValue(TrackBrushProperty, value);
    }

    /// <summary>Progress fill brush (Normal state). Falls back to DockAccentBrush → #0078D4.</summary>
    public Brush? ProgressBrush
    {
        get => (Brush?)GetValue(ProgressBrushProperty);
        set => SetValue(ProgressBrushProperty, value);
    }

    /// <summary>Brush for indeterminate mode. Falls back to ProgressBrush.</summary>
    public Brush? IndeterminateBrush
    {
        get => (Brush?)GetValue(IndeterminateBrushProperty);
        set => SetValue(IndeterminateBrushProperty, value);
    }

    /// <summary>Fill brush for Error state. Falls back to #E51400.</summary>
    public Brush? ErrorBrush
    {
        get => (Brush?)GetValue(ErrorBrushProperty);
        set => SetValue(ErrorBrushProperty, value);
    }

    /// <summary>Fill brush for Success state. Falls back to #60A917.</summary>
    public Brush? SuccessBrush
    {
        get => (Brush?)GetValue(SuccessBrushProperty);
        set => SetValue(SuccessBrushProperty, value);
    }

    /// <summary>Fill brush for Paused state. Falls back to #F0A30A.</summary>
    public Brush? PausedBrush
    {
        get => (Brush?)GetValue(PausedBrushProperty);
        set => SetValue(PausedBrushProperty, value);
    }

    /// <summary>Foreground brush for percentage text. Falls back to DockMenuForegroundBrush → #F1F1F1.</summary>
    public Brush? PercentageForeground
    {
        get => (Brush?)GetValue(PercentageForegroundProperty);
        set => SetValue(PercentageForegroundProperty, value);
    }

    // ── Protected accessors for subclasses ────────────────────────────────────

    /// <summary>Smoothly animated progress value (0–1) for rendering.</summary>
    protected double AnimatedProgress => _animatedProgress;

    /// <summary>Cycling offset (0–1) for indeterminate animations.</summary>
    protected double IndeterminateOffset => _indeterminateOffset;

    /// <summary>Resolves the track background brush using the 3-tier strategy.</summary>
    protected Brush ResolveTrackBrush()
        => ThemeBrushHelper.Resolve(this, TrackBrush, "DockBorderBrush", DefaultTrack);

    /// <summary>Resolves the active fill brush based on current <see cref="State"/>.</summary>
    protected Brush ResolveActiveBrush()
    {
        if (IsIndeterminate)
            return ThemeBrushHelper.Resolve(this, IndeterminateBrush,
                "DockAccentBrush", DefaultAccent);

        return State switch
        {
            ProgressState.Error   => ThemeBrushHelper.Resolve(this, ErrorBrush,   "", DefaultError),
            ProgressState.Success => ThemeBrushHelper.Resolve(this, SuccessBrush, "", DefaultSuccess),
            ProgressState.Paused  => ThemeBrushHelper.Resolve(this, PausedBrush,  "", DefaultPaused),
            _                     => ThemeBrushHelper.Resolve(this, ProgressBrush, "DockAccentBrush", DefaultAccent),
        };
    }

    /// <summary>Resolves the percentage text foreground brush.</summary>
    protected Brush ResolvePercentageForeground()
        => ThemeBrushHelper.Resolve(this, PercentageForeground, "DockMenuForegroundBrush", DefaultForeground);

    /// <summary>Creates a centered <see cref="FormattedText"/> showing the current percentage.</summary>
    protected FormattedText CreatePercentageText(double fontSize = 10)
    {
        var pct = (int)Math.Round(AnimatedProgress * 100);
        return new FormattedText(
            $"{pct}%",
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Segoe UI"),
            fontSize,
            ResolvePercentageForeground(),
            VisualTreeHelper.GetDpi(this).PixelsPerDip);
    }

    // ── Animation engine ──────────────────────────────────────────────────────

    private static object CoerceProgress(DependencyObject d, object value)
        => Math.Clamp((double)value, 0.0, 1.0);

    private static void OnProgressChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ProgressBarBase self)
            self.EnsureAnimation();
    }

    private static void OnAnimationStateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ProgressBarBase self)
            self.EnsureAnimation();
    }

    private void EnsureAnimation()
    {
        bool needsAnim = IsIndeterminate || Math.Abs(_animatedProgress - Progress) > 0.001;
        if (needsAnim && !_needsAnimation)
        {
            _needsAnimation = true;
            _animSub ??= AnimationHelper.Register(OnAnimationTick);
        }
    }

    private void OnAnimationTick(TimeSpan elapsed)
    {
        bool dirty = false;

        // Smooth progress interpolation
        if (Math.Abs(_animatedProgress - Progress) > 0.001)
        {
            var duration = AnimationDuration.TotalSeconds;
            var t = duration > 0 ? Math.Min(elapsed.TotalSeconds / duration, 1.0) : 1.0;
            _animatedProgress = AnimationHelper.EaseOut(_animatedProgress, Progress, Math.Min(t * 3, 1.0));
            dirty = true;
        }
        else if (Math.Abs(_animatedProgress - Progress) > 0)
        {
            _animatedProgress = Progress;
            dirty = true;
        }

        // Indeterminate offset cycling
        if (IsIndeterminate)
        {
            _indeterminateOffset += elapsed.TotalSeconds * 0.8; // ~0.8 cycles/sec
            if (_indeterminateOffset >= 1.0) _indeterminateOffset -= 1.0;
            dirty = true;
        }

        if (dirty)
            InvalidateVisual();

        // Stop animation when not needed
        if (!IsIndeterminate && Math.Abs(_animatedProgress - Progress) < 0.001)
        {
            _needsAnimation = false;
            _animSub?.Dispose();
            _animSub = null;
        }
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    protected ProgressBarBase()
    {
        Unloaded += (_, _) =>
        {
            _animSub?.Dispose();
            _animSub = null;
            _needsAnimation = false;
        };
    }
}
