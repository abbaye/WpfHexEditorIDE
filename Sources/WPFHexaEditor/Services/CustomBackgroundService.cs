//////////////////////////////////////////////
// Apache 2.0  - 2016-2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using WpfHexaEditor.Core;

namespace WpfHexaEditor.Services
{
    /// <summary>
    /// Service responsible for custom background block management operations
    /// </summary>
    public class CustomBackgroundService
    {
        #region Private Fields

        /// <summary>
        /// Internal list of custom background blocks
        /// </summary>
        private readonly List<CustomBackgroundBlock> _backgroundBlocks = new();

        #endregion

        #region Add Operations

        /// <summary>
        /// Add a custom background block
        /// </summary>
        /// <param name="block">Block to add</param>
        /// <returns>True if block was added</returns>
        public bool AddBlock(CustomBackgroundBlock block)
        {
            if (block == null || block.Length <= 0 || block.StartOffset < 0)
                return false;

            _backgroundBlocks.Add(block);
            return true;
        }

        /// <summary>
        /// Add a custom background block with parameters
        /// </summary>
        /// <param name="startOffset">Start position</param>
        /// <param name="length">Block length</param>
        /// <param name="color">Background color</param>
        /// <param name="description">Optional description</param>
        /// <returns>True if block was added</returns>
        public bool AddBlock(long startOffset, long length, SolidColorBrush color, string description = "")
        {
            if (length <= 0 || startOffset < 0 || color == null)
                return false;

            var block = new CustomBackgroundBlock(startOffset, length, color, description);
            _backgroundBlocks.Add(block);
            return true;
        }

        /// <summary>
        /// Add multiple blocks at once
        /// </summary>
        /// <param name="blocks">Blocks to add</param>
        /// <returns>Number of blocks successfully added</returns>
        public int AddBlocks(IEnumerable<CustomBackgroundBlock> blocks)
        {
            if (blocks == null)
                return 0;

            var count = 0;
            foreach (var block in blocks)
            {
                if (AddBlock(block))
                    count++;
            }

            return count;
        }

        #endregion

        #region Remove Operations

        /// <summary>
        /// Remove a specific block
        /// </summary>
        /// <param name="block">Block to remove</param>
        /// <returns>True if block was removed</returns>
        public bool RemoveBlock(CustomBackgroundBlock block)
        {
            if (block == null)
                return false;

            return _backgroundBlocks.Remove(block);
        }

        /// <summary>
        /// Remove all blocks that overlap with a specific position
        /// </summary>
        /// <param name="position">Position to check</param>
        /// <returns>Number of blocks removed</returns>
        public int RemoveBlocksAt(long position)
        {
            if (position < 0)
                return 0;

            return _backgroundBlocks.RemoveAll(block =>
                position >= block.StartOffset && position < block.StopOffset);
        }

        /// <summary>
        /// Remove blocks within a range
        /// </summary>
        /// <param name="startPosition">Start position</param>
        /// <param name="endPosition">End position</param>
        /// <returns>Number of blocks removed</returns>
        public int RemoveBlocksInRange(long startPosition, long endPosition)
        {
            if (startPosition < 0 || endPosition < startPosition)
                return 0;

            return _backgroundBlocks.RemoveAll(block =>
                block.StartOffset >= startPosition && block.StartOffset <= endPosition ||
                block.StopOffset >= startPosition && block.StopOffset <= endPosition);
        }

        /// <summary>
        /// Clear all blocks
        /// </summary>
        /// <returns>Number of blocks cleared</returns>
        public int ClearAll()
        {
            var count = _backgroundBlocks.Count;
            _backgroundBlocks.Clear();
            return count;
        }

        #endregion

        #region Query Operations

        /// <summary>
        /// Get all background blocks
        /// </summary>
        /// <returns>Enumerable of all blocks</returns>
        public IEnumerable<CustomBackgroundBlock> GetAllBlocks() => _backgroundBlocks.ToList();

        /// <summary>
        /// Get the first block that contains the specified position
        /// </summary>
        /// <param name="position">Position to check</param>
        /// <returns>Block at position, or null if not found</returns>
        public CustomBackgroundBlock GetBlockAt(long position)
        {
            if (position < 0)
                return null;

            return _backgroundBlocks
                .OrderBy(c => c.StartOffset)
                .FirstOrDefault(block =>
                    position >= block.StartOffset && position < block.StopOffset);
        }

        /// <summary>
        /// Get all blocks that overlap with a specific position
        /// </summary>
        /// <param name="position">Position to check</param>
        /// <returns>Enumerable of overlapping blocks</returns>
        public IEnumerable<CustomBackgroundBlock> GetBlocksAt(long position)
        {
            if (position < 0)
                return Enumerable.Empty<CustomBackgroundBlock>();

            return _backgroundBlocks
                .Where(block => position >= block.StartOffset && position < block.StopOffset)
                .OrderBy(c => c.StartOffset)
                .ToList();
        }

        /// <summary>
        /// Get blocks that overlap with a range
        /// </summary>
        /// <param name="startPosition">Start of range</param>
        /// <param name="endPosition">End of range</param>
        /// <returns>Enumerable of blocks in range</returns>
        public IEnumerable<CustomBackgroundBlock> GetBlocksInRange(long startPosition, long endPosition)
        {
            if (startPosition < 0 || endPosition < startPosition)
                return Enumerable.Empty<CustomBackgroundBlock>();

            return _backgroundBlocks
                .Where(block =>
                    block.StartOffset >= startPosition && block.StartOffset <= endPosition ||
                    block.StopOffset >= startPosition && block.StopOffset <= endPosition ||
                    block.StartOffset <= startPosition && block.StopOffset >= endPosition)
                .OrderBy(c => c.StartOffset)
                .ToList();
        }

        /// <summary>
        /// Check if any block exists at position
        /// </summary>
        /// <param name="position">Position to check</param>
        /// <returns>True if block exists at position</returns>
        public bool HasBlockAt(long position) => GetBlockAt(position) != null;

        /// <summary>
        /// Get total number of blocks
        /// </summary>
        /// <returns>Count of blocks</returns>
        public int GetBlockCount() => _backgroundBlocks.Count;

        /// <summary>
        /// Check if any blocks exist
        /// </summary>
        /// <returns>True if blocks exist</returns>
        public bool HasBlocks() => _backgroundBlocks.Count > 0;

        #endregion

        #region Validation Operations

        /// <summary>
        /// Check if a block would overlap with existing blocks
        /// </summary>
        /// <param name="startOffset">Start position of new block</param>
        /// <param name="length">Length of new block</param>
        /// <returns>True if block would overlap</returns>
        public bool WouldOverlap(long startOffset, long length)
        {
            if (startOffset < 0 || length <= 0)
                return false;

            var endOffset = startOffset + length;

            return _backgroundBlocks.Any(block =>
                startOffset >= block.StartOffset && startOffset < block.StopOffset ||
                endOffset > block.StartOffset && endOffset <= block.StopOffset ||
                startOffset <= block.StartOffset && endOffset >= block.StopOffset);
        }

        /// <summary>
        /// Get overlapping blocks for a proposed new block
        /// </summary>
        /// <param name="startOffset">Start position of new block</param>
        /// <param name="length">Length of new block</param>
        /// <returns>Enumerable of overlapping blocks</returns>
        public IEnumerable<CustomBackgroundBlock> GetOverlappingBlocks(long startOffset, long length)
        {
            if (startOffset < 0 || length <= 0)
                return Enumerable.Empty<CustomBackgroundBlock>();

            var endOffset = startOffset + length;

            return _backgroundBlocks
                .Where(block =>
                    startOffset >= block.StartOffset && startOffset < block.StopOffset ||
                    endOffset > block.StartOffset && endOffset <= block.StopOffset ||
                    startOffset <= block.StartOffset && endOffset >= block.StopOffset)
                .ToList();
        }

        #endregion

        #region Sort Operations

        /// <summary>
        /// Get blocks sorted by start offset
        /// </summary>
        /// <returns>Sorted enumerable of blocks</returns>
        public IEnumerable<CustomBackgroundBlock> GetBlocksSorted() =>
            _backgroundBlocks.OrderBy(c => c.StartOffset).ToList();

        /// <summary>
        /// Get blocks sorted by length (descending)
        /// </summary>
        /// <returns>Sorted enumerable of blocks</returns>
        public IEnumerable<CustomBackgroundBlock> GetBlocksSortedByLength() =>
            _backgroundBlocks.OrderByDescending(c => c.Length).ToList();

        #endregion
    }
}
