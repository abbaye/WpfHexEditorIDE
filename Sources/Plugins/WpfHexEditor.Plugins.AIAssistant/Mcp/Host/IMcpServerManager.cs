// ==========================================================
// Project: WpfHexEditor.Plugins.AIAssistant
// File: IMcpServerManager.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.6
// Created: 2026-03-31
// License: GNU Affero General Public License v3.0 (AGPL-3.0)
// Description:
//     Contract for the MCP server orchestrator (stdio + in-process servers).
// ==========================================================
using WpfHexEditor.Plugins.AIAssistant.Api;

namespace WpfHexEditor.Plugins.AIAssistant.Mcp.Host;

public interface IMcpServerManager : IAsyncDisposable
{
    IReadOnlyList<McpServerInfo> GetRunningServers();
    IReadOnlyList<ToolDefinition> GetAllTools();
    Task<string> CallToolAsync(string toolName, string argsJson, CancellationToken ct = default);
    Task StartAllAsync(CancellationToken ct = default);
    Task StopAllAsync(CancellationToken ct = default);
}

public sealed record McpServerInfo(string ServerId, string DisplayName, bool IsRunning, bool IsIdeServer);
