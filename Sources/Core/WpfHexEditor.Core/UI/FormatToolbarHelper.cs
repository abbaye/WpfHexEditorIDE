// ==========================================================
// Project: WpfHexEditor.Core
// File: FormatToolbarHelper.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude (Anthropic)
// Created: 2026-03-26
// Description:
//     Shared helper for building format-related toolbar and status bar items.
//     Any editor implementing IEditorToolbarContributor / IStatusBarContributor
//     can use these helpers to display detected format info without duplicating logic.
//
// Architecture Notes:
//     Static utility — takes IFormatParsingService state, returns display strings.
//     No WPF dependencies — works with plain string/bool values.
// ==========================================================

using WpfHexEditor.Core.Interfaces;

namespace WpfHexEditor.Core.UI
{
    /// <summary>
    /// Shared helpers for building format-related toolbar and status bar display values.
    /// </summary>
    public static class FormatToolbarHelper
    {
        /// <summary>
        /// Build the format chip label (e.g. "PE Executable").
        /// </summary>
        public static string GetFormatChipLabel(IFormatParsingService? service)
        {
            if (service?.ActiveFormat == null) return string.Empty;
            return service.ActiveFormat.FormatName ?? "Unknown";
        }

        /// <summary>
        /// Build the confidence badge label (e.g. "97%").
        /// </summary>
        public static string GetConfidenceBadge(IFormatParsingService? service)
        {
            if (service?.Candidates == null || service.Candidates.Count == 0)
                return string.Empty;
            return $"{service.Candidates[0].ConfidenceScore:P0}";
        }

        /// <summary>
        /// Build the status bar format display text (e.g. "Format: PE Executable").
        /// </summary>
        public static string GetStatusBarFormatText(IFormatParsingService? service)
        {
            if (service?.ActiveFormat == null) return string.Empty;
            return $"Format: {service.ActiveFormat.FormatName}";
        }

        /// <summary>
        /// Whether format info is available for display.
        /// </summary>
        public static bool HasFormat(IFormatParsingService? service)
            => service?.ActiveFormat != null;

        /// <summary>
        /// Whether multiple format candidates are available for a dropdown.
        /// </summary>
        public static bool HasMultipleCandidates(IFormatParsingService? service)
            => service?.Candidates != null && service.Candidates.Count > 1;
    }
}
