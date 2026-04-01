// ==========================================================
// Project: WpfHexEditor.Plugins.ClaudeAssistant
// File: ClaudeConnectionService.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.6
// Created: 2026-03-31
// License: GNU Affero General Public License v3.0 (AGPL-3.0)
// Description:
//     Background health-check, rate-limit backoff, offline detection. Drives titlebar badge.
// ==========================================================
using System.Diagnostics;
using System.Net.Http;
using WpfHexEditor.Plugins.ClaudeAssistant.Options;

namespace WpfHexEditor.Plugins.ClaudeAssistant.Connection;

public sealed class ClaudeConnectionService : IDisposable
{
    private readonly NetworkAvailabilityMonitor _networkMonitor = new();
    private readonly RateLimitHandler _rateLimiter = new();
    private CancellationTokenSource? _cts;
    private Task? _healthLoop;

    public ClaudeConnectionStatus Status { get; private set; } = ClaudeConnectionStatus.NotConfigured;
    public ConnectionInfo? CurrentConnection { get; private set; }
    public RateLimitHandler RateLimiter => _rateLimiter;

    public event EventHandler<ClaudeConnectionStatus>? StatusChanged;

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
        SetStatus(ClaudeConnectionStatus.Connected);
    }

    public void NotifyRateLimit(TimeSpan? retryAfter = null)
    {
        _rateLimiter.RecordRateLimit(retryAfter);
        SetStatus(ClaudeConnectionStatus.RateLimited);
    }

    public void NotifyError(string message)
    {
        CurrentConnection = new ConnectionInfo(
            ClaudeAssistantOptions.Instance.DefaultProviderId,
            ClaudeAssistantOptions.Instance.DefaultModelId,
            -1,
            ClaudeConnectionStatus.Error,
            message);
        SetStatus(ClaudeConnectionStatus.Error);
    }

    public void NotifySuccess(int latencyMs)
    {
        _rateLimiter.RecordSuccess();
        CurrentConnection = new ConnectionInfo(
            ClaudeAssistantOptions.Instance.DefaultProviderId,
            ClaudeAssistantOptions.Instance.DefaultModelId,
            latencyMs,
            ClaudeConnectionStatus.Connected);
        SetStatus(ClaudeConnectionStatus.Connected);
    }

    private void OnNetworkChanged(object? sender, bool isAvailable)
    {
        if (!isAvailable)
            SetStatus(ClaudeConnectionStatus.Offline);
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
        var opts = ClaudeAssistantOptions.Instance;
        var key = opts.GetApiKey(opts.DefaultProviderId);

        if (opts.DefaultProviderId != "ollama" && string.IsNullOrEmpty(key))
        {
            SetStatus(ClaudeConnectionStatus.NotConfigured);
            return;
        }

        if (!_networkMonitor.IsAvailable)
        {
            SetStatus(ClaudeConnectionStatus.Offline);
            return;
        }

        SetStatus(ClaudeConnectionStatus.Connecting);

        // TODO Phase 1: call IModelProvider.TestConnectionAsync for real validation
        // For now, simulate a lightweight ping
        var sw = Stopwatch.StartNew();
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var testUrl = opts.DefaultProviderId switch
            {
                "anthropic" => "https://api.anthropic.com/v1/messages",
                "openai" => "https://api.openai.com/v1/models",
                "gemini" => "https://generativelanguage.googleapis.com/v1beta/models",
                "ollama" => $"{opts.OllamaBaseUrl}/api/tags",
                _ => null
            };

            if (testUrl is null)
            {
                SetStatus(ClaudeConnectionStatus.NotConfigured);
                return;
            }

            // HEAD request for connectivity check (no auth needed for reachability)
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

    private void SetStatus(ClaudeConnectionStatus newStatus)
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
