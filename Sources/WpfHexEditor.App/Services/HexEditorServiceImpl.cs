
//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Windows;
using System.Windows.Threading;
using WpfHexEditor.Core.Interfaces;
using WpfHexEditor.SDK.Contracts.Services;
using HexEditorControl = WpfHexEditor.HexEditor.HexEditor;
using CoreFormatArgs  = WpfHexEditor.Core.Events.FormatDetectedEventArgs;

namespace WpfHexEditor.App.Services;

/// <summary>
/// Adapts the active HexEditor control to the IHexEditorService SDK contract.
/// MainWindow updates ActiveEditor when the focused document changes.
/// </summary>
public sealed class HexEditorServiceImpl : IHexEditorService
{
    private HexEditorControl? _activeEditor;

    public bool IsActive => _activeEditor is not null;

    public string? CurrentFilePath => _activeEditor?.FileName;

    public long FileSize => _activeEditor?.Length ?? 0L;

    public long CurrentOffset => _activeEditor?.SelectionStart ?? 0L;

    public long SelectionStart => _activeEditor?.SelectionStart ?? 0L;

    public long SelectionStop => _activeEditor?.SelectionStop ?? 0L;

    public long SelectionLength => _activeEditor?.SelectionLength ?? 0L;

    public long FirstVisibleByteOffset
    {
        get
        {
            if (_activeEditor is null) return 0;
            return _activeEditor.ScrollPosition * _activeEditor.BytePerLine + _activeEditor.ByteShiftLeft;
        }
    }

    public long LastVisibleByteOffset
    {
        get
        {
            if (_activeEditor is null) return 0;
            return FirstVisibleByteOffset + (long)_activeEditor.VisibleLines * _activeEditor.BytePerLine;
        }
    }

    public event EventHandler? SelectionChanged;
    public event EventHandler? ViewportScrolled;
    public event EventHandler? FileOpened;
    public event EventHandler<FormatDetectedArgs>? FormatDetected;
    public event EventHandler? ActiveEditorChanged;

    public byte[] ReadBytes(long offset, int length)
    {
        if (_activeEditor is null || length <= 0) return [];
        try { return _activeEditor.GetCopyData(offset, offset + length - 1, false) ?? []; }
        catch { return []; }
    }

    public byte[] GetSelectedBytes()
    {
        if (_activeEditor is null) return [];

        // Use the HexEditor's own caret-fallback method (reads up to 8 bytes at caret when no selection)
        return _activeEditor.GetSelectionByteArray() ?? [];
    }

    public void SetSelection(long start, long end)
    {
        if (_activeEditor is null) return;
        _activeEditor.SelectionStart = start;
        _activeEditor.SelectionStop  = end;
    }

    public void NavigateTo(long offset)
    {
        if (_activeEditor is null) return;
        // SetPosition scrolls the viewport so the offset is visible (EnsurePositionVisible),
        // then sets the caret. SelectionStop is set separately to produce a 1-byte highlight.
        _activeEditor.SetPosition(offset);
        _activeEditor.SelectionStop = offset;
    }

    public void ConnectParsedFieldsPanel(IParsedFieldsPanel panel)
        => _activeEditor?.ConnectParsedFieldsPanel(panel);

    public void DisconnectParsedFieldsPanel()
        => _activeEditor?.DisconnectParsedFieldsPanel();

    public void AddCustomBackgroundBlock(WpfHexEditor.Core.CustomBackgroundBlock block)
        => _activeEditor?.AddCustomBackgroundBlock(block);

    public void ClearCustomBackgroundBlockByTag(string tag)
        => _activeEditor?.ClearCustomBackgroundBlockByTag(tag);

    public void WriteBytes(long offset, byte[] data)
    {
        if (_activeEditor is null || data.Length == 0) return;
        // TODO: wire to HexEditor's SetByte / paste API when available.
        // Stub: no-op until the HexEditor write API is exposed.
    }

    public IReadOnlyList<long> SearchHex(string hexPattern)
    {
        // Full implementation deferred to Phase 4 — requires HexEditor search API wiring.
        return [];
    }

    public IReadOnlyList<long> SearchText(string text)
    {
        // Full implementation deferred to Phase 4 — requires HexEditor search API wiring.
        return [];
    }

    /// <summary>
    /// Called by MainWindow when the active hex editor changes.
    /// </summary>
    public void SetActiveEditor(HexEditorControl? editor)
    {
        if (ReferenceEquals(_activeEditor, editor)) return;

        if (_activeEditor is not null)
        {
            _activeEditor.SelectionStartChanged  -= OnSelectionChanged;
            _activeEditor.SelectionStopChanged   -= OnSelectionChanged;
            _activeEditor.FormatDetected         -= OnFormatDetected;
            _activeEditor.FileOpened             -= OnHexEditorFileOpened;
            _activeEditor.VerticalScrollBarChanged -= OnVerticalScrollBarChanged;
        }

        _activeEditor = editor;

        if (_activeEditor is not null)
        {
            _activeEditor.SelectionStartChanged  += OnSelectionChanged;
            _activeEditor.SelectionStopChanged   += OnSelectionChanged;
            _activeEditor.FormatDetected         += OnFormatDetected;
            // Forward the native FileOpened (fires after file stream is ready, not before).
            _activeEditor.FileOpened             += OnHexEditorFileOpened;
            _activeEditor.VerticalScrollBarChanged += OnVerticalScrollBarChanged;

            // Tab switch: file already loaded — fire at Background priority so the
            // UI finishes rendering the tab before plugin handlers run.
            if (_activeEditor.IsFileLoaded)
                FireDeferred(FileOpened);
        }

        // Notify plugins that the active editor has changed (e.g. tab switch).
        // ParsedFieldsPlugin uses this to reconnect its panel to the new editor.
        FireDeferred(ActiveEditorChanged);
    }

    // Forwarded from HexEditorControl.FileOpened — fires after the stream is ready,
    // so plugins that read bytes (FileStats, PatternAnalysis) get valid data.
    private void OnHexEditorFileOpened(object? sender, EventArgs e)
        => FireDeferred(FileOpened);

    private void OnSelectionChanged(object? sender, EventArgs e)
        => SelectionChanged?.Invoke(this, EventArgs.Empty);

    private void OnVerticalScrollBarChanged(object? sender, EventArgs e)
        => ViewportScrolled?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// Dispatches a plugin-facing event at Background priority so the UI thread
    /// can finish rendering the newly opened file before any plugin handler runs.
    /// Plugins that read bytes will therefore never block the initial render pass.
    /// </summary>
    private void FireDeferred(EventHandler? handler)
    {
        if (handler is null) return;
        Application.Current?.Dispatcher.InvokeAsync(
            () => handler(this, EventArgs.Empty),
            DispatcherPriority.Background);
    }

    private void OnFormatDetected(object? sender, CoreFormatArgs e)
        => FormatDetected?.Invoke(this, new FormatDetectedArgs
        {
            Success             = e.Success,
            FormatId            = e.Format?.FormatName,   // FormatDefinition has no separate ID — use Name as key
            FormatName          = e.Format?.FormatName,
            RawFormatDefinition = e.Format              // full object available to bundled first-party plugins
        });
}
