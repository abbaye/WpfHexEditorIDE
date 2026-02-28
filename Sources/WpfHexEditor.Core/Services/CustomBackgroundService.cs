//////////////////////////////////////////////
// Apache 2.0  - 2016-2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using WpfHexaEditor.Core;
using WpfHexaEditor.Events;

namespace WpfHexaEditor.Services
{
    /// <summary>
    /// Service responsible for custom background block management operations
    /// </summary>
    /// <example>
    /// Basic usage:
    /// <code>
    /// var service = new CustomBackgroundService();
    ///
    /// // Add background blocks with colors and descriptions
    /// service.AddBlock(startOffset: 0, length: 16,
    ///                  color: Brushes.LightBlue, description: "File Header");
    /// service.AddBlock(startOffset: 100, length: 256,
    ///                  color: Brushes.LightGreen, description: "Data Section");
    /// service.AddBlock(startOffset: 500, length: 64,
    ///                  color: Brushes.LightYellow, description: "Metadata");
    ///
    /// // Query blocks at specific positions
    /// var block = service.GetBlockAt(position: 105);
    /// if (block != null)
    ///     Console.WriteLine($"Block at 105: {block.Description}, Color: {block.Color}");
    ///
    /// // Get all blocks in a range
    /// var rangeBlocks = service.GetBlocksInRange(startPosition: 50, endPosition: 200);
    /// foreach (var b in rangeBlocks)
    ///     Console.WriteLine($"Block: {b.StartOffset}-{b.StopOffset}, {b.Description}");
    ///
    /// // Check for overlaps before adding
    /// if (!service.WouldOverlap(startOffset: 150, length: 50))
    /// {
    ///     service.AddBlock(150, 50, Brushes.LightCoral, "Additional Data");
    /// }
    /// else
    /// {
    ///     var overlapping = service.GetOverlappingBlocks(150, 50);
    ///     Console.WriteLine($"Would overlap with {overlapping.Count()} existing blocks");
    /// }
    ///
    /// // Get statistics
    /// int totalBlocks = service.GetBlockCount();
    /// bool hasAny = service.HasBlocks();
    ///
    /// // Remove blocks
    /// service.RemoveBlocksAt(position: 105);  // Remove blocks at specific position
    /// service.RemoveBlocksInRange(50, 200);   // Remove blocks in range
    ///
    /// // Get sorted blocks
    /// var sorted = service.GetBlocksSorted();  // By start offset
    /// var byLength = service.GetBlocksSortedByLength();  // By length descending
    ///
    /// // Clear all
    /// service.ClearAll();
    /// </code>
    /// </example>
    public class CustomBackgroundService
    {
        #region Private Fields

        /// <summary>
        /// Internal list of custom background blocks
        /// </summary>
        private readonly List<CustomBackgroundBlock> _backgroundBlocks = new();

        #endregion

        #region Events

        /// <summary>
        /// Raised when a block or blocks are added
        /// </summary>
        public event EventHandler<CustomBackgroundBlockEventArgs> BlockAdded;

        /// <summary>
        /// Raised when a block or blocks are removed
        /// </summary>
        public event EventHandler<CustomBackgroundBlockEventArgs> BlockRemoved;

        /// <summary>
        /// Raised when all blocks are cleared
        /// </summary>
        public event EventHandler<CustomBackgroundBlockEventArgs> BlocksCleared;

        /// <summary>
        /// Raised for any change (added, removed, cleared)
        /// Convenience event for subscribers that want all notifications
        /// </summary>
        public event EventHandler<CustomBackgroundBlockEventArgs> BlocksChanged;

        /// <summary>
        /// Raise BlockAdded event
        /// </summary>
        protected virtual void OnBlockAdded(CustomBackgroundBlockEventArgs e)
        {
            BlockAdded?.Invoke(this, e);
            BlocksChanged?.Invoke(this, e);
        }

        /// <summary>
        /// Raise BlockRemoved event
        /// </summary>
        protected virtual void OnBlockRemoved(CustomBackgroundBlockEventArgs e)
        {
            BlockRemoved?.Invoke(this, e);
            BlocksChanged?.Invoke(this, e);
        }

        /// <summary>
        /// Raise BlocksCleared event
        /// </summary>
        protected virtual void OnBlocksCleared(CustomBackgroundBlockEventArgs e)
        {
            BlocksCleared?.Invoke(this, e);
            BlocksChanged?.Invoke(this, e);
        }

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

            // Raise event
            OnBlockAdded(new CustomBackgroundBlockEventArgs(
                BlockChangeType.Added,
                block,
                _backgroundBlocks.Count));

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

            // Raise event
            OnBlockAdded(new CustomBackgroundBlockEventArgs(
                BlockChangeType.Added,
                block,
                _backgroundBlocks.Count));

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

            var addedBlocks = new List<CustomBackgroundBlock>();

            foreach (var block in blocks)
            {
                if (block != null && block.Length > 0 && block.StartOffset >= 0)
                {
                    _backgroundBlocks.Add(block);
                    addedBlocks.Add(block);
                }
            }

            // Raise event if any blocks were added
            if (addedBlocks.Count > 0)
            {
                OnBlockAdded(new CustomBackgroundBlockEventArgs(
                    BlockChangeType.AddedMultiple,
                    addedBlocks.AsReadOnly(),
                    _backgroundBlocks.Count));
            }

            return addedBlocks.Count;
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

            bool removed = _backgroundBlocks.Remove(block);

            if (removed)
            {
                // Raise event
                OnBlockRemoved(new CustomBackgroundBlockEventArgs(
                    BlockChangeType.Removed,
                    block,
                    _backgroundBlocks.Count));
            }

            return removed;
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

            // Collect blocks to remove first
            var removedBlocks = _backgroundBlocks
                .Where(block => position >= block.StartOffset && position < block.StopOffset)
                .ToList();

            int count = _backgroundBlocks.RemoveAll(block =>
                position >= block.StartOffset && position < block.StopOffset);

            if (count > 0)
            {
                // Raise event
                OnBlockRemoved(new CustomBackgroundBlockEventArgs(
                    BlockChangeType.RemovedMultiple,
                    removedBlocks.AsReadOnly(),
                    _backgroundBlocks.Count));
            }

            return count;
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

            // Collect blocks to remove first
            var removedBlocks = _backgroundBlocks
                .Where(block =>
                    block.StartOffset >= startPosition && block.StartOffset <= endPosition ||
                    block.StopOffset >= startPosition && block.StopOffset <= endPosition)
                .ToList();

            int count = _backgroundBlocks.RemoveAll(block =>
                block.StartOffset >= startPosition && block.StartOffset <= endPosition ||
                block.StopOffset >= startPosition && block.StopOffset <= endPosition);

            if (count > 0)
            {
                // Raise event
                OnBlockRemoved(new CustomBackgroundBlockEventArgs(
                    BlockChangeType.RemovedMultiple,
                    removedBlocks.AsReadOnly(),
                    _backgroundBlocks.Count));
            }

            return count;
        }

        /// <summary>
        /// Clear all blocks
        /// </summary>
        /// <returns>Number of blocks cleared</returns>
        public int ClearAll()
        {
            var count = _backgroundBlocks.Count;

            if (count > 0)
            {
                _backgroundBlocks.Clear();

                // Raise event
                OnBlocksCleared(new CustomBackgroundBlockEventArgs
                {
                    ChangeType = BlockChangeType.Cleared,
                    AffectedCount = count,
                    TotalBlockCount = 0
                });
            }

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
