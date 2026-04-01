// ==========================================================
// Project: WpfHexEditor.MCP.IDEServers
// File: IdeMcpServerBase.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.6
// Created: 2026-03-31
// License: GNU Affero General Public License v3.0 (AGPL-3.0)
// Description:
//     Base class for IDE MCP servers with tool registration and dispatch.
// ==========================================================
using System.Text.Json;

namespace WpfHexEditor.MCP.IDEServers.Base;

public abstract class IdeMcpServerBase : IIDEMcpServer
{
    public abstract string ServerId { get; }
    public abstract string DisplayName { get; }

    private readonly Dictionary<string, Func<JsonElement, CancellationToken, Task<object>>> _toolHandlers = [];
    private readonly List<McpToolDefinition> _toolDefinitions = [];

    protected void RegisterTool(string name, string description, string inputSchemaJson,
        Func<JsonElement, CancellationToken, Task<object>> handler)
    {
        _toolDefinitions.Add(new McpToolDefinition(name, description, inputSchemaJson));
        _toolHandlers[name] = handler;
    }

    public IReadOnlyList<McpToolDefinition> GetToolDefinitions() => _toolDefinitions;

    public async Task<string> CallToolAsync(string toolName, string argsJson, CancellationToken ct = default)
    {
        if (!_toolHandlers.TryGetValue(toolName, out var handler))
            return JsonSerializer.Serialize(new { error = $"Unknown tool: {toolName}" });

        var args = JsonDocument.Parse(argsJson).RootElement;
        var result = await handler(args, ct);
        return JsonSerializer.Serialize(result);
    }

    public virtual Task StartAsync(CancellationToken ct = default) => Task.CompletedTask;
    public virtual Task StopAsync(CancellationToken ct = default) => Task.CompletedTask;
    public ValueTask DisposeAsync() => new(StopAsync());
}
