// ==========================================================
// Project: WpfHexEditor.Plugins.ClaudeAssistant
// File: IModelProvider.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.6
// Created: 2026-03-31
// License: GNU Affero General Public License v3.0 (AGPL-3.0)
// Description:
//     Abstraction for AI model backends (Anthropic, OpenAI, Gemini, Ollama).
// ==========================================================
namespace WpfHexEditor.Plugins.ClaudeAssistant.Api;

public interface IModelProvider
{
    string ProviderId { get; }
    string DisplayName { get; }
    string[] AvailableModels { get; }
    bool SupportsTools { get; }
    bool SupportsVision { get; }
    bool SupportsThinking { get; }
    int MaxContextTokens { get; }

    Task<bool> TestConnectionAsync(CancellationToken ct = default);

    IAsyncEnumerable<ChatStreamChunk> StreamAsync(
        IReadOnlyList<ChatMessage> messages,
        string modelId,
        IReadOnlyList<ToolDefinition>? tools = null,
        ThinkingConfig? thinking = null,
        CancellationToken ct = default);
}

public sealed record ToolDefinition(string Name, string Description, string InputSchemaJson);
