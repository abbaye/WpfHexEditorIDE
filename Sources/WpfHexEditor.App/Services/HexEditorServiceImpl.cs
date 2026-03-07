
//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

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

    public event EventHandler? SelectionChanged;
    public event EventHandler? FileOpened;
    public event EventHandler<FormatDetectedArgs>? FormatDetected;
    public event EventHandler? ActiveEditorChanged;

    public byte[] ReadBytes(long offset, int length)
    {
        if (_activeEditor is null || length <= 0) return [];
        try { return _activeEditor.GetCopyData(offset, (long)length, false) ?? []; }
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

    public void ConnectParsedFieldsPanel(IParsedFieldsPanel panel)
        => _activeEditor?.ConnectParsedFieldsPanel(panel);

    public void DisconnectParsedFieldsPanel()
        => _activeEditor?.DisconnectParsedFieldsPanel();

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
            _activeEditor.SelectionStartChanged -= OnSelectionChanged;
            _activeEditor.SelectionStopChanged  -= OnSelectionChanged;
            _activeEditor.FormatDetected        -= OnFormatDetected;
        }

        _activeEditor = editor;

        if (_activeEditor is not null)
        {
            _activeEditor.SelectionStartChanged += OnSelectionChanged;
            _activeEditor.SelectionStopChanged  += OnSelectionChanged;
            _activeEditor.FormatDetected        += OnFormatDetected;
            FileOpened?.Invoke(this, EventArgs.Empty);
        }

        // Notify plugins that the active editor has changed (e.g. tab switch).
        // ParsedFieldsPlugin uses this to reconnect its panel to the new editor.
        ActiveEditorChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnSelectionChanged(object? sender, EventArgs e)
        => SelectionChanged?.Invoke(this, EventArgs.Empty);

    private void OnFormatDetected(object? sender, CoreFormatArgs e)
        => FormatDetected?.Invoke(this, new FormatDetectedArgs
        {
            Success             = e.Success,
            FormatId            = e.Format?.FormatName,   // FormatDefinition has no separate ID — use Name as key
            FormatName          = e.Format?.FormatName,
            RawFormatDefinition = e.Format              // full object available to bundled first-party plugins
        });
}
