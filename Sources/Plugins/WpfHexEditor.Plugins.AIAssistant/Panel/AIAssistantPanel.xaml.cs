// ==========================================================
// Project: WpfHexEditor.Plugins.AIAssistant
// File: AIAssistantPanel.xaml.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.6
// Created: 2026-03-31
// License: GNU Affero General Public License v3.0 (AGPL-3.0)
// Description:
//     Panel code-behind. All handlers wrapped in SafeGuard.Run().
// ==========================================================
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WpfHexEditor.Plugins.AIAssistant.Panel.AccountUsage;
using WpfHexEditor.Plugins.AIAssistant.Panel.Tabs;
using WpfHexEditor.Plugins.AIAssistant.Properties;

namespace WpfHexEditor.Plugins.AIAssistant.Panel;

public partial class AIAssistantPanel : UserControl
{
    public AIAssistantPanel()
    {
        InitializeComponent();
    }

    private AIAssistantPanelViewModel? Vm => DataContext as AIAssistantPanelViewModel;

    private void OnNewTabClick(object sender, MouseButtonEventArgs e)
        => SafeGuard.Run(() => Vm?.CreateNewTabCommand.Execute(null));

    private void OnHistoryClick(object sender, MouseButtonEventArgs e)
        => SafeGuard.Run(() => Vm?.ToggleHistoryCommand.Execute(null));

    private void OnTabClick(object sender, MouseButtonEventArgs e)
        => SafeGuard.Run(() =>
        {
            if (sender is FrameworkElement { DataContext: ConversationTabViewModel tab } && Vm is not null)
                Vm.ActiveTab = tab;
        });

    private void OnTabRightClick(object sender, MouseButtonEventArgs e)
        => SafeGuard.Run(() =>
        {
            // Select the tab on right-click so context menu actions target it
            if (sender is FrameworkElement { DataContext: ConversationTabViewModel tab } && Vm is not null)
                Vm.ActiveTab = tab;
        });

    private void OnCloseTabClick(object sender, MouseButtonEventArgs e)
        => SafeGuard.Run(() =>
        {
            if (sender is FrameworkElement { DataContext: ConversationTabViewModel tab })
            {
                Vm?.CloseTabCommand.Execute(tab);
                e.Handled = true;
            }
        });

    private void OnCloseTabFromMenuClick(object sender, RoutedEventArgs e)
        => SafeGuard.Run(() =>
        {
            var tab = GetTabFromMenuItem(sender);
            if (tab is not null) Vm?.CloseTabCommand.Execute(tab);
        });

    private void OnCloseOtherTabsClick(object sender, RoutedEventArgs e)
        => SafeGuard.Run(() =>
        {
            if (Vm is null) return;
            var tab = GetTabFromMenuItem(sender);
            if (tab is null) return;
            var others = Vm.Tabs.Where(t => !ReferenceEquals(t, tab)).ToList();
            foreach (var t in others) Vm.CloseTabCommand.Execute(t);
        });

    private void OnCloseAllTabsClick(object sender, RoutedEventArgs e)
        => SafeGuard.Run(() =>
        {
            if (Vm is null) return;
            var all = Vm.Tabs.ToList();
            foreach (var t in all) Vm.CloseTabCommand.Execute(t);
        });

    private void OnRenameTabClick(object sender, RoutedEventArgs e)
        => SafeGuard.Run(() =>
        {
            var tab = GetTabFromMenuItem(sender);
            if (tab is null) return;

            // Themed rename dialog matching VS2022 style
            var dlg = new Window
            {
                Title = AIAssistantResources.AIAssistant_RenameConversation,
                Width = 380, Height = 150,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Window.GetWindow(this),
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = Brushes.Transparent,
                ResizeMode = ResizeMode.NoResize,
                ShowInTaskbar = false
            };

            // Outer border (themed)
            var outerBorder = new Border
            {
                CornerRadius = new CornerRadius(4),
                BorderThickness = new Thickness(1),
            };
            outerBorder.SetResourceReference(Border.BackgroundProperty, "DockBackgroundBrush");
            outerBorder.SetResourceReference(Border.BorderBrushProperty, "DockBorderBrush");

            var rootGrid = new Grid();
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });  // title bar
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // content

            // Title bar
            var titleBar = new Border { Height = 28, Cursor = Cursors.SizeAll };
            titleBar.SetResourceReference(Border.BackgroundProperty, "DockMenuBackgroundBrush");
            titleBar.MouseLeftButtonDown += (_, me) => { if (me.ClickCount == 1) dlg.DragMove(); };

            var titleDock = new DockPanel();
            var titleText = new TextBlock
            {
                Text = AIAssistantResources.AIAssistant_RenameConversation,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10, 0, 0, 0),
                FontSize = 12
            };
            titleText.SetResourceReference(TextBlock.ForegroundProperty, "DockMenuForegroundBrush");

            var closeBtn = new Button
            {
                Content = "\uE106", Width = 36, Height = 28,
                FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 10,
                BorderThickness = new Thickness(0), Cursor = Cursors.Hand
            };
            closeBtn.SetResourceReference(Button.BackgroundProperty, "DockMenuBackgroundBrush");
            closeBtn.SetResourceReference(Button.ForegroundProperty, "DockMenuForegroundBrush");
            closeBtn.Click += (_, _) => dlg.Close();
            DockPanel.SetDock(closeBtn, Dock.Right);
            titleDock.Children.Add(closeBtn);
            titleDock.Children.Add(titleText);
            titleBar.Child = titleDock;
            Grid.SetRow(titleBar, 0);

            // Content
            var content = new StackPanel { Margin = new Thickness(16, 12, 16, 14) };
            var tb = new TextBox
            {
                Text = tab.Title,
                FontSize = 13,
                Padding = new Thickness(6, 4, 6, 4),
                BorderThickness = new Thickness(1)
            };
            tb.SetResourceReference(TextBox.BackgroundProperty, "DockBackgroundBrush");
            tb.SetResourceReference(TextBox.ForegroundProperty, "DockMenuForegroundBrush");
            tb.SetResourceReference(TextBox.BorderBrushProperty, "DockBorderBrush");
            tb.SetResourceReference(TextBox.CaretBrushProperty, "DockMenuForegroundBrush");
            tb.SelectAll();
            content.Children.Add(tb);

            var btnPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 10, 0, 0)
            };

            var okBtn = new Button
            {
                Content = "OK", Width = 80, Height = 26,
                IsDefault = true, Margin = new Thickness(0, 0, 8, 0),
                FontSize = 12
            };
            okBtn.SetResourceReference(Button.BackgroundProperty, "CA_AccentBrandingBrush");
            okBtn.SetResourceReference(Button.ForegroundProperty, "DockMenuForegroundBrush");
            okBtn.Click += (_, _) => { dlg.DialogResult = true; dlg.Close(); };

            var cancelBtn = new Button
            {
                Content = "Cancel", Width = 80, Height = 26,
                IsCancel = true, FontSize = 12
            };
            cancelBtn.SetResourceReference(Button.BackgroundProperty, "DockMenuBackgroundBrush");
            cancelBtn.SetResourceReference(Button.ForegroundProperty, "DockMenuForegroundBrush");

            btnPanel.Children.Add(okBtn);
            btnPanel.Children.Add(cancelBtn);
            content.Children.Add(btnPanel);
            Grid.SetRow(content, 1);

            rootGrid.Children.Add(titleBar);
            rootGrid.Children.Add(content);
            outerBorder.Child = rootGrid;
            dlg.Content = outerBorder;

            dlg.Loaded += (_, _) => { tb.Focus(); tb.SelectAll(); };

            if (dlg.ShowDialog() == true && !string.IsNullOrWhiteSpace(tb.Text))
            {
                tab.Session.Title = tb.Text.Trim();
                tab.NotifyTitleChanged();
            }
        });

    // ── Toolbar: Provider chip ─────────────────────────────────────────
    public event Action<UIElement?>? ShowModelSwitcherRequested;

    private void OnProviderChipClick(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        SafeGuard.Run(() => ShowModelSwitcherRequested?.Invoke(sender as UIElement));
    }

    // ── Toolbar: Usage button ───────────────────────────────────────────
    private void OnUsageClick(object sender, MouseButtonEventArgs e)
        => SafeGuard.Run(() =>
        {
            var providerId = Vm?.ActiveTab?.SelectedProviderId ?? "anthropic";
            var owner = Window.GetWindow(this);
            var anchor = (sender as UIElement)?.TranslatePoint(new Point(11, 26), owner);
            var screenAnchor = anchor.HasValue ? owner!.PointToScreen(anchor.Value) : (Point?)null;
            var popup = new AccountUsagePopup(providerId, owner, screenAnchor);
            popup.Show();
        });

    // ── Toolbar: More menu ──────────────────────────────────────────────
    public event Action? ManageConnectionsRequested;
    public event Action? OpenPresetsRequested;
    public event Action? OpenOptionsRequested;

    private void OnMoreMenuClick(object sender, MouseButtonEventArgs e)
        => SafeGuard.Run(() =>
        {
            if (sender is FrameworkElement fe && fe.ContextMenu is not null)
            {
                fe.ContextMenu.PlacementTarget = fe;
                fe.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
                fe.ContextMenu.IsOpen = true;
            }
        });

    private void OnMoreConnectionsClick(object sender, RoutedEventArgs e)
        => SafeGuard.Run(() => ManageConnectionsRequested?.Invoke());

    private void OnMorePresetsClick(object sender, RoutedEventArgs e)
        => SafeGuard.Run(() => OpenPresetsRequested?.Invoke());

    private void OnMoreOptionsClick(object sender, RoutedEventArgs e)
        => SafeGuard.Run(() => OpenOptionsRequested?.Invoke());

    private static ConversationTabViewModel? GetTabFromMenuItem(object sender)
    {
        if (sender is MenuItem { Parent: System.Windows.Controls.ContextMenu ctx } && ctx.PlacementTarget is FrameworkElement fe)
            return fe.DataContext as ConversationTabViewModel;
        return null;
    }
}
