// ==========================================================
// Project: WpfHexEditor.Plugins.AIAssistant
// File: AIConnectionStatus.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.6
// Created: 2026-03-31
// License: GNU Affero General Public License v3.0 (AGPL-3.0)
// Description:
//     Connection state enum and ConnectionInfo record.
// ==========================================================
namespace WpfHexEditor.Plugins.AIAssistant.Connection;

public enum AIConnectionStatus
{
    NotConfigured,
    Connecting,
    Connected,
    CliConnected,
    RateLimited,
    Error,
    Offline
}

public sealed record ConnectionInfo(
    string ProviderId,
    string ModelId,
    int LatencyMs,
    AIConnectionStatus Status,
    string? ErrorMessage = null);
