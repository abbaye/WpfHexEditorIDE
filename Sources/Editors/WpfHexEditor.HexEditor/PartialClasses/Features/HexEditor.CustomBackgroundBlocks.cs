// ==========================================================
// Project: WpfHexEditor.HexEditor
// File: HexEditor.CustomBackgroundBlocks.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude (Anthropic)
// Created: 2026-03-06
// Description:
//     Partial class managing custom background color blocks in the HexEditor viewport.
//     Allows consumers to define colored byte ranges that are rendered as background
//     highlights in the hex view, independent of selection and search highlights.
//
// Architecture Notes:
//     Uses CustomBackgroundBlockService from WpfHexEditor.Core.Services for block management.
//     Rendering delegated to HexViewport DrawingVisual pipeline.
//
// ==========================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using WpfHexEditor.Core;
using WpfHexEditor.Core.Events;
using WpfHexEditor.Core.Services;

namespace WpfHexEditor.HexEditor
{
    /// <summary>
    /// HexEditor partial class - Custom Background Blocks
    /// Contains methods for managing custom background highlighting
    /// </summary>
    public partial class HexEditor
    {
        #region Private Fields

        private readonly CustomBackgroundService _customBackgroundService = new CustomBackgroundService();

        #endregion

        #region Dependency Property Wrappers

        /// <summary>
        /// Enable or disable custom background blocks
        /// Note: DependencyProperty is defined in HexEditor.xaml.cs
        /// </summary>
        public bool AllowCustomBackgroundBlock
        {
            get => (bool)GetValue(AllowCustomBackgroundBlockProperty);
            set => SetValue(AllowCustomBackgroundBlockProperty, value);
        }

        #endregion

        #region Public Properties

        /// <summary>
        /// Get the custom background service for advanced operations
        /// Provides query, validation, and bulk operations
        /// </summary>
        public CustomBackgroundService CustomBackgroundService => _customBackgroundService;

        /// <summary>
        /// Get the list of custom background blocks (for backward compatibility)
        /// WARNING: Modifying this list directly bypasses events and caching
        /// Use CustomBackgroundService methods instead
        /// </summary>
        [Obsolete("Use CustomBackgroundService methods instead. Direct list access bypasses events and caching.", false)]
        public List<Core.CustomBackgroundBlock> CustomBackgroundBlockItems =>
            _customBackgroundService.GetAllBlocks().ToList();

        #endregion

        #region Events

        /// <summary>
        /// Raised when custom background blocks change (added, removed, cleared)
        /// </summary>
        public event EventHandler<CustomBackgroundBlockEventArgs> CustomBackgroundBlockChanged;

        #endregion

        #region Initialization

        /// <summary>
        /// Initialize custom background block system
        /// This should be called from the HexEditor constructor
        /// </summary>
        private void InitializeCustomBackgroundBlocks()
        {
            // Subscribe to service events
            _customBackgroundService.BlocksChanged += OnCustomBackgroundBlocksChanged;
        }

        /// <summary>
        /// Handle custom background block changes from the service
        /// </summary>
        private void OnCustomBackgroundBlocksChanged(object sender, CustomBackgroundBlockEventArgs e)
        {
            // Sync with HexViewport renderer
            if (HexViewport != null)
            {
                // Update viewport's block list
                HexViewport.CustomBackgroundBlocks = _customBackgroundService.GetAllBlocks().ToList();
                HexViewport.InvalidateVisual();
            }

            // Forward event to HexEditor consumers
            CustomBackgroundBlockChanged?.Invoke(this, e);
        }

        #endregion

        #region Public Methods - Custom Background Blocks

        /// <summary>
        /// Add a custom background block
        /// </summary>
        /// <param name="block">Block to add</param>
        public void AddCustomBackgroundBlock(Core.CustomBackgroundBlock block)
        {
            if (block == null) return;
            _customBackgroundService.AddBlock(block);
            // Event handling triggers viewport update automatically
        }

        /// <summary>
        /// Remove a custom background block
        /// </summary>
        /// <param name="block">Block to remove</param>
        public void RemoveCustomBackgroundBlock(Core.CustomBackgroundBlock block)
        {
            if (block == null) return;
            _customBackgroundService.RemoveBlock(block);
            // Event handling triggers viewport update automatically
        }

        /// <summary>
        /// Clear all custom background blocks
        /// </summary>
        public void ClearCustomBackgroundBlock()
        {
            _customBackgroundService.ClearAll();
            // Event handling triggers viewport update automatically
        }

        /// <summary>
        /// Clear only the custom background blocks that match a specific tag (Description).
        /// Use this when a feature must clean up only its own blocks without disturbing
        /// blocks added by other features (e.g. format detection, file comparison).
        /// </summary>
        /// <param name="tag">The Description value used when the blocks were created</param>
        public void ClearCustomBackgroundBlockByTag(string tag)
        {
            _customBackgroundService.RemoveBlocksByTag(tag);
            // Event handling triggers viewport update automatically
        }

        /// <summary>
        /// Get custom background block at position
        /// </summary>
        /// <param name="position">Position to check</param>
        /// <returns>First block at position, or null</returns>
        public Core.CustomBackgroundBlock GetCustomBackgroundBlock(long position)
        {
            return _customBackgroundService.GetBlockAt(position);
        }

        /// <summary>
        /// Get all custom background blocks at position
        /// </summary>
        /// <param name="position">Position to check</param>
        /// <returns>All blocks overlapping position</returns>
        public IEnumerable<Core.CustomBackgroundBlock> GetCustomBackgroundBlocks(long position)
        {
            return _customBackgroundService.GetBlocksAt(position);
        }

        #endregion
    }
}
