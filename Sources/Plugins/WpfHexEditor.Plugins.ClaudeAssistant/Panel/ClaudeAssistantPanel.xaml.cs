// ==========================================================
// Project: WpfHexEditor.Plugins.ClaudeAssistant
// File: ClaudeAssistantPanel.xaml.cs
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
using WpfHexEditor.Plugins.ClaudeAssistant.Panel.Tabs;

namespace WpfHexEditor.Plugins.ClaudeAssistant.Panel;

public partial class ClaudeAssistantPanel : UserControl
{
    public ClaudeAssistantPanel()
    {
        InitializeComponent();
    }

    private ClaudeAssistantPanelViewModel? Vm => DataContext as ClaudeAssistantPanelViewModel;

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

            // Show a simple rename dialog
            var dlg = new Window
            {
                Title = "Rename Conversation",
                Width = 360, Height = 130,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Window.GetWindow(this),
                WindowStyle = WindowStyle.ToolWindow,
                ResizeMode = ResizeMode.NoResize
            };
            var panel = new StackPanel { Margin = new Thickness(12) };
            var tb = new TextBox { Text = tab.Title, FontSize = 13, Padding = new Thickness(4) };
            tb.SelectAll();
            panel.Children.Add(tb);
            var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 8, 0, 0) };
            var okBtn = new Button { Content = "OK", Width = 70, IsDefault = true, Margin = new Thickness(0, 0, 6, 0) };
            var cancelBtn = new Button { Content = "Cancel", Width = 70, IsCancel = true };
            okBtn.Click += (_, _) => { dlg.DialogResult = true; dlg.Close(); };
            btnPanel.Children.Add(okBtn);
            btnPanel.Children.Add(cancelBtn);
            panel.Children.Add(btnPanel);
            dlg.Content = panel;

            if (dlg.ShowDialog() == true && !string.IsNullOrWhiteSpace(tb.Text))
            {
                tab.Session.Title = tb.Text.Trim();
                tab.NotifyTitleChanged();
            }
        });

    private static ConversationTabViewModel? GetTabFromMenuItem(object sender)
    {
        if (sender is MenuItem { Parent: System.Windows.Controls.ContextMenu ctx } && ctx.PlacementTarget is FrameworkElement fe)
            return fe.DataContext as ConversationTabViewModel;
        return null;
    }
}
