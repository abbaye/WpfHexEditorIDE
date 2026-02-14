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

        private WpfHexaEditor.V2.ByteProvider.ByteProvider _provider; // Not readonly - can be reassigned when saving with insertions
        private readonly UndoRedoService _undoRedoService = new();
        private readonly ClipboardService _clipboardService = new();
        private readonly SelectionService _selectionService = new();
        private readonly FindReplaceService _findReplaceService = new();

        /// <summary>
        /// Expose ByteProvider V2 for external configuration
        /// </summary>
        public WpfHexaEditor.V2.ByteProvider.ByteProvider Provider => _provider;

        // ByteProvider V2 handles insertions/deletions internally - no manual tracking needed!

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

                    // Force full refresh when BytePerLine changes (line structure completely different)
                    // Clear cache to prevent incremental update which can cause index errors
                    ClearLineCache();
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
        /// Total virtual length (file + inserted - deleted)
        /// ByteProvider V2 handles this calculation internally
        /// </summary>
        public long VirtualLength => _provider?.VirtualLength ?? 0;

        /// <summary>
        /// Total number of lines
        /// </summary>
        public long TotalLines => (VirtualLength + BytePerLine - 1) / BytePerLine;

        /// <summary>
        /// Visible lines (observable collection for UI binding)
        /// </summary>
        public ObservableCollection<HexLine> Lines { get; } = new();

        /// <summary>
        /// Can undo? (ByteProvider V2 handles undo internally)
        /// TODO: Implement undo/redo properly for ByteProvider V2
        /// </summary>
        public bool CanUndo => _provider?.CanUndo ?? false;

        /// <summary>
        /// Can redo? (ByteProvider V2 handles redo internally)
        /// TODO: Implement undo/redo properly for ByteProvider V2
        /// </summary>
        public bool CanRedo => _provider?.CanRedo ?? false;

        #endregion

        #region Constructor

        public HexEditorViewModel(WpfHexaEditor.V2.ByteProvider.ByteProvider provider)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));

            // Set default copy mode to ASCII string for normal copy/paste operations
            _clipboardService.DefaultCopyMode = CopyPasteMode.AsciiString;

            // ByteProvider V2 handles all edits internally with proper Virtual/Physical mapping
            // RefreshVisibleLines will be called manually after operations

            // STARTUP OPTIMIZATION: Don't call RefreshVisibleLines() here
            // It will be called later when the control is fully loaded and VisibleLines is properly set
            // RefreshVisibleLines();
        }

        #endregion

        #region Public Methods - File Operations

        /// <summary>
        /// Open a file for editing
        /// </summary>
        public static HexEditorViewModel OpenFile(string filePath)
        {
            var provider = new WpfHexaEditor.V2.ByteProvider.ByteProvider();
            provider.OpenFile(filePath);
            return new HexEditorViewModel(provider);
        }

        /// <summary>
        /// Save changes to file
        /// ByteProvider V2 handles all modifications/insertions/deletions internally
        /// </summary>
        public void Save()
        {
            // ByteProvider V2 handles everything internally
            _provider.SubmitChanges();

            System.Diagnostics.Debug.WriteLine($"[SAVE] File saved successfully");
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
            // TODO: ByteProvider V2 needs undo/redo implementation
            // _undoRedoService.ClearAll(_provider); // Needs ByteProvider V2 support
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
            // ByteProvider V2 works with virtual positions directly
            var (value, success) = _provider.GetByte(virtualPos.Value);
            return success ? value : (byte)0;
        }

        /// <summary>
        /// Modify byte at virtual position
        /// ByteProvider V2 handles all byte types (inserted/file/deleted) internally
        /// </summary>
        public void ModifyByte(VirtualPosition virtualPos, byte newValue)
        {
            if (ReadOnlyMode) return;

            // ByteProvider V2 handles all modifications internally
            _provider.ModifyByte(virtualPos.Value, newValue);
            System.Diagnostics.Debug.WriteLine($"[MODIFYBYTE] Modified byte at virtual pos {virtualPos.Value}: 0x{newValue:X2}");

            // Notify Undo/Redo state changed (ByteProvider handles undo for modifications)
            OnPropertyChanged(nameof(CanUndo));
            OnPropertyChanged(nameof(CanRedo));

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

            System.Diagnostics.Debug.WriteLine($"[INSERTBYTE] Inserting 0x{value:X2} at virtualPos={virtualPos.Value}");

            // ByteProvider V2 handles insertions internally with proper LIFO (stack-like) behavior
            _provider.InsertByte(virtualPos.Value, value);

            // TODO: Undo/Redo for insertions (needs to be reimplemented for ByteProvider V2)
            // For now, insertions are not undoable
            OnPropertyChanged(nameof(CanUndo));
            OnPropertyChanged(nameof(CanRedo));

            System.Diagnostics.Debug.WriteLine($"[INSERTBYTE] VirtualLength before refresh: {VirtualLength}");

            // OPTIMIZATION: Since insert shifts all following bytes, we need full refresh
            ClearLineCache();
            RefreshVisibleLines();

            System.Diagnostics.Debug.WriteLine($"[INSERTBYTE] VirtualLength after refresh: {VirtualLength}");
        }

        /// <summary>
        /// Insert multiple bytes at virtual position (OPTIMIZED for Paste/bulk operations)
        /// ByteProvider V2 handles batch insertion efficiently with proper LIFO ordering
        /// </summary>
        public void InsertBytes(VirtualPosition startVirtualPos, byte[] bytes)
        {
            if (ReadOnlyMode || EditMode != EditMode.Insert || bytes == null || bytes.Length == 0) return;

            System.Diagnostics.Debug.WriteLine($"[INSERTBYTES] === Batch insert START === {bytes.Length} bytes at pos {startVirtualPos.Value}");

            // ByteProvider V2 handles all insertion logic internally
            _provider.InsertBytes(startVirtualPos.Value, bytes);

            // TODO: Undo/Redo for insertions (needs to be reimplemented for ByteProvider V2)
            OnPropertyChanged(nameof(CanUndo));
            OnPropertyChanged(nameof(CanRedo));

            // ONE refresh at the end (not per byte!)
            ClearLineCache();
            RefreshVisibleLines();

            System.Diagnostics.Debug.WriteLine($"[INSERTBYTES] === Batch insert END === VirtualLength: {VirtualLength}");
        }

        /// <summary>
        /// Delete byte at virtual position
        /// ByteProvider V2 handles deletion internally
        /// </summary>
        public void DeleteByte(VirtualPosition virtualPos)
        {
            if (ReadOnlyMode) return;

            // ByteProvider V2 handles deletions internally
            _provider.DeleteByte(virtualPos.Value);

            // Notify Undo/Redo state changed
            OnPropertyChanged(nameof(CanUndo));
            OnPropertyChanged(nameof(CanRedo));

            // OPTIMIZATION: Since delete shifts all following bytes, we need full refresh
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
            var length = stop - start + 1;

            // Get bytes directly from ByteProvider V2 (works with virtual positions)
            var bytes = new byte[length];
            for (long i = 0; i < length; i++)
            {
                bytes[i] = GetByteAt(new VirtualPosition(start + i));
            }

            // Copy to clipboard (simple implementation - can be enhanced later)
            try
            {
                using (var ms = new MemoryStream(bytes))
                {
                    var dataObj = new System.Windows.DataObject();
                    dataObj.SetData("BinaryData", ms);
                    System.Windows.Clipboard.SetDataObject(dataObj, true);
                }
                return true;
            }
            catch
            {
                return false;
            }
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
                // Use optimized batch insert (MUCH faster than individual InsertByte() calls)
                var startVirtualPos = new VirtualPosition(pastePosition);
                InsertBytes(startVirtualPos, bytesToPaste);
                System.Diagnostics.Debug.WriteLine($"[PASTE INSERT] Batch inserted {bytesToPaste.Length} bytes at virtual pos {pastePosition} (green borders)");
            }
            else
            {
                // OVERWRITE MODE: Create ByteModified entries (orange borders)
                // Overwrite existing bytes directly (ByteProvider V2 works with virtual positions)
                for (int i = 0; i < bytesToPaste.Length; i++)
                {
                    ModifyByte(new VirtualPosition(pastePosition + i), bytesToPaste[i]);
                }
                System.Diagnostics.Debug.WriteLine($"[PASTE OVERWRITE] Modified {bytesToPaste.Length} bytes at virtual pos {pastePosition} (orange borders)");

                // Refresh for overwrite mode (Insert mode already refreshes in InsertBytes)
                ClearLineCache();
                RefreshVisibleLines();
            }

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
        /// Undo last operation
        /// </summary>
        public void Undo()
        {
            if (_provider == null || !CanUndo)
                return;

            // Call ByteProvider's Undo method
            _provider.Undo();

            // Refresh the display
            ClearLineCache();
            RefreshVisibleLines();

            // Notify properties changed
            OnPropertyChanged(nameof(CanUndo));
            OnPropertyChanged(nameof(CanRedo));
            OnPropertyChanged(nameof(VirtualLength));
            OnPropertyChanged(nameof(FileLength));
        }

        /// <summary>
        /// Redo last undone operation
        /// </summary>
        public void Redo()
        {
            if (_provider == null || !CanRedo)
                return;

            // Call ByteProvider's Redo method
            _provider.Redo();

            // Refresh the display
            ClearLineCache();
            RefreshVisibleLines();

            // Notify properties changed
            OnPropertyChanged(nameof(CanUndo));
            OnPropertyChanged(nameof(CanRedo));
            OnPropertyChanged(nameof(VirtualLength));
            OnPropertyChanged(nameof(FileLength));
        }

        #endregion

        #region Public Methods - Find/Replace (V1 Compatible)

        /// <summary>
        /// Find first occurrence of byte array
        /// TODO: Optimize with Boyer-Moore or similar algorithm
        /// </summary>
        public long FindFirst(byte[] data, long startPosition = 0)
        {
            if (data == null || data.Length == 0) return -1;

            long virtualLength = VirtualLength;
            for (long pos = startPosition; pos <= virtualLength - data.Length; pos++)
            {
                bool match = true;
                for (int i = 0; i < data.Length; i++)
                {
                    if (GetByteAt(new VirtualPosition(pos + i)) != data[i])
                    {
                        match = false;
                        break;
                    }
                }
                if (match) return pos;
            }
            return -1;
        }

        /// <summary>
        /// Find next occurrence from current position
        /// </summary>
        public long FindNext(byte[] data, long currentPosition)
        {
            return FindFirst(data, currentPosition + 1);
        }

        /// <summary>
        /// Find last occurrence of byte array
        /// TODO: Optimize by searching backwards
        /// </summary>
        public long FindLast(byte[] data, long startPosition = 0)
        {
            if (data == null || data.Length == 0) return -1;

            long lastFound = -1;
            long pos = startPosition;
            while ((pos = FindFirst(data, pos)) != -1)
            {
                lastFound = pos;
                pos++;
            }
            return lastFound;
        }

        /// <summary>
        /// Find all occurrences of byte array
        /// </summary>
        public IEnumerable<long> FindAll(byte[] data, long startPosition = 0)
        {
            if (data == null || data.Length == 0) yield break;

            long pos = startPosition;
            while ((pos = FindFirst(data, pos)) != -1)
            {
                yield return pos;
                pos++;
            }
        }

        /// <summary>
        /// Replace first occurrence
        /// </summary>
        public long ReplaceFirst(byte[] findData, byte[] replaceData, long startPosition = 0, bool truncateLength = false)
        {
            if (ReadOnlyMode) return -1;

            long pos = FindFirst(findData, startPosition);
            if (pos == -1) return -1;

            // Replace bytes
            for (int i = 0; i < replaceData.Length && i < findData.Length; i++)
            {
                ModifyByte(new VirtualPosition(pos + i), replaceData[i]);
            }

            ClearLineCache();
            RefreshVisibleLines();
            return pos;
        }

        /// <summary>
        /// Replace next occurrence
        /// </summary>
        public long ReplaceNext(byte[] findData, byte[] replaceData, long currentPosition, bool truncateLength = false)
        {
            return ReplaceFirst(findData, replaceData, currentPosition + 1, truncateLength);
        }

        /// <summary>
        /// Replace all occurrences
        /// OPTIMIZED: Batch modifications for better performance
        /// </summary>
        public int ReplaceAll(byte[] findData, byte[] replaceData, bool truncateLength = false)
        {
            if (ReadOnlyMode) return 0;

            var positions = FindAll(findData).ToList();

            if (positions.Count > 0)
            {
                // Use BeginUpdate to suppress display refresh during batch operations
                BeginUpdate();
                try
                {
                    foreach (var pos in positions)
                    {
                        // Use ModifyByte which won't refresh display due to BeginUpdate
                        for (int i = 0; i < replaceData.Length && i < findData.Length; i++)
                        {
                            ModifyByte(new VirtualPosition(pos + i), replaceData[i]);
                        }
                    }
                }
                finally
                {
                    // EndUpdate will refresh display once
                    EndUpdate();
                }
            }

            return positions.Count;
        }

        /// <summary>
        /// Clear find/replace cache (call after data modifications)
        /// </summary>
        public void ClearFindCache()
        {
            // No cache in this simple implementation
            // TODO: Implement caching for better performance
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

            // Clamp length to available space
            long actualLength = Math.Min(length, VirtualLength - startPosition);

            // Fill each byte (ByteProvider V2 works with virtual positions)
            for (long i = 0; i < actualLength; i++)
            {
                ModifyByte(new VirtualPosition(startPosition + i), value);
            }

            // Refresh display
            ClearLineCache();
            RefreshVisibleLines();
        }

        #endregion

        #region Position Mapping

        /// <summary>
        /// Convert virtual position to physical position
        /// NOTE: ByteProvider V2 handles virtual/physical mapping internally.
        /// This method is kept for compatibility but may not be needed.
        /// </summary>
        public PhysicalPosition VirtualToPhysical(VirtualPosition virtualPos)
        {
            // TODO: ByteProvider V2 integration - determine if this is still needed
            // For now, return the same position (1:1 mapping as placeholder)
            if (!virtualPos.IsValid) return PhysicalPosition.Invalid;
            return new PhysicalPosition(virtualPos.Value);
        }

        /// <summary>
        /// Convert physical position to virtual position
        /// NOTE: ByteProvider V2 handles virtual/physical mapping internally.
        /// This method is kept for compatibility but may not be needed.
        /// </summary>
        public VirtualPosition PhysicalToVirtual(PhysicalPosition physicalPos)
        {
            // TODO: ByteProvider V2 integration - determine if this is still needed
            // For now, return the same position (1:1 mapping as placeholder)
            if (!physicalPos.IsValid) return VirtualPosition.Invalid;
            return new VirtualPosition(physicalPos.Value);
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

                // Remove lines from bottom (with safety check)
                while (Lines.Count > 0 && Lines.Count > (newEnd - newStart))
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
                    // Viewport got smaller: remove lines from bottom (with safety check)
                    System.Diagnostics.Debug.WriteLine($"[REFRESH] Viewport got smaller: remove lines from bottom (Lines.Count={Lines.Count}, newEnd={newEnd}, newStart={newStart}, target={(newEnd - newStart)})");
                    while (Lines.Count > 0 && Lines.Count > (newEnd - newStart))
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
        /// OPTIMIZED: Use GetBytes() to read entire line at once instead of byte-by-byte
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

            if (lineLength <= 0) return line;

            // OPTIMIZATION: Read entire line at once using ByteProvider V2's GetBytes()
            byte[] lineBytes = _provider.GetBytes(startVirtualPos, lineLength);

            // Calculate cursor position once
            var cursorPos = _selectionStop.IsValid ? _selectionStop : _selectionStart;

            // Create ByteData for each byte in the line
            for (int i = 0; i < lineBytes.Length; i++)
            {
                long virtualPos = startVirtualPos + i;
                var byteData = new ByteData
                {
                    VirtualPos = new VirtualPosition(virtualPos),
                    PhysicalPos = null, // ByteProvider V2 handles mapping internally
                    Value = lineBytes[i],
                    Action = _provider.GetByteAction(virtualPos),
                    IsSelected = IsByteSelected(new VirtualPosition(virtualPos)),
                    IsCursor = cursorPos.IsValid && virtualPos == cursorPos.Value
                };
                line.Bytes.Add(byteData);
            }

            return line;
        }

        // REMOVED: CreateByteDataOptimized() and CreateByteData()
        // These methods read one byte at a time which is extremely slow
        // Now we use GetBytes() in CreateLine() to read entire lines at once

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
        /// Begin batch operation (suppresses UI updates and provider cache invalidations)
        /// OPTIMIZED: Also tells ByteProvider to defer cache invalidations
        /// </summary>
        public void BeginUpdate()
        {
            _suppressRefresh = true;
            _provider?.BeginBatch();
        }

        /// <summary>
        /// End batch operation (triggers single UI update and cache invalidation)
        /// OPTIMIZED: Invalidates provider caches and refreshes display once
        /// </summary>
        public void EndUpdate()
        {
            _provider?.EndBatch();
            _suppressRefresh = false;
            ClearLineCache(); // Clear ViewModel line cache
            RefreshVisibleLines(); // Refresh display once
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
