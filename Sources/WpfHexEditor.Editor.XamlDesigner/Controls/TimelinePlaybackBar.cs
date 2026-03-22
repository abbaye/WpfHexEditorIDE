// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: TimelinePlaybackBar.cs
// Description:
//     VCR-style playback control bar for the Animation Timeline panel.
//     Provides: |< (To Start), < (Step Back), Play/Pause, > (Step Fwd), >| (To End),
//     Loop toggle, and a horizontal scrubber Slider.
//     Fires PlaybackCommand events that StoryboardSyncService responds to.
//
// Architecture Notes:
//     WPF UserControl equivalent built as a Border in code.
//     Owned by AnimationTimelinePanel.
//     ViewModel-free — driven directly by StoryboardSyncService callbacks.
//     Theme-aware via XD_PlaybackBarBackground and XD_PlayButtonBrush tokens.
// ==========================================================

using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace WpfHexEditor.Editor.XamlDesigner.Controls;

/// <summary>
/// Playback commands the bar can send.
/// </summary>
public enum PlaybackCommand
{
    ToStart,
    StepBack,
    PlayPause,
    StepForward,
    ToEnd,
}

/// <summary>
/// VCR-style animation playback bar with Play/Pause, Step, and scrubber Slider.
/// </summary>
public sealed class TimelinePlaybackBar : Border
{
    private readonly Button      _btnToStart;
    private readonly Button      _btnStepBack;
    private readonly ToggleButton _btnPlayPause;
    private readonly Button      _btnStepFwd;
    private readonly Button      _btnToEnd;
    private readonly ToggleButton _btnLoop;
    private readonly Slider      _scrubber;
    private readonly TextBlock   _timeLabel;

    private bool _isPlaying;
    private bool _suppressScrubberEvent;

    // ── Events ────────────────────────────────────────────────────────────────

    /// <summary>Fired when a VCR button is pressed.</summary>
    public event EventHandler<PlaybackCommand>? CommandRequested;

    /// <summary>Fired when the user scrubs the timeline position.</summary>
    public event EventHandler<double>? PositionScrubbed;

    // ── Constructor ───────────────────────────────────────────────────────────

    public TimelinePlaybackBar()
    {
        var barBg = Application.Current?.TryFindResource("XD_PlaybackBarBackground") as Brush
                    ?? new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x2E));

        Background      = barBg;
        BorderThickness = new Thickness(0, 1, 0, 0);
        BorderBrush     = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255));
        Padding         = new Thickness(4, 2, 4, 2);
        Height          = 36;

        var btnFg = Application.Current?.TryFindResource("XD_PlayButtonBrush") as Brush
                    ?? Brushes.White;

        _btnToStart   = MakeVcrButton("\u23EE", "To Start",    btnFg, () => Fire(PlaybackCommand.ToStart));
        _btnStepBack  = MakeVcrButton("\u23EA", "Step Back",   btnFg, () => Fire(PlaybackCommand.StepBack));
        _btnPlayPause = MakeVcrToggle("\u25B6", "\u23F8", "Play / Pause", btnFg, TogglePlay);
        _btnStepFwd   = MakeVcrButton("\u23E9", "Step Fwd",    btnFg, () => Fire(PlaybackCommand.StepForward));
        _btnToEnd     = MakeVcrButton("\u23ED", "To End",      btnFg, () => Fire(PlaybackCommand.ToEnd));
        _btnLoop      = MakeVcrToggle("\uD83D\uDD01", "\uD83D\uDD01", "Loop", btnFg, null);

        _scrubber = new Slider
        {
            Minimum          = 0,
            Maximum          = 1,
            Value            = 0,
            VerticalAlignment = VerticalAlignment.Center,
            Margin           = new Thickness(6, 0, 6, 0),
        };
        _scrubber.ValueChanged += OnScrubberChanged;

        _timeLabel = new TextBlock
        {
            Text              = "0:00.0",
            Foreground        = new SolidColorBrush(Color.FromRgb(0xBB, 0xBB, 0xBB)),
            FontSize          = 9,
            VerticalAlignment = VerticalAlignment.Center,
            Margin            = new Thickness(0, 0, 4, 0),
            MinWidth          = 40,
        };

        var panel = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(_btnToStart,   Dock.Left);
        DockPanel.SetDock(_btnStepBack,  Dock.Left);
        DockPanel.SetDock(_btnPlayPause, Dock.Left);
        DockPanel.SetDock(_btnStepFwd,   Dock.Left);
        DockPanel.SetDock(_btnToEnd,     Dock.Left);
        DockPanel.SetDock(_btnLoop,      Dock.Left);
        DockPanel.SetDock(_timeLabel,    Dock.Right);
        panel.Children.Add(_btnToStart);
        panel.Children.Add(_btnStepBack);
        panel.Children.Add(_btnPlayPause);
        panel.Children.Add(_btnStepFwd);
        panel.Children.Add(_btnToEnd);
        panel.Children.Add(_btnLoop);
        panel.Children.Add(_timeLabel);
        panel.Children.Add(_scrubber);
        Child = panel;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Updates the scrubber position and time label without firing PositionScrubbed.</summary>
    public void SetPosition(double normalizedPosition, double totalSeconds)
    {
        _suppressScrubberEvent = true;
        _scrubber.Value = Math.Clamp(normalizedPosition, 0, 1);
        _suppressScrubberEvent = false;

        double current = normalizedPosition * totalSeconds;
        int    min     = (int)(current / 60);
        double sec     = current % 60;
        _timeLabel.Text = $"{min}:{sec:00.0}";
    }

    /// <summary>Synchronizes the Play/Pause toggle visual state.</summary>
    public void SetPlayingState(bool isPlaying)
    {
        _isPlaying = isPlaying;
        _btnPlayPause.IsChecked = isPlaying;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void Fire(PlaybackCommand cmd) => CommandRequested?.Invoke(this, cmd);

    private void TogglePlay(bool isChecked)
    {
        _isPlaying = isChecked;
        Fire(PlaybackCommand.PlayPause);
    }

    private void OnScrubberChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_suppressScrubberEvent) return;
        PositionScrubbed?.Invoke(this, e.NewValue);
    }

    private static Button MakeVcrButton(string glyph, string tooltip, Brush fg, Action onClick)
    {
        var btn = new Button
        {
            Content         = glyph,
            Foreground      = fg,
            Background      = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding         = new Thickness(4, 0, 4, 0),
            FontSize        = 12,
            ToolTip         = tooltip,
            Cursor          = Cursors.Hand,
        };
        btn.Click += (_, _) => onClick();
        return btn;
    }

    private static ToggleButton MakeVcrToggle(
        string uncheckedGlyph, string checkedGlyph, string tooltip, Brush fg,
        Action<bool>? onToggle)
    {
        var btn = new ToggleButton
        {
            Content         = uncheckedGlyph,
            Foreground      = fg,
            Background      = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding         = new Thickness(4, 0, 4, 0),
            FontSize        = 12,
            ToolTip         = tooltip,
            Cursor          = Cursors.Hand,
        };
        btn.Checked   += (_, _) => { btn.Content = checkedGlyph;    onToggle?.Invoke(true);  };
        btn.Unchecked += (_, _) => { btn.Content = uncheckedGlyph;  onToggle?.Invoke(false); };
        return btn;
    }
}
