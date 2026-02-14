//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using WpfHexaEditor.Core;
using WpfHexaEditor.Core.Bytes;
using WpfHexaEditor.Core.CharacterTable;
using WpfHexaEditor.Services;
using WpfHexaEditor.V2.Models;

namespace WpfHexaEditor.V2.ViewModels
{
    /// <summary>
    /// ViewModel for HexEditorV2 - handles all business logic
    /// Architecture: Virtual positions (display) ↔ Physical positions (file)
    /// </summary>
    public class HexEditorViewModel : INotifyPropertyChanged
    {
        #region Fields

        private readonly ByteProvider _provider;
        private readonly UndoRedoService _undoRedoService = new();
        private readonly ClipboardService _clipboardService = new();
        private readonly SelectionService _selectionService = new();
        private readonly FindReplaceService _findReplaceService = new();

        /// <summary>
        /// Expose ByteProvider for external configuration (e.g., CanInsertAnywhere)
        /// </summary>
        public ByteProvider Provider => _provider;

        // Position mapping: tracks insertions/deletions for Virtual ↔ Physical conversion
        private readonly Dictionary<long, long> _insertions = new(); // physicalPos → count
        private readonly Dictionary<long, long> _deletions = new();  // physicalPos → count

        // Store inserted bytes separately (virtual position → byte value)
        // ByteProvider can't handle multiple insertions at same physical position correctly
        private readonly Dictionary<long, byte> _insertedBytes = new();

        // Undo/Redo stacks for inserted bytes (ViewModel-managed, separate from ByteProvider)
        private readonly Stack<InsertByteEdit> _insertUndoStack = new();
        private readonly Stack<InsertByteEdit> _insertRedoStack = new();

        // Performance: Line cache to avoid recreating lines on every scroll
        private readonly Dictionary<long, HexLine> _lineCache = new();
        private long _lastScrollStart = -1;
        private long _lastScrollEnd = -1;

        private EditMode _editMode = EditMode.Overwrite;
        private int _bytePerLine = 16;
        private long _scrollPosition = 0;
        private int _visibleLines = 20;
        private VirtualPosition _selectionStart = VirtualPosition.Invalid;
        private VirtualPosition _selectionStop = VirtualPosition.Invalid;
        private bool _readOnlyMode = false;
        private bool _suppressRefresh = false; // Flag to batch updates

        #endregion

        #region Properties

        /// <summary>
        /// Edit mode (Insert or Overwrite)
        /// </summary>
        public EditMode EditMode
        {
            get => _editMode;
            set
            {
                if (_editMode != value)
                {
                    _editMode = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Number of bytes per line
        /// </summary>
        public int BytePerLine
        {
            get => _bytePerLine;
            set
            {
                if (_bytePerLine != value && value > 0)
                {
                    _bytePerLine = value;
                    OnPropertyChanged();
                    RefreshVisibleLines();
                }
            }
        }

        /// <summary>
        /// Current scroll position (line number)
        /// </summary>
        public long ScrollPosition
        {
            get => _scrollPosition;
            set
            {
                if (_scrollPosition != value)
                {
                    _scrollPosition = value;
                    OnPropertyChanged();
                    RefreshVisibleLines();
                }
            }
        }

        /// <summary>
        /// Number of visible lines in viewport
        /// </summary>
        public int VisibleLines
        {
            get => _visibleLines;
            set
            {
                if (_visibleLines != value && value > 0)
                {
                    _visibleLines = value;
                    OnPropertyChanged();
                    RefreshVisibleLines();
                }
            }
        }

        /// <summary>
        /// Selection start (virtual position)
        /// </summary>
        public VirtualPosition SelectionStart
        {
            get => _selectionStart;
            set
            {
                if (_selectionStart != value)
                {
                    _selectionStart = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(HasSelection));
                    UpdateSelectionState(); // Much faster than full refresh
                }
            }
        }

        /// <summary>
        /// Selection stop (virtual position)
        /// </summary>
        public VirtualPosition SelectionStop
        {
            get => _selectionStop;
            set
            {
                if (_selectionStop != value)
                {
                    _selectionStop = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(HasSelection));
                    UpdateSelectionState(); // Much faster than full refresh
                }
            }
        }

        /// <summary>
        /// Is there an active selection? (includes single byte selection)
        /// </summary>
        public bool HasSelection => _selectionStart.IsValid && _selectionStop.IsValid;

        /// <summary>
        /// Selection length in bytes
        /// </summary>
        public long SelectionLength => HasSelection ? Math.Abs(_selectionStop - _selectionStart) + 1 : 0;

        /// <summary>
        /// Read-only mode
        /// </summary>
        public bool ReadOnlyMode
        {
            get => _readOnlyMode;
            set
            {
                if (_readOnlyMode != value)
                {
                    _readOnlyMode = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Total file length (physical)
        /// </summary>
        public long FileLength => _provider?.Length ?? 0;

        /// <summary>
        /// Get all modified byte positions (for scroll markers)
        /// </summary>
        public IEnumerable<long> GetModifiedPositions()
        {
            if (_provider == null)
                return Enumerable.Empty<long>();

            // Collect modified positions from all cached lines
            var modifiedPositions = new List<long>();
            foreach (var line in Lines)
            {
                foreach (var byteData in line.Bytes)
                {
                    if (byteData.Action != ByteAction.Nothing && byteData.VirtualPos.IsValid)
                    {
                        modifiedPositions.Add(byteData.VirtualPos.Value);
                    }
                }
            }
            return modifiedPositions;
        }

        /// <summary>
        /// Total virtual length (file + inserted - deleted_if_hidden)
        /// </summary>
        public long VirtualLength
        {
            get
            {
                long length = FileLength;
                length += _insertions.Values.Sum();
                // Note: deleted bytes are still in file, only hidden from view
                return length;
            }
        }

        /// <summary>
        /// Total number of lines
        /// </summary>
        public long TotalLines => (VirtualLength + BytePerLine - 1) / BytePerLine;

        /// <summary>
        /// Visible lines (observable collection for UI binding)
        /// </summary>
        public ObservableCollection<HexLine> Lines { get; } = new();

        /// <summary>
        /// Can undo? (checks both ByteProvider and ViewModel insert stacks)
        /// </summary>
        public bool CanUndo => _undoRedoService.CanUndo(_provider) || _insertUndoStack.Count > 0;

        /// <summary>
        /// Can redo? (checks both ByteProvider and ViewModel insert stacks)
        /// </summary>
        public bool CanRedo => _undoRedoService.CanRedo(_provider) || _insertRedoStack.Count > 0;

        #endregion

        #region Constructor

        public HexEditorViewModel(ByteProvider provider)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));

            // Set default copy mode to ASCII string for normal copy/paste operations
            _clipboardService.DefaultCopyMode = CopyPasteMode.AsciiString;

            // Note: ByteProvider doesn't expose DataChanged event
            // RefreshVisibleLines will be called manually after operations

            RefreshVisibleLines();
        }

        #endregion

        #region Public Methods - File Operations

        /// <summary>
        /// Open a file for editing
        /// </summary>
        public static HexEditorViewModel OpenFile(string filePath)
        {
            var provider = new ByteProvider(filePath);
            return new HexEditorViewModel(provider);
        }

        /// <summary>
        /// Save changes to file
        /// </summary>
        public void Save()
        {
            _provider.SubmitChanges();
        }

        /// <summary>
        /// Save changes to a new file (V1 compatible)
        /// </summary>
        public bool SaveAs(string newFilename, bool overwrite = false)
        {
            return _provider.SubmitChanges(newFilename, overwrite);
        }

        /// <summary>
        /// Close file and cleanup
        /// </summary>
        public void Close()
        {
            _provider?.Close();
        }

        /// <summary>
        /// Clear all undo/redo history (V1 compatible)
        /// </summary>
        public void ClearUndoRedo()
        {
            _undoRedoService.ClearAll(_provider);
            _insertUndoStack.Clear();
            _insertRedoStack.Clear();
            OnPropertyChanged(nameof(CanUndo));
            OnPropertyChanged(nameof(CanRedo));
        }

        /// <summary>
        /// Refresh display (clear cache and refresh visible lines) (V1 compatible)
        /// </summary>
        public void RefreshDisplay()
        {
            ClearLineCache();
            RefreshVisibleLines();
        }

        #endregion

        #region Public Methods - Edit Operations

        /// <summary>
        /// Get byte value at virtual position
        /// </summary>
        public byte GetByteAt(VirtualPosition virtualPos)
        {
            var physicalPos = VirtualToPhysical(virtualPos);
            if (!physicalPos.IsValid || physicalPos.Value >= _provider.Length)
                return 0;

            // Set position and read byte
            _provider.Position = physicalPos.Value;
            int byteValue = _provider.ReadByte();
            return byteValue >= 0 ? (byte)byteValue : (byte)0;
        }

        /// <summary>
        /// Modify byte at virtual position
        /// Handles both inserted bytes (_insertedBytes) and file bytes (ByteProvider)
        /// </summary>
        public void ModifyByte(VirtualPosition virtualPos, byte newValue)
        {
            if (ReadOnlyMode) return;

            // Check if this is an inserted byte (stored in ViewModel)
            if (_insertedBytes.ContainsKey(virtualPos.Value))
            {
                // Update the inserted byte directly
                _insertedBytes[virtualPos.Value] = newValue;
                System.Diagnostics.Debug.WriteLine($"[MODIFYBYTE] Updated inserted byte at virtual pos {virtualPos.Value}: 0x{newValue:X2}");
            }
            else
            {
                // Modify file byte via ByteProvider
                var physicalPos = VirtualToPhysical(virtualPos);
                if (!physicalPos.IsValid) return;

                _provider.AddByteModified(newValue, physicalPos.Value);
                System.Diagnostics.Debug.WriteLine($"[MODIFYBYTE] Modified file byte at physical pos {physicalPos.Value}: 0x{newValue:X2}");

                // Notify Undo/Redo state changed (ByteProvider handles undo for modifications)
                OnPropertyChanged(nameof(CanUndo));
                OnPropertyChanged(nameof(CanRedo));
            }

            // OPTIMIZATION: Invalidate only the affected line, not the entire cache
            InvalidateLineAtPosition(virtualPos.Value);

            // Refresh only the affected line if it's currently visible
            long lineNumber = virtualPos.Value / BytePerLine;
            if (lineNumber >= ScrollPosition && lineNumber < ScrollPosition + VisibleLines)
            {
                RefreshLine(lineNumber);
            }
        }

        /// <summary>
        /// Update byte preview (real-time display during editing, not committed)
        /// Shows partial byte edit immediately (e.g., FF → 4F when high nibble is edited)
        /// </summary>
        public void UpdateBytePreview(VirtualPosition virtualPos, byte previewValue)
        {
            if (!virtualPos.IsValid) return;

            // Find the byte in currently displayed lines
            foreach (var line in Lines)
            {
                foreach (var byteData in line.Bytes)
                {
                    if (byteData.VirtualPos == virtualPos)
                    {
                        // Update the value temporarily - this triggers HexString property update
                        byteData.Value = previewValue;
                        return; // Found and updated, done
                    }
                }
            }
        }

        /// <summary>
        /// Insert byte at virtual position (shifts existing bytes)
        /// </summary>
        public void InsertByte(VirtualPosition virtualPos, byte value)
        {
            if (ReadOnlyMode || EditMode != EditMode.Insert) return;

            var physicalPos = VirtualToPhysical(virtualPos);

            System.Diagnostics.Debug.WriteLine($"[INSERTBYTE] Inserting 0x{value:X2} at virtualPos={virtualPos.Value}, physicalPos={physicalPos.Value}");

            // Store inserted byte in ViewModel dictionary (indexed by VIRTUAL position)
            // This prevents ByteProvider confusion between inserted bytes and file bytes
            _insertedBytes[virtualPos.Value] = value;
            System.Diagnostics.Debug.WriteLine($"[INSERTBYTE] Stored in _insertedBytes[{virtualPos.Value}] = 0x{value:X2}");

            // NOTE: We do NOT call _provider.AddByteAdded() here because:
            // 1. It would add entry to ByteProvider's dictionary at virtual position
            // 2. This blocks reading of file bytes at same physical position
            // 3. Undo/Redo for insertions will be handled separately in ViewModel

            // Record operation for undo/redo
            var edit = new InsertByteEdit(virtualPos.Value, physicalPos.Value, value);
            _insertUndoStack.Push(edit);
            _insertRedoStack.Clear(); // New operation invalidates redo history
            System.Diagnostics.Debug.WriteLine($"[INSERTBYTE UNDO] Recorded edit, undo stack count: {_insertUndoStack.Count}");

            // Notify Undo/Redo state changed
            OnPropertyChanged(nameof(CanUndo));
            OnPropertyChanged(nameof(CanRedo));

            // Track insertion for position mapping
            if (_insertions.ContainsKey(physicalPos.Value))
            {
                _insertions[physicalPos.Value]++;
                System.Diagnostics.Debug.WriteLine($"[INSERTBYTE] _insertions[{physicalPos.Value}] incremented to {_insertions[physicalPos.Value]}");
            }
            else
            {
                _insertions[physicalPos.Value] = 1;
                System.Diagnostics.Debug.WriteLine($"[INSERTBYTE] _insertions[{physicalPos.Value}] = 1");
            }

            System.Diagnostics.Debug.WriteLine($"[INSERTBYTE] VirtualLength before refresh: {VirtualLength}, FileLength: {FileLength}, Total insertions: {_insertions.Values.Sum()}");

            // OPTIMIZATION: Since insert shifts all following bytes, we need full refresh
            // But we can still use incremental update if scrolling is stable
            ClearLineCache();
            RefreshVisibleLines();

            System.Diagnostics.Debug.WriteLine($"[INSERTBYTE] VirtualLength after refresh: {VirtualLength}");
        }

        /// <summary>
        /// Delete byte at virtual position
        /// </summary>
        public void DeleteByte(VirtualPosition virtualPos)
        {
            if (ReadOnlyMode) return;

            var physicalPos = VirtualToPhysical(virtualPos);
            if (!physicalPos.IsValid) return;

            _provider.AddByteDeleted(physicalPos.Value, 1);

            // Notify Undo/Redo state changed
            OnPropertyChanged(nameof(CanUndo));
            OnPropertyChanged(nameof(CanRedo));

            // Track deletion for position mapping
            if (_deletions.ContainsKey(physicalPos.Value))
                _deletions[physicalPos.Value]++;
            else
                _deletions[physicalPos.Value] = 1;

            // OPTIMIZATION: Since delete shifts all following bytes, we need full refresh
            // But we can still use incremental update if scrolling is stable
            ClearLineCache();
            RefreshVisibleLines();
        }

        /// <summary>
        /// Delete selection (optimized to batch deletions)
        /// </summary>
        public void DeleteSelection()
        {
            if (!HasSelection || ReadOnlyMode) return;

            var start = Math.Min(_selectionStart.Value, _selectionStop.Value);
            var length = SelectionLength;

            // Batch deletions for performance
            BeginUpdate();
            try
            {
                for (long i = 0; i < length; i++)
                {
                    DeleteByte(new VirtualPosition(start));
                }
            }
            finally
            {
                EndUpdate();
            }

            SelectionStart = VirtualPosition.Invalid;
            SelectionStop = VirtualPosition.Invalid;
        }

        /// <summary>
        /// Copy selected bytes to clipboard
        /// </summary>
        public bool CopyToClipboard()
        {
            if (!HasSelection) return false;

            var start = Math.Min(_selectionStart.Value, _selectionStop.Value);
            var stop = Math.Max(_selectionStart.Value, _selectionStop.Value);

            // Convert virtual positions to physical positions
            var physicalStart = VirtualToPhysical(new VirtualPosition(start));
            var physicalStop = VirtualToPhysical(new VirtualPosition(stop));

            if (!physicalStart.IsValid || !physicalStop.IsValid) return false;

            _clipboardService.CopyToClipboard(_provider, physicalStart.Value, physicalStop.Value);
            return true;
        }

        /// <summary>
        /// Cut selected bytes to clipboard (copy + delete)
        /// </summary>
        public bool Cut()
        {
            if (!HasSelection || ReadOnlyMode) return false;

            // Copy first
            if (!CopyToClipboard()) return false;

            // Then delete
            DeleteSelection();
            return true;
        }

        /// <summary>
        /// Paste bytes from clipboard at current position
        /// </summary>
        public bool Paste()
        {
            if (ReadOnlyMode) return false;

            // Get the paste position (virtual)
            long pastePosition;
            if (HasSelection)
            {
                // Paste at the start of the selection (will overwrite)
                pastePosition = Math.Min(_selectionStart.Value, _selectionStop.Value);

                // Clear selection (but don't delete bytes - we'll overwrite them)
                SelectionStart = VirtualPosition.Invalid;
                SelectionStop = VirtualPosition.Invalid;
            }
            else
            {
                // No selection, paste at current cursor position
                pastePosition = _selectionStart.IsValid ? _selectionStart.Value : 0;
            }

            // Get bytes to paste
            byte[] bytesToPaste = null;

            // Try to get binary data from clipboard first (preferred format)
            var dataObj = System.Windows.Clipboard.GetDataObject();
            if (dataObj != null && dataObj.GetDataPresent("BinaryData"))
            {
                try
                {
                    var memStream = dataObj.GetData("BinaryData") as MemoryStream;
                    if (memStream != null && memStream.Length > 0)
                    {
                        bytesToPaste = memStream.ToArray();
                    }
                }
                catch
                {
                    // Fall back to text format
                }
            }

            // Fall back to text format if no binary data
            if (bytesToPaste == null)
            {
                string clipboardText = System.Windows.Clipboard.GetText();
                if (string.IsNullOrEmpty(clipboardText)) return false;

                // Convert each character to byte (same as V1 ByteProvider.Paste(string))
                // Each character's ASCII code becomes a byte value
                bytesToPaste = new byte[clipboardText.Length];
                for (int i = 0; i < clipboardText.Length; i++)
                {
                    bytesToPaste[i] = ByteConverters.CharToByte(clipboardText[i]);
                }
            }

            if (bytesToPaste == null || bytesToPaste.Length == 0) return false;

            // Handle paste based on EditMode
            if (EditMode == Models.EditMode.Insert)
            {
                // INSERT MODE: Create ByteAdded entries (green borders)
                // Insert each byte at the current virtual position, shifting subsequent bytes
                var currentVirtualPos = new VirtualPosition(pastePosition);

                foreach (byte b in bytesToPaste)
                {
                    InsertByte(currentVirtualPos, b);
                    currentVirtualPos = new VirtualPosition(currentVirtualPos.Value + 1);
                }

                System.Diagnostics.Debug.WriteLine($"[PASTE INSERT] Inserted {bytesToPaste.Length} bytes at virtual pos {pastePosition} (green borders)");
            }
            else
            {
                // OVERWRITE MODE: Create ByteModified entries (orange borders)
                // Use ByteProvider to overwrite existing bytes
                var pasteVirtualPos = new VirtualPosition(pastePosition);
                var physicalStart = VirtualToPhysical(pasteVirtualPos);
                if (!physicalStart.IsValid) return false;

                _provider.Paste(physicalStart.Value, bytesToPaste, false);
                System.Diagnostics.Debug.WriteLine($"[PASTE OVERWRITE] Modified {bytesToPaste.Length} bytes at physical pos {physicalStart.Value} (orange borders)");
            }

            ClearLineCache();
            RefreshVisibleLines();
            return true;
        }

        /// <summary>
        /// Get bytes from current selection as byte array
        /// </summary>
        public byte[] GetSelectionBytes()
        {
            if (!HasSelection) return null;

            var start = Math.Min(_selectionStart.Value, _selectionStop.Value);
            var length = SelectionLength;
            var bytes = new byte[length];

            for (long i = 0; i < length; i++)
            {
                bytes[i] = GetByteAt(new VirtualPosition(start + i));
            }

            return bytes;
        }

        #endregion

        #region Public Methods - Selection

        /// <summary>
        /// Select all bytes
        /// </summary>
        public void SelectAll()
        {
            SelectionStart = VirtualPosition.Zero;
            SelectionStop = new VirtualPosition(VirtualLength - 1);
        }

        /// <summary>
        /// Clear selection
        /// </summary>
        public void ClearSelection()
        {
            SelectionStart = VirtualPosition.Invalid;
            SelectionStop = VirtualPosition.Invalid;
        }

        /// <summary>
        /// Set selection to a single byte at position
        /// </summary>
        public void SetSelection(VirtualPosition position)
        {
            if (position.Value < 0 || position.Value >= VirtualLength)
                return;

            SelectionStart = position;
            SelectionStop = position;
            OnPropertyChanged(nameof(SelectionLength));
        }

        /// <summary>
        /// Set selection range (for mouse drag) - optimized to update UI only once
        /// </summary>
        public void SetSelectionRange(VirtualPosition start, VirtualPosition stop)
        {
            // Clamp positions to valid range
            long minPos = Math.Max(0, Math.Min(start.Value, stop.Value));
            long maxPos = Math.Min(VirtualLength - 1, Math.Max(start.Value, stop.Value));

            var newStart = new VirtualPosition(minPos);
            var newStop = new VirtualPosition(maxPos);

            // Only update if changed to avoid unnecessary UI updates
            if (_selectionStart == newStart && _selectionStop == newStop)
                return;

            // Set backing fields directly to avoid double update
            _selectionStart = newStart;
            _selectionStop = newStop;

            // Notify changes (calls UpdateSelectionState once instead of twice)
            OnPropertyChanged(nameof(SelectionStart));
            OnPropertyChanged(nameof(SelectionStop));
            OnPropertyChanged(nameof(HasSelection));
            OnPropertyChanged(nameof(SelectionLength));
            UpdateSelectionState();
        }

        /// <summary>
        /// Extend selection from current start to new position (for Shift+Click)
        /// </summary>
        public void ExtendSelection(VirtualPosition position)
        {
            if (!SelectionStart.IsValid)
            {
                SetSelection(position);
                return;
            }

            if (position.Value < 0 || position.Value >= VirtualLength)
                return;

            SelectionStop = position;
            OnPropertyChanged(nameof(SelectionLength));
        }

        #endregion

        #region Public Methods - Undo/Redo

        /// <summary>
        /// Undo last operation (handles both ByteProvider modifications and ViewModel insertions)
        /// </summary>
        public void Undo()
        {
            // Prioritize ViewModel insert operations (most recent)
            if (_insertUndoStack.Count > 0)
            {
                var edit = _insertUndoStack.Pop();
                System.Diagnostics.Debug.WriteLine($"[UNDO INSERT] Undoing insertion at virtual pos {edit.VirtualPosition}, value 0x{edit.Value:X2}");

                // Remove from _insertedBytes dictionary
                _insertedBytes.Remove(edit.VirtualPosition);

                // Update position mapping (decrement insertion count)
                if (_insertions.ContainsKey(edit.PhysicalPosition))
                {
                    _insertions[edit.PhysicalPosition]--;
                    if (_insertions[edit.PhysicalPosition] <= 0)
                        _insertions.Remove(edit.PhysicalPosition);
                }

                // Push to redo stack
                _insertRedoStack.Push(edit);
                System.Diagnostics.Debug.WriteLine($"[UNDO INSERT] Insert undo complete, undo stack: {_insertUndoStack.Count}, redo stack: {_insertRedoStack.Count}");
            }
            else
            {
                // Delegate to ByteProvider for modifications
                long position = _undoRedoService.Undo(_provider);
                System.Diagnostics.Debug.WriteLine($"[UNDO] ByteProvider undo at position {position}");
            }

            ClearLineCache();
            RefreshVisibleLines();
            OnPropertyChanged(nameof(CanUndo));
            OnPropertyChanged(nameof(CanRedo));
            OnPropertyChanged(nameof(VirtualLength));
            OnPropertyChanged(nameof(FileLength));
        }

        /// <summary>
        /// Redo last undone operation (handles both ByteProvider modifications and ViewModel insertions)
        /// </summary>
        public void Redo()
        {
            // Prioritize ViewModel insert operations (most recent undone)
            if (_insertRedoStack.Count > 0)
            {
                var edit = _insertRedoStack.Pop();
                System.Diagnostics.Debug.WriteLine($"[REDO INSERT] Redoing insertion at virtual pos {edit.VirtualPosition}, value 0x{edit.Value:X2}");

                // Add back to _insertedBytes dictionary
                _insertedBytes[edit.VirtualPosition] = edit.Value;

                // Update position mapping (increment insertion count)
                if (_insertions.ContainsKey(edit.PhysicalPosition))
                    _insertions[edit.PhysicalPosition]++;
                else
                    _insertions[edit.PhysicalPosition] = 1;

                // Push back to undo stack
                _insertUndoStack.Push(edit);
                System.Diagnostics.Debug.WriteLine($"[REDO INSERT] Insert redo complete, undo stack: {_insertUndoStack.Count}, redo stack: {_insertRedoStack.Count}");
            }
            else
            {
                // Delegate to ByteProvider for modifications
                long position = _undoRedoService.Redo(_provider);
                System.Diagnostics.Debug.WriteLine($"[REDO] ByteProvider redo at position {position}");
            }

            ClearLineCache();
            RefreshVisibleLines();
            OnPropertyChanged(nameof(CanUndo));
            OnPropertyChanged(nameof(CanRedo));
            OnPropertyChanged(nameof(VirtualLength));
            OnPropertyChanged(nameof(FileLength));
        }

        #endregion

        #region Public Methods - Find/Replace (V1 Compatible)

        /// <summary>
        /// Find first occurrence of byte array
        /// </summary>
        public long FindFirst(byte[] data, long startPosition = 0)
        {
            return _findReplaceService.FindFirst(_provider, data, startPosition);
        }

        /// <summary>
        /// Find next occurrence from current position
        /// </summary>
        public long FindNext(byte[] data, long currentPosition)
        {
            return _findReplaceService.FindNext(_provider, data, currentPosition);
        }

        /// <summary>
        /// Find last occurrence of byte array
        /// </summary>
        public long FindLast(byte[] data, long startPosition = 0)
        {
            return _findReplaceService.FindLast(_provider, data, startPosition);
        }

        /// <summary>
        /// Find all occurrences of byte array
        /// </summary>
        public IEnumerable<long> FindAll(byte[] data, long startPosition = 0)
        {
            return _findReplaceService.FindAll(_provider, data, startPosition);
        }

        /// <summary>
        /// Replace first occurrence
        /// </summary>
        public long ReplaceFirst(byte[] findData, byte[] replaceData, long startPosition = 0, bool truncateLength = false)
        {
            var result = _findReplaceService.ReplaceFirst(_provider, findData, replaceData, startPosition, truncateLength, _readOnlyMode);
            if (result != -1)
            {
                _findReplaceService.ClearCache(); // Clear cache after modification
                ClearLineCache();
                RefreshVisibleLines();
            }
            return result;
        }

        /// <summary>
        /// Replace next occurrence
        /// </summary>
        public long ReplaceNext(byte[] findData, byte[] replaceData, long currentPosition, bool truncateLength = false)
        {
            var result = _findReplaceService.ReplaceNext(_provider, findData, replaceData, currentPosition, truncateLength, _readOnlyMode);
            if (result != -1)
            {
                _findReplaceService.ClearCache(); // Clear cache after modification
                ClearLineCache();
                RefreshVisibleLines();
            }
            return result;
        }

        /// <summary>
        /// Replace all occurrences
        /// </summary>
        public int ReplaceAll(byte[] findData, byte[] replaceData, bool truncateLength = false)
        {
            var positions = _findReplaceService.ReplaceAll(_provider, findData, replaceData, truncateLength, _readOnlyMode);
            int count = positions?.Count() ?? 0;

            if (count > 0)
            {
                _findReplaceService.ClearCache(); // Clear cache after modification
                ClearLineCache();
                RefreshVisibleLines();
            }

            return count;
        }

        /// <summary>
        /// Clear find/replace cache (call after data modifications)
        /// </summary>
        public void ClearFindCache()
        {
            _findReplaceService.ClearCache();
        }

        #endregion

        #region Public Methods - Byte Operations (V1 Compatible)

        /// <summary>
        /// Get byte value at virtual position (V1 compatible)
        /// </summary>
        /// <param name="position">Virtual position</param>
        /// <returns>Byte value at position, or 0 if position is invalid</returns>
        public byte GetByte(long position)
        {
            return GetByteAt(new VirtualPosition(position));
        }

        /// <summary>
        /// Set byte value at virtual position (V1 compatible)
        /// </summary>
        /// <param name="position">Virtual position</param>
        /// <param name="value">Byte value to set</param>
        public void SetByte(long position, byte value)
        {
            ModifyByte(new VirtualPosition(position), value);
        }

        /// <summary>
        /// Fill a range with a specific byte value (V1 compatible)
        /// </summary>
        /// <param name="value">Byte value to fill with</param>
        /// <param name="startPosition">Start position (virtual)</param>
        /// <param name="length">Number of bytes to fill</param>
        public void FillWithByte(byte value, long startPosition, long length)
        {
            if (ReadOnlyMode) return;
            if (startPosition < 0 || length <= 0) return;
            if (startPosition >= VirtualLength) return;

            // Convert virtual start position to physical
            var physicalStart = VirtualToPhysical(new VirtualPosition(startPosition));
            if (!physicalStart.IsValid) return;

            // Clamp length to available space
            long actualLength = Math.Min(length, VirtualLength - startPosition);

            // Fill using provider
            _provider.FillWithByte(physicalStart.Value, actualLength, value);

            // Refresh display
            ClearLineCache();
            RefreshVisibleLines();
        }

        #endregion

        #region Position Mapping

        /// <summary>
        /// Convert virtual position to physical position
        /// </summary>
        public PhysicalPosition VirtualToPhysical(VirtualPosition virtualPos)
        {
            if (!virtualPos.IsValid) return PhysicalPosition.Invalid;

            long physical = virtualPos.Value;

            // Subtract all insertions before this position
            foreach (var kvp in _insertions.OrderBy(x => x.Key))
            {
                if (kvp.Key <= physical)
                    physical -= kvp.Value;
                else
                    break;
            }

            // Add all deletions before this position (deleted bytes still in file)
            foreach (var kvp in _deletions.OrderBy(x => x.Key))
            {
                if (kvp.Key <= physical)
                    physical += kvp.Value;
                else
                    break;
            }

            return new PhysicalPosition(Math.Max(0, physical));
        }

        /// <summary>
        /// Convert physical position to virtual position
        /// </summary>
        public VirtualPosition PhysicalToVirtual(PhysicalPosition physicalPos)
        {
            if (!physicalPos.IsValid) return VirtualPosition.Invalid;

            long virtual_ = physicalPos.Value;

            // Add all insertions before this position
            foreach (var kvp in _insertions.OrderBy(x => x.Key))
            {
                if (kvp.Key <= virtual_)
                    virtual_ += kvp.Value;
                else
                    break;
            }

            // Subtract all deletions before this position
            foreach (var kvp in _deletions.OrderBy(x => x.Key))
            {
                if (kvp.Key <= virtual_)
                    virtual_ -= kvp.Value;
                else
                    break;
            }

            return new VirtualPosition(Math.Max(0, virtual_));
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Refresh visible lines based on scroll position (optimized with caching)
        /// </summary>
        private void RefreshVisibleLines()
        {
            if (_suppressRefresh) return;

            var startLine = ScrollPosition;
            var endLine = Math.Min(startLine + VisibleLines, TotalLines);

            // Check if we can do incremental update (small scroll)
            bool canDoIncrementalUpdate = _lastScrollStart >= 0 &&
                                         Math.Abs(startLine - _lastScrollStart) <= 10 && // Increased tolerance
                                         Lines.Count > 0;

            if (canDoIncrementalUpdate)
            {
                // Incremental update: reuse existing lines where possible
                RefreshVisibleLinesIncremental(startLine, endLine);
            }
            else
            {
                // Full refresh: clear Lines but KEEP cache for reuse
                Lines.Clear();
                // OPTIMIZATION: Don't clear _lineCache here - let GetOrCreateLine use cached lines!
                // _lineCache.Clear(); // REMOVED - cache is cleared only by ClearLineCache() when data changes

                for (long lineNum = startLine; lineNum < endLine; lineNum++)
                {
                    var line = GetOrCreateLine(lineNum);
                    Lines.Add(line);
                }
            }

            _lastScrollStart = startLine;
            _lastScrollEnd = endLine;

            OnPropertyChanged(nameof(VirtualLength));
            OnPropertyChanged(nameof(TotalLines));
        }

        /// <summary>
        /// Incremental refresh for small scrolls (reuses existing lines)
        /// </summary>
        private void RefreshVisibleLinesIncremental(long newStart, long newEnd)
        {
            long oldStart = _lastScrollStart;
            long oldEnd = _lastScrollEnd;

            if (newStart > oldStart)
            {
                // Scrolling down: remove lines from top, add to bottom
                int removeCount = (int)(newStart - oldStart);
                for (int i = 0; i < removeCount && Lines.Count > 0; i++)
                {
                    Lines.RemoveAt(0);
                }

                // Add new lines at bottom
                for (long lineNum = oldEnd; lineNum < newEnd; lineNum++)
                {
                    var line = GetOrCreateLine(lineNum);
                    Lines.Add(line);
                }
            }
            else if (newStart < oldStart)
            {
                // Scrolling up: add lines at top, remove from bottom
                for (long lineNum = newStart; lineNum < oldStart; lineNum++)
                {
                    var line = GetOrCreateLine(lineNum);
                    Lines.Insert((int)(lineNum - newStart), line);
                }

                // Remove lines from bottom
                while (Lines.Count > (newEnd - newStart))
                {
                    Lines.RemoveAt(Lines.Count - 1);
                }
            }
            else
            {
                // newStart == oldStart: viewport size changed (resize), but scroll position unchanged
                if (newEnd > oldEnd)
                {
                    // Viewport got bigger: add lines at bottom
                    for (long lineNum = oldEnd; lineNum < newEnd; lineNum++)
                    {
                        var line = GetOrCreateLine(lineNum);
                        Lines.Add(line);
                    }
                }
                else if (newEnd < oldEnd)
                {
                    // Viewport got smaller: remove lines from bottom
                    while (Lines.Count > (newEnd - newStart))
                    {
                        Lines.RemoveAt(Lines.Count - 1);
                    }
                }
            }
        }

        /// <summary>
        /// Get line from cache or create new one
        /// </summary>
        private HexLine GetOrCreateLine(long lineNumber)
        {
            if (_lineCache.TryGetValue(lineNumber, out var cachedLine))
            {
                return cachedLine;
            }

            var line = CreateLine(lineNumber);

            // Cache with limit to prevent memory issues
            if (_lineCache.Count < 1000)
            {
                _lineCache[lineNumber] = line;
            }

            return line;
        }

        /// <summary>
        /// Clear line cache (call after data modifications)
        /// </summary>
        private void ClearLineCache()
        {
            _lineCache.Clear();
            _lastScrollStart = -1;
            _lastScrollEnd = -1;
        }

        /// <summary>
        /// Invalidate only the line containing the specified virtual position
        /// Much faster than ClearLineCache() for single-byte modifications
        /// </summary>
        private void InvalidateLineAtPosition(long virtualPos)
        {
            long lineNumber = virtualPos / BytePerLine;
            if (_lineCache.ContainsKey(lineNumber))
            {
                _lineCache.Remove(lineNumber);
            }
        }

        /// <summary>
        /// Refresh a specific line by recreating it (but cache is preserved)
        /// </summary>
        private void RefreshLine(long lineNumber)
        {
            // Find the line in currently displayed Lines collection
            int lineIndex = -1;
            for (int i = 0; i < Lines.Count; i++)
            {
                if (Lines[i].LineNumber == lineNumber)
                {
                    lineIndex = i;
                    break;
                }
            }

            if (lineIndex < 0) return; // Line not currently visible

            // Remove from cache to force recreation
            if (_lineCache.ContainsKey(lineNumber))
            {
                _lineCache.Remove(lineNumber);
            }

            // Recreate the line
            var newLine = GetOrCreateLine(lineNumber);

            // Replace in Lines collection
            Lines[lineIndex] = newLine;
        }

        /// <summary>
        /// Update only selection state on existing lines (much faster than full refresh)
        /// </summary>
        private void UpdateSelectionState()
        {
            // Cursor is at SelectionStop (the active end during Shift+navigation)
            // SelectionStart is the anchor point
            var cursorPos = _selectionStop.IsValid ? _selectionStop : _selectionStart;

            foreach (var line in Lines)
            {
                foreach (var byteData in line.Bytes)
                {
                    byteData.IsSelected = IsByteSelected(byteData.VirtualPos);
                    byteData.IsCursor = cursorPos.IsValid && byteData.VirtualPos == cursorPos;
                }
            }
        }

        /// <summary>
        /// Create a single hex line (optimized with batch reading)
        /// </summary>
        private HexLine CreateLine(long lineNumber)
        {
            var startVirtualPos = lineNumber * BytePerLine;
            var line = new HexLine
            {
                LineNumber = lineNumber,
                StartPosition = new VirtualPosition(startVirtualPos),
                OffsetLabel = $"0x{startVirtualPos:X8}"
            };

            var endPos = Math.Min(startVirtualPos + BytePerLine, VirtualLength);
            var lineLength = (int)(endPos - startVirtualPos);

            System.Diagnostics.Debug.WriteLine($"[CREATELINE] Line {lineNumber}: start={startVirtualPos}, endPos={endPos}, VirtualLength={VirtualLength}, expected bytes={lineLength}");

            // Batch read bytes for better performance
            int bytesAdded = 0;
            for (long virtualPos = startVirtualPos; virtualPos < endPos; virtualPos++)
            {
                var byteData = CreateByteDataOptimized(new VirtualPosition(virtualPos));
                if (byteData != null)
                {
                    line.Bytes.Add(byteData);
                    bytesAdded++;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[CREATELINE] NULL ByteData at virtualPos {virtualPos}!");
                }
            }

            System.Diagnostics.Debug.WriteLine($"[CREATELINE] Line {lineNumber}: Added {bytesAdded} bytes to line (expected {lineLength})");

            return line;
        }

        /// <summary>
        /// Create a single hex byte (optimized version with reduced provider calls)
        /// </summary>
        private ByteData CreateByteDataOptimized(VirtualPosition virtualPos)
        {
            var physicalPos = VirtualToPhysical(virtualPos);

            // Calculate cursor position (cursor is at the active end of selection)
            var cursorPos = _selectionStop.IsValid ? _selectionStop : _selectionStart;
            bool isCursor = cursorPos.IsValid && virtualPos == cursorPos;

            // Fast path: Check if this is an inserted byte first (check ViewModel dictionary)
            if (_insertedBytes.TryGetValue(virtualPos.Value, out byte insertedValue))
            {
                System.Diagnostics.Debug.WriteLine($"[CREATEBYTE] Virtual {virtualPos.Value}: INSERTED byte (from ViewModel), value=0x{insertedValue:X2}");
                return new ByteData
                {
                    VirtualPos = virtualPos,
                    PhysicalPos = null,
                    Value = insertedValue,
                    Action = ByteAction.Added,
                    IsSelected = IsByteSelected(virtualPos),
                    IsCursor = isCursor
                };
            }

            // Validate physical position
            if (!physicalPos.IsValid)
            {
                System.Diagnostics.Debug.WriteLine($"[CREATEBYTE] Virtual {virtualPos.Value}: Physical position INVALID");
                return null;
            }

            if (physicalPos.Value >= FileLength)
            {
                System.Diagnostics.Debug.WriteLine($"[CREATEBYTE] Virtual {virtualPos.Value}: Physical {physicalPos.Value} >= FileLength {FileLength}");
                return null;
            }

            // Read byte value
            var (byteValue, success) = _provider.GetByte(physicalPos.Value);
            if (!success || !byteValue.HasValue)
            {
                System.Diagnostics.Debug.WriteLine($"[CREATEBYTE] Virtual {virtualPos.Value}: Failed to read from physical {physicalPos.Value}");
                return null;
            }

            // Check if modified - use combined check to reduce calls
            var (modSuccess, modByte) = _provider.CheckIfIsByteModified(physicalPos.Value, ByteAction.Modified);

            return new ByteData
            {
                VirtualPos = virtualPos,
                PhysicalPos = physicalPos,
                Value = byteValue.Value,
                Action = modSuccess ? ByteAction.Modified : ByteAction.Nothing,
                IsSelected = IsByteSelected(virtualPos),
                IsCursor = isCursor
            };
        }

        /// <summary>
        /// Create a single hex byte (legacy method, kept for compatibility)
        /// </summary>
        private ByteData CreateByteData(VirtualPosition virtualPos)
        {
            return CreateByteDataOptimized(virtualPos);
        }

        /// <summary>
        /// Check if byte at virtual position is selected
        /// </summary>
        private bool IsByteSelected(VirtualPosition virtualPos)
        {
            if (!HasSelection) return false;

            var start = Math.Min(_selectionStart.Value, _selectionStop.Value);
            var end = Math.Max(_selectionStart.Value, _selectionStop.Value);

            return virtualPos.Value >= start && virtualPos.Value <= end;
        }

        // Note: ByteProvider doesn't expose DataChanged event
        // RefreshVisibleLines is called manually after each operation
        // If needed in future, could subscribe to Undone/Redone events

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            if (_suppressRefresh) return; // Batch mode: suppress notifications
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion

        #region Performance Helpers

        /// <summary>
        /// Begin batch operation (suppresses UI updates)
        /// </summary>
        public void BeginUpdate()
        {
            _suppressRefresh = true;
        }

        /// <summary>
        /// End batch operation (triggers single UI update)
        /// </summary>
        public void EndUpdate()
        {
            _suppressRefresh = false;
            RefreshVisibleLines();
        }

        #endregion

        #region Undo/Redo Internal Types

        /// <summary>
        /// Record of a single byte insertion operation for undo/redo
        /// </summary>
        private class InsertByteEdit
        {
            public long VirtualPosition { get; set; }
            public long PhysicalPosition { get; set; }
            public byte Value { get; set; }

            public InsertByteEdit(long virtualPos, long physicalPos, byte value)
            {
                VirtualPosition = virtualPos;
                PhysicalPosition = physicalPos;
                Value = value;
            }
        }

        #endregion
    }
}
