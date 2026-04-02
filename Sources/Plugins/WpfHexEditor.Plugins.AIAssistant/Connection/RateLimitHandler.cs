// ==========================================================
// Project: WpfHexEditor.Plugins.AIAssistant
// File: RateLimitHandler.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.6
// Created: 2026-03-31
// License: GNU Affero General Public License v3.0 (AGPL-3.0)
// Description:
//     Handles API rate limits with exponential backoff and request queuing.
// ==========================================================
namespace WpfHexEditor.Plugins.AIAssistant.Connection;

public sealed class RateLimitHandler
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private DateTime _retryAfter = DateTime.MinValue;
    private int _consecutiveFailures;

    public bool IsRateLimited => DateTime.UtcNow < _retryAfter;
    public TimeSpan RemainingWait => IsRateLimited ? _retryAfter - DateTime.UtcNow : TimeSpan.Zero;

    public void RecordRateLimit(TimeSpan? retryAfter = null)
    {
        _consecutiveFailures++;
        var backoff = retryAfter ?? TimeSpan.FromSeconds(Math.Min(60, Math.Pow(2, _consecutiveFailures)));
        _retryAfter = DateTime.UtcNow + backoff;
    }

    public void RecordSuccess()
    {
        _consecutiveFailures = 0;
        _retryAfter = DateTime.MinValue;
    }

    public async Task WaitIfNeededAsync(CancellationToken ct)
    {
        if (!IsRateLimited) return;

        await _gate.WaitAsync(ct);
        try
        {
            var remaining = _retryAfter - DateTime.UtcNow;
            if (remaining > TimeSpan.Zero)
                await Task.Delay(remaining, ct);
        }
        finally
        {
            _gate.Release();
        }
    }
}
