//////////////////////////////////////////////
// Apache 2.0  - 2016-2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5
// Sample demonstrating service usage
//////////////////////////////////////////////

using System;
using System.IO;
using System.Linq;
using System.Windows.Media;
using WpfHexaEditor.Core;
using WpfHexaEditor.Core.Bytes;
using WpfHexaEditor.Core.CharacterTable;
using WpfHexaEditor.Services;

namespace WpfHexEditor.Sample.ServiceUsage
{
    /// <summary>
    /// Sample demonstrating direct usage of the 10 services in WPFHexaEditor
    /// This shows how to use services without the HexEditor UI control
    /// </summary>
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("=".PadRight(80, '='));
            Console.WriteLine("WPFHexaEditor - Service Usage Sample");
            Console.WriteLine("Demonstrates using all 10 services directly");
            Console.WriteLine("=".PadRight(80, '='));
            Console.WriteLine();

            // Create a sample file for testing
            var testFile = Path.GetTempFileName();
            CreateSampleFile(testFile);

            // Initialize ByteProvider (automatically opens the file)
            using (var provider = new ByteProviderLegacy(testFile))
            {
                Console.WriteLine($"Opened file: {testFile}");
                Console.WriteLine($"File size: {provider.Length} bytes");
                Console.WriteLine();

                // Demo each service
                Demo1_SelectionService(provider);
                Demo2_FindReplaceService(provider);
                Demo3_ClipboardService(provider);
                Demo4_HighlightService();
                Demo5_ByteModificationService(provider);
                Demo6_UndoRedoService(provider);
                Demo7_BookmarkService();
                Demo8_CustomBackgroundService();
                Demo9_PositionService(provider);
                Demo10_TblService();

                Console.WriteLine();
                Console.WriteLine("=".PadRight(80, '='));
                Console.WriteLine("All service demos completed successfully!");
                Console.WriteLine("=".PadRight(80, '='));
            }

            // Cleanup after provider is disposed
            try
            {
                if (File.Exists(testFile))
                    File.Delete(testFile);
            }
            catch
            {
                // Ignore cleanup errors
            }

            Console.WriteLine();
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }

        static void CreateSampleFile(string path)
        {
            // Create a file with some recognizable patterns
            var data = new byte[1024];

            // Header: "SAMPLE" + version
            var header = new byte[] { 0x53, 0x41, 0x4D, 0x50, 0x4C, 0x45, 0x01, 0x00 };
            Array.Copy(header, 0, data, 0, header.Length);

            // Fill with pattern
            for (int i = 8; i < data.Length; i++)
            {
                data[i] = (byte)(i % 256);
            }

            // Add some searchable patterns
            var pattern1 = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };
            Array.Copy(pattern1, 0, data, 100, pattern1.Length);
            Array.Copy(pattern1, 0, data, 200, pattern1.Length);
            Array.Copy(pattern1, 0, data, 300, pattern1.Length);

            File.WriteAllBytes(path, data);
        }

        static void Demo1_SelectionService(ByteProviderLegacy provider)
        {
            Console.WriteLine("--- Demo 1: SelectionService ---");

            var selectionService = new SelectionService();

            // Test selection validation
            long selectionStart = 10;
            long selectionStop = 50;

            if (selectionService.IsValidSelection(selectionStart, selectionStop))
            {
                long length = selectionService.GetSelectionLength(selectionStart, selectionStop);
                Console.WriteLine($"Selection is valid: {selectionStart} - {selectionStop} ({length} bytes)");
            }

            // Get selection bytes
            var selectionBytes = selectionService.GetSelectionBytes(provider, selectionStart, selectionStop);
            if (selectionBytes != null)
            {
                Console.WriteLine($"Retrieved {selectionBytes.Length} bytes from selection");
                Console.WriteLine($"First 8 bytes: {BitConverter.ToString(selectionBytes.Take(8).ToArray())}");
            }

            // Fix inverted selection
            var (fixedStart, fixedStop) = selectionService.FixSelectionRange(selectionStop, selectionStart);
            Console.WriteLine($"Fixed inverted selection: {fixedStart} - {fixedStop}");

            // Validate and adjust to bounds
            var (validStart, validStop) = selectionService.ValidateSelection(provider, -10, provider.Length + 100);
            Console.WriteLine($"Validated out-of-bounds selection: {validStart} - {validStop}");

            Console.WriteLine();
        }

        static void Demo2_FindReplaceService(ByteProviderLegacy provider)
        {
            Console.WriteLine("--- Demo 2: FindReplaceService ---");

            var findService = new FindReplaceService();

            // Search for pattern
            var searchPattern = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };
            Console.WriteLine($"Searching for pattern: {BitConverter.ToString(searchPattern)}");

            // Find first
            long firstPos = findService.FindFirst(provider, searchPattern);
            Console.WriteLine($"First occurrence at: 0x{firstPos:X}");

            // Find all
            var allPositions = findService.FindAll(provider, searchPattern);
            if (allPositions != null)
            {
                var positions = allPositions.ToList();
                Console.WriteLine($"Total occurrences: {positions.Count}");
                Console.WriteLine($"Positions: {string.Join(", ", positions.Select(p => $"0x{p:X}"))}");
            }

            // Find with cache
            var cachedResults = findService.FindAllCached(provider, searchPattern);
            Console.WriteLine($"Cached search returned {cachedResults?.Count()} results");

            // Find last
            long lastPos = findService.FindLast(provider, searchPattern);
            Console.WriteLine($"Last occurrence at: 0x{lastPos:X}");

            // Clear cache
            findService.ClearCache();
            Console.WriteLine("Search cache cleared");

            Console.WriteLine();
        }

        static void Demo3_ClipboardService(ByteProviderLegacy provider)
        {
            Console.WriteLine("--- Demo 3: ClipboardService ---");

            var clipboardService = new ClipboardService
            {
                DefaultCopyMode = CopyPasteMode.HexaString
            };

            long selectionStart = 0;
            long selectionStop = 8;

            // Check if can copy
            long selectionLength = selectionStop - selectionStart + 1;
            if (clipboardService.CanCopy(selectionLength, provider))
            {
                Console.WriteLine("Copy operation is allowed");

                // Get copy data
                var copyData = clipboardService.GetCopyData(provider, selectionStart, selectionStop, false);
                if (copyData != null)
                {
                    Console.WriteLine($"Copy data prepared: {copyData.Length} bytes");
                    Console.WriteLine($"As hex string: {BitConverter.ToString(copyData)}");
                }
            }

            // Check if can delete
            if (clipboardService.CanDelete(selectionLength, provider, false, true))
            {
                Console.WriteLine("Delete operation is allowed");
            }

            Console.WriteLine();
        }

        static void Demo4_HighlightService()
        {
            Console.WriteLine("--- Demo 4: HighlightService ---");

            var highlightService = new HighlightService();

            // Add highlights
            highlightService.AddHighLight(100, 4);
            highlightService.AddHighLight(200, 4);
            highlightService.AddHighLight(300, 4);

            Console.WriteLine($"Total highlights: {highlightService.GetHighlightCount()}");

            // Check specific position
            if (highlightService.IsHighlighted(102))
            {
                Console.WriteLine("Position 102 is highlighted");
            }

            // Get highlighted ranges
            var ranges = highlightService.GetHighlightedRanges();
            Console.WriteLine($"Highlighted ranges: {ranges.Count()}");
            foreach (var (start, length) in ranges)
            {
                Console.WriteLine($"  - 0x{start:X}: {length} bytes");
            }

            // Clear all highlights
            highlightService.UnHighLightAll();
            Console.WriteLine($"Highlights after clear: {highlightService.GetHighlightCount()}");

            Console.WriteLine();
        }

        static void Demo5_ByteModificationService(ByteProviderLegacy provider)
        {
            Console.WriteLine("--- Demo 5: ByteModificationService ---");

            var modService = new ByteModificationService();

            // Check if can modify
            if (modService.CanModify(provider, false))
            {
                Console.WriteLine("Modification is allowed");

                // Modify a byte
                bool modified = modService.ModifyByte(provider, 0xFF, 500, 1, false);
                Console.WriteLine($"Modified byte at position 500: {modified}");
            }

            // Check if can insert
            if (modService.CanInsert(provider, false))
            {
                Console.WriteLine("Insertion is allowed");

                // Insert bytes
                var insertData = new byte[] { 0x11, 0x22, 0x33 };
                int inserted = modService.InsertBytes(provider, insertData, 510, false);
                Console.WriteLine($"Inserted {inserted} bytes at position 510");
            }

            // Check if can delete
            if (modService.CanDelete(provider, false, true))
            {
                Console.WriteLine("Deletion is allowed");

                // Delete bytes
                long lastDeletedPos = modService.DeleteBytes(provider, 520, 5, false, true);
                Console.WriteLine($"Deleted 5 bytes, last deleted position: 0x{lastDeletedPos:X}");
            }

            Console.WriteLine();
        }

        static void Demo6_UndoRedoService(ByteProviderLegacy provider)
        {
            Console.WriteLine("--- Demo 6: UndoRedoService ---");

            var undoService = new UndoRedoService();
            var modService = new ByteModificationService();

            // Make some modifications first using ByteModificationService
            modService.ModifyByte(provider, 0xAA, 600, 1, false);
            modService.ModifyByte(provider, 0xBB, 601, 1, false);

            Console.WriteLine($"Undo count: {undoService.GetUndoCount(provider)}");

            // Check if can undo
            if (undoService.CanUndo(provider))
            {
                Console.WriteLine("Undo is available");

                // Perform undo
                long undoPosition = undoService.Undo(provider);
                Console.WriteLine($"Undo performed, affected position: 0x{undoPosition:X}");
            }

            // Check if can redo
            if (undoService.CanRedo(provider))
            {
                Console.WriteLine("Redo is available");

                // Perform redo
                long redoPosition = undoService.Redo(provider);
                Console.WriteLine($"Redo performed, affected position: 0x{redoPosition:X}");
            }

            // Clear all history
            undoService.ClearAll(provider);
            Console.WriteLine($"Undo count after clear: {undoService.GetUndoCount(provider)}");

            Console.WriteLine();
        }

        static void Demo7_BookmarkService()
        {
            Console.WriteLine("--- Demo 7: BookmarkService ---");

            var bookmarkService = new BookmarkService();

            // Add bookmarks
            bookmarkService.AddBookmark(100, "Header start", ScrollMarker.Bookmark);
            bookmarkService.AddBookmark(200, "Data section", ScrollMarker.Bookmark);
            bookmarkService.AddBookmark(300, "Footer", ScrollMarker.Bookmark);
            bookmarkService.AddBookmark(150, "Search result 1", ScrollMarker.SearchHighLight);

            Console.WriteLine($"Total bookmarks: {bookmarkService.GetAllBookmarks().Count()}");

            // Get bookmark at position
            var bookmark = bookmarkService.GetBookmarkAt(200);
            if (bookmark != null)
            {
                Console.WriteLine($"Bookmark at 200: '{bookmark.Description}'");
            }

            // Navigate bookmarks
            var nextBookmark = bookmarkService.GetNextBookmark(150);
            if (nextBookmark != null)
            {
                Console.WriteLine($"Next bookmark after 150: {nextBookmark.BytePositionInStream} - '{nextBookmark.Description}'");
            }

            // Get bookmarks by type
            var searchBookmarks = bookmarkService.GetBookmarksByMarker(ScrollMarker.SearchHighLight);
            Console.WriteLine($"Search highlight bookmarks: {searchBookmarks.Count()}");

            // Clear all
            bookmarkService.ClearAll();
            Console.WriteLine($"Bookmarks after clear: {bookmarkService.GetAllBookmarks().Count()}");

            Console.WriteLine();
        }

        static void Demo8_CustomBackgroundService()
        {
            Console.WriteLine("--- Demo 8: CustomBackgroundService ---");

            var backgroundService = new CustomBackgroundService();

            // Add background blocks
            backgroundService.AddBlock(0, 64, Brushes.LightBlue, "Header");
            backgroundService.AddBlock(64, 128, Brushes.LightGreen, "Data");
            backgroundService.AddBlock(192, 64, Brushes.LightYellow, "Footer");

            Console.WriteLine($"Total blocks: {backgroundService.GetBlockCount()}");

            // Get block at position
            var block = backgroundService.GetBlockAt(80);
            if (block != null)
            {
                Console.WriteLine($"Block at position 80: '{block.Description}' (0x{block.StartOffset:X} - 0x{block.StopOffset:X})");
            }

            // Check for overlaps
            bool wouldOverlap = backgroundService.WouldOverlap(50, 30);
            Console.WriteLine($"Block at 50-80 would overlap: {wouldOverlap}");

            // Get blocks in range
            var blocksInRange = backgroundService.GetBlocksInRange(60, 200);
            Console.WriteLine($"Blocks in range 60-200: {blocksInRange.Count()}");
            foreach (var b in blocksInRange)
            {
                Console.WriteLine($"  - {b.Description}: 0x{b.StartOffset:X} - 0x{b.StopOffset:X}");
            }

            // Clear all
            backgroundService.ClearAll();
            Console.WriteLine($"Blocks after clear: {backgroundService.GetBlockCount()}");

            Console.WriteLine();
        }

        static void Demo9_PositionService(ByteProviderLegacy provider)
        {
            Console.WriteLine("--- Demo 9: PositionService ---");

            var positionService = new PositionService();

            long testPosition = 128;
            int bytePerLine = 16;

            // Get line number
            long lineNumber = positionService.GetLineNumber(testPosition, 0, false, bytePerLine, 1, provider);
            Console.WriteLine($"Position {testPosition} is on line: {lineNumber}");

            // Get column number
            long columnNumber = positionService.GetColumnNumber(testPosition, false, false, 0, 0, bytePerLine, provider);
            Console.WriteLine($"Position {testPosition} is at column: {columnNumber}");

            // Hex conversion
            string hexString = positionService.LongToHex(testPosition);
            Console.WriteLine($"Position as hex: {hexString}");

            // Parse hex string
            var (success, parsedPosition) = positionService.HexLiteralToLong("0x80");
            if (success)
            {
                Console.WriteLine($"Parsed '0x80' to: {parsedPosition}");
            }

            // Validate position
            bool isValid = positionService.IsPositionValid(testPosition, provider.Length);
            Console.WriteLine($"Position {testPosition} is valid: {isValid}");

            // Clamp position
            long clampedPosition = positionService.ClampPosition(9999, 0, provider.Length);
            Console.WriteLine($"Clamped position 9999 to valid range: {clampedPosition}");

            Console.WriteLine();
        }

        static void Demo10_TblService()
        {
            Console.WriteLine("--- Demo 10: TblService ---");

            var tblService = new TblService();

            // Load default ASCII table
            if (tblService.LoadDefault(DefaultCharacterTableType.Ascii))
            {
                Console.WriteLine("Loaded default ASCII table");

                // Convert bytes to string
                var testBytes = new byte[] { 0x48, 0x65, 0x6C, 0x6C, 0x6F }; // "Hello"
                string text = tblService.BytesToString(testBytes);
                Console.WriteLine($"Bytes to string: {text}");

                // Get table info
                var tableInfo = tblService.GetTableInfo();
                Console.WriteLine($"Table type: {tableInfo}");
            }

            // Check for bookmarks in TBL
            if (tblService.HasBookmarks())
            {
                Console.WriteLine($"TBL has {tblService.GetBookmarkCount()} bookmarks");
            }
            else
            {
                Console.WriteLine("TBL has no bookmarks");
            }

            // Clear TBL
            tblService.Clear();
            Console.WriteLine("TBL cleared");

            Console.WriteLine();
        }
    }
}
