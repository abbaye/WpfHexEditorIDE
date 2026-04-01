// ==========================================================
// Project: WpfHexEditor.Plugins.ClaudeAssistant
// File: SessionMetadata.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.6
// Created: 2026-03-31
// License: GNU Affero General Public License v3.0 (AGPL-3.0)
// Description:
//     Lightweight metadata for conversation index (no full message history).
// ==========================================================
namespace WpfHexEditor.Plugins.ClaudeAssistant.Session;

public sealed class SessionMetadata
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string ProviderId { get; set; } = "";
    public string ModelId { get; set; } = "";
    public int MessageCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastModifiedAt { get; set; }
}
