// ==========================================================
// Project: WpfHexEditor.Plugins.FileComparison
// File: Services/DiffServiceAdapter.cs
// Description: Adapts DiffEngine + DiffHubPanel to the SDK IDiffService contract,
//              so the terminal and other consumers can request diffs without
//              taking a hard dependency on the plugin or Core.Diff internals.
// Architecture: Adapter pattern — bridges plugin-internal types to the SDK surface.
// ==========================================================

using System.Windows;
using WpfHexEditor.Core.Diff.Services;
using WpfHexEditor.Plugins.FileComparison.Views;
using WpfHexEditor.SDK.Contracts.Services;

namespace WpfHexEditor.Plugins.FileComparison.Services;

/// <summary>
/// Implements <see cref="IDiffService"/> by delegating to the plugin's
/// <see cref="DiffEngine"/> (for headless comparison) and <see cref="DiffHubPanel"/>
/// (for opening the visual viewer).
/// </summary>
internal sealed class DiffServiceAdapter : IDiffService
{
    private readonly DiffEngine   _engine;
    private readonly DiffHubPanel _panel;
    private readonly string       _panelUiId;
    private readonly Action       _showPanel;

    public DiffServiceAdapter(DiffEngine engine, DiffHubPanel panel,
        string panelUiId, Action showPanel)
    {
        _engine    = engine;
        _panel     = panel;
        _panelUiId = panelUiId;
        _showPanel = showPanel;
    }

    /// <inheritdoc/>
    public async Task<DiffSummary> CompareAsync(
        string leftPath, string rightPath, CancellationToken ct = default)
    {
        var result = await _engine.CompareAsync(leftPath, rightPath, ct: ct)
                                   .ConfigureAwait(false);

        int modified = 0, added = 0, removed = 0;
        if (result.TextResult is { } tr)
        {
            modified = tr.Stats.ModifiedLines;
            added    = tr.Stats.InsertedLines;
            removed  = tr.Stats.DeletedLines;
        }
        else if (result.BinaryResult is { } br)
        {
            modified = br.Stats.ModifiedCount;
            added    = br.Stats.InsertedCount;
            removed  = br.Stats.DeletedCount;
        }

        var similarity = (int)(result.Similarity * 100);
        return new DiffSummary(modified, added, removed, similarity);
    }

    /// <inheritdoc/>
    public Task OpenInViewerAsync(
        string leftPath, string rightPath, CancellationToken ct = default)
    {
        // Must run on UI thread — panel is a WPF control.
        Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            _showPanel();
            _panel.OpenFiles(leftPath, rightPath);
        });

        return Task.CompletedTask;
    }
}
