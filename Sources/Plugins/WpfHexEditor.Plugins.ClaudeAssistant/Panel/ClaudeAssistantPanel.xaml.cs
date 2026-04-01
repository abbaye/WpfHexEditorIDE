// ==========================================================
// Project: WpfHexEditor.Plugins.ClaudeAssistant
// File: ClaudeAssistantPanel.xaml.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.6
// Created: 2026-03-31
// License: GNU Affero General Public License v3.0 (AGPL-3.0)
// Description:
//     Panel code-behind. Tab click handler.
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

    private void OnTabClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: ConversationTabViewModel tab }
            && DataContext is ClaudeAssistantPanelViewModel vm)
        {
            vm.ActiveTab = tab;
        }
    }
}
