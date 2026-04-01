// ==========================================================
// Project: WpfHexEditor.Plugins.ClaudeAssistant
// File: ClaudeTitleBarButton.xaml.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.6
// Created: 2026-03-31
// License: GNU Affero General Public License v3.0 (AGPL-3.0)
// Description:
//     Claude icon button for the IDE title bar. All handlers wrapped in SafeGuard.
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

    public event Action? ShowCommandPaletteRequested;
    public event Action? NewTabRequested;
    public event Action? AskSelectionRequested;
    public event Action? FixErrorsRequested;
    public event Action? OpenOptionsRequested;

    public ClaudeTitleBarButton()
    {
        InitializeComponent();
        SafeGuard.Run(BuildPulseAnimation);
    }

    public void UpdateStatus(ClaudeConnectionStatus status)
        => SafeGuard.Run(() =>
        {
            _pulseStoryboard?.Stop();

            var brushKey = status switch
            {
                ClaudeConnectionStatus.NotConfigured => "DockBorderBrush",
                ClaudeConnectionStatus.Connecting => "DockTabActiveBrush",
                ClaudeConnectionStatus.Connected => "DockTabActiveBrush",
                ClaudeConnectionStatus.RateLimited => "DockBorderBrush",
                ClaudeConnectionStatus.Error => "DockBorderBrush",
                ClaudeConnectionStatus.Offline => "DockBorderBrush",
                _ => "DockBorderBrush"
            };

            if (TryFindResource(brushKey) is Brush brush)
                StatusBadge.Fill = brush;

            if (status is ClaudeConnectionStatus.Connecting or ClaudeConnectionStatus.RateLimited)
                _pulseStoryboard?.Begin();

            ToolTip = status switch
            {
                ClaudeConnectionStatus.NotConfigured => "Claude AI — No API key configured",
                ClaudeConnectionStatus.Connecting => "Claude AI — Connecting...",
                ClaudeConnectionStatus.Connected => "Claude AI Assistant (Ctrl+Shift+A)",
                ClaudeConnectionStatus.RateLimited => "Claude AI — Rate limited",
                ClaudeConnectionStatus.Error => "Claude AI — Connection error",
                ClaudeConnectionStatus.Offline => "Claude AI — Offline",
                _ => "Claude AI Assistant"
            };
        });

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
        => SafeGuard.Run(() =>
        {
            if (TryFindResource("DockTabHoverBrush") is Brush brush)
                ButtonBorder.Background = brush;
        });

    private void OnMouseLeave(object sender, MouseEventArgs e)
        => SafeGuard.Run(() =>
        {
            ButtonBorder.Background = Brushes.Transparent;
        });

    private void OnLeftClick(object sender, MouseButtonEventArgs e) => SafeGuard.Run(() => ShowCommandPaletteRequested?.Invoke());
    private void OnRightClick(object sender, MouseButtonEventArgs e) => SafeGuard.Run(() => ContextMenu!.IsOpen = true);
    private void OnNewTabClick(object sender, RoutedEventArgs e) => SafeGuard.Run(() => NewTabRequested?.Invoke());
    private void OnAskSelectionClick(object sender, RoutedEventArgs e) => SafeGuard.Run(() => AskSelectionRequested?.Invoke());
    private void OnFixErrorsClick(object sender, RoutedEventArgs e) => SafeGuard.Run(() => FixErrorsRequested?.Invoke());
    private void OnOptionsClick(object sender, RoutedEventArgs e) => SafeGuard.Run(() => OpenOptionsRequested?.Invoke());
}
