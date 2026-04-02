// ==========================================================
// Project: WpfHexEditor.Plugins.AIAssistant
// File: AICommandEntry.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.6
// Created: 2026-04-01
// License: GNU Affero General Public License v3.0 (AGPL-3.0)
// Description:
//     Data model for a Claude command palette entry.
// ==========================================================
namespace WpfHexEditor.Plugins.AIAssistant.Panel.CommandPalette;

public sealed record AICommandEntry(
    string Name,
    string? Description,
    string? IconGlyph,
    string Section,
    Action Execute);
