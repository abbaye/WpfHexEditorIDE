// ==========================================================
// Project: WpfHexEditor.Plugins.ClaudeAssistant
// File: ClaudeTitleBarButton.xaml.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.6
// Created: 2026-03-31
// License: GNU Affero General Public License v3.0 (AGPL-3.0)
// Description:
//     Claude icon button for the IDE title bar with animated status badge.
// ==========================================================
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using WpfHexEditor.Plugins.ClaudeAssistant.Connection;

namespace WpfHexEditor.Plugins.ClaudeAssistant.TitleBar;

public partial class ClaudeTitleBarButton : UserControl
{
    private Storyboard? _pulseStoryboard;

    public event Action? TogglePanelRequested;
    public event Action? NewTabRequested;
    public event Action? AskSelectionRequested;
    public event Action? FixErrorsRequested;
    public event Action? OpenOptionsRequested;

    public ClaudeTitleBarButton()
    {
        InitializeComponent();
        BuildPulseAnimation();
    }

    public void UpdateStatus(ClaudeConnectionStatus status)
    {
        _pulseStoryboard?.Stop();

        var brushKey = status switch
        {
            ClaudeConnectionStatus.NotConfigured => "DockBorderBrush",
            ClaudeConnectionStatus.Connecting => "CA_TitleBarBadgeStreamingBrush",
            ClaudeConnectionStatus.Connected => "CA_TitleBarBadgeIdleBrush",
            ClaudeConnectionStatus.RateLimited => "CA_TitleBarBadgeStreamingBrush",
            ClaudeConnectionStatus.Error => "CA_TitleBarBadgeErrorBrush",
            ClaudeConnectionStatus.Offline => "DockBorderBrush",
            _ => "DockBorderBrush"
        };

        if (TryFindResource(brushKey) is Brush brush)
            StatusBadge.Fill = brush;

        if (status is ClaudeConnectionStatus.Connecting or ClaudeConnectionStatus.RateLimited)
            _pulseStoryboard?.Begin();

        var tooltip = status switch
        {
            ClaudeConnectionStatus.NotConfigured => "Claude AI — No API key configured",
            ClaudeConnectionStatus.Connecting => "Claude AI — Connecting...",
            ClaudeConnectionStatus.Connected => "Claude AI Assistant (Ctrl+Shift+A)",
            ClaudeConnectionStatus.RateLimited => "Claude AI — Rate limited, waiting...",
            ClaudeConnectionStatus.Error => "Claude AI — Connection error",
            ClaudeConnectionStatus.Offline => "Claude AI — Offline",
            _ => "Claude AI Assistant"
        };
        ToolTip = tooltip;
    }

    private void BuildPulseAnimation()
    {
        var anim = new DoubleAnimation(1.0, 0.3, TimeSpan.FromMilliseconds(600))
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever
        };

        _pulseStoryboard = new Storyboard();
        Storyboard.SetTarget(anim, StatusBadge);
        Storyboard.SetTargetProperty(anim, new PropertyPath(OpacityProperty));
        _pulseStoryboard.Children.Add(anim);
    }

    private void OnMouseEnter(object sender, MouseEventArgs e)
    {
        if (TryFindResource("CA_TitleBarButtonHoverBrush") is Brush brush)
            ButtonBorder.Background = brush;
    }

    private void OnMouseLeave(object sender, MouseEventArgs e)
    {
        if (TryFindResource("CA_TitleBarButtonBackgroundBrush") is Brush brush)
            ButtonBorder.Background = brush;
    }

    private void OnLeftClick(object sender, MouseButtonEventArgs e) => TogglePanelRequested?.Invoke();
    private void OnRightClick(object sender, MouseButtonEventArgs e) => ContextMenu!.IsOpen = true;
    private void OnNewTabClick(object sender, RoutedEventArgs e) => NewTabRequested?.Invoke();
    private void OnAskSelectionClick(object sender, RoutedEventArgs e) => AskSelectionRequested?.Invoke();
    private void OnFixErrorsClick(object sender, RoutedEventArgs e) => FixErrorsRequested?.Invoke();
    private void OnOptionsClick(object sender, RoutedEventArgs e) => OpenOptionsRequested?.Invoke();
}
