// ==========================================================
// Project: WpfHexEditor.Plugins.AIAssistant
// File: ConversationTabViewModel.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.6
// Created: 2026-03-31
// License: GNU Affero General Public License v3.0 (AGPL-3.0)
// Description:
//     ViewModel for a conversation tab. Streaming loop, Send/Cancel, tool execution.
// ==========================================================
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WpfHexEditor.Plugins.AIAssistant.Api;
using WpfHexEditor.Plugins.AIAssistant.Mcp.Host;
using WpfHexEditor.Plugins.AIAssistant.Panel.Messages;
using WpfHexEditor.Plugins.AIAssistant.Session;

namespace WpfHexEditor.Plugins.AIAssistant.Panel.Tabs;

public sealed partial class ConversationTabViewModel : ObservableObject
{
    public ModelRegistry Registry => _registry;
    private readonly ModelRegistry _registry;
    private IMcpServerManager? _mcpManager;
    private CancellationTokenSource? _streamCts;

    public ConversationSession Session { get; }
    public ObservableCollection<ChatMessageViewModel> Messages { get; } = [];
    public ObservableCollection<AttachmentViewModel> Attachments { get; } = [];

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

    public void AddImageAttachment(System.Windows.Media.Imaging.BitmapSource image)
    {
        using var ms = new System.IO.MemoryStream();
        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(image));
        encoder.Save(ms);
        var base64 = Convert.ToBase64String(ms.ToArray());
        Attachments.Add(new AttachmentViewModel
        {
            DisplayName = $"Image ({image.PixelWidth}x{image.PixelHeight})",
            Base64Data = base64,
            MediaType = "image/png",
            IsImage = true
        });
    }

    public void AddFileAttachment(string filePath)
    {
        var name = System.IO.Path.GetFileName(filePath);
        var ext = System.IO.Path.GetExtension(filePath).ToLowerInvariant();
        var isImage = ext is ".png" or ".jpg" or ".jpeg" or ".gif" or ".webp" or ".bmp";

        string? base64 = null;
        string mediaType = "application/octet-stream";
        if (isImage)
        {
            base64 = Convert.ToBase64String(System.IO.File.ReadAllBytes(filePath));
            mediaType = ext switch
            {
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                _ => "image/png"
            };
        }
        else
        {
            mediaType = "text/plain";
        }

        Attachments.Add(new AttachmentViewModel
        {
            DisplayName = name,
            FilePath = filePath,
            Base64Data = base64,
            MediaType = mediaType,
            IsImage = isImage
        });
    }

    [RelayCommand(CanExecute = nameof(CanSend))]
    private async Task SendAsync()
    {
        var text = InputText.Trim();
        if (string.IsNullOrEmpty(text) && Attachments.Count == 0) return;

        // Build content blocks from text + attachments
        var content = new List<ContentBlock>();
        if (!string.IsNullOrEmpty(text))
            content.Add(new TextBlock(text));

        var displayText = text;
        foreach (var att in Attachments)
        {
            if (att.IsImage && att.Base64Data is not null)
            {
                content.Add(new ImageBlock(att.Base64Data, att.MediaType));
                displayText += $"\n[Attached: {att.DisplayName}]";
            }
            else if (att.FilePath is not null)
            {
                var ext = System.IO.Path.GetExtension(att.FilePath).ToLowerInvariant();
                var isTextFile = ext is ".txt" or ".cs" or ".xml" or ".json" or ".md" or ".yaml" or ".yml"
                    or ".html" or ".css" or ".js" or ".ts" or ".py" or ".cpp" or ".h" or ".c"
                    or ".java" or ".rb" or ".go" or ".rs" or ".toml" or ".ini" or ".cfg"
                    or ".log" or ".csv" or ".sql" or ".sh" or ".bat" or ".ps1" or ".xaml";

                if (isTextFile)
                {
                    try
                    {
                        var fileText = System.IO.File.ReadAllText(att.FilePath);
                        content.Add(new TextBlock($"\n--- {att.DisplayName} ---\n{fileText}\n---"));
                        displayText += $"\n[Attached: {att.DisplayName}]";
                    }
                    catch { displayText += $"\n[Failed to read: {att.DisplayName}]"; }
                }
                else
                {
                    // Binary file — just mention the path so CLI can access it
                    content.Add(new TextBlock($"\n[File attached: {att.FilePath}]"));
                    displayText += $"\n[Attached: {att.DisplayName}]";
                }
            }
        }
        Attachments.Clear();

        // Add user message
        var userMsg = new ChatMessage { Role = "user", Content = content };
        Session.AddMessage(userMsg);
        Messages.Add(new ChatMessageViewModel { Role = "user", Text = displayText ?? "" });
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

            // Batched text accumulation for smooth streaming
            var textBuf = new StringBuilder();
            var lastFlush = Stopwatch.GetTimestamp();

            await foreach (var chunk in provider.StreamAsync(
                Session.Messages, SelectedModelId, tools, thinking, _streamCts.Token))
            {
                switch (chunk.Kind)
                {
                    case ChunkKind.TextDelta:
                        textBuf.Append(chunk.Text ?? "");
                        if (Stopwatch.GetElapsedTime(lastFlush).TotalMilliseconds >= 50)
                        {
                            var batch = textBuf.ToString();
                            textBuf.Clear();
                            lastFlush = Stopwatch.GetTimestamp();
                            await Application.Current.Dispatcher.InvokeAsync(() => assistantVm.AppendText(batch));
                        }
                        break;

                    case ChunkKind.ThinkingDelta:
                        // Flush text first
                        if (textBuf.Length > 0)
                        {
                            var tb = textBuf.ToString(); textBuf.Clear();
                            await Application.Current.Dispatcher.InvokeAsync(() => assistantVm.AppendText(tb));
                        }
                        await Application.Current.Dispatcher.InvokeAsync(() => assistantVm.AppendThinking(chunk.ThinkingText ?? ""));
                        break;

                    case ChunkKind.ToolUseStart:
                        if (textBuf.Length > 0)
                        {
                            var tb = textBuf.ToString(); textBuf.Clear();
                            await Application.Current.Dispatcher.InvokeAsync(() => assistantVm.AppendText(tb));
                        }
                        await Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            currentToolCallId = chunk.ToolCallId;
                            currentToolCall = new ToolCallViewModel
                            {
                                ToolName = chunk.ToolName ?? "",
                                Status = ToolCallStatus.Running
                            };
                            assistantVm.ToolCalls.Add(currentToolCall);
                        });
                        break;

                    case ChunkKind.ToolInputDelta:
                        await Application.Current.Dispatcher.InvokeAsync(() => currentToolCall?.AppendArgs(chunk.ToolInputJson ?? ""));
                        break;

                    case ChunkKind.Error:
                        await Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            assistantVm.Text = $"Error: {chunk.ErrorMessage}";
                            assistantVm.IsError = true;
                        });
                        break;

                    case ChunkKind.Done:
                        break;
                }
            }

            // Final flush
            if (textBuf.Length > 0)
                await Application.Current.Dispatcher.InvokeAsync(() => assistantVm.AppendText(textBuf.ToString()));

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
