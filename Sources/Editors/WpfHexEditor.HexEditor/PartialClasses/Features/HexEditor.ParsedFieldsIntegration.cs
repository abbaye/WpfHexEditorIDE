// ==========================================================
// Project: WpfHexEditor.HexEditor
// File: HexEditor.ParsedFieldsIntegration.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude (Anthropic)
// Created: 2026-03-06
// Refactored: 2026-03-26
// Description:
//     Thin delegation layer connecting HexEditor to FormatParsingService.
//     All parsing logic has been extracted to WpfHexEditor.Core.Services.FormatParsing/.
//     This file retains only: DP for backward compat, public API surface,
//     data source lifecycle, and E2 field-click selection (HexEditor-specific UX).
//
// Architecture Notes:
//     Adapter pattern — HexEditorDataSource bridges HexEditor to IBinaryDataSource.
//     FormatParsingService handles detection, parsing, formatting, bookmarks, forensics.
// ==========================================================

using System;
using System.Collections.Generic;
using System.Windows;
using WpfHexEditor.Core;
using WpfHexEditor.Core.Events;
using WpfHexEditor.Core.FormatDetection;
using WpfHexEditor.Core.Interfaces;
using WpfHexEditor.Core.Models;
using WpfHexEditor.Core.Services.FormatParsing;
using WpfHexEditor.Core.ViewModels;
using WpfHexEditor.HexEditor.Services;

namespace WpfHexEditor.HexEditor
{
    public partial class HexEditor
    {
        #region Dependency Properties

        /// <summary>
        /// External ParsedFieldsPanel connected to this HexEditor.
        /// Set via ConnectParsedFieldsPanel() or direct binding.
        /// Kept for backward compatibility — new code should use IFormatParsingService.ConnectPanel().
        /// </summary>
        public static readonly DependencyProperty ParsedFieldsPanelProperty =
            DependencyProperty.Register(
                nameof(ParsedFieldsPanel),
                typeof(IParsedFieldsPanel),
                typeof(HexEditor),
                new PropertyMetadata(null, OnParsedFieldsPanelChanged));

        public IParsedFieldsPanel ParsedFieldsPanel
        {
            get => (IParsedFieldsPanel)GetValue(ParsedFieldsPanelProperty);
            set => SetValue(ParsedFieldsPanelProperty, value);
        }

        private static void OnParsedFieldsPanelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not HexEditor editor) return;

            // Disconnect old panel from the service
            if (e.OldValue is IParsedFieldsPanel)
                editor._formatParsingService?.DisconnectPanel();

            // Connect new panel to the service
            if (e.NewValue is IParsedFieldsPanel newPanel)
            {
                // Ensure the data source is attached before connecting the panel so that
                // ConnectPanel can immediately schedule ParseFieldsOnDispatcher on tab-switch
                // (the source may be null if the file was opened before the panel was connected,
                // or after a ClearFormatDetectionState/Detach cycle).
                if (editor._formatParsingService?.ActiveSource == null && editor.Stream != null)
                    editor.AttachDataSourceToParsingService();

                editor._formatParsingService?.ConnectPanel(newPanel);

                // If a format was previously detected on this tab, sync it into the service
                // so ParseFieldsOnDispatcher runs immediately (ConnectPanel only parses when
                // _activeFormat is already set on the service — which it isn't on a tab switch
                // because SyncDetectionResultsToService is only called after auto-detect completes).
                if (editor._detectedFormat != null)
                {
                    editor.SyncDetectionResultsToService();
                    newPanel.SetEnrichedFormat(editor._detectedFormat);
                }
            }
        }

        /// <summary>
        /// Enable or disable auto-refresh of parsed fields when bytes are modified.
        /// </summary>
        public static readonly DependencyProperty AutoRefreshParsedFieldsProperty =
            DependencyProperty.Register(
                nameof(AutoRefreshParsedFields),
                typeof(bool),
                typeof(HexEditor),
                new PropertyMetadata(true));

        public bool AutoRefreshParsedFields
        {
            get => (bool)GetValue(AutoRefreshParsedFieldsProperty);
            set => SetValue(AutoRefreshParsedFieldsProperty, value);
        }

        #endregion

        #region Fields

        // Format detection state — shared with HexEditor.FormatDetection.cs partial class
        internal FormatDefinition _detectedFormat;
        internal Dictionary<string, object> _detectionVariables;
        internal System.Collections.Generic.List<FormatMatchCandidate> _detectionCandidates;
        internal System.Collections.Generic.List<AssertionResult> _detectionAssertions;

        // Services
        private FormatParsingService _formatParsingService;
        private HexEditorDataSource _hexEditorDataSource;

        #endregion

        #region Initialization

        /// <summary>
        /// Initialize parsed fields integration.
        /// Called from HexEditor constructor.
        /// </summary>
        private void InitializeParsedFieldsPanel()
        {
            // Create the shared format parsing service
            _formatParsingService = new FormatParsingService();

            // E2 — Field-click selection: subscribe after HexViewport is fully initialized
            Loaded += (_, __) =>
            {
                if (HexViewport != null)
                    HexViewport.ByteClicked += HexViewport_ByteClickedForFieldSelect;
            };
        }

        /// <summary>
        /// Creates and attaches the data source adapter when a file is opened.
        /// Called by HexEditor after stream is ready.
        /// </summary>
        internal void AttachDataSourceToParsingService()
        {
            // Dispose previous data source
            _hexEditorDataSource?.Dispose();

            if (Stream != null)
            {
                _hexEditorDataSource = new HexEditorDataSource(this);
                _formatParsingService.Attach(_hexEditorDataSource, autoDetect: false);
            }
        }

        /// <summary>
        /// Syncs format detection results from FormatDetection.cs into the FormatParsingService
        /// so that field parsing uses the correct format, variables, and assertions.
        /// Called after AutoDetectAndApplyFormat() or ApplyFormat() completes.
        /// </summary>
        internal void SyncDetectionResultsToService()
        {
            if (_formatParsingService == null || _detectedFormat == null) return;

            // Ensure a data source is attached
            if (_formatParsingService.ActiveSource == null)
                AttachDataSourceToParsingService();

            // The service needs the detection context — pass it through a detection result
            var syntheticResult = new WpfHexEditor.Core.Events.FormatDetectionResult
            {
                Success = true,
                Format = _detectedFormat,
                Variables = _detectionVariables != null
                    ? new System.Collections.Generic.Dictionary<string, object>(_detectionVariables)
                    : null,
                Candidates = _detectionCandidates,
                AssertionResults = _detectionAssertions
            };

            // Store on the service for parsing
            _formatParsingService.ApplyDetectionResult(syntheticResult);
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// E2 — Field-click selection.
        /// When the user clicks a byte that belongs to a whfmt-parsed CustomBackgroundBlock,
        /// extend the selection to cover the full field range.
        /// </summary>
        private void HexViewport_ByteClickedForFieldSelect(object sender, long position)
        {
            if (_viewModel == null || _detectedFormat == null) return;

            var block = GetCustomBackgroundBlock(position);
            if (block == null || !block.ShowInTooltip || block.Length <= 1) return;

            // Avoid selection loop
            long selStart = Math.Min(SelectionStart, SelectionStop);
            long selStop  = Math.Max(SelectionStart, SelectionStop);
            if (selStart == block.StartOffset && selStop == block.StopOffset - 1) return;

            _viewModel.SetSelectionRange(
                new VirtualPosition(block.StartOffset),
                new VirtualPosition(block.StopOffset - 1));
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Connect a ParsedFieldsPanel to this editor (legacy API).
        /// Events are wired automatically via the DP.
        /// </summary>
        public void ConnectParsedFieldsPanel(IParsedFieldsPanel panel)
            => ParsedFieldsPanel = panel;

        /// <summary>
        /// Disconnect the current ParsedFieldsPanel (legacy API).
        /// </summary>
        public void DisconnectParsedFieldsPanel()
            => ParsedFieldsPanel = null;

        /// <summary>
        /// Refresh parsed fields using the current detected format.
        /// Delegates to FormatParsingService.
        /// </summary>
        public void RefreshParsedFields()
        {
            SyncDetectionResultsToService();
            _formatParsingService?.Refresh();
        }

        /// <summary>
        /// Clear format detection state (called when file is closed).
        /// </summary>
        internal void ClearFormatDetectionState()
        {
            try
            {
                _detectedFormat = null;
                _detectionVariables = null;
                _detectionCandidates = null;
                _detectionAssertions = null;

                _formatParsingService?.Clear();

                _hexEditorDataSource?.Dispose();
                _hexEditorDataSource = null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[HexEditor] Error clearing format detection state: {ex.Message}");
            }
        }

        #endregion
    }
}
