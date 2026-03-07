//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.PluginHost;

/// <summary>
/// Rate-limits plugin callbacks to prevent CPU spikes from high-frequency plugins.
/// Uses a sliding window rate limiter per plugin.
/// </summary>
internal sealed class PluginScheduler
{
    private readonly int _maxCallsPerSecond;
    private readonly Dictionary<string, SlidingWindowCounter> _counters = new();
    private readonly object _lock = new();

    public PluginScheduler(int maxCallsPerSecond = 30)
    {
        _maxCallsPerSecond = maxCallsPerSecond;
    }

    /// <summary>
    /// Returns true if a callback for the specified plugin is allowed right now.
    /// Returns false if the plugin is over its rate limit for the current second.
    /// </summary>
    public bool TryAcquire(string pluginId)
    {
        lock (_lock)
        {
            if (!_counters.TryGetValue(pluginId, out var counter))
            {
                counter = new SlidingWindowCounter(_maxCallsPerSecond);
                _counters[pluginId] = counter;
            }
            return counter.TryIncrement();
        }
    }

    /// <summary>Removes tracking for a plugin (called on unload).</summary>
    public void Remove(string pluginId)
    {
        lock (_lock) { _counters.Remove(pluginId); }
    }

    // -- Sliding window counter -----------------------------------------------

    private sealed class SlidingWindowCounter(int maxPerSecond)
    {
        private readonly int _max = maxPerSecond;
        private DateTime _windowStart = DateTime.UtcNow;
        private int _count;

        public bool TryIncrement()
        {
            var now = DateTime.UtcNow;
            if ((now - _windowStart).TotalSeconds >= 1.0)
            {
                _windowStart = now;
                _count = 0;
            }
            if (_count >= _max) return false;
            _count++;
            return true;
        }
    }
}
