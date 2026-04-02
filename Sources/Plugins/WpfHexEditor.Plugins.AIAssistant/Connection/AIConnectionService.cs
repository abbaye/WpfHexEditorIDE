// ==========================================================
// Project: WpfHexEditor.Plugins.AIAssistant
// File: AIConnectionService.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.6
// Created: 2026-03-31
// License: GNU Affero General Public License v3.0 (AGPL-3.0)
// Description:
//     Background health-check, rate-limit backoff, offline detection. Drives titlebar badge.
// ==========================================================
using System.Diagnostics;
using System.Net.Http;
using WpfHexEditor.Plugins.AIAssistant.Options;
using WpfHexEditor.Plugins.AIAssistant.Providers.ClaudeCode;

namespace WpfHexEditor.Plugins.AIAssistant.Connection;

public sealed class AIConnectionService : IDisposable
{
    private readonly NetworkAvailabilityMonitor _networkMonitor = new();
    private readonly RateLimitHandler _rateLimiter = new();
    private CancellationTokenSource? _cts;
    private Task? _healthLoop;

    public AIConnectionStatus Status { get; private set; } = AIConnectionStatus.NotConfigured;
    public ConnectionInfo? CurrentConnection { get; private set; }
    public RateLimitHandler RateLimiter => _rateLimiter;

    public event EventHandler<AIConnectionStatus>? StatusChanged;

    public void Start()
    {
        _networkMonitor.AvailabilityChanged += OnNetworkChanged;
        _cts = new CancellationTokenSource();
        _healthLoop = RunHealthLoopAsync(_cts.Token);
    }

    public void Stop()
    {
        _cts?.Cancel();
        _networkMonitor.AvailabilityChanged -= OnNetworkChanged;
    }

    public void NotifyStreaming()
    {
        SetStatus(AIConnectionStatus.Connected);
    }

    public void NotifyRateLimit(TimeSpan? retryAfter = null)
    {
        _rateLimiter.RecordRateLimit(retryAfter);
        SetStatus(AIConnectionStatus.RateLimited);
    }

    public void NotifyError(string message)
    {
        CurrentConnection = new ConnectionInfo(
            AIAssistantOptions.Instance.DefaultProviderId,
            AIAssistantOptions.Instance.DefaultModelId,
            -1,
            AIConnectionStatus.Error,
            message);
        SetStatus(AIConnectionStatus.Error);
    }

    public void NotifySuccess(int latencyMs)
    {
        _rateLimiter.RecordSuccess();
        CurrentConnection = new ConnectionInfo(
            AIAssistantOptions.Instance.DefaultProviderId,
            AIAssistantOptions.Instance.DefaultModelId,
            latencyMs,
            AIConnectionStatus.Connected);
        SetStatus(AIConnectionStatus.Connected);
    }

    private void OnNetworkChanged(object? sender, bool isAvailable)
    {
        if (!isAvailable)
            SetStatus(AIConnectionStatus.Offline);
        else
            _ = TestConnectionAsync(CancellationToken.None);
    }

    private async Task RunHealthLoopAsync(CancellationToken ct)
    {
        // Initial check
        await TestConnectionAsync(ct);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(5), ct);
                await TestConnectionAsync(ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task TestConnectionAsync(CancellationToken ct)
    {
        var opts = AIAssistantOptions.Instance;

        // Determine effective provider: auto-fallback to claude-code if no API key
        var effectiveProvider = opts.DefaultProviderId;
        if (effectiveProvider != "ollama" && effectiveProvider != "claude-code"
            && string.IsNullOrEmpty(opts.GetApiKey(effectiveProvider))
            && ClaudeCodeModelProvider.FindClaudeExecutable() is not null)
        {
            effectiveProvider = "claude-code";
        }

        // Claude Code CLI: just check if executable exists
        if (effectiveProvider == "claude-code")
        {
            if (ClaudeCodeModelProvider.FindClaudeExecutable() is not null)
            {
                SetStatus(AIConnectionStatus.CliConnected);
                return;
            }
            SetStatus(AIConnectionStatus.NotConfigured);
            return;
        }

        var key = opts.GetApiKey(effectiveProvider);
        if (effectiveProvider != "ollama" && string.IsNullOrEmpty(key))
        {
            SetStatus(AIConnectionStatus.NotConfigured);
            return;
        }

        if (!_networkMonitor.IsAvailable)
        {
            SetStatus(AIConnectionStatus.Offline);
            return;
        }

        SetStatus(AIConnectionStatus.Connecting);

        var sw = Stopwatch.StartNew();
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var testUrl = effectiveProvider switch
            {
                "anthropic" => "https://api.anthropic.com/v1/messages",
                "openai" => "https://api.openai.com/v1/models",
                "gemini" => "https://generativelanguage.googleapis.com/v1beta/models",
                "ollama" => $"{opts.OllamaBaseUrl}/api/tags",
                _ => null
            };

            if (testUrl is null)
            {
                SetStatus(AIConnectionStatus.NotConfigured);
                return;
            }

            using var req = new HttpRequestMessage(HttpMethod.Head, testUrl);
            await http.SendAsync(req, ct);
            sw.Stop();

            NotifySuccess((int)sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            NotifyError(ex.Message);
        }
    }

    private void SetStatus(AIConnectionStatus newStatus)
    {
        if (Status == newStatus) return;
        Status = newStatus;
        StatusChanged?.Invoke(this, newStatus);
    }

    public void Dispose()
    {
        Stop();
        _cts?.Dispose();
        _networkMonitor.Dispose();
    }
}
