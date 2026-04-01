// ==========================================================
// Project: WpfHexEditor.Plugins.ClaudeAssistant
// File: ClaudeConnectionStatus.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.6
// Created: 2026-03-31
// License: GNU Affero General Public License v3.0 (AGPL-3.0)
// Description:
//     Connection state enum and ConnectionInfo record.
// ==========================================================
namespace WpfHexEditor.Plugins.ClaudeAssistant.Connection;

public enum ClaudeConnectionStatus
{
    NotConfigured,
    Connecting,
    Connected,
    RateLimited,
    Error,
    Offline
}

public sealed record ConnectionInfo(
    string ProviderId,
    string ModelId,
    int LatencyMs,
    ClaudeConnectionStatus Status,
    string? ErrorMessage = null);
