// ==========================================================
// Project: WpfHexEditor.Plugins.ClaudeAssistant
// File: InlineChatPopup.xaml.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.6
// Created: 2026-03-31
// License: GNU Affero General Public License v3.0 (AGPL-3.0)
// Description:
//     Inline chat popup code-behind. Handles Escape/Enter key shortcuts.
// ==========================================================
using System.Windows.Controls;
using System.Windows.Input;

namespace WpfHexEditor.Plugins.ClaudeAssistant.Panel.InlineChat;

public partial class InlineChatPopup : UserControl
{
    public InlineChatPopup()
    {
        InitializeComponent();
    }

    private void OnInputKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not InlineChatViewModel vm) return;

        switch (e.Key)
        {
            case Key.Enter when Keyboard.Modifiers == ModifierKeys.None:
                if (vm.SendCommand.CanExecute(null))
                    vm.SendCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Escape:
                vm.DismissCommand.Execute(null);
                e.Handled = true;
                break;
        }
    }
}
