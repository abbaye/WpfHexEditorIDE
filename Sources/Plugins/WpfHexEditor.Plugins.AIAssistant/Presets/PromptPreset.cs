// ==========================================================
// Project: WpfHexEditor.Plugins.AIAssistant
// File: PromptPreset.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.6
// Created: 2026-03-31
// License: GNU Affero General Public License v3.0 (AGPL-3.0)
// Description:
//     Data model for a reusable prompt preset with auto-inject @mentions.
// ==========================================================
namespace WpfHexEditor.Plugins.AIAssistant.Presets;

public sealed class PromptPreset
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public string Prompt { get; set; } = "";
    public List<string> AutoInjectMentions { get; set; } = [];
    public string? IconGlyph { get; set; }
    public int Order { get; set; }
    public bool IsBuiltIn { get; set; }
}
