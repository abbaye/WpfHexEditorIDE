// ==========================================================
// Project: WpfHexEditor.Plugins.ClaudeAssistant
// File: ConversationTab.xaml.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.6
// Created: 2026-03-31
// License: GNU Affero General Public License v3.0 (AGPL-3.0)
// Description:
//     Conversation tab code-behind. All handlers wrapped in SafeGuard.Run().
// ==========================================================
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace WpfHexEditor.Plugins.ClaudeAssistant.Panel.Tabs;

public partial class ConversationTab : UserControl
{
    private ConversationTabViewModel? _wiredVm;
    private bool _mentionWired;

    public ConversationTab()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        DataContextChanged += (_, _) => SafeGuard.Run(WireCurrentVm);
    }

    private ConversationTabViewModel? Vm => DataContext as ConversationTabViewModel;

    private void OnLoaded(object sender, RoutedEventArgs e)
        => SafeGuard.Run(() =>
        {
            WireCurrentVm();

            // Wire @mention selection (once only)
            if (!_mentionWired)
            {
                _mentionWired = true;
                MentionControl.MentionSelected += token =>
                {
                    SafeGuard.Run(() =>
                    {
                        if (Vm is null) return;
                        var text = Vm.InputText;
                        var atIdx = text.LastIndexOf('@');
                        if (atIdx >= 0)
                            Vm.InputText = text[..atIdx] + token;
                        else
                            Vm.InputText += token;
                        MentionPopup.IsOpen = false;
                        InputBox.CaretIndex = Vm.InputText.Length;
                        InputBox.Focus();
                    });
                };
            }
        });

    private void WireCurrentVm()
    {
        var vm = Vm;
        if (vm is null) { UpdateEmptyState(); return; }
        if (ReferenceEquals(vm, _wiredVm)) { UpdateEmptyState(); return; }

        _wiredVm = vm;

        ((INotifyCollectionChanged)vm.Messages).CollectionChanged += (_, _) =>
            SafeGuard.Run(() =>
            {
                ChatScroller.ScrollToEnd();
                UpdateEmptyState();
            });

        vm.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(vm.InputText))
            {
                UpdateWatermark();
                CheckMentionTrigger();
            }
        };

        UpdateEmptyState();
        UpdateWatermark();
    }

    private void UpdateWatermark()
    {
        InputWatermark.Visibility = string.IsNullOrEmpty(Vm?.InputText)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void OnInputKeyDown(object sender, KeyEventArgs e)
        => SafeGuard.Run(() =>
        {
            if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Alt)
            {
                if (Vm?.SendCommand is { } cmd && cmd.CanExecute(null))
                {
                    cmd.Execute(null);
                    e.Handled = true;
                }
            }
            else if (e.Key == Key.Escape && Vm?.IsStreaming == true)
            {
                Vm.CancelCommand.Execute(null);
                e.Handled = true;
            }
        });

    private void OnSendClick(object sender, MouseButtonEventArgs e)
        => SafeGuard.Run(() =>
        {
            if (Vm?.SendCommand is { } cmd && cmd.CanExecute(null))
                cmd.Execute(null);
        });

    private void OnCancelClick(object sender, MouseButtonEventArgs e)
        => SafeGuard.Run(() => Vm?.CancelCommand.Execute(null));

    private void UpdateEmptyState()
    {
        var hasMessages = Vm?.Messages.Count > 0;
        WelcomePanel.Visibility = hasMessages ? Visibility.Collapsed : Visibility.Visible;
        ChatScroller.Visibility = hasMessages ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnSuggestionClick(object sender, MouseButtonEventArgs e)
        => SafeGuard.Run(() =>
        {
            if (sender is FrameworkElement { Tag: string prompt } && Vm is not null)
            {
                Vm.InputText = prompt;
                if (Vm.SendCommand.CanExecute(null))
                    Vm.SendCommand.Execute(null);
            }
        });

    private void CheckMentionTrigger()
    {
        var text = Vm?.InputText ?? "";
        var atIdx = text.LastIndexOf('@');
        if (atIdx >= 0 && (atIdx == 0 || text[atIdx - 1] == ' '))
        {
            var query = text[(atIdx + 1)..];
            // Don't show if the token is already complete (has a space after it)
            if (!query.Contains(' '))
            {
                MentionControl.Filter(query);
                MentionPopup.IsOpen = true;
                return;
            }
        }
        MentionPopup.IsOpen = false;
    }

    private void OnModelPillClick(object sender, MouseButtonEventArgs e)
        => SafeGuard.Run(() =>
        {
            // Model switcher is handled by the parent panel which has access to the registry.
            // Fire a routed event or let the pill click bubble up.
            // For now, this is a no-op placeholder until Phase 5 wiring.
        });
}
