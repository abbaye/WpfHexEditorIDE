// ==========================================================
// Project: WpfHexEditor.Core
// File: FormatMetadataBuilder.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude (Anthropic)
// Created: 2026-03-26
// Description:
//     Populates format metadata on the IParsedFieldsPanel: navigation bookmarks (E3),
//     inspector groups (D4), export templates (D5), and forensic alerts (D3).
//     Extracted from HexEditor.ParsedFieldsIntegration.cs for reuse.
//
// Architecture Notes:
//     Stateless builder — takes FormatDefinition, VariableContext, and panel as params.
//     Returns metadata counts for FormatParsingCompleteEventArgs.
// ==========================================================

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using WpfHexEditor.Core.FormatDetection;
using WpfHexEditor.Core.Interfaces;

namespace WpfHexEditor.Core.Services.FormatParsing
{
    /// <summary>
    /// Populates format metadata on the parsed fields panel.
    /// </summary>
    internal static class FormatMetadataBuilder
    {
        /// <summary>
        /// Build navigation bookmarks from whfmt navigation.bookmarks (E3).
        /// Returns the list of bookmark offsets for editor registration.
        /// </summary>
        public static List<long> BuildBookmarks(
            FormatDefinition format,
            VariableContext? variableContext,
            long sourceLength,
            IParsedFieldsPanel panel)
        {
            var bookmarkOffsets = new List<long>();
            var navDef = format.Navigation;
            var panelBookmarks = new ObservableCollection<FormatNavigationBookmark>();

            if (navDef?.Bookmarks != null)
            {
                foreach (var bm in navDef.Bookmarks)
                {
                    if (string.IsNullOrWhiteSpace(bm.OffsetVar)) continue;
                    var varValue = variableContext?.GetVariable(bm.OffsetVar);
                    if (varValue == null) continue;

                    long offset;
                    try { offset = Convert.ToInt64(varValue); }
                    catch { continue; }
                    if (offset < 0 || offset >= sourceLength) continue;

                    bookmarkOffsets.Add(offset);

                    panelBookmarks.Add(new FormatNavigationBookmark
                    {
                        Name = bm.Name ?? bm.OffsetVar,
                        Offset = offset,
                        Icon = bm.Icon,
                        Color = bm.Color,
                        Description = $"0x{offset:X8}"
                    });
                }
            }

            panel.FormatInfo.Bookmarks = panelBookmarks.Count > 0 ? panelBookmarks : null;
            return bookmarkOffsets;
        }

        /// <summary>
        /// Build forensic alerts from failed/warning assertions (D3).
        /// </summary>
        public static List<AssertionResult>? BuildForensicAlerts(
            List<AssertionResult>? assertionResults,
            IParsedFieldsPanel panel)
        {
            var forensicAlerts = assertionResults?
                .Where(a => !a.Passed)
                .ToList();

            panel.FormatInfo.ForensicAlerts = forensicAlerts?.Count > 0 ? forensicAlerts : null;
            return forensicAlerts;
        }

        /// <summary>
        /// Build inspector groups from whfmt inspector.groups (D4).
        /// </summary>
        public static void BuildInspectorGroups(
            FormatDefinition format,
            VariableContext? variableContext,
            IParsedFieldsPanel panel)
        {
            var inspDef = format.Inspector;
            if (inspDef?.Groups?.Count > 0)
            {
                var groups = new List<InspectorGroupItem>();
                foreach (var g in inspDef.Groups)
                {
                    var item = new InspectorGroupItem
                    {
                        Title = g.Title ?? "Group",
                        Icon = g.Icon,
                        Highlight = g.Highlight,
                        IsExpanded = !g.Collapsed
                    };

                    if (g.Fields != null)
                    {
                        foreach (var varName in g.Fields)
                        {
                            string val = "\u2014"; // em-dash
                            if (variableContext != null && variableContext.HasVariable(varName))
                                val = variableContext.GetVariable(varName)?.ToString() ?? "null";
                            item.Fields.Add(new InspectorFieldItem
                            {
                                Name = varName,
                                DisplayValue = val
                            });
                        }
                    }
                    groups.Add(item);
                }
                panel.FormatInfo.InspectorGroups = groups;

                // Inspector badge
                if (!string.IsNullOrEmpty(inspDef.Badge) && variableContext != null
                    && variableContext.HasVariable(inspDef.Badge))
                    panel.FormatInfo.InspectorBadge = variableContext.GetVariable(inspDef.Badge)?.ToString();
            }
            else
            {
                panel.FormatInfo.InspectorGroups = null;
                panel.FormatInfo.InspectorBadge = null;
            }
        }

        /// <summary>
        /// Build export templates from whfmt exportTemplates (D5).
        /// </summary>
        public static void BuildExportTemplates(
            FormatDefinition format,
            IParsedFieldsPanel panel)
        {
            if (format.ExportTemplates?.Count > 0)
            {
                var templates = format.ExportTemplates
                    .Select(t => new ExportTemplateItem
                    {
                        Name = t.Name ?? "Export",
                        Format = t.Format ?? "json",
                        Source = t
                    })
                    .ToList();
                panel.FormatInfo.ExportTemplates = templates;
            }
            else
            {
                panel.FormatInfo.ExportTemplates = null;
            }
        }

        /// <summary>
        /// Build AI hints from whfmt aiHints (D6).
        /// Maps AnalysisContext, SuggestedInspections, KnownVulnerabilities, ForensicContext.
        /// </summary>
        public static void BuildAiHints(FormatDefinition format, IParsedFieldsPanel panel)
        {
            var src = format.AiHints;
            if (src == null) { panel.FormatInfo.AiHintsData = null; return; }

            var data = new AiHintsData
            {
                AnalysisContext = src.AnalysisContext,
                ForensicContext = src.ForensicContext
            };

            if (src.SuggestedInspections != null)
                foreach (var t in src.SuggestedInspections)
                    data.Inspections.Add(new AiInspectionItem { Text = t });

            if (src.KnownVulnerabilities != null)
                foreach (var t in src.KnownVulnerabilities)
                    data.Vulnerabilities.Add(new AiVulnerabilityChip { Text = t });

            var hasContent = data.TotalCount > 0 || data.HasAnalysisContext || data.HasForensicContext;
            panel.FormatInfo.AiHintsData = hasContent ? data : null;
        }
    }
}
