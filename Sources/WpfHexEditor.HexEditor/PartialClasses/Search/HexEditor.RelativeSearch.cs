/*
    Apache 2.0  2026
    Author : Derek Tremblay (derektremblay666@gmail.com)
    Contributors: Claude Sonnet 4.5
*/

using System;
using System.Threading;
using System.Windows;
using WpfHexEditor.Core.Search.Models;
using WpfHexEditor.Core.Search.Services;
using WpfHexEditor.HexEditor.Search.ViewModels;
using WpfHexEditor.HexEditor.Search.Views;

namespace WpfHexEditor.HexEditor
{
    /// <summary>
    /// Partial class for HexEditor - Relative Search functionality.
    /// Exposes public API for ROM encoding discovery.
    /// </summary>
    public partial class HexEditor
    {
        #region Relative Search - Public API

        /// <summary>
        /// Performs relative search to discover character encoding.
        /// This is the main programmatic API for Relative Search.
        /// </summary>
        /// <param name="searchText">Known text to search for (e.g., "World", "Start", "Hero").</param>
        /// <param name="options">Optional search options. If null, uses defaults.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>Search result with encoding proposals sorted by score.</returns>
        /// <example>
        /// <code>
        /// var result = hexEditor.PerformRelativeSearch("World");
        /// if (result.Success &amp;&amp; result.Proposals.Count > 0)
        /// {
        ///     var bestProposal = result.Proposals[0];
        ///     hexEditor.ExportProposalToTbl(bestProposal, "discovered_encoding.tbl");
        ///     hexEditor.LoadTBLFile("discovered_encoding.tbl");
        /// }
        /// </code>
        /// </example>
        public RelativeSearchResult PerformRelativeSearch(
            string searchText,
            RelativeSearchOptions options = null,
            CancellationToken cancellationToken = default)
        {
            // Validate inputs
            if (_viewModel?.Provider == null || _viewModel?.Provider.IsOpen != true)
                return RelativeSearchResult.CreateError(
                    WpfHexEditor.Core.Properties.Resources.RelativeSearchNoFileOpenString);

            if (string.IsNullOrWhiteSpace(searchText))
                return RelativeSearchResult.CreateError("Search text cannot be empty");

            // Use provided options or create defaults
            options ??= new RelativeSearchOptions
            {
                SearchText = searchText,
                StartPosition = 0,
                EndPosition = -1,
                MinMatchesRequired = 1,
                MaxProposals = 20,
                UseParallelSearch = true,
                CaseSensitive = false
            };

            // Ensure search text is set
            if (string.IsNullOrEmpty(options.SearchText))
                options.SearchText = searchText;

            // Create engine with current TBL for validation scoring
            var engine = new RelativeSearchEngine(_viewModel?.Provider, _tblStream);

            // Perform search
            return engine.Search(options, cancellationToken);
        }

        /// <summary>
        /// Exports an encoding proposal to a TBL file.
        /// Creates a NEW TBL file - does NOT modify the currently loaded TBL.
        /// </summary>
        /// <param name="proposal">The encoding proposal to export.</param>
        /// <param name="filePath">Path where to save the TBL file.</param>
        /// <exception cref="ArgumentNullException">Thrown if proposal is null.</exception>
        /// <exception cref="ArgumentException">Thrown if filePath is empty.</exception>
        /// <example>
        /// <code>
        /// var result = hexEditor.PerformRelativeSearch("Start");
        /// if (result.Success)
        /// {
        ///     hexEditor.ExportProposalToTbl(result.BestProposal, "game_encoding.tbl");
        /// }
        /// </code>
        /// </example>
        public void ExportProposalToTbl(EncodingProposal proposal, string filePath)
        {
            if (proposal == null)
                throw new ArgumentNullException(nameof(proposal));

            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path cannot be empty", nameof(filePath));

            if (_viewModel?.Provider == null || _viewModel?.Provider.IsOpen != true)
                throw new InvalidOperationException("No file is open");

            // Create engine and export
            var engine = new RelativeSearchEngine(_viewModel?.Provider, _tblStream);
            var tbl = engine.ExportToTbl(proposal);
            // TODO: TblStream.FileName has an internal setter; need InternalsVisibleTo or a Save(string) overload
            // tbl.FileName = filePath;
            // tbl.Save();
            // Workaround: write TBL content manually
            var sb = new System.Text.StringBuilder();
            foreach (var entry in tbl.GetAllEntries())
            {
                var value = entry.Value.Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
                sb.AppendLine($"{entry.Entry}={value}");
            }
            System.IO.File.WriteAllText(filePath, sb.ToString(), System.Text.Encoding.UTF8);
        }

        /// <summary>
        /// Shows the Relative Search dialog (UI).
        /// The dialog allows interactive encoding discovery with visual feedback.
        /// </summary>
        /// <remarks>
        /// The dialog will use the current ByteProvider and TBL stream.
        /// If a TBL is loaded, proposals will be validated against it for bonus scoring.
        /// </remarks>
        /// <example>
        /// <code>
        /// hexEditor.ShowRelativeSearchDialog();
        /// </code>
        /// </example>
        public void ShowRelativeSearchDialog()
        {
            // Validate state
            if (_viewModel?.Provider == null || _viewModel?.Provider.IsOpen != true)
            {
                StatusText.Text = WpfHexEditor.Core.Properties.Resources.RelativeSearchNoFileOpenString;
                return;
            }

            // Create dialog with ViewModel
            var dialog = new RelativeSearchDialog
            {
                Owner = Window.GetWindow(this),
                DataContext = new RelativeSearchViewModel
                {
                    ByteProvider = _viewModel?.Provider,
                    CurrentTbl = _tblStream  // Pass current TBL for validation (read-only)
                }
            };

            // Subscribe to navigation events
            var viewModel = dialog.DataContext as RelativeSearchViewModel;
            if (viewModel != null)
            {
                viewModel.OnNavigateToPosition += (sender, position) =>
                {
                    // Navigate to the position in the hex editor
                    SetPosition(position);
                    Focus();
                };
            }

            // Show dialog
            dialog.ShowDialog();
        }

        /// <summary>
        /// Gets the currently loaded TBL stream (if any).
        /// Allows Relative Search to validate proposals against existing TBL.
        /// This is a read-only accessor - Relative Search does NOT modify the loaded TBL.
        /// </summary>
        /// <value>
        /// The current TBL stream, or null if no TBL is loaded.
        /// </value>
        /// <remarks>
        /// When performing relative search, if a TBL is loaded, proposals that match
        /// existing TBL entries will receive bonus points in the scoring system.
        /// This helps refine partially-complete TBL files.
        /// </remarks>
        public Core.CharacterTable.TblStream CurrentTbl => _tblStream;

        #endregion
    }
}
