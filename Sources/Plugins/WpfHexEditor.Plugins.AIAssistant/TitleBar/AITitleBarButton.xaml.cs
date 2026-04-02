// ==========================================================
// Project: WpfHexEditor.Plugins.AIAssistant
// File: AITitleBarButton.xaml.cs
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
using WpfHexEditor.Plugins.AIAssistant.Connection;

namespace WpfHexEditor.Plugins.AIAssistant.TitleBar;

public partial class AITitleBarButton : UserControl
{
    private Storyboard? _pulseStoryboard;

    public event Action? ShowCommandPaletteRequested;
    public event Action? NewTabRequested;
    public event Action? AskSelectionRequested;
    public event Action? FixErrorsRequested;
    public event Action? OpenOptionsRequested;
    public event Action? AccountUsageRequested;
    public event Action? ManageConnectionsRequested;

    public AITitleBarButton()
    {
        InitializeComponent();
        SafeGuard.Run(BuildPulseAnimation);
    }

    public void UpdateStatus(AIConnectionStatus status)
        => SafeGuard.Run(() =>
        {
            _pulseStoryboard?.Stop();

            var brushKey = status switch
            {
                AIConnectionStatus.NotConfigured => "DockBorderBrush",
                AIConnectionStatus.Connecting => "DockTabActiveBrush",
                AIConnectionStatus.Connected => "DockTabActiveBrush",
                AIConnectionStatus.CliConnected => "DockTabActiveBrush",
                AIConnectionStatus.RateLimited => "DockBorderBrush",
                AIConnectionStatus.Error => "DockBorderBrush",
                AIConnectionStatus.Offline => "DockBorderBrush",
                _ => "DockBorderBrush"
            };

            if (status == AIConnectionStatus.CliConnected)
                StatusBadge.Fill = new SolidColorBrush(Color.FromRgb(0x4E, 0xC9, 0xB0)); // green
            else if (TryFindResource(brushKey) is Brush brush)
                StatusBadge.Fill = brush;

            if (status is AIConnectionStatus.Connecting or AIConnectionStatus.RateLimited)
                _pulseStoryboard?.Begin();

            // Show/hide CLI badge text
            CliBadgeText.Visibility = status == AIConnectionStatus.CliConnected
                ? Visibility.Visible : Visibility.Collapsed;

            ToolTip = status switch
            {
                AIConnectionStatus.NotConfigured => "AI Assistant — No API key configured",
                AIConnectionStatus.Connecting => "AI Assistant — Connecting...",
                AIConnectionStatus.Connected => "AI Assistant (Ctrl+Shift+A)",
                AIConnectionStatus.CliConnected => "AI Assistant — Connected via CLI",
                AIConnectionStatus.RateLimited => "AI Assistant — Rate limited",
                AIConnectionStatus.Error => "AI Assistant — Connection error",
                AIConnectionStatus.Offline => "AI Assistant — Offline",
                _ => "AI Assistant"
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
    private void OnAccountUsageClick(object sender, RoutedEventArgs e) => SafeGuard.Run(() => AccountUsageRequested?.Invoke());
    private void OnManageConnectionsClick(object sender, RoutedEventArgs e) => SafeGuard.Run(() => ManageConnectionsRequested?.Invoke());
}
