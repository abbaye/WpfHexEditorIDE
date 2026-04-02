// ==========================================================
// Project: WpfHexEditor.Plugins.AIAssistant
// File: ChatMessageViewModel.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.6
// Created: 2026-03-31
// License: GNU Affero General Public License v3.0 (AGPL-3.0)
// Description:
//     ViewModel for a single chat message bubble with streaming support.
// ==========================================================
using System.Collections.ObjectModel;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;

namespace WpfHexEditor.Plugins.AIAssistant.Panel.Messages;

public sealed partial class ChatMessageViewModel : ObservableObject
{
    [ObservableProperty] private string _role = "user";
    [ObservableProperty] private string _text = "";
    [ObservableProperty] private string _thinkingText = "";
    [ObservableProperty] private bool _isStreaming;
    [ObservableProperty] private bool _isError;

    private readonly StringBuilder _textBuilder = new(4096);
    private readonly StringBuilder _thinkingBuilder = new(2048);

    public ObservableCollection<ToolCallViewModel> ToolCalls { get; } = [];
    public bool HasToolCalls => ToolCalls.Count > 0;

    public bool IsUser => Role == "user";
    public bool IsAssistant => Role == "assistant";
    public bool HasThinking => !string.IsNullOrEmpty(ThinkingText);

    public void AppendText(string delta)
    {
        _textBuilder.Append(delta);
        Text = _textBuilder.ToString();
    }

    public void AppendThinking(string delta)
    {
        _thinkingBuilder.Append(delta);
        ThinkingText = _thinkingBuilder.ToString();
        OnPropertyChanged(nameof(HasThinking));
    }

    /// <summary>Resets builders for reuse (e.g. new streaming message).</summary>
    public void ResetBuilders()
    {
        _textBuilder.Clear();
        _thinkingBuilder.Clear();
    }
}
