// ==========================================================
// Project: WpfHexEditor.App
// File: Analysis/Services/AnalysisAiInsightService.cs
// Description: Builds a compact prompt from the analysis report and sends it
//              to a configured LLM provider (OpenAI-compatible HTTP API).
//              Returns markdown insights or null on failure / no key.
// Architecture Notes:
//     Network call. Uses HttpClient with 30s timeout. Prompt is bounded
//     (top 10 worst files, score breakdown, top issue categories) to keep
//     cost predictable (~5K tokens / run).
// ==========================================================

using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using WpfHexEditor.App.Analysis.Models;

namespace WpfHexEditor.App.Analysis.Services;

internal sealed class AnalysisAiInsightService
{
    private readonly HttpClient _http;

    internal AnalysisAiInsightService(HttpClient? http = null)
    {
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    public sealed record InsightResult(string Markdown, bool Success, string? Error);

    internal async Task<InsightResult> GenerateAsync(
        CodeAnalysisReport report, AiInsightConfig config, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(config.ApiKey))
            return new InsightResult("", false, "API key not configured.");

        string prompt = BuildPrompt(report);

        try
        {
            var body = new JsonObject
            {
                ["model"] = config.Model,
                ["messages"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["role"]    = "system",
                        ["content"] = "You are a senior software architect reviewing a code analysis report. "
                                    + "Identify the top 5 most actionable improvements. Be concise, technical, opinionated. "
                                    + "Output GitHub-flavored markdown.",
                    },
                    new JsonObject { ["role"] = "user", ["content"] = prompt },
                },
                ["max_tokens"]  = 1500,
                ["temperature"] = 0.2,
            };

            using var req = new HttpRequestMessage(HttpMethod.Post, config.Endpoint)
            {
                Content = JsonContent.Create(body),
            };
            req.Headers.Add("Authorization", $"Bearer {config.ApiKey}");

            var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
                return new InsightResult("", false, $"HTTP {(int)resp.StatusCode}");

            var json    = await resp.Content.ReadFromJsonAsync<JsonNode>(ct).ConfigureAwait(false);
            var content = json?["choices"]?[0]?["message"]?["content"]?.GetValue<string>() ?? "";
            return new InsightResult(content, true, null);
        }
        catch (Exception ex)
        {
            return new InsightResult("", false, ex.Message);
        }
    }

    private static string BuildPrompt(CodeAnalysisReport report)
    {
        var sw = new StringWriter();
        sw.WriteLine("# Code Analysis Report");
        sw.WriteLine($"Score: {report.Score.Score}/100 ({report.Score.Grade})");
        sw.WriteLine($"Files: {report.TotalFiles}, LOC: {report.TotalLines}");
        sw.WriteLine($"Sub-scores: V={report.Score.VolumeScore} CC={report.Score.ComplexityScore} "
                   + $"Coup={report.Score.CouplingScore} Dup={report.Score.DuplicationScore} "
                   + $"Dead={report.Score.DeadCodeScore} Conv={report.Score.ConventionScore}");
        sw.WriteLine();

        sw.WriteLine("## Top 10 worst files");
        foreach (var f in report.Score.WorstFiles.Take(10))
            sw.WriteLine($"- `{f.FileName}` — score {f.Score}, LOC {f.TotalLines}, MaxCC {f.MaxCyclomaticComplexity}, MI {f.MaintainabilityIndex:F0}");
        sw.WriteLine();

        sw.WriteLine("## Issue categories");
        foreach (var grp in report.Diagnostics.GroupBy(d => d.Id).OrderByDescending(g => g.Count()).Take(10))
            sw.WriteLine($"- {grp.Key} — {grp.Count()} occurrences");

        sw.WriteLine();
        sw.WriteLine("Identify the 5 most impactful refactorings (file-level), explain why, and propose concrete first steps.");
        return sw.ToString();
    }
}

public sealed class AiInsightConfig
{
    public string Endpoint { get; set; } = "https://api.openai.com/v1/chat/completions";
    public string Model    { get; set; } = "gpt-4o-mini";
    public string ApiKey   { get; set; } = string.Empty;
}
