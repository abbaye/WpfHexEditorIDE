// ==========================================================
// Project: WpfHexEditor.Plugins.AIAssistant
// File: UsageTracker.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.6
// Created: 2026-04-02
// License: GNU Affero General Public License v3.0 (AGPL-3.0)
// Description:
//     Captures and caches rate-limit headers from HTTP providers.
//     Provides ProviderUsageInfo for the Account & Usage popup.
// ==========================================================
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using WpfHexEditor.Plugins.AIAssistant.Providers.ClaudeCode;

namespace WpfHexEditor.Plugins.AIAssistant.Connection;

public sealed class UsageTracker
{
    public static UsageTracker Instance { get; } = new();

    private readonly ConcurrentDictionary<string, CapturedRateLimits> _captured = new();

    private UsageTracker() { }

    /// <summary>
    /// Fire-and-forget capture of rate-limit headers from an HTTP response.
    /// Safe to call from any thread; never throws.
    /// </summary>
    public void RecordRateLimitHeaders(string providerId, HttpResponseHeaders headers)
    {
        try
        {
            var limits = new CapturedRateLimits { CapturedAt = DateTime.UtcNow };

            if (providerId == "anthropic")
                ParseAnthropicHeaders(headers, limits);
            else if (providerId is "openai" or "azure-openai")
                ParseOpenAIHeaders(headers, limits);

            if (limits.Buckets.Count > 0)
                _captured[providerId] = limits;
        }
        catch { /* never throw from header capture */ }
    }

    /// <summary>Builds a ProviderUsageInfo for the given provider.</summary>
    public async Task<ProviderUsageInfo> GetUsageAsync(string providerId, CancellationToken ct = default)
    {
        var info = providerId switch
        {
            "anthropic" => BuildHttpProviderInfo("anthropic", "Anthropic Claude", "API Key",
                "https://console.anthropic.com/settings/billing"),
            "openai" => BuildHttpProviderInfo("openai", "OpenAI", "API Key",
                "https://platform.openai.com/usage"),
            "azure-openai" => BuildHttpProviderInfo("azure-openai", "Azure OpenAI", "API Key",
                "https://portal.azure.com"),
            "gemini" => new ProviderUsageInfo
            {
                ProviderId = "gemini", DisplayName = "Google Gemini",
                AuthMethod = "API Key",
                ManageUrl = "https://aistudio.google.com",
                LastRefreshed = DateTime.UtcNow
            },
            "ollama" => new ProviderUsageInfo
            {
                ProviderId = "ollama", DisplayName = "Ollama (Local)",
                AuthMethod = "Local — no authentication",
                LastRefreshed = DateTime.UtcNow
            },
            "claude-code" => await BuildClaudeCodeInfoAsync(ct),
            _ => new ProviderUsageInfo
            {
                ProviderId = providerId, DisplayName = providerId,
                LastRefreshed = DateTime.UtcNow
            }
        };

        return info;
    }

    private ProviderUsageInfo BuildHttpProviderInfo(string providerId, string displayName,
        string authMethod, string manageUrl)
    {
        var info = new ProviderUsageInfo
        {
            ProviderId = providerId,
            DisplayName = displayName,
            AuthMethod = authMethod,
            ManageUrl = manageUrl,
            LastRefreshed = DateTime.UtcNow
        };

        if (_captured.TryGetValue(providerId, out var limits))
        {
            info.Buckets = limits.Buckets;
            info.LastRefreshed = limits.CapturedAt;
        }

        return info;
    }

    private static async Task<ProviderUsageInfo> BuildClaudeCodeInfoAsync(CancellationToken ct)
    {
        var info = new ProviderUsageInfo
        {
            ProviderId = "claude-code",
            DisplayName = "Claude Code (CLI)",
            AuthMethod = "CLI Subscription (claude.ai)",
            ManageUrl = "https://claude.ai/settings",
            LastRefreshed = DateTime.UtcNow
        };

        var exe = ClaudeCodeModelProvider.FindClaudeExecutable();
        if (exe is null) return info;

        // Attempt to get account info via `claude profile --output-format json`
        try
        {
            var psi = new ProcessStartInfo(exe, "profile --output-format json")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = Process.Start(psi);
            if (proc is null) return info;

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(5));

            var output = await proc.StandardOutput.ReadToEndAsync(timeoutCts.Token);
            await proc.WaitForExitAsync(timeoutCts.Token);

            if (proc.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
            {
                using var doc = JsonDocument.Parse(output);
                var root = doc.RootElement;

                if (root.TryGetProperty("email", out var email))
                    info.Email = email.GetString();
                if (root.TryGetProperty("plan", out var plan))
                    info.Plan = plan.GetString();
                if (root.TryGetProperty("auth_method", out var auth))
                    info.AuthMethod = auth.GetString();
            }
        }
        catch { /* best-effort — CLI may not support profile command */ }

        return info;
    }

    private static void ParseAnthropicHeaders(HttpResponseHeaders headers, CapturedRateLimits limits)
    {
        // Requests bucket
        var reqLimit = GetHeaderLong(headers, "anthropic-ratelimit-requests-limit");
        var reqRemaining = GetHeaderLong(headers, "anthropic-ratelimit-requests-remaining");
        var reqReset = GetHeaderString(headers, "anthropic-ratelimit-requests-reset");

        if (reqLimit > 0)
        {
            var used = reqLimit.Value - (reqRemaining ?? 0);
            limits.Buckets.Add(new UsageBucket
            {
                Label = "Requests",
                UsedPercent = (double)used / reqLimit.Value * 100.0,
                ResetsIn = FormatResetTime(reqReset),
                DetailText = $"{used}/{reqLimit} requests"
            });
        }

        // Tokens bucket
        var tokLimit = GetHeaderLong(headers, "anthropic-ratelimit-tokens-limit");
        var tokRemaining = GetHeaderLong(headers, "anthropic-ratelimit-tokens-remaining");
        var tokReset = GetHeaderString(headers, "anthropic-ratelimit-tokens-reset");

        if (tokLimit > 0)
        {
            var used = tokLimit.Value - (tokRemaining ?? 0);
            limits.Buckets.Add(new UsageBucket
            {
                Label = "Tokens",
                UsedPercent = (double)used / tokLimit.Value * 100.0,
                ResetsIn = FormatResetTime(tokReset),
                DetailText = $"{FormatNumber(used)}/{FormatNumber(tokLimit.Value)} tokens"
            });
        }
    }

    private static void ParseOpenAIHeaders(HttpResponseHeaders headers, CapturedRateLimits limits)
    {
        // Requests bucket
        var reqLimit = GetHeaderLong(headers, "x-ratelimit-limit-requests");
        var reqRemaining = GetHeaderLong(headers, "x-ratelimit-remaining-requests");
        var reqReset = GetHeaderString(headers, "x-ratelimit-reset-requests");

        if (reqLimit > 0)
        {
            var used = reqLimit.Value - (reqRemaining ?? 0);
            limits.Buckets.Add(new UsageBucket
            {
                Label = "Requests",
                UsedPercent = (double)used / reqLimit.Value * 100.0,
                ResetsIn = FormatResetTime(reqReset),
                DetailText = $"{used}/{reqLimit} requests"
            });
        }

        // Tokens bucket
        var tokLimit = GetHeaderLong(headers, "x-ratelimit-limit-tokens");
        var tokRemaining = GetHeaderLong(headers, "x-ratelimit-remaining-tokens");
        var tokReset = GetHeaderString(headers, "x-ratelimit-reset-tokens");

        if (tokLimit > 0)
        {
            var used = tokLimit.Value - (tokRemaining ?? 0);
            limits.Buckets.Add(new UsageBucket
            {
                Label = "Tokens",
                UsedPercent = (double)used / tokLimit.Value * 100.0,
                ResetsIn = FormatResetTime(tokReset),
                DetailText = $"{FormatNumber(used)}/{FormatNumber(tokLimit.Value)} tokens"
            });
        }
    }

    private static long? GetHeaderLong(HttpResponseHeaders headers, string name)
    {
        if (headers.TryGetValues(name, out var values))
        {
            var val = values.FirstOrDefault();
            if (long.TryParse(val, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n))
                return n;
        }
        return null;
    }

    private static string? GetHeaderString(HttpResponseHeaders headers, string name)
    {
        if (headers.TryGetValues(name, out var values))
            return values.FirstOrDefault();
        return null;
    }

    private static string? FormatResetTime(string? resetValue)
    {
        if (string.IsNullOrEmpty(resetValue)) return null;

        // Try ISO 8601 date
        if (DateTime.TryParse(resetValue, CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind, out var resetDate))
        {
            var remaining = resetDate - DateTime.UtcNow;
            return remaining.TotalSeconds <= 0 ? "Resets now" : $"Resets in {FormatDuration(remaining)}";
        }

        // OpenAI uses duration strings like "6m0s", "1h30m"
        if (resetValue.Contains('s') || resetValue.Contains('m') || resetValue.Contains('h'))
            return $"Resets in {resetValue}";

        return null;
    }

    private static string FormatDuration(TimeSpan ts)
    {
        if (ts.TotalDays >= 1) return $"{(int)ts.TotalDays}d {ts.Hours}h";
        if (ts.TotalHours >= 1) return $"{(int)ts.TotalHours}h {ts.Minutes}m";
        if (ts.TotalMinutes >= 1) return $"{(int)ts.TotalMinutes}m";
        return $"{(int)ts.TotalSeconds}s";
    }

    private static string FormatNumber(long n) =>
        n >= 1_000_000 ? $"{n / 1_000_000.0:F1}M" :
        n >= 1_000 ? $"{n / 1_000.0:F1}K" :
        n.ToString(CultureInfo.InvariantCulture);

    private sealed class CapturedRateLimits
    {
        public DateTime CapturedAt { get; init; }
        public List<UsageBucket> Buckets { get; } = [];
    }
}
