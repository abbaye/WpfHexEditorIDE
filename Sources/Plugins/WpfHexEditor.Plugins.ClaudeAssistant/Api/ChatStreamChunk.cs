// ==========================================================
// Project: WpfHexEditor.Plugins.ClaudeAssistant
// File: ChatStreamChunk.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.6
// Created: 2026-03-31
// License: GNU Affero General Public License v3.0 (AGPL-3.0)
// Description:
//     Provider-agnostic streaming chunk model for token-by-token delivery.
// ==========================================================
namespace WpfHexEditor.Plugins.ClaudeAssistant.Api;

public sealed record ChatStreamChunk(
    ChunkKind Kind,
    string? Text = null,
    string? ToolCallId = null,
    string? ToolName = null,
    string? ToolInputJson = null,
    string? ThinkingText = null,
    bool IsFinal = false,
    string? ErrorMessage = null);

public enum ChunkKind
{
    TextDelta,
    ToolUseStart,
    ToolInputDelta,
    ThinkingDelta,
    Done,
    Error
}
