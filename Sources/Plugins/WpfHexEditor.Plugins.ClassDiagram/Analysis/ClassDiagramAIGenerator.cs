// ==========================================================
// Project: WpfHexEditor.Plugins.ClassDiagram
// File: Analysis/ClassDiagramAIGenerator.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-04-07
// Description:
//     AI-assisted class diagram generation from natural language.
//     Sends a structured prompt to Claude API and parses the response
//     as a class diagram DSL via ClassDiagramParser.
//     Falls back to skeleton generation when no API key is configured.
//
// Architecture Notes:
//     Uses HttpClient to call Anthropic Messages API directly.
//     Reads the API key from AIAssistant plugin settings (DPAPI-protected)
//     located at %AppData%\WpfHexEditor\AIAssistant\settings.json.
//     Fires ProgressChanged during streaming (approximate token count).
// ==========================================================

using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using WpfHexEditor.Editor.ClassDiagram.Core.Layout;
using WpfHexEditor.Editor.ClassDiagram.Core.Model;
using WpfHexEditor.Editor.ClassDiagram.Core.Parser;
using WpfHexEditor.Plugins.ClassDiagram.Options;

namespace WpfHexEditor.Plugins.ClassDiagram.Analysis;

/// <summary>
/// Generates a <see cref="DiagramDocument"/> from a natural-language description
/// by calling the Claude API and parsing the response as class diagram DSL.
/// </summary>
public sealed class ClassDiagramAIGenerator : IDisposable
{
    private readonly HttpClient _http = new();
    private bool _disposed;

    // ── Prompt template ───────────────────────────────────────────────────────

    private const string SystemPrompt = """
        You are a UML class diagram generator.
        Given a natural language description, output ONLY a class diagram in this exact DSL format:

        class ClassName {
          +PublicMethod(): ReturnType
          -privateField: Type
          #protectedProp: Type
        }
        interface IInterfaceName { }
        enum EnumName { Value1, Value2 }
        ClassName --> AnotherClass
        ClassName implements IInterfaceName
        ClassName extends BaseClass

        Rules:
        - Use only the DSL above. No Markdown fences. No explanations.
        - Relationship arrows: --> (association), extends (inheritance), implements (realization)
        - Include realistic member names and types matching the domain
        - Generate 2-6 classes/interfaces for a typical design pattern
        """;

    // ── Public API ────────────────────────────────────────────────────────────

    public event EventHandler<string>? ProgressChanged;

    /// <summary>
    /// Generates a <see cref="DiagramDocument"/> from a natural-language description.
    /// If no API key is available, generates a skeleton diagram from the prompt keywords.
    /// </summary>
    public async Task<DiagramDocument> GenerateAsync(
        string               userPrompt,
        ClassDiagramOptions  options,
        CancellationToken    ct = default)
    {
        string? apiKey = ReadAnthropicApiKey();
        if (string.IsNullOrEmpty(apiKey))
        {
            ProgressChanged?.Invoke(this, "No API key found — generating skeleton diagram…");
            return GenerateSkeleton(userPrompt, options);
        }

        ProgressChanged?.Invoke(this, "Calling Claude API…");

        string dsl;
        try
        {
            dsl = await CallClaudeAsync(apiKey, userPrompt, ct);
        }
        catch (Exception ex)
        {
            ProgressChanged?.Invoke(this, $"API error: {ex.Message}. Generating skeleton…");
            return GenerateSkeleton(userPrompt, options);
        }

        ProgressChanged?.Invoke(this, "Parsing response…");

        var doc = ClassDiagramParser.Parse(dsl).Document;
        if (doc.Classes.Count == 0)
        {
            ProgressChanged?.Invoke(this, "Response parse produced no classes — generating skeleton…");
            return GenerateSkeleton(userPrompt, options);
        }

        // Apply layout
        LayoutStrategyFactory
            .Create(options.LayoutStrategy)
            .Layout(doc);

        ProgressChanged?.Invoke(this, $"Generated {doc.Classes.Count} classes from AI response.");
        return doc;
    }

    // ── Claude API ────────────────────────────────────────────────────────────

    private async Task<string> CallClaudeAsync(
        string apiKey, string prompt, CancellationToken ct)
    {
        var body = new JsonObject
        {
            ["model"]      = "claude-haiku-4-5-20251001",
            ["max_tokens"] = 1024,
            ["system"]     = SystemPrompt,
            ["messages"]   = new JsonArray
            {
                new JsonObject
                {
                    ["role"]    = "user",
                    ["content"] = prompt
                }
            }
        };

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "https://api.anthropic.com/v1/messages");

        request.Headers.Add("x-api-key",         apiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");
        request.Content = new StringContent(
            body.ToJsonString(), Encoding.UTF8, "application/json");

        using var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        string json = await response.Content.ReadAsStringAsync(ct);
        var node = JsonNode.Parse(json);

        return node?["content"]?[0]?["text"]?.GetValue<string>() ?? string.Empty;
    }

    // ── Skeleton fallback ─────────────────────────────────────────────────────

    private static DiagramDocument GenerateSkeleton(
        string prompt, ClassDiagramOptions options)
    {
        // Extract likely class names from the prompt (capitalize first letter).
        var words = prompt
            .Split([' ', ',', '.', '!', '?', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 3 && char.IsLetter(w[0]))
            .Select(w => char.ToUpper(w[0]) + w[1..].ToLower())
            .Distinct()
            .Take(4)
            .ToList();

        if (words.Count == 0) words = ["GeneratedClass"];

        var sb = new StringBuilder();
        foreach (var name in words)
        {
            sb.AppendLine($"class {name} {{");
            sb.AppendLine($"  +{char.ToLower(name[0]) + name[1..]}(): void");
            sb.AppendLine("}");
        }

        var doc = ClassDiagramParser.Parse(sb.ToString()).Document;
        LayoutStrategyFactory
            .Create(options.LayoutStrategy)
            .Layout(doc);
        return doc;
    }

    // ── API key reader ────────────────────────────────────────────────────────

    private static string? ReadAnthropicApiKey()
    {
        try
        {
            string settingsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "WpfHexEditor", "AIAssistant", "settings.json");

            if (!File.Exists(settingsPath)) return null;

            var json = JsonNode.Parse(File.ReadAllText(settingsPath));
            string? encrypted = json?["EncryptedAnthropicKey"]?.GetValue<string>();
            if (string.IsNullOrEmpty(encrypted)) return null;

            byte[] cipher  = Convert.FromBase64String(encrypted);
            byte[] plain   = ProtectedData.Unprotect(cipher, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plain);
        }
        catch
        {
            return null;
        }
    }

    // ── IDisposable ───────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _http.Dispose();
    }
}
