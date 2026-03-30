// ==========================================================
// Project: WpfHexEditor.Editor.DocumentEditor.Core
// File: Model/DocumentModel.cs
// Description:
//     Observable document model owned by DocumentEditorHost.
//     Single source of truth for blocks, binary map, undo engine,
//     forensic alerts, and format metadata.
//
//     UndoEngine lives here so both the text view and the hex view
//     push entries to the same stack — enabling cross-view undo.
// ==========================================================

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using WpfHexEditor.Editor.Core.Undo;
using WpfHexEditor.Editor.DocumentEditor.Core.BinaryMap;
using WpfHexEditor.Editor.DocumentEditor.Core.Forensic;

namespace WpfHexEditor.Editor.DocumentEditor.Core.Model;

/// <summary>
/// Observable model for an open document.
/// Owns the undo engine, binary map, and forensic alert list.
/// </summary>
public sealed class DocumentModel : INotifyPropertyChanged
{
    // ──────────────────────────────── Identity ────────────────────────────────

    private string _filePath = string.Empty;

    /// <summary>Absolute path to the source file.</summary>
    public string FilePath
    {
        get => _filePath;
        set => SetField(ref _filePath, value);
    }

    private DocumentMetadata _metadata = new();

    /// <summary>Format-level metadata extracted by the loader.</summary>
    public DocumentMetadata Metadata
    {
        get => _metadata;
        set => SetField(ref _metadata, value);
    }

    // ──────────────────────────────── Content ─────────────────────────────────

    /// <summary>Top-level block tree (populated by IDocumentLoader).</summary>
    public ObservableCollection<DocumentBlock> Blocks { get; } = [];

    // ──────────────────────────────── Binary map ──────────────────────────────

    /// <summary>
    /// Bidirectional offset ↔ block mapping.
    /// Sealed by the loader after all blocks have been added.
    /// </summary>
    public BinaryMap.BinaryMap BinaryMap { get; } = new();

    // ──────────────────────────────── Undo/Redo ───────────────────────────────

    /// <summary>
    /// Shared undo engine. Both the TextPane and the HexPane push entries here
    /// so Ctrl+Z always undoes the last operation regardless of which view made it.
    /// </summary>
    public UndoEngine UndoEngine { get; } = new() { MaxHistorySize = 500 };

    // ──────────────────────────────── Forensic ────────────────────────────────

    private ForensicMode _forensicMode = ForensicMode.Normal;

    /// <summary>Current forensic display mode (Normal / Debug / Forensic).</summary>
    public ForensicMode ForensicMode
    {
        get => _forensicMode;
        set
        {
            if (SetField(ref _forensicMode, value))
                OnPropertyChanged(nameof(IsForensicActive));
        }
    }

    /// <summary>True when forensic mode is Debug or Forensic.</summary>
    public bool IsForensicActive => _forensicMode != ForensicMode.Normal;

    private IReadOnlyList<ForensicAlert> _forensicAlerts = [];

    /// <summary>Alerts produced by <c>ForensicAnalyzer.Analyze()</c>.</summary>
    public IReadOnlyList<ForensicAlert> ForensicAlerts
    {
        get => _forensicAlerts;
        set
        {
            if (!ReferenceEquals(_forensicAlerts, value))
            {
                _forensicAlerts = value;
                OnPropertyChanged();
                ForensicAlertsChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    // ──────────────────────────────── Dirty tracking ─────────────────────────

    private bool _isDirty;

    /// <summary>True when unsaved changes exist.</summary>
    public bool IsDirty
    {
        get => _isDirty;
        set => SetField(ref _isDirty, value);
    }

    // ──────────────────────────────── Events ──────────────────────────────────

    /// <summary>Raised when <see cref="Blocks"/> content changes structurally.</summary>
    public event EventHandler? BlocksChanged;

    /// <summary>Raised when <see cref="ForensicAlerts"/> is replaced.</summary>
    public event EventHandler? ForensicAlertsChanged;

    public event PropertyChangedEventHandler? PropertyChanged;

    // ──────────────────────────────── Helpers ─────────────────────────────────

    /// <summary>Raises <see cref="BlocksChanged"/>.</summary>
    public void NotifyBlocksChanged() => BlocksChanged?.Invoke(this, EventArgs.Empty);

    /// <summary>Replaces forensic alerts and raises <see cref="ForensicAlertsChanged"/>.</summary>
    public void SetForensicAlerts(IReadOnlyList<ForensicAlert> alerts) =>
        ForensicAlerts = alerts;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }
}
