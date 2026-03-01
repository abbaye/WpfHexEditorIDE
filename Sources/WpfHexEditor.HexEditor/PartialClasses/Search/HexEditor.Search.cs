//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5, Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Collections.Generic;
using System.Linq;
using WpfHexEditor.Core.Models;

namespace WpfHexEditor.HexEditor
{
    /// <summary>
    /// HexEditor partial class - Search and Replace Operations
    /// Contains methods for finding and replacing byte patterns
    /// </summary>
    public partial class HexEditor
    {
        #region Public Methods - Find/Replace

        /// <summary>
        /// Find first occurrence of byte array
        /// </summary>
        /// <param name="data">Byte pattern to search for</param>
        /// <param name="startPosition">Position to start search from (default: 0)</param>
        /// <returns>Position of first occurrence, or -1 if not found</returns>
        public long FindFirst(byte[] data, long startPosition = 0)
        {
            if (_viewModel == null) return -1;
            return _viewModel.FindFirst(data, startPosition);
        }

        /// <summary>
        /// Find next occurrence after current position
        /// </summary>
        /// <param name="data">Byte pattern to search for</param>
        /// <param name="currentPosition">Current position (search starts at currentPosition + 1)</param>
        /// <returns>Position of next occurrence, or -1 if not found</returns>
        public long FindNext(byte[] data, long currentPosition)
        {
            if (_viewModel == null) return -1;
            return _viewModel.FindNext(data, currentPosition);
        }

        /// <summary>
        /// Find last occurrence of byte array
        /// </summary>
        /// <param name="data">Byte pattern to search for</param>
        /// <param name="startPosition">Position to start search from (default: 0)</param>
        /// <returns>Position of last occurrence, or -1 if not found</returns>
        public long FindLast(byte[] data, long startPosition = 0)
        {
            if (_viewModel == null) return -1;
            return _viewModel.FindLast(data, startPosition);
        }

        /// <summary>
        /// Find all occurrences of byte array
        /// </summary>
        /// <param name="data">Byte pattern to search for</param>
        /// <param name="startPosition">Position to start search from (default: 0)</param>
        /// <returns>Enumerable of positions where pattern was found, or null if not found</returns>
        public IEnumerable<long> FindAll(byte[] data, long startPosition = 0)
        {
            if (_viewModel == null) return null;
            return _viewModel.FindAll(data, startPosition);
        }

        /// <summary>
        /// Count occurrences of byte pattern without storing positions (memory efficient)
        /// </summary>
        /// <param name="data">Byte pattern to count</param>
        /// <param name="startPosition">Position to start search from (default: 0)</param>
        /// <returns>Number of occurrences found</returns>
        /// <remarks>
        /// This method is more memory-efficient than FindAll().Count() for large files,
        /// as it counts occurrences without storing all their positions.
        ///
        /// Example:
        /// <code>
        /// var pattern = new byte[] { 0xFF, 0xFE };
        /// int count = hexEditor.CountOccurrences(pattern);
        /// Console.WriteLine($"Found {count} occurrences of pattern");
        /// </code>
        ///
        /// Performance comparison for large files (1GB with 100K matches):
        /// - FindAll().Count(): ~800KB memory allocated
        /// - CountOccurrences(): ~0KB memory allocated
        /// </remarks>
        public int CountOccurrences(byte[] data, long startPosition = 0)
        {
            if (_viewModel?.Provider == null || data == null || data.Length == 0) return 0;
            return _viewModel.Provider.CountOccurrences(data, startPosition);
        }

        /// <summary>
        /// Set selection to a specific range (used after find operations)
        /// </summary>
        /// <param name="position">Start position</param>
        /// <param name="length">Selection length in bytes</param>
        public void FindSelect(long position, long length)
        {
            if (_viewModel == null) return;
            if (position < 0 || length <= 0) return;

            var start = new VirtualPosition(position);
            var stop = new VirtualPosition(position + length - 1);

            _viewModel.SetSelectionRange(start, stop);

            // Scroll to make selection visible
            EnsurePositionVisible(start);
        }

        /// <summary>
        /// Replace first occurrence of findData with replaceData
        /// </summary>
        /// <param name="findData">Byte pattern to find</param>
        /// <param name="replaceData">Byte pattern to replace with</param>
        /// <param name="startPosition">Position to start search from (default: 0)</param>
        /// <param name="truncateLength">If true, truncate replaceData to match findData length</param>
        /// <returns>Position where replacement occurred, or -1 if pattern not found</returns>
        public long ReplaceFirst(byte[] findData, byte[] replaceData, long startPosition = 0, bool truncateLength = false)
        {
            if (_viewModel == null) return -1;
            return _viewModel.ReplaceFirst(findData, replaceData, startPosition, truncateLength);
        }

        /// <summary>
        /// Replace next occurrence after current position
        /// </summary>
        /// <param name="findData">Byte pattern to find</param>
        /// <param name="replaceData">Byte pattern to replace with</param>
        /// <param name="currentPosition">Current position (search starts at currentPosition + 1)</param>
        /// <param name="truncateLength">If true, truncate replaceData to match findData length</param>
        /// <returns>Position where replacement occurred, or -1 if pattern not found</returns>
        public long ReplaceNext(byte[] findData, byte[] replaceData, long currentPosition, bool truncateLength = false)
        {
            if (_viewModel == null) return -1;
            return _viewModel.ReplaceNext(findData, replaceData, currentPosition, truncateLength);
        }

        /// <summary>
        /// Replace all occurrences of findData with replaceData
        /// </summary>
        /// <param name="findData">Byte pattern to find</param>
        /// <param name="replaceData">Byte pattern to replace with</param>
        /// <param name="truncateLength">If true, truncate replaceData to match findData length</param>
        /// <returns>Number of replacements made</returns>
        public int ReplaceAll(byte[] findData, byte[] replaceData, bool truncateLength = false)
        {
            if (_viewModel == null) return 0;
            return _viewModel.ReplaceAll(findData, replaceData, truncateLength);
        }

        /// <summary>
        /// Gets the underlying ByteProvider for advanced search operations.
        /// V2 ENHANCED: Used by SearchModule dialogs for ultra-performant searching.
        /// </summary>
        /// <returns>The ByteProvider instance, or null if no file is loaded</returns>
        public Core.Bytes.ByteProvider GetByteProvider()
        {
            return _viewModel?.GetByteProvider();
        }

        /// <summary>
        /// Opens the Advanced Search Dialog bound to this HexEditor.
        /// Supports 5 modes: TEXT, HEX, WILDCARD, TBL TEXT (if TBL loaded), RELATIVE (encoding discovery).
        /// </summary>
        /// <param name="owner">Owner window for centering the dialog (optional)</param>
        public void ShowAdvancedSearchDialog(System.Windows.Window owner = null)
        {
            // Verify file/stream is loaded by checking ByteProvider
            var provider = GetByteProvider();
            if (provider == null || provider.Length == 0)
            {
                System.Windows.MessageBox.Show(
                    "No file or stream loaded. Please open a file first.",
                    "Advanced Search",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
                return;
            }

            var dialog = new Search.Views.AdvancedSearchDialog();
            var vm = new Search.ViewModels.AdvancedSearchViewModel();

            // ⚠️ BINDING CRITIQUE: Lier le ViewModel à CE HexEditor
            vm.BindToHexEditor(this);

            // Wire navigation event: double-click result → scroll and select in HexEditor
            vm.ResultNavigationRequested += (s, result) =>
            {
                if (result != null)
                {
                    // Navigate and select in THIS HexEditor
                    FindSelect(result.Position, result.Length);
                    // Ensure visible in viewport
                    SetPosition(result.Position);
                }
            };

            // Wire highlight event: show all results with yellow highlight
            vm.HighlightResultsRequested += (s, matches) =>
            {
                if (matches == null) return;

                // Clear existing search highlights only (preserve format detection blocks)
                ClearCustomBackgroundBlockByTag("AdvancedSearchResult");

                // Create highlight brush
                var highlightBrush = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Colors.Yellow)
                { Opacity = 0.3 };

                // Add highlight for each match
                foreach (var match in matches.Take(10000)) // Limit to 10K for performance
                {
                    AddCustomBackgroundBlock(new Core.CustomBackgroundBlock(
                        match.Position,
                        match.Length,
                        highlightBrush,
                        "AdvancedSearchResult"));
                }
            };

            // Wire clear highlights event
            vm.HighlightClearRequested += (s, e) =>
            {
                ClearCustomBackgroundBlockByTag("AdvancedSearchResult");
            };

            // Wire TBL load event: when user applies discovered encoding from Relative Search or loads TBL file
            vm.TblLoadRequested += (s, newTbl) =>
            {
                if (newTbl == null) return;

                // Load the newly discovered TBL into THIS HexEditor
                _tblStream = newTbl;
                _characterTableType = Core.CharacterTableType.TblFile;

                // Update viewport to show TBL
                if (HexViewport != null)
                {
                    HexViewport.TblStream = newTbl;
                    HexViewport.ShowTblMte = true;
                }

                // Update status bar
                StatusText.Text = $"TBL loaded: {newTbl.FileName}";
                TblStatusIcon.Visibility = System.Windows.Visibility.Visible;
            };

            // Wire TBL close event: when user closes TBL from Advanced Search
            vm.TblCloseRequested += (s, e) =>
            {
                // Close the TBL in THIS HexEditor
                CloseTBL();
            };

            // Cleanup when dialog closes
            dialog.Closed += (s, e) =>
            {
                // Clear only search highlights, preserve format detection blocks
                ClearCustomBackgroundBlockByTag("AdvancedSearchResult");
                // Dispose ViewModel (unsubscribe events, clear references)
                vm.Dispose();
            };

            // Set DataContext and show modal dialog
            dialog.DataContext = vm;
            dialog.Owner = owner;
            dialog.ShowDialog();
        }

        #endregion
    }
}
