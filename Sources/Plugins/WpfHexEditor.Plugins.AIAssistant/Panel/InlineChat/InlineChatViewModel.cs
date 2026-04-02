// ==========================================================
// Project: WpfHexEditor.Plugins.AIAssistant
// File: InlineChatViewModel.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.6
// Created: 2026-03-31
// License: GNU Affero General Public License v3.0 (AGPL-3.0)
// Description:
//     ViewModel for the inline chat popup (Ctrl+I). Lightweight session,
//     not persisted by default. Can be transferred to a full tab.
// ==========================================================
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WpfHexEditor.Plugins.AIAssistant.Api;
using WpfHexEditor.Plugins.AIAssistant.Panel.Messages;

namespace WpfHexEditor.Plugins.AIAssistant.Panel.InlineChat;

public sealed partial class InlineChatViewModel : ObservableObject
{
    private readonly ModelRegistry _registry;
    private CancellationTokenSource? _streamCts;

    [ObservableProperty] private string _inputText = "";
    [ObservableProperty] private string _responseText = "";
    [ObservableProperty] private bool _isStreaming;
    [ObservableProperty] private bool _isVisible;
    [ObservableProperty] private string _selectedProviderId = "anthropic";
    [ObservableProperty] private string _selectedModelId = "claude-sonnet-4-6";

    public string? InjectedSelection { get; set; }

    public event Action? DismissRequested;
    public event Action<string, string>? OpenInTabRequested;

    public InlineChatViewModel(ModelRegistry registry)
    {
        _registry = registry;
    }

    [RelayCommand]
    private async Task SendAsync()
    {
        var text = InputText.Trim();
        if (string.IsNullOrEmpty(text)) return;

        var provider = _registry.GetProvider(SelectedProviderId);
        if (provider is null)
        {
            ResponseText = "Provider not found.";
            return;
        }

        IsStreaming = true;
        ResponseText = "";
        _streamCts = new CancellationTokenSource();

        // Build messages with optional selection context
        var messages = new List<ChatMessage>();
        if (!string.IsNullOrEmpty(InjectedSelection))
        {
            messages.Add(ChatMessage.User($"```\n{InjectedSelection}\n```\n\n{text}"));
        }
        else
        {
            messages.Add(ChatMessage.User(text));
        }

        try
        {
            await foreach (var chunk in provider.StreamAsync(messages, SelectedModelId, ct: _streamCts.Token))
            {
                if (chunk.Kind == ChunkKind.TextDelta)
                {
                    Application.Current.Dispatcher.Invoke(() => ResponseText += chunk.Text);
                }
                else if (chunk.Kind == ChunkKind.Error)
                {
                    Application.Current.Dispatcher.Invoke(() => ResponseText = $"Error: {chunk.ErrorMessage}");
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            ResponseText += " [cancelled]";
        }
        finally
        {
            IsStreaming = false;
            _streamCts?.Dispose();
            _streamCts = null;
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        _streamCts?.Cancel();
    }

    [RelayCommand]
    private void Dismiss()
    {
        _streamCts?.Cancel();
        IsVisible = false;
        DismissRequested?.Invoke();
    }

    [RelayCommand]
    private void OpenInTab()
    {
        var question = InputText;
        var response = ResponseText;
        Dismiss();
        OpenInTabRequested?.Invoke(question, response);
    }

    public void Show(string? selectedText)
    {
        InjectedSelection = selectedText;
        InputText = "";
        ResponseText = "";
        IsVisible = true;
    }
}
