// ==========================================================
// Project: WpfHexEditor.Plugins.ClaudeAssistant
// File: ConversationTabViewModel.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.6
// Created: 2026-03-31
// License: GNU Affero General Public License v3.0 (AGPL-3.0)
// Description:
//     ViewModel for a conversation tab. Streaming loop, Send/Cancel, tool execution.
// ==========================================================
using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WpfHexEditor.Plugins.ClaudeAssistant.Api;
using WpfHexEditor.Plugins.ClaudeAssistant.Mcp.Host;
using WpfHexEditor.Plugins.ClaudeAssistant.Panel.Messages;
using WpfHexEditor.Plugins.ClaudeAssistant.Session;

namespace WpfHexEditor.Plugins.ClaudeAssistant.Panel.Tabs;

public sealed partial class ConversationTabViewModel : ObservableObject
{
    private readonly ModelRegistry _registry;
    private IMcpServerManager? _mcpManager;
    private CancellationTokenSource? _streamCts;

    public ConversationSession Session { get; }
    public ObservableCollection<ChatMessageViewModel> Messages { get; } = [];

    [ObservableProperty] private string _inputText = "";
    [ObservableProperty] private bool _isStreaming;
    [ObservableProperty] private bool _isActive;
    [ObservableProperty] private string _selectedProviderId;
    [ObservableProperty] private string _selectedModelId;
    [ObservableProperty] private bool _thinkingEnabled;

    public string Title => Session.Title;
    public void NotifyTitleChanged() => OnPropertyChanged(nameof(Title));

    public ConversationTabViewModel(ConversationSession session, ModelRegistry registry, IMcpServerManager? mcpManager = null)
    {
        Session = session;
        _registry = registry;
        _mcpManager = mcpManager;
        _selectedProviderId = session.ProviderId;
        _selectedModelId = session.ModelId;
        _thinkingEnabled = session.ThinkingEnabled;
    }

    public void SetMcpManager(IMcpServerManager manager) => _mcpManager = manager;

    [RelayCommand(CanExecute = nameof(CanSend))]
    private async Task SendAsync()
    {
        var text = InputText.Trim();
        if (string.IsNullOrEmpty(text)) return;

        // Add user message
        var userMsg = ChatMessage.User(text);
        Session.AddMessage(userMsg);
        Messages.Add(new ChatMessageViewModel { Role = "user", Text = text });
        InputText = "";

        // Auto-title from first message
        if (Session.Messages.Count == 1)
            Session.Title = text.Length > 40 ? text[..40] + "..." : text;

        // Update session provider/model
        Session.ProviderId = SelectedProviderId;
        Session.ModelId = SelectedModelId;
        Session.ThinkingEnabled = ThinkingEnabled;

        // Get provider
        var provider = _registry.GetProvider(SelectedProviderId);
        if (provider is null)
        {
            Messages.Add(new ChatMessageViewModel
            {
                Role = "assistant", Text = $"Provider '{SelectedProviderId}' not found.", IsError = true
            });
            return;
        }

        // Start streaming
        var assistantVm = new ChatMessageViewModel { Role = "assistant", IsStreaming = true };
        Messages.Add(assistantVm);
        IsStreaming = true;
        _streamCts = new CancellationTokenSource();

        var thinking = provider.SupportsThinking && ThinkingEnabled
            ? new ThinkingConfig(true, Session.ThinkingBudgetTokens)
            : null;

        // Get MCP tools if available
        var tools = _mcpManager?.GetAllTools();

        try
        {
            ToolCallViewModel? currentToolCall = null;
            string? currentToolCallId = null;

            await foreach (var chunk in provider.StreamAsync(
                Session.Messages, SelectedModelId, tools, thinking, _streamCts.Token))
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    switch (chunk.Kind)
                    {
                        case ChunkKind.TextDelta:
                            assistantVm.AppendText(chunk.Text ?? "");
                            break;
                        case ChunkKind.ThinkingDelta:
                            assistantVm.AppendThinking(chunk.ThinkingText ?? "");
                            break;
                        case ChunkKind.ToolUseStart:
                            currentToolCallId = chunk.ToolCallId;
                            currentToolCall = new ToolCallViewModel
                            {
                                ToolName = chunk.ToolName ?? "",
                                Status = ToolCallStatus.Running
                            };
                            assistantVm.ToolCalls.Add(currentToolCall);
                            break;
                        case ChunkKind.ToolInputDelta:
                            currentToolCall?.AppendArgs(chunk.ToolInputJson ?? "");
                            break;
                        case ChunkKind.Error:
                            assistantVm.Text = $"Error: {chunk.ErrorMessage}";
                            assistantVm.IsError = true;
                            break;
                        case ChunkKind.Done:
                            break;
                    }
                });
            }

            // Execute pending tool calls and feed results back
            if (assistantVm.HasToolCalls && _mcpManager is not null)
            {
                foreach (var tc in assistantVm.ToolCalls.Where(t => t.Status == ToolCallStatus.Running))
                {
                    try
                    {
                        var result = await _mcpManager.CallToolAsync(tc.ToolName, tc.ArgsJson, _streamCts.Token);
                        Application.Current.Dispatcher.Invoke(() => tc.SetResult(result));
                    }
                    catch (Exception ex)
                    {
                        Application.Current.Dispatcher.Invoke(() => tc.SetResult($"{{\"error\":\"{ex.Message}\"}}", isError: true));
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            assistantVm.AppendText(" [cancelled]");
        }
        catch (Exception ex)
        {
            assistantVm.Text = $"Error: {ex.Message}";
            assistantVm.IsError = true;
        }
        finally
        {
            assistantVm.IsStreaming = false;
            IsStreaming = false;
            _streamCts?.Dispose();
            _streamCts = null;

            // Save assistant message to session
            Session.AddMessage(ChatMessage.Assistant(assistantVm.Text));

            OnPropertyChanged(nameof(Title));
            SendCommand.NotifyCanExecuteChanged();
            CancelCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanSend() => !IsStreaming && !string.IsNullOrWhiteSpace(InputText);

    [RelayCommand(CanExecute = nameof(IsStreaming))]
    private void Cancel()
    {
        _streamCts?.Cancel();
    }

    partial void OnInputTextChanged(string value) => SendCommand.NotifyCanExecuteChanged();
    partial void OnIsStreamingChanged(bool value)
    {
        SendCommand.NotifyCanExecuteChanged();
        CancelCommand.NotifyCanExecuteChanged();
    }
}
