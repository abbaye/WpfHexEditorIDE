// ==========================================================
// Project: WpfHexEditor.Plugins.ClaudeAssistant
// File: OllamaModelProvider.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.6
// Created: 2026-03-31
// License: GNU Affero General Public License v3.0 (AGPL-3.0)
// Description:
//     Ollama local provider. OpenAI-compatible API, auto model discovery.
// ==========================================================
using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using WpfHexEditor.Plugins.ClaudeAssistant.Api;
using WpfHexEditor.Plugins.ClaudeAssistant.Options;

namespace WpfHexEditor.Plugins.ClaudeAssistant.Providers.Ollama;

public sealed class OllamaModelProvider : IModelProvider
{
    public string ProviderId => "ollama";
    public string DisplayName => "Ollama (Local)";
    public bool SupportsTools => true;
    public bool SupportsVision => true;
    public bool SupportsThinking => false;
    public int MaxContextTokens => 128_000;

    private string[] _discoveredModels = ["llama3.2", "mistral", "phi-4"];
    public string[] AvailableModels => _discoveredModels;

    private string BaseUrl => ClaudeAssistantOptions.Instance.OllamaBaseUrl.TrimEnd('/');

    public async Task<bool> TestConnectionAsync(CancellationToken ct = default)
    {
        try
        {
            await DiscoverModelsAsync(ct);
            return _discoveredModels.Length > 0;
        }
        catch
        {
            return false;
        }
    }

    public async Task DiscoverModelsAsync(CancellationToken ct = default)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        var resp = await http.GetStringAsync($"{BaseUrl}/api/tags", ct);
        using var doc = JsonDocument.Parse(resp);

        if (doc.RootElement.TryGetProperty("models", out var models))
        {
            _discoveredModels = models.EnumerateArray()
                .Select(m => m.GetProperty("name").GetString()!)
                .Where(n => !string.IsNullOrEmpty(n))
                .ToArray();
        }
    }

    public async IAsyncEnumerable<ChatStreamChunk> StreamAsync(
        IReadOnlyList<ChatMessage> messages,
        string modelId,
        IReadOnlyList<ToolDefinition>? tools = null,
        ThinkingConfig? thinking = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        // Ollama supports OpenAI-compatible endpoint
        var url = $"{BaseUrl}/v1/chat/completions";

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

        var json = JsonSerializer.Serialize(body);

        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        using var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        HttpResponseMessage? resp = null;
        string? connectError = null;
        try
        {
            resp = await http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        }
        catch (Exception ex)
        {
            connectError = $"Ollama connection failed: {ex.Message}";
        }

        if (connectError is not null)
        {
            yield return new ChatStreamChunk(ChunkKind.Error, ErrorMessage: connectError);
            yield break;
        }

        if (!resp.IsSuccessStatusCode)
        {
            yield return new ChatStreamChunk(ChunkKind.Error,
                ErrorMessage: $"Ollama HTTP {(int)resp.StatusCode}");
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

            ChatStreamChunk? parsed = null;
            try
            {
                using var doc = JsonDocument.Parse(data);
                var choices = doc.RootElement.GetProperty("choices");
                if (choices.GetArrayLength() > 0)
                {
                    var delta = choices[0].GetProperty("delta");
                    if (delta.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.String)
                        parsed = new ChatStreamChunk(ChunkKind.TextDelta, Text: content.GetString());
                }
            }
            catch { /* skip malformed */ }

            if (parsed is not null)
                yield return parsed;
        }

        yield return new ChatStreamChunk(ChunkKind.Done, IsFinal: true);
    }
}
