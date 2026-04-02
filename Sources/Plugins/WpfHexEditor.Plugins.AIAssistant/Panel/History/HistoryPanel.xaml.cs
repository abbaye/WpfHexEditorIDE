// ==========================================================
// Project: WpfHexEditor.Plugins.AIAssistant
// File: HistoryPanel.xaml.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.6
// Created: 2026-03-31
// License: GNU Affero General Public License v3.0 (AGPL-3.0)
// Description:
//     History panel code-behind. All handlers wrapped in SafeGuard.
// ==========================================================
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfHexEditor.Plugins.AIAssistant.Session;

namespace WpfHexEditor.Plugins.AIAssistant.Panel.History;

public partial class HistoryPanel : UserControl
{
    public HistoryPanel()
    {
        InitializeComponent();
    }

    private HistoryPanelViewModel? Vm => DataContext as HistoryPanelViewModel;

    private void OnEntryClick(object sender, MouseButtonEventArgs e)
        => SafeGuard.Run(() =>
        {
            if (sender is FrameworkElement { DataContext: SessionMetadata meta })
                Vm?.OpenSessionCommand.Execute(meta);
        });

    private void OnDeleteClick(object sender, MouseButtonEventArgs e)
        => SafeGuard.Run(() =>
        {
            if (sender is FrameworkElement { DataContext: SessionMetadata meta })
            {
                Vm?.DeleteSessionCommand.Execute(meta);
                e.Handled = true;
            }
        });

    private void OnClearAllClick(object sender, MouseButtonEventArgs e)
        => SafeGuard.Run(() => Vm?.ClearAllCommand.Execute(null));

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
        => SafeGuard.Run(() =>
        {
            SearchWatermark.Visibility = string.IsNullOrEmpty(SearchBox.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;
        });
}
