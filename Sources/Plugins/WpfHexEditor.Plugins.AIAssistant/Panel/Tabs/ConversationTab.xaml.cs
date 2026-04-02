// ==========================================================
// Project: WpfHexEditor.Plugins.AIAssistant
// File: ConversationTab.xaml.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.6
// Created: 2026-03-31
// License: GNU Affero General Public License v3.0 (AGPL-3.0)
// Description:
//     Conversation tab code-behind. All handlers wrapped in SafeGuard.Run().
// ==========================================================
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WpfHexEditor.Plugins.AIAssistant.Options;
using WpfHexEditor.Plugins.AIAssistant.Panel.ConnectionManager;
using WpfHexEditor.Plugins.AIAssistant.Panel.Messages;
using WpfHexEditor.Plugins.AIAssistant.Panel.ModelSwitcher;

namespace WpfHexEditor.Plugins.AIAssistant.Panel.Tabs;

public partial class ConversationTab : UserControl
{
    private ConversationTabViewModel? _wiredVm;
    private bool _mentionWired;
    private NotifyCollectionChangedEventHandler? _collectionHandler;
    private PropertyChangedEventHandler? _propertyHandler;
    private System.Windows.Threading.DispatcherTimer? _scrollTimer;

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

        // Unwire old handlers to prevent leaks
        if (_wiredVm is not null)
        {
            ((INotifyCollectionChanged)_wiredVm.Messages).CollectionChanged -= _collectionHandler;
            _wiredVm.PropertyChanged -= _propertyHandler;
        }

        if (vm is null) { _wiredVm = null; UpdateEmptyState(); return; }
        if (ReferenceEquals(vm, _wiredVm)) { UpdateEmptyState(); return; }

        _wiredVm = vm;

        _collectionHandler = (_, _) =>
            SafeGuard.Run(() =>
            {
                ScrollChatToEnd();
                UpdateEmptyState();
            });

        _propertyHandler = (_, args) =>
        {
            if (args.PropertyName == nameof(vm.InputText))
            {
                UpdateWatermark();
                CheckMentionTrigger();
            }
            else if (args.PropertyName == nameof(vm.IsStreaming))
            {
                // Auto-scroll during streaming
                if (vm.IsStreaming)
                {
                    _scrollTimer ??= new System.Windows.Threading.DispatcherTimer
                    {
                        Interval = TimeSpan.FromMilliseconds(200)
                    };
                    _scrollTimer.Tick += (_, _) => ScrollChatToEnd();
                    _scrollTimer.Start();
                }
                else
                {
                    _scrollTimer?.Stop();
                    ScrollChatToEnd();
                }
            }
        };

        ((INotifyCollectionChanged)vm.Messages).CollectionChanged += _collectionHandler;
        vm.PropertyChanged += _propertyHandler;

        UpdateEmptyState();
        UpdateWatermark();
    }

    private void ScrollChatToEnd()
    {
        if (VisualTreeHelper.GetChildrenCount(ChatList) > 0)
        {
            var border = VisualTreeHelper.GetChild(ChatList, 0) as Border;
            var sv = border?.Child as ScrollViewer;
            sv?.ScrollToEnd();
        }
    }

    private void OnInputTextChanged(object sender, TextChangedEventArgs e)
        => SafeGuard.Run(UpdateWatermark);

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

    private void OnInputPreviewKeyDown(object sender, KeyEventArgs e)
        => SafeGuard.Run(() =>
        {
            if (e.Key == Key.V && Keyboard.Modifiers == ModifierKeys.Control)
            {
                if (Clipboard.ContainsImage())
                {
                    var img = Clipboard.GetImage();
                    if (img is not null)
                        Vm?.AddImageAttachment(img);
                    e.Handled = true;
                }
                else if (Clipboard.ContainsFileDropList())
                {
                    var files = Clipboard.GetFileDropList();
                    foreach (string? file in files)
                        if (file is not null) Vm?.AddFileAttachment(file);
                    e.Handled = true;
                }
                // else: normal text paste, don't handle
            }
        });

    private void OnFileDrop(object sender, DragEventArgs e)
        => SafeGuard.Run(() =>
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = e.Data.GetData(DataFormats.FileDrop) as string[];
                if (files is not null)
                    foreach (var file in files)
                        Vm?.AddFileAttachment(file);
            }
        });

    private void OnRemoveAttachment(object sender, MouseButtonEventArgs e)
        => SafeGuard.Run(() =>
        {
            if (sender is FrameworkElement { DataContext: Messages.AttachmentViewModel att } && Vm is not null)
                Vm.Attachments.Remove(att);
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
        ChatList.Visibility = hasMessages ? Visibility.Visible : Visibility.Collapsed;
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

    private ChatMessageViewModel? GetMessageFromSender(object sender)
    {
        // Direct DataContext (from hover button)
        if (sender is FrameworkElement { DataContext: ChatMessageViewModel msg })
            return msg;
        // From ContextMenu MenuItem → PlacementTarget is the Grid
        if (sender is MenuItem mi && mi.Parent is System.Windows.Controls.ContextMenu ctx
            && ctx.PlacementTarget is FrameworkElement { DataContext: ChatMessageViewModel ctxMsg })
            return ctxMsg;
        return null;
    }

    private void OnCopyMessageClick(object sender, RoutedEventArgs e)
        => SafeGuard.Run(() =>
        {
            var msg = GetMessageFromSender(sender);
            if (msg is not null && !string.IsNullOrEmpty(msg.Text))
                Clipboard.SetText(msg.Text);
        });

    private void OnCopyMarkdownClick(object sender, RoutedEventArgs e)
        => SafeGuard.Run(() =>
        {
            var msg = GetMessageFromSender(sender);
            if (msg is not null && !string.IsNullOrEmpty(msg.Text))
                Clipboard.SetText(msg.Text);
        });

    private void OnRegenerateClick(object sender, RoutedEventArgs e)
        => SafeGuard.Run(() =>
        {
            if (Vm is null) return;
            // Find last user message and resend
            var lastUserMsg = Vm.Messages.LastOrDefault(m => m.Role == "user");
            if (lastUserMsg is null) return;
            Vm.InputText = lastUserMsg.Text;
            if (Vm.SendCommand is { } cmd && cmd.CanExecute(null))
                cmd.Execute(null);
        });

    private void OnDeleteMessageClick(object sender, RoutedEventArgs e)
        => SafeGuard.Run(() =>
        {
            var msg = GetMessageFromSender(sender);
            if (msg is not null && Vm is not null)
                Vm.Messages.Remove(msg);
        });

}
