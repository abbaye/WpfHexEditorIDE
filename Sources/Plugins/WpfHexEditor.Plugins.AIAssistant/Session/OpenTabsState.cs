// ==========================================================
// Project: WpfHexEditor.Plugins.AIAssistant
// File: OpenTabsState.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.6
// Created: 2026-04-01
// License: GNU Affero General Public License v3.0 (AGPL-3.0)
// Description:
//     Tracks which conversation tabs are open and which is active,
//     so the plugin restores exactly the same tabs on next startup.
// ==========================================================
namespace WpfHexEditor.Plugins.AIAssistant.Session;

public sealed class OpenTabsState
{
    public List<string> OpenSessionIds { get; set; } = [];
    public string? ActiveSessionId { get; set; }
}
