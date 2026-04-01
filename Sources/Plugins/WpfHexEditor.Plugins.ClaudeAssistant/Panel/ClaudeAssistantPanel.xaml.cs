// ==========================================================
// Project: WpfHexEditor.Plugins.ClaudeAssistant
// File: ClaudeAssistantPanel.xaml.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.6
// Created: 2026-03-31
// License: GNU Affero General Public License v3.0 (AGPL-3.0)
// Description:
//     Panel code-behind. Tab click, close tab, new tab handlers.
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
        => Vm?.CreateNewTabCommand.Execute(null);

    private void OnTabClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: ConversationTabViewModel tab } && Vm is not null)
            Vm.ActiveTab = tab;
    }

    private void OnCloseTabClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: ConversationTabViewModel tab })
        {
            Vm?.CloseTabCommand.Execute(tab);
            e.Handled = true;
        }
    }
}
