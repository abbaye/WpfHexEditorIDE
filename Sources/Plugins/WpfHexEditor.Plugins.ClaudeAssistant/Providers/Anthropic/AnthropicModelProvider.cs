// ==========================================================
// Project: WpfHexEditor.Plugins.ClaudeAssistant
// File: AnthropicModelProvider.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.6
// Created: 2026-03-31
// License: GNU Affero General Public License v3.0 (AGPL-3.0)
// Description:
//     Anthropic Claude provider. HTTP SSE streaming with tool_use and thinking.
// ==========================================================
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using WpfHexEditor.Plugins.ClaudeAssistant.Api;
using WpfHexEditor.Plugins.ClaudeAssistant.Options;

namespace WpfHexEditor.Plugins.ClaudeAssistant.Providers.Anthropic;

public sealed class AnthropicModelProvider : IModelProvider
{
    public string ProviderId => "anthropic";
    public string DisplayName => "Anthropic Claude";
    public string[] AvailableModels => ["claude-opus-4-6", "claude-sonnet-4-6", "claude-haiku-4-5"];
    public bool SupportsTools => true;
    public bool SupportsVision => true;
    public bool SupportsThinking => true;
    public int MaxContextTokens => 200_000;

    private const string ApiUrl = "https://api.anthropic.com/v1/messages";
    private const string ApiVersion = "2023-06-01";

    public async Task<bool> TestConnectionAsync(CancellationToken ct = default)
    {
        var key = ClaudeAssistantOptions.Instance.GetApiKey("anthropic");
        if (string.IsNullOrEmpty(key)) return false;

        using var http = CreateClient(key);
        var body = JsonSerializer.Serialize(new
        {
            model = "claude-haiku-4-5",
            max_tokens = 1,
            messages = new[] { new { role = "user", content = "hi" } }
        });

        using var req = new HttpRequestMessage(HttpMethod.Post, ApiUrl)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        var resp = await http.SendAsync(req, ct);
        return resp.IsSuccessStatusCode;
    }

    public async IAsyncEnumerable<ChatStreamChunk> StreamAsync(
        IReadOnlyList<ChatMessage> messages,
        string modelId,
        IReadOnlyList<ToolDefinition>? tools = null,
        ThinkingConfig? thinking = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var key = ClaudeAssistantOptions.Instance.GetApiKey("anthropic");
        if (string.IsNullOrEmpty(key))
        {
            yield return new ChatStreamChunk(ChunkKind.Error, ErrorMessage: "Anthropic API key not configured");
            yield break;
        }

        using var http = CreateClient(key);

        // Build request body
        var requestObj = BuildRequestBody(messages, modelId, tools, thinking);
        var json = JsonSerializer.Serialize(requestObj, s_jsonOptions);

        using var req = new HttpRequestMessage(HttpMethod.Post, ApiUrl)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        using var resp = await http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);

        if (!resp.IsSuccessStatusCode)
        {
            var errorBody = await resp.Content.ReadAsStringAsync(ct);
            yield return new ChatStreamChunk(ChunkKind.Error,
                ErrorMessage: $"HTTP {(int)resp.StatusCode}: {errorBody}");
            yield break;
        }

        using var stream = await resp.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream && !ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (line is null) break;
            if (!line.StartsWith("data: ")) continue;

            var data = line[6..];
            if (data == "[DONE]") break;

            var chunk = ParseSseEvent(data);
            if (chunk is not null)
                yield return chunk;
        }

        yield return new ChatStreamChunk(ChunkKind.Done, IsFinal: true);
    }

    private static object BuildRequestBody(
        IReadOnlyList<ChatMessage> messages,
        string modelId,
        IReadOnlyList<ToolDefinition>? tools,
        ThinkingConfig? thinking)
    {
        var apiMessages = new List<object>();
        string? systemPrompt = null;

        foreach (var msg in messages)
        {
            if (msg.Role == "system")
            {
                systemPrompt = msg.GetTextContent();
                continue;
            }

            var contentBlocks = new List<object>();
            foreach (var block in msg.Content)
            {
                switch (block)
                {
                    case TextBlock tb:
                        contentBlocks.Add(new { type = "text", text = tb.Text });
                        break;
                    case ImageBlock ib:
                        contentBlocks.Add(new
                        {
                            type = "image",
                            source = new { type = "base64", media_type = ib.MediaType, data = ib.Base64Data }
                        });
                        break;
                    case ToolResultBlock tr:
                        contentBlocks.Add(new
                        {
                            type = "tool_result",
                            tool_use_id = tr.ToolCallId,
                            content = tr.ResultJson,
                            is_error = tr.IsError
                        });
                        break;
                }
            }

            apiMessages.Add(new { role = msg.Role, content = contentBlocks });
        }

        var body = new Dictionary<string, object>
        {
            ["model"] = modelId,
            ["max_tokens"] = 8192,
            ["stream"] = true,
            ["messages"] = apiMessages
        };

        if (systemPrompt is not null)
            body["system"] = systemPrompt;

        if (tools is { Count: > 0 })
        {
            body["tools"] = tools.Select(t => new
            {
                name = t.Name,
                description = t.Description,
                input_schema = JsonSerializer.Deserialize<JsonElement>(t.InputSchemaJson)
            }).ToList();
        }

        if (thinking is { Enabled: true })
        {
            body["thinking"] = new { type = "enabled", budget_tokens = thinking.BudgetTokens };
        }

        return body;
    }

    private static ChatStreamChunk? ParseSseEvent(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var type = root.GetProperty("type").GetString();

            return type switch
            {
                "content_block_delta" => ParseDelta(root),
                "content_block_start" => ParseBlockStart(root),
                "message_stop" => new ChatStreamChunk(ChunkKind.Done, IsFinal: true),
                "error" => new ChatStreamChunk(ChunkKind.Error,
                    ErrorMessage: root.GetProperty("error").GetProperty("message").GetString()),
                _ => null
            };
        }
        catch
        {
            return null;
        }
    }

    private static ChatStreamChunk? ParseDelta(JsonElement root)
    {
        var delta = root.GetProperty("delta");
        var deltaType = delta.GetProperty("type").GetString();

        return deltaType switch
        {
            "text_delta" => new ChatStreamChunk(ChunkKind.TextDelta, Text: delta.GetProperty("text").GetString()),
            "input_json_delta" => new ChatStreamChunk(ChunkKind.ToolInputDelta,
                ToolInputJson: delta.GetProperty("partial_json").GetString()),
            "thinking_delta" => new ChatStreamChunk(ChunkKind.ThinkingDelta,
                ThinkingText: delta.GetProperty("thinking").GetString()),
            _ => null
        };
    }

    private static ChatStreamChunk? ParseBlockStart(JsonElement root)
    {
        var block = root.GetProperty("content_block");
        var blockType = block.GetProperty("type").GetString();

        if (blockType == "tool_use")
        {
            return new ChatStreamChunk(ChunkKind.ToolUseStart,
                ToolCallId: block.GetProperty("id").GetString(),
                ToolName: block.GetProperty("name").GetString());
        }

        return null;
    }

    private static HttpClient CreateClient(string apiKey)
    {
        var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        http.DefaultRequestHeaders.Add("x-api-key", apiKey);
        http.DefaultRequestHeaders.Add("anthropic-version", ApiVersion);
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        return http;
    }

    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };
}
