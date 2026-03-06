
//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using WpfHexEditor.SDK.Contracts.Services;
using HexEditorControl = WpfHexEditor.HexEditor.HexEditor;

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

    public byte[]? ReadBytes(long offset, int length)
    {
        if (_activeEditor is null || length <= 0) return null;
        try { return _activeEditor.GetCopyData(offset, length); }
        catch { return null; }
    }

    public byte[]? GetSelectedBytes()
    {
        if (_activeEditor is null || SelectionLength == 0) return null;
        return ReadBytes(SelectionStart, (int)Math.Min(SelectionLength, int.MaxValue));
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
            _activeEditor.SelectionStopChanged -= OnSelectionChanged;
        }

        _activeEditor = editor;

        if (_activeEditor is not null)
        {
            _activeEditor.SelectionStartChanged += OnSelectionChanged;
            _activeEditor.SelectionStopChanged += OnSelectionChanged;
            FileOpened?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnSelectionChanged(object? sender, EventArgs e)
        => SelectionChanged?.Invoke(this, EventArgs.Empty);
}
