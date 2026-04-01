// ==========================================================
// Project: WpfHexEditor.Plugins.ClaudeAssistant
// File: StdioMcpServerProcess.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.6
// Created: 2026-03-31
// License: GNU Affero General Public License v3.0 (AGPL-3.0)
// Description:
//     Manages a Node.js MCP server process via stdio JSON-RPC.
// ==========================================================
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using WpfHexEditor.Plugins.ClaudeAssistant.Api;

namespace WpfHexEditor.Plugins.ClaudeAssistant.Mcp.Host;

public sealed class StdioMcpServerProcess : IDisposable
{
    private readonly string _serverId;
    private readonly string _command;
    private readonly string[] _args;
    private readonly Dictionary<string, string> _env;
    private Process? _process;
    private int _requestId;

    public bool IsRunning => _process is { HasExited: false };

    public StdioMcpServerProcess(string serverId, string command, string[] args, Dictionary<string, string> env)
    {
        _serverId = serverId;
        _command = command;
        _args = args;
        _env = env;
    }

    public Task StartAsync(CancellationToken ct = default)
    {
        var psi = new ProcessStartInfo
        {
            FileName = _command,
            Arguments = string.Join(' ', _args),
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8
        };

        foreach (var (key, val) in _env)
            psi.Environment[key] = val;

        _process = Process.Start(psi)
            ?? throw new InvalidOperationException($"Failed to start MCP server: {_serverId}");

        // Send initialize request (MCP protocol)
        return SendRequestAsync("initialize", new
        {
            protocolVersion = "2024-11-05",
            capabilities = new { },
            clientInfo = new { name = "WpfHexEditor", version = "1.0.0" }
        }, ct);
    }

    public async Task<List<ToolDefinition>> ListToolsAsync(CancellationToken ct = default)
    {
        var result = await SendRequestAsync("tools/list", new { }, ct);
        var tools = new List<ToolDefinition>();

        try
        {
            using var doc = JsonDocument.Parse(result);
            if (doc.RootElement.TryGetProperty("result", out var res)
                && res.TryGetProperty("tools", out var toolsArray))
            {
                foreach (var t in toolsArray.EnumerateArray())
                {
                    var name = t.GetProperty("name").GetString() ?? "";
                    var desc = t.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "";
                    var schema = t.TryGetProperty("inputSchema", out var s) ? s.GetRawText() : "{}";
                    tools.Add(new ToolDefinition(name, desc, schema));
                }
            }
        }
        catch { /* malformed response */ }

        return tools;
    }

    public async Task<string> CallToolAsync(string toolName, string argsJson, CancellationToken ct = default)
    {
        var args = JsonDocument.Parse(argsJson).RootElement;
        return await SendRequestAsync("tools/call", new { name = toolName, arguments = args }, ct);
    }

    private async Task<string> SendRequestAsync(string method, object @params, CancellationToken ct)
    {
        if (_process is null || _process.HasExited)
            return """{"error":"Process not running"}""";

        var id = Interlocked.Increment(ref _requestId);
        var request = JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            id,
            method,
            @params
        });

        await _process.StandardInput.WriteLineAsync(request.AsMemory(), ct);
        await _process.StandardInput.FlushAsync();

        // Read response line (simple sync — one request at a time for now)
        var responseLine = await _process.StandardOutput.ReadLineAsync(ct);
        return responseLine ?? """{"error":"No response"}""";
    }

    public void Dispose()
    {
        if (_process is { HasExited: false })
        {
            try
            {
                _process.StandardInput.Close();
                if (!_process.WaitForExit(3000))
                    _process.Kill();
            }
            catch { /* ignore cleanup errors */ }
        }
        _process?.Dispose();
    }
}
