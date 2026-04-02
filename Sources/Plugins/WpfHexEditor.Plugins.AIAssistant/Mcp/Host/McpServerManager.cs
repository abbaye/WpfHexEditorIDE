// ==========================================================
// Project: WpfHexEditor.Plugins.AIAssistant
// File: McpServerManager.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.6
// Created: 2026-03-31
// License: GNU Affero General Public License v3.0 (AGPL-3.0)
// Description:
//     Orchestrates all MCP servers. Aggregates tool lists, routes tool calls.
// ==========================================================
using WpfHexEditor.Core.MCP.Base;
using WpfHexEditor.Plugins.AIAssistant.Api;
using WpfHexEditor.Plugins.AIAssistant.Options;

namespace WpfHexEditor.Plugins.AIAssistant.Mcp.Host;

public sealed class McpServerManager : IMcpServerManager
{
    private readonly Dictionary<string, StdioMcpServerProcess> _stdioProcesses = [];
    private readonly Dictionary<string, IIDEMcpServer> _ideServers = [];
    private readonly Dictionary<string, string> _toolToServer = [];
    private readonly List<ToolDefinition> _allTools = [];

    // --- IDE server registration ---

    public void RegisterIdeServer(IIDEMcpServer server)
    {
        _ideServers[server.ServerId] = server;
    }

    // --- Lifecycle ---

    public async Task StartAllAsync(CancellationToken ct = default)
    {
        // Start IDE servers
        foreach (var (id, server) in _ideServers)
        {
            await server.StartAsync(ct);
            foreach (var tool in server.GetToolDefinitions())
            {
                var def = new ToolDefinition(tool.Name, tool.Description, tool.InputSchemaJson);
                _allTools.Add(def);
                _toolToServer[tool.Name] = id;
            }
        }

        // Start configured Node.js stdio servers
        foreach (var config in AIAssistantOptions.Instance.McpServers.Where(s => s.Enabled && !s.IsIdeServer))
        {
            var process = new StdioMcpServerProcess(config.ServerId, config.Command, config.Args, config.Env);
            try
            {
                await process.StartAsync(ct);
                _stdioProcesses[config.ServerId] = process;

                foreach (var tool in await process.ListToolsAsync(ct))
                {
                    _allTools.Add(tool);
                    _toolToServer[tool.Name] = config.ServerId;
                }
            }
            catch
            {
                // Graceful degradation — log but don't crash
                process.Dispose();
            }
        }
    }

    public async Task StopAllAsync(CancellationToken ct = default)
    {
        foreach (var process in _stdioProcesses.Values)
            process.Dispose();
        _stdioProcesses.Clear();

        foreach (var server in _ideServers.Values)
            await server.StopAsync(ct);

        _allTools.Clear();
        _toolToServer.Clear();
    }

    // --- Tool access ---

    public IReadOnlyList<ToolDefinition> GetAllTools() => _allTools;

    public IReadOnlyList<McpServerInfo> GetRunningServers()
    {
        var list = new List<McpServerInfo>();
        foreach (var (id, _) in _ideServers)
            list.Add(new McpServerInfo(id, id, true, true));
        foreach (var (id, p) in _stdioProcesses)
            list.Add(new McpServerInfo(id, id, p.IsRunning, false));
        return list;
    }

    public async Task<string> CallToolAsync(string toolName, string argsJson, CancellationToken ct = default)
    {
        if (!_toolToServer.TryGetValue(toolName, out var serverId))
            return """{"error":"Tool not found"}""";

        if (_ideServers.TryGetValue(serverId, out var ideServer))
            return await ideServer.CallToolAsync(toolName, argsJson, ct);

        if (_stdioProcesses.TryGetValue(serverId, out var stdioProcess))
            return await stdioProcess.CallToolAsync(toolName, argsJson, ct);

        return """{"error":"Server not running"}""";
    }

    public async ValueTask DisposeAsync()
    {
        await StopAllAsync();
    }
}
