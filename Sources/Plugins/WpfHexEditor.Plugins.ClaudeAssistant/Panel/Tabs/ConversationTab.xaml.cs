// ==========================================================
// Project: WpfHexEditor.Plugins.ClaudeAssistant
// File: ConversationTab.xaml.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.6
// Created: 2026-03-31
// License: GNU Affero General Public License v3.0 (AGPL-3.0)
// Description:
//     Conversation tab code-behind. Auto-scroll and input key handling.
// ==========================================================
using System.Collections.Specialized;
using System.Windows.Controls;
using System.Windows.Input;

namespace WpfHexEditor.Plugins.ClaudeAssistant.Panel.Tabs;

public partial class ConversationTab : UserControl
{
    public ConversationTab()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is ConversationTabViewModel vm)
        {
            ((INotifyCollectionChanged)vm.Messages).CollectionChanged += (_, _) =>
            {
                ChatScroller.ScrollToEnd();
            };
        }
    }

    private void OnInputKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None)
        {
            if (DataContext is ConversationTabViewModel { SendCommand: { } cmd } && cmd.CanExecute(null))
            {
                cmd.Execute(null);
                e.Handled = true;
            }
        }
    }

    private void OnProviderChanged(object sender, SelectionChangedEventArgs e)
    {
        // Model list update handled by VM property change
    }
}
