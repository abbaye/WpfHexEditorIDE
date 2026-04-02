// ==========================================================
// Project: WpfHexEditor.Plugins.AIAssistant
// File: ContextPayload.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.6
// Created: 2026-03-31
// License: GNU Affero General Public License v3.0 (AGPL-3.0)
// Description:
//     Data structures for @mention context injection into chat messages.
// ==========================================================
namespace WpfHexEditor.Plugins.AIAssistant.Context;

public sealed class ContextPayload
{
    public List<ContextBlock> Blocks { get; } = [];
    public string CleanedText { get; set; } = "";
}

public sealed record ContextBlock(string Label, string Content, ContextKind Kind);

public enum ContextKind
{
    File,
    Selection,
    Errors,
    Solution,
    Hex
}
