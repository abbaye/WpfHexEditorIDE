// ==========================================================
// Project: WpfHexEditor.Plugins.ClaudeAssistant
// File: OpenAIModelProvider.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.6
// Created: 2026-03-31
// License: GNU Affero General Public License v3.0 (AGPL-3.0)
// Description:
//     OpenAI provider. SSE streaming with function_calling normalized to tool_use.
// ==========================================================
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using WpfHexEditor.Plugins.ClaudeAssistant.Api;
using WpfHexEditor.Plugins.ClaudeAssistant.Options;

namespace WpfHexEditor.Plugins.ClaudeAssistant.Providers.OpenAI;

public sealed class OpenAIModelProvider : IModelProvider
{
    public string ProviderId => "openai";
    public string DisplayName => "OpenAI";
    public string[] AvailableModels => ["gpt-4o", "gpt-4o-mini", "o3", "o4-mini"];
    public bool SupportsTools => true;
    public bool SupportsVision => true;
    public bool SupportsThinking => false;
    public int MaxContextTokens => 128_000;

    private string ApiUrl => "https://api.openai.com/v1/chat/completions";

    private string? GetApiKey() => ClaudeAssistantOptions.Instance.GetApiKey("openai");

    public async Task<bool> TestConnectionAsync(CancellationToken ct = default)
    {
        var key = GetApiKey();
        if (string.IsNullOrEmpty(key)) return false;

        using var http = CreateClient(key);
        using var req = new HttpRequestMessage(HttpMethod.Get, "https://api.openai.com/v1/models");
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
        var key = GetApiKey();
        if (string.IsNullOrEmpty(key))
        {
            yield return new ChatStreamChunk(ChunkKind.Error, ErrorMessage: "OpenAI API key not configured");
            yield break;
        }

        using var http = CreateClient(key);

        var body = BuildRequestBody(messages, modelId, tools);
        var json = JsonSerializer.Serialize(body, s_jsonOptions);

        using var req = new HttpRequestMessage(HttpMethod.Post, ApiUrl)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        using var resp = await http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);

        if (!resp.IsSuccessStatusCode)
        {
            var errorBody = await resp.Content.ReadAsStringAsync(ct);
            yield return new ChatStreamChunk(ChunkKind.Error, ErrorMessage: $"HTTP {(int)resp.StatusCode}: {errorBody}");
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

            var chunk = ParseChunk(data);
            if (chunk is not null)
                yield return chunk;
        }

        yield return new ChatStreamChunk(ChunkKind.Done, IsFinal: true);
    }

    private static object BuildRequestBody(
        IReadOnlyList<ChatMessage> messages,
        string modelId,
        IReadOnlyList<ToolDefinition>? tools)
    {
        var apiMessages = messages.Select(m => new
        {
            role = m.Role,
            content = m.GetTextContent()
        }).ToList();

        var body = new Dictionary<string, object>
        {
            ["model"] = modelId,
            ["stream"] = true,
            ["messages"] = apiMessages
        };

        if (tools is { Count: > 0 })
        {
            body["tools"] = tools.Select(t => new
            {
                type = "function",
                function = new
                {
                    name = t.Name,
                    description = t.Description,
                    parameters = JsonSerializer.Deserialize<JsonElement>(t.InputSchemaJson)
                }
            }).ToList();
        }

        return body;
    }

    private static ChatStreamChunk? ParseChunk(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var choices = doc.RootElement.GetProperty("choices");
            if (choices.GetArrayLength() == 0) return null;

            var delta = choices[0].GetProperty("delta");
            if (delta.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.String)
                return new ChatStreamChunk(ChunkKind.TextDelta, Text: content.GetString());

            if (delta.TryGetProperty("tool_calls", out var toolCalls) && toolCalls.GetArrayLength() > 0)
            {
                var tc = toolCalls[0];
                if (tc.TryGetProperty("function", out var fn))
                {
                    if (fn.TryGetProperty("name", out var name))
                        return new ChatStreamChunk(ChunkKind.ToolUseStart,
                            ToolCallId: tc.GetProperty("id").GetString(),
                            ToolName: name.GetString());
                    if (fn.TryGetProperty("arguments", out var args))
                        return new ChatStreamChunk(ChunkKind.ToolInputDelta, ToolInputJson: args.GetString());
                }
            }

            return null;
        }
        catch { return null; }
    }

    private static HttpClient CreateClient(string apiKey)
    {
        var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        return http;
    }

    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };
}
