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

        // Position mapping: tracks insertions/deletions for Virtual ↔ Physical conversion
        private readonly Dictionary<long, long> _insertions = new(); // physicalPos → count
        private readonly Dictionary<long, long> _deletions = new();  // physicalPos → count

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
        /// Can undo?
        /// </summary>
        public bool CanUndo => _undoRedoService.CanUndo(_provider);

        /// <summary>
        /// Can redo?
        /// </summary>
        public bool CanRedo => _undoRedoService.CanRedo(_provider);

        #endregion

        #region Constructor

        public HexEditorViewModel(ByteProvider provider)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));

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
        /// Close file and cleanup
        /// </summary>
        public void Close()
        {
            _provider?.Close();
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
        /// </summary>
        public void ModifyByte(VirtualPosition virtualPos, byte newValue)
        {
            if (ReadOnlyMode) return;

            var physicalPos = VirtualToPhysical(virtualPos);
            if (!physicalPos.IsValid) return;

            _provider.AddByteModified(newValue, physicalPos.Value);

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

            // Add byte to insertions dictionary
            _provider.AddByteAdded(value, virtualPos.Value);

            // Track insertion for position mapping
            if (_insertions.ContainsKey(physicalPos.Value))
                _insertions[physicalPos.Value]++;
            else
                _insertions[physicalPos.Value] = 1;

            // OPTIMIZATION: Since insert shifts all following bytes, we need full refresh
            // But we can still use incremental update if scrolling is stable
            ClearLineCache();
            RefreshVisibleLines();
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
            if (ReadOnlyMode || !_selectionStart.IsValid) return false;

            // Get data from clipboard
            string clipboardText = System.Windows.Clipboard.GetText();
            if (string.IsNullOrEmpty(clipboardText)) return false;

            var startPos = _selectionStart;

            // If we have a selection, delete it first
            if (HasSelection)
            {
                DeleteSelection();
                startPos = new VirtualPosition(Math.Min(_selectionStart.Value, _selectionStop.Value));
            }

            // Convert virtual position to physical
            var physicalStart = VirtualToPhysical(startPos);
            if (!physicalStart.IsValid) return false;

            // Use ByteProvider's paste method
            // expend=false means don't expand file beyond current length
            _provider.PasteNotInsert(physicalStart.Value, clipboardText);

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
        /// Undo last operation
        /// </summary>
        public void Undo()
        {
            _undoRedoService.Undo(_provider);
            ClearLineCache();
            RefreshVisibleLines();
            OnPropertyChanged(nameof(CanUndo));
            OnPropertyChanged(nameof(CanRedo));
        }

        /// <summary>
        /// Redo last undone operation
        /// </summary>
        public void Redo()
        {
            _undoRedoService.Redo(_provider);
            ClearLineCache();
            RefreshVisibleLines();
            OnPropertyChanged(nameof(CanUndo));
            OnPropertyChanged(nameof(CanRedo));
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
            foreach (var line in Lines)
            {
                foreach (var byteData in line.Bytes)
                {
                    byteData.IsSelected = IsByteSelected(byteData.VirtualPos);
                    byteData.IsCursor = _selectionStart.IsValid && byteData.VirtualPos == _selectionStart;
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

            // Batch read bytes for better performance
            for (long virtualPos = startVirtualPos; virtualPos < endPos; virtualPos++)
            {
                var byteData = CreateByteDataOptimized(new VirtualPosition(virtualPos));
                if (byteData != null)
                    line.Bytes.Add(byteData);
            }

            return line;
        }

        /// <summary>
        /// Create a single hex byte (optimized version with reduced provider calls)
        /// </summary>
        private ByteData CreateByteDataOptimized(VirtualPosition virtualPos)
        {
            var physicalPos = VirtualToPhysical(virtualPos);

            // Fast path: Check if this is an inserted byte first
            var (addedSuccess, addedByte) = _provider.CheckIfIsByteModified(virtualPos.Value, ByteAction.Added);
            if (addedSuccess && addedByte.Byte.HasValue)
            {
                return new ByteData
                {
                    VirtualPos = virtualPos,
                    PhysicalPos = null,
                    Value = addedByte.Byte.Value,
                    Action = ByteAction.Added,
                    IsSelected = IsByteSelected(virtualPos)
                };
            }

            // Validate physical position
            if (!physicalPos.IsValid || physicalPos.Value >= FileLength)
                return null;

            // Read byte value
            var (byteValue, success) = _provider.GetByte(physicalPos.Value);
            if (!success || !byteValue.HasValue)
                return null;

            // Check if modified - use combined check to reduce calls
            var (modSuccess, modByte) = _provider.CheckIfIsByteModified(physicalPos.Value, ByteAction.Modified);

            return new ByteData
            {
                VirtualPos = virtualPos,
                PhysicalPos = physicalPos,
                Value = byteValue.Value,
                Action = modSuccess ? ByteAction.Modified : ByteAction.Nothing,
                IsSelected = IsByteSelected(virtualPos)
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
    }
}
