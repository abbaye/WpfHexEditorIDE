// ==========================================================
// Project: WpfHexEditor.Plugins.ClaudeAssistant
// File: ChatMessage.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.6
// Created: 2026-03-31
// License: GNU Affero General Public License v3.0 (AGPL-3.0)
// Description:
//     Provider-agnostic chat message model with text, image, tool-use, and thinking blocks.
// ==========================================================
namespace WpfHexEditor.Plugins.ClaudeAssistant.Api;

public sealed class ChatMessage
{
    public required string Role { get; init; }  // "user", "assistant", "system"
    public required List<ContentBlock> Content { get; init; }

    public static ChatMessage User(string text) => new()
    {
        Role = "user",
        Content = [new TextBlock(text)]
    };

    public static ChatMessage Assistant(string text) => new()
    {
        Role = "assistant",
        Content = [new TextBlock(text)]
    };

    public static ChatMessage System(string text) => new()
    {
        Role = "system",
        Content = [new TextBlock(text)]
    };

    public string GetTextContent() => string.Join("", Content.OfType<TextBlock>().Select(b => b.Text));
}

public abstract record ContentBlock;

public sealed record TextBlock(string Text) : ContentBlock;

public sealed record ImageBlock(string Base64Data, string MediaType) : ContentBlock;

public sealed record ToolUseBlock(string ToolCallId, string ToolName, string ArgsJson) : ContentBlock;

public sealed record ToolResultBlock(string ToolCallId, string ResultJson, bool IsError = false) : ContentBlock;

public sealed record ThinkingBlock(string ThinkingText) : ContentBlock;
