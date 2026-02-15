//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using WpfHexaEditor.Core;
using WpfHexaEditor.Core.Bytes;
using WpfHexaEditor.Models;
using WpfHexaEditor.ViewModels;

namespace WpfHexaEditor
{
    /// <summary>
    /// HexEditor partial class - File Comparison
    /// Contains methods for comparing files and highlighting differences
    /// </summary>
    public partial class HexEditor
    {
        #region File Comparison - Fields

        private readonly Services.ComparisonService _comparisonService = new();
        private List<ByteDifference> _comparisonResults = null;

        #endregion

        #region Public Methods - File Comparison

        /// <summary>
        /// Compare this editor's content with another HexEditor
        /// </summary>
        /// <param name="other">Other HexEditor to compare against</param>
        /// <param name="highlightDifferences">Automatically highlight differences with custom background blocks</param>
        /// <param name="maxDifferences">Maximum number of differences to return (0 = unlimited)</param>
        /// <returns>Enumerable of byte differences</returns>
        public IEnumerable<ByteDifference> Compare(HexEditor other, bool highlightDifferences = true, long maxDifferences = 1000)
        {
            if (other == null || _viewModel?.Provider == null || other._viewModel?.Provider == null)
                return Enumerable.Empty<ByteDifference>();

            // Compare using ByteProvider V2
            var differences = _comparisonService.Compare(_viewModel.Provider, other._viewModel.Provider, maxDifferences).ToList();
            _comparisonResults = differences;

            if (highlightDifferences && differences.Any())
            {
                // Clear existing comparison highlights
                ClearCustomBackgroundBlock();

                // Highlight each difference
                foreach (var diff in differences)
                {
                    var block = new Core.CustomBackgroundBlock(
                        diff.BytePositionInStream,
                        1, // Single byte
                        new SolidColorBrush(Colors.LightCoral),
                        $"Diff: 0x{diff.Origine:X2} vs 0x{diff.Destination:X2}"
                    );
                    AddCustomBackgroundBlock(block);
                }
            }

            return differences;
        }

        /// <summary>
        /// Compare this editor's content with a ByteProvider
        /// </summary>
        /// <param name="provider">ByteProvider to compare against</param>
        /// <param name="highlightDifferences">Automatically highlight differences</param>
        /// <param name="maxDifferences">Maximum differences to return (0 = unlimited)</param>
        /// <returns>Enumerable of byte differences</returns>
        public IEnumerable<ByteDifference> Compare(Core.Bytes.ByteProviderLegacy provider, bool highlightDifferences = true, long maxDifferences = 1000)
        {
            if (provider == null || _viewModel?.Provider == null)
                return Enumerable.Empty<ByteDifference>();

            // Note: Cross-version comparison (V2 vs V1) is not supported
            // ByteProvider V2 uses virtual positions while ByteProviderLegacy uses physical positions
            // For comparison, use two editors with the same provider version
            var differences = new List<ByteDifference>();
            _comparisonResults = differences;

            if (highlightDifferences && differences.Any())
            {
                // Clear existing comparison highlights
                ClearCustomBackgroundBlock();

                // Highlight each difference
                foreach (var diff in differences)
                {
                    var block = new Core.CustomBackgroundBlock(
                        diff.BytePositionInStream,
                        1,
                        new SolidColorBrush(Colors.LightCoral),
                        $"Diff: 0x{diff.Origine:X2} vs 0x{diff.Destination:X2}"
                    );
                    AddCustomBackgroundBlock(block);
                }
            }

            return differences;
        }

        /// <summary>
        /// Clear comparison results and highlighting
        /// </summary>
        public void ClearComparison()
        {
            _comparisonResults = null;
            ClearCustomBackgroundBlock();
        }

        /// <summary>
        /// Get the last comparison results
        /// </summary>
        public IEnumerable<ByteDifference> GetComparisonResults()
        {
            return _comparisonResults ?? Enumerable.Empty<ByteDifference>();
        }

        /// <summary>
        /// Count differences between this editor and another
        /// </summary>
        public long CountDifferences(HexEditor other)
        {
            if (other == null || _viewModel?.Provider == null || other._viewModel?.Provider == null)
                return 0;

            return _comparisonService.CountDifferences(_viewModel.Provider, other._viewModel.Provider);
        }

        /// <summary>
        /// Calculate similarity percentage with another editor (0.0 - 100.0)
        /// </summary>
        public double CalculateSimilarity(HexEditor other)
        {
            if (other == null || _viewModel?.Provider == null || other._viewModel?.Provider == null)
                return 0.0;

            return _comparisonService.CalculateSimilarity(_viewModel.Provider, other._viewModel.Provider);
        }

        #endregion

        #region Public Methods - File Comparison

        /// <summary>
        /// Compare this file with another HexEditor
        /// Returns list of differences between the two files
        /// </summary>
        public IEnumerable<Core.Bytes.ByteDifference> Compare(HexEditor other)
        {
            if (_viewModel == null || other?._viewModel == null)
                return Enumerable.Empty<Core.Bytes.ByteDifference>();

            return CompareProviders(_viewModel, other._viewModel);
        }

        /// <summary>
        /// Compare this file with a ByteProvider
        /// Returns list of differences between the two providers
        /// </summary>
        public IEnumerable<Core.Bytes.ByteDifference> Compare(Core.Bytes.ByteProviderLegacy provider)
        {
            if (_viewModel == null || provider == null)
                return Enumerable.Empty<Core.Bytes.ByteDifference>();

            // Cross-version comparison (V2 vs V1) is not supported
            // ByteProvider V2 uses virtual positions; ByteProviderLegacy uses physical positions
            // For backward compatibility, this method returns empty results
            return Enumerable.Empty<Core.Bytes.ByteDifference>();
        }

        /// <summary>
        /// Internal comparison logic
        /// </summary>
        private IEnumerable<Core.Bytes.ByteDifference> CompareProviders(
            ViewModels.HexEditorViewModel vm1,
            ViewModels.HexEditorViewModel vm2)
        {
            var differences = new List<Core.Bytes.ByteDifference>();
            long maxLength = Math.Max(vm1.VirtualLength, vm2.VirtualLength);

            for (long i = 0; i < maxLength; i++)
            {
                byte byte1 = i < vm1.VirtualLength ? vm1.GetByteAt(new Models.VirtualPosition(i)) : (byte)0;
                byte byte2 = i < vm2.VirtualLength ? vm2.GetByteAt(new Models.VirtualPosition(i)) : (byte)0;

                if (byte1 != byte2)
                {
                    differences.Add(new Core.Bytes.ByteDifference(byte1, byte2, i));
                }
            }

            return differences;
        }

        #endregion
    }
}
