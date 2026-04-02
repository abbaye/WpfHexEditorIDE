// ==========================================================
// Project: WpfHexEditor.Plugins.AIAssistant
// File: GeminiModelProvider.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.6
// Created: 2026-03-31
// License: GNU Affero General Public License v3.0 (AGPL-3.0)
// Description:
//     Google Gemini provider. HTTP direct streaming, 1M context.
// ==========================================================
using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using WpfHexEditor.Plugins.AIAssistant.Api;
using WpfHexEditor.Plugins.AIAssistant.Options;

namespace WpfHexEditor.Plugins.AIAssistant.Providers.Google;

public sealed class GeminiModelProvider : IModelProvider
{
    public string ProviderId => "gemini";
    public string DisplayName => "Google Gemini";
    public string[] AvailableModels => ["gemini-2.5-pro", "gemini-2.0-flash"];
    public bool SupportsTools => true;
    public bool SupportsVision => true;
    public bool SupportsThinking => false;
    public int MaxContextTokens => 1_000_000;

    private static readonly HttpClient s_http = new() { Timeout = TimeSpan.FromMinutes(5) };

    public async Task<bool> TestConnectionAsync(CancellationToken ct = default)
    {
        var key = AIAssistantOptions.Instance.GetApiKey("gemini");
        if (string.IsNullOrEmpty(key)) return false;

        var url = $"https://generativelanguage.googleapis.com/v1beta/models?key={key}";
        using var resp = await s_http.GetAsync(url, ct);
        return resp.IsSuccessStatusCode;
    }

    public async IAsyncEnumerable<ChatStreamChunk> StreamAsync(
        IReadOnlyList<ChatMessage> messages,
        string modelId,
        IReadOnlyList<ToolDefinition>? tools = null,
        ThinkingConfig? thinking = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var key = AIAssistantOptions.Instance.GetApiKey("gemini");
        if (string.IsNullOrEmpty(key))
        {
            yield return new ChatStreamChunk(ChunkKind.Error, ErrorMessage: "Gemini API key not configured");
            yield break;
        }

        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{modelId}:streamGenerateContent?alt=sse&key={key}";
        var contents = messages
            .Where(m => m.Role != "system")
            .Select(m => new { role = m.Role == "assistant" ? "model" : "user", parts = new[] { new { text = m.GetTextContent() } } })
            .ToList();

        var body = new { contents };
        var json = JsonSerializer.Serialize(body);

        using var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        using var resp = await s_http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!resp.IsSuccessStatusCode)
        {
            yield return new ChatStreamChunk(ChunkKind.Error,
                ErrorMessage: $"HTTP {(int)resp.StatusCode}: {await resp.Content.ReadAsStringAsync(ct)}");
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

            ChatStreamChunk? parsed = null;
            try
            {
                using var doc = JsonDocument.Parse(data);
                var candidates = doc.RootElement.GetProperty("candidates");
                if (candidates.GetArrayLength() > 0)
                {
                    var parts = candidates[0].GetProperty("content").GetProperty("parts");
                    if (parts.GetArrayLength() > 0 && parts[0].TryGetProperty("text", out var text))
                        parsed = new ChatStreamChunk(ChunkKind.TextDelta, Text: text.GetString());
                }
            }
            catch { /* skip malformed chunks */ }

            if (parsed is not null)
                yield return parsed;
        }

        yield return new ChatStreamChunk(ChunkKind.Done, IsFinal: true);
    }
}
