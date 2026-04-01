// ==========================================================
// Project: WpfHexEditor.Plugins.ClaudeAssistant
// File: ChatMessageViewModel.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.6
// Created: 2026-03-31
// License: GNU Affero General Public License v3.0 (AGPL-3.0)
// Description:
//     ViewModel for a single chat message bubble with streaming support.
// ==========================================================
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace WpfHexEditor.Plugins.ClaudeAssistant.Panel.Messages;

public sealed partial class ChatMessageViewModel : ObservableObject
{
    [ObservableProperty] private string _role = "user";
    [ObservableProperty] private string _text = "";
    [ObservableProperty] private string _thinkingText = "";
    [ObservableProperty] private bool _isStreaming;
    [ObservableProperty] private bool _isError;

    public ObservableCollection<ToolCallViewModel> ToolCalls { get; } = [];
    public bool HasToolCalls => ToolCalls.Count > 0;

    public bool IsUser => Role == "user";
    public bool IsAssistant => Role == "assistant";
    public bool HasThinking => !string.IsNullOrEmpty(ThinkingText);

    public void AppendText(string delta)
    {
        Text += delta;
        OnPropertyChanged(nameof(Text));
    }

    public void AppendThinking(string delta)
    {
        ThinkingText += delta;
        OnPropertyChanged(nameof(ThinkingText));
        OnPropertyChanged(nameof(HasThinking));
    }
}
