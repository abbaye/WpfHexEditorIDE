// ==========================================================
// Project: WpfHexEditor.HexEditor
// File: HexEditor.ToolbarContributor.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude (Anthropic)
// Created: 2026-03-26
// Description:
//     Partial class implementing IEditorToolbarContributor for the HexEditor.
//     Contributes format-aware toolbar items (format name chip, confidence badge,
//     Change Format dropdown) to the IDE's contextual toolbar pod.
//
// Architecture Notes:
//     Implements IEditorToolbarContributor from WpfHexEditor.Editor.Core.
//     Items are refreshed via RefreshToolbarItems() called after format detection.
// ==========================================================

using System.Collections.ObjectModel;
using WpfHexEditor.Core.Localization.Services;
using WpfHexEditor.Editor.Core;
using L10n = WpfHexEditor.Core.Properties.Resources;

namespace WpfHexEditor.HexEditor
{
    /// <summary>
    /// HexEditor partial class — IEditorToolbarContributor implementation (E4).
    /// Exposes format-aware contextual toolbar items: the active format chip,
    /// a confidence badge, and a "Change Format" dropdown.
    /// </summary>
    public partial class HexEditor : IEditorToolbarContributor
    {
        // ═══════════════════════════════════════════════════════════════════
        // Fields
        // ═══════════════════════════════════════════════════════════════════

        private ObservableCollection<EditorToolbarItem>? _toolbarItems;
        private EditorToolbarItem _tbFormat       = null!;
        private EditorToolbarItem _tbConfidence   = null!;
        private EditorToolbarItem _tbChangeFormat = null!;

        // ═══════════════════════════════════════════════════════════════════
        // IEditorToolbarContributor
        // ═══════════════════════════════════════════════════════════════════

        public ObservableCollection<EditorToolbarItem> ToolbarItems
            => _toolbarItems ??= BuildToolbarItems();

        // ═══════════════════════════════════════════════════════════════════
        // Build
        // ═══════════════════════════════════════════════════════════════════

        private ObservableCollection<EditorToolbarItem> BuildToolbarItems()
        {
            // Format name chip — display-only label (no command), updated by RefreshToolbarItems
            _tbFormat = new EditorToolbarItem
            {
                Icon    = "\uE7C1",   // Page icon (Segoe MDL2)
                Label   = "—",
                Tooltip = L10n.HE_Toolbar_DetectedFormat
            };

            // Separator
            var sep = new EditorToolbarItem { IsSeparator = true };

            // Confidence badge — display-only
            _tbConfidence = new EditorToolbarItem
            {
                Icon    = "\uE9D9",   // Shield icon
                Label   = "—",
                Tooltip = L10n.HE_Toolbar_ConfidenceScore
            };

            // Separator
            var sep2 = new EditorToolbarItem { IsSeparator = true };

            // Change Format dropdown — populated dynamically in RefreshToolbarItems
            _tbChangeFormat = new EditorToolbarItem
            {
                Icon          = "\uE8AB",   // Switch icon
                Label         = L10n.HE_Toolbar_ChangeFormat,
                Tooltip       = L10n.HE_Toolbar_ChangeFormatTooltip,
                DropdownItems = new ObservableCollection<EditorToolbarItem>()
            };

            // Refresh static labels/tooltips when culture changes at runtime.
            LocalizedResourceDictionary.CultureChanged += (_, _) =>
            {
                _tbFormat.Tooltip       = L10n.HE_Toolbar_DetectedFormat;
                _tbConfidence.Tooltip   = L10n.HE_Toolbar_ConfidenceScore;
                _tbChangeFormat.Label   = L10n.HE_Toolbar_ChangeFormat;
                _tbChangeFormat.Tooltip = L10n.HE_Toolbar_ChangeFormatTooltip;
                RefreshToolbarItems();
            };

            RefreshToolbarItems();

            return new ObservableCollection<EditorToolbarItem>
            {
                _tbFormat,
                sep,
                _tbConfidence,
                sep2,
                _tbChangeFormat
            };
        }

        // ═══════════════════════════════════════════════════════════════════
        // Refresh
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Syncs all toolbar item labels with current format detection state.
        /// Called after format detection and when candidates change.
        /// </summary>
        internal void RefreshToolbarItems()
        {
            if (_toolbarItems == null) return;

            // Format name chip
            if (_tbFormat != null)
                _tbFormat.Label = _detectedFormat?.FormatName ?? "—";

            // Confidence badge
            if (_tbConfidence != null)
            {
                if (_detectionCandidates?.Count > 0)
                {
                    var topScore = _detectionCandidates[0].ConfidenceScore;
                    _tbConfidence.Label   = $"{topScore:P0}";
                    _tbConfidence.Tooltip = string.Format(L10n.HE_Toolbar_ConfidenceFormat, $"{topScore:P1}");
                }
                else
                {
                    _tbConfidence.Label   = "—";
                    _tbConfidence.Tooltip = L10n.HE_Toolbar_ConfidenceScore;
                }
            }

            // Change Format dropdown — rebuild from current candidates
            if (_tbChangeFormat?.DropdownItems != null)
            {
                _tbChangeFormat.DropdownItems.Clear();
                if (_detectionCandidates != null)
                {
                    foreach (var candidate in _detectionCandidates)
                    {
                        var capture = candidate; // closure capture
                        _tbChangeFormat.DropdownItems.Add(new EditorToolbarItem
                        {
                            Label   = $"{capture.Format.FormatName} ({capture.ConfidenceScore:P0})",
                            Tooltip = capture.Format.Description ?? capture.Format.FormatName,
                            Command = new HexEditorRelayCommand(_ =>
                            {
                                // Switch active format — same path as ParsedFieldsPanel candidate switch
                                _detectedFormat      = capture.Format;
                                _detectionVariables  = capture.Variables;
                                RefreshParsedFields();
                                RefreshToolbarItems();
                                RefreshStatusBarItemValues();
                            })
                        });
                    }
                }

                _tbChangeFormat.IsEnabled = _tbChangeFormat.DropdownItems.Count > 1;
            }
        }
    }
}
