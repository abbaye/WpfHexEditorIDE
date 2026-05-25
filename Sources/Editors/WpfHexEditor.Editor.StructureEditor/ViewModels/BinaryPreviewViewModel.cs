// ==========================================================
// Project: WpfHexEditor.Editor.StructureEditor
// File: ViewModels/BinaryPreviewViewModel.cs
// Description:
//     ViewModel for the live binary-preview panel. Subscribes to
//     LiveWhfmtBuffer.Changed, runs SimpleBlockInterpreter on a background
//     thread, and exposes FieldResultRows for display in a DataGrid.
// Architecture: MVVM — no WPF types; data-bound by BinaryPreviewPanel.
// ==========================================================

using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using WpfHexEditor.Core.ViewModels;
using WpfHexEditor.Editor.Core.Documents;
using WpfHexEditor.Editor.StructureEditor.Services;
using WpfHexEditor.Core.FormatDetection;
// StructureHexSyncService is in the same Services namespace, already imported above.

namespace WpfHexEditor.Editor.StructureEditor.ViewModels;

/// <summary>One decoded field row shown in the binary-preview DataGrid.</summary>
public sealed class FieldResultRow
{
    public string Name         { get; init; } = "";
    public string Offset       { get; init; } = "";   // hex string, e.g. "0x001C"
    public string Length       { get; init; } = "";   // decimal bytes
    public string RawHex       { get; init; } = "";
    public string DecodedValue { get; init; } = "";
    public string Status       { get; init; } = "OK"; // OK | Warning | Error | Skipped
}

/// <summary>
/// Drives the live binary-preview panel. Call <see cref="Attach"/> with an
/// <see cref="IDocumentBuffer"/> to start listening for structure changes.
/// </summary>
public sealed class BinaryPreviewViewModel : ViewModelBase
{
    // ── State ─────────────────────────────────────────────────────────────────
    private IDocumentBuffer?           _buffer;
    private byte[]?                    _binaryBytes;
    private bool                       _isRunning;
    private string                     _statusText = "No binary loaded.";
    private IStructureHexSyncService?  _sync;

    public ObservableCollection<FieldResultRow> Rows { get; } = new();

    /// <summary>
    /// Optional sync service. When set, <see cref="EditFieldHex"/> writes back
    /// to the underlying buffer and notifies the host HexEditor.
    /// </summary>
    public IStructureHexSyncService? SyncService
    {
        get => _sync;
        set => _sync = value;
    }

    public string StatusText
    {
        get => _statusText;
        private set { _statusText = value; OnPropertyChanged(); }
    }

    public bool IsRunning
    {
        get => _isRunning;
        private set { _isRunning = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// When set, fires with (offset, byteLength) each time the user selects a field row.
    /// The host wires this to navigate the active HexEditor.
    /// </summary>
    public Action<long, long>? FieldNavigationRequested { get; set; }

    /// <summary>Navigates the hex editor to the field represented by <paramref name="row"/>.</summary>
    public void NavigateToField(FieldResultRow row)
    {
        if (FieldNavigationRequested is null) return;
        if (!long.TryParse(
                row.Offset.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? row.Offset[2..] : row.Offset,
                System.Globalization.NumberStyles.HexNumber,
                System.Globalization.CultureInfo.InvariantCulture,
                out long offset))
            return;

        long length = long.TryParse(row.Length, out long l) ? l : 1;
        FieldNavigationRequested(offset, length);
    }

    // ── Wiring ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Attaches to a <see cref="IDocumentBuffer"/> and triggers a refresh on every change.
    /// </summary>
    public void Attach(IDocumentBuffer buffer)
    {
        Detach();
        _buffer = buffer;
        _buffer.Changed += OnBufferChanged;
        _ = RefreshAsync(_buffer.Text);
    }

    /// <summary>Detaches from the current buffer.</summary>
    public void Detach()
    {
        if (_buffer is not null)
        {
            _buffer.Changed -= OnBufferChanged;
            _buffer = null;
        }
    }

    /// <summary>Sets the binary file to interpret against.</summary>
    public async Task LoadBinaryAsync(string filePath)
    {
        try
        {
            _binaryBytes = await File.ReadAllBytesAsync(filePath).ConfigureAwait(false);
            _sync?.SetBytes(_binaryBytes);
            StatusText = $"Binary: {Path.GetFileName(filePath)} ({_binaryBytes.Length:N0} bytes)";
            if (_buffer is not null)
                _ = RefreshAsync(_buffer.Text);
        }
        catch (Exception ex)
        {
            StatusText = $"Error loading binary: {ex.Message}";
        }
    }

    /// <summary>
    /// Persists a new hex value for the given row back to the buffer (via
    /// <see cref="SyncService"/>) and refreshes the preview. Offset is parsed
    /// from <see cref="FieldResultRow.Offset"/> (e.g. "0x001C").
    /// </summary>
    public bool EditFieldHex(FieldResultRow row, string newHex)
    {
        if (_sync is null || _binaryBytes is null) return false;

        var bytes = StructureHexSyncService.TryParseHex(newHex);
        if (bytes is null) return false;

        var offsetStr = row.Offset.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? row.Offset[2..]
            : row.Offset;
        if (!long.TryParse(offsetStr,
                System.Globalization.NumberStyles.HexNumber,
                System.Globalization.CultureInfo.InvariantCulture, out var offset))
            return false;

        if (!_sync.WriteField(offset, bytes, row.Name)) return false;

        if (_buffer is not null) _ = RefreshAsync(_buffer.Text);
        return true;
    }

    // ── Refresh ───────────────────────────────────────────────────────────────

    private void OnBufferChanged(object? sender, DocumentBufferChangedEventArgs e)
        => _ = RefreshAsync(e.NewText);

    private async Task RefreshAsync(string json)
    {
        if (_binaryBytes is null || string.IsNullOrWhiteSpace(json)) return;

        IsRunning = true;
        try
        {
            var bytes = _binaryBytes; // capture
            var rows  = await Task.Run(() => RunInterpreter(json, bytes)).ConfigureAwait(true);

            Rows.Clear();
            foreach (var row in rows)
                Rows.Add(row);

            StatusText = rows.Count > 0
                ? $"{rows.Count} fields parsed."
                : "No fields. Check structure definition.";
        }
        catch (Exception ex)
        {
            StatusText = $"Parse error: {ex.Message}";
        }
        finally
        {
            IsRunning = false;
        }
    }

    private static List<FieldResultRow> RunInterpreter(string json, byte[] bytes)
    {
        FormatDefinition def;
        try
        {
            def = JsonSerializer.Deserialize<FormatDefinition>(json,
                      new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                  ?? new FormatDefinition();
        }
        catch
        {
            return [];
        }

        var interpreter = new SimpleBlockInterpreter(bytes);
        var results     = interpreter.Run(def);

        return results
            .Where(r => !r.IsSummary)
            .Select(r => new FieldResultRow
            {
                Name         = r.BlockName,
                Offset       = r.Offset >= 0 ? $"0x{r.Offset:X4}" : "",
                Length       = r.Length > 0 ? r.Length.ToString() : "",
                RawHex       = r.RawHex,
                DecodedValue = r.ParsedValue,
                Status       = r.Status,
            })
            .ToList();
    }
}
