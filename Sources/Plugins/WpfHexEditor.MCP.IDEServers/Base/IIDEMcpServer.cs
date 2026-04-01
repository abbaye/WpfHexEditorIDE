// ==========================================================
// Project: WpfHexEditor.MCP.IDEServers
// File: IIDEMcpServer.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.6
// Created: 2026-03-31
// License: GNU Affero General Public License v3.0 (AGPL-3.0)
// Description:
//     Contract for in-process MCP servers that expose IDE state as tools.
// ==========================================================
namespace WpfHexEditor.MCP.IDEServers.Base;

public interface IIDEMcpServer : IAsyncDisposable
{
    string ServerId { get; }
    string DisplayName { get; }

    Task StartAsync(CancellationToken ct = default);
    Task StopAsync(CancellationToken ct = default);

    IReadOnlyList<McpToolDefinition> GetToolDefinitions();
    Task<string> CallToolAsync(string toolName, string argsJson, CancellationToken ct = default);
}

public sealed record McpToolDefinition(
    string Name,
    string Description,
    string InputSchemaJson);
