
//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using WpfHexEditor.Editor.Core;
using WpfHexEditor.Panels.IDE.Panels;
using WpfHexEditor.SDK.Contracts.Services;

namespace WpfHexEditor.App.Services;

/// <summary>
/// Adapts the ErrorPanel (IDiagnosticSource) to the IErrorPanelService SDK contract.
/// Each plugin gets an isolated diagnostic source keyed by its plugin ID.
/// </summary>
public sealed class ErrorPanelServiceImpl : IErrorPanelService
{
    private ErrorPanel? _errorPanel;
    private readonly Dictionary<string, PluginDiagnosticSource> _sources =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Called by MainWindow once the ErrorPanel is created.</summary>
    public void SetErrorPanel(ErrorPanel panel) => _errorPanel = panel;

    public void PostDiagnostic(string pluginId, string severity, string message,
                               string? source = null, int line = -1, int col = -1)
    {
        if (_errorPanel is null) return;

        var ds = GetOrCreateSource(pluginId);
        var sev = severity.ToLowerInvariant() switch
        {
            "error" => DiagnosticSeverity.Error,
            "warning" => DiagnosticSeverity.Warning,
            _ => DiagnosticSeverity.Info
        };

        ds.Add(new DiagnosticEntry(sev, message, source ?? pluginId, line, col));
    }

    public void ClearPluginDiagnostics(string pluginId)
    {
        if (_sources.TryGetValue(pluginId, out var ds)) ds.Clear();
    }

    private PluginDiagnosticSource GetOrCreateSource(string pluginId)
    {
        if (!_sources.TryGetValue(pluginId, out var ds))
        {
            ds = new PluginDiagnosticSource(pluginId);
            _sources[pluginId] = ds;
            _errorPanel?.RegisterSource(ds);
        }
        return ds;
    }

    /// <summary>Per-plugin diagnostic source forwarded to ErrorPanel.</summary>
    private sealed class PluginDiagnosticSource : IDiagnosticSource
    {
        private readonly List<DiagnosticEntry> _entries = new();
        private readonly object _lock = new();

        public string SourceLabel { get; }
        public event EventHandler? DiagnosticsChanged;

        public PluginDiagnosticSource(string pluginId) =>
            SourceLabel = $"Plugin: {pluginId}";

        public IReadOnlyList<DiagnosticEntry> GetDiagnostics()
        {
            lock (_lock) return _entries.ToList();
        }

        public void Add(DiagnosticEntry entry)
        {
            lock (_lock) _entries.Add(entry);
            DiagnosticsChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Clear()
        {
            lock (_lock) _entries.Clear();
            DiagnosticsChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
