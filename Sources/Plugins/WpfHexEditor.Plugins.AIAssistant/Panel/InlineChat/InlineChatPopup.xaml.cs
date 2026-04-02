// ==========================================================
// Project: WpfHexEditor.Plugins.AIAssistant
// File: InlineChatPopup.xaml.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.6
// Created: 2026-03-31
// License: GNU Affero General Public License v3.0 (AGPL-3.0)
// Description:
//     Inline chat popup code-behind. All handlers wrapped in SafeGuard.
// ==========================================================
using System.Windows.Controls;
using System.Windows.Input;

namespace WpfHexEditor.Plugins.AIAssistant.Panel.InlineChat;

public partial class InlineChatPopup : UserControl
{
    public InlineChatPopup()
    {
        InitializeComponent();
    }

    private void OnInputKeyDown(object sender, KeyEventArgs e)
        => SafeGuard.Run(() =>
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
        });
}
