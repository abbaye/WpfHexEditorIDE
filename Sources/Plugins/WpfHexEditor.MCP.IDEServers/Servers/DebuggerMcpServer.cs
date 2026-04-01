// ==========================================================
// Project: WpfHexEditor.MCP.IDEServers
// File: DebuggerMcpServer.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.6
// Created: 2026-03-31
// License: GNU Affero General Public License v3.0 (AGPL-3.0)
// Description:
//     MCP server exposing debugger tools.
//     Tools: get_callstack, get_variables, evaluate_expression, set_breakpoint,
//            get_breakpoints, delete_breakpoint, get_session_state.
// ==========================================================
using System.Text.Json;
using WpfHexEditor.MCP.IDEServers.Base;

namespace WpfHexEditor.MCP.IDEServers.Servers;

public sealed class DebuggerMcpServer : IdeMcpServerBase
{
    public override string ServerId => "debugger";
    public override string DisplayName => "Debugger";

    private readonly Func<Task<object>>? _getCallStack;
    private readonly Func<int?, Task<object>>? _getVariables;
    private readonly Func<string, int?, Task<object>>? _evaluateExpression;
    private readonly Func<string, int, string?, Task<object>>? _setBreakpoint;
    private readonly Func<Task<object>>? _getBreakpoints;
    private readonly Func<string, int, Task<object>>? _deleteBreakpoint;
    private readonly Func<Task<object>>? _getSessionState;

    public DebuggerMcpServer(
        Func<Task<object>>? getCallStack = null,
        Func<int?, Task<object>>? getVariables = null,
        Func<string, int?, Task<object>>? evaluateExpression = null,
        Func<string, int, string?, Task<object>>? setBreakpoint = null,
        Func<Task<object>>? getBreakpoints = null,
        Func<string, int, Task<object>>? deleteBreakpoint = null,
        Func<Task<object>>? getSessionState = null)
    {
        _getCallStack = getCallStack;
        _getVariables = getVariables;
        _evaluateExpression = evaluateExpression;
        _setBreakpoint = setBreakpoint;
        _getBreakpoints = getBreakpoints;
        _deleteBreakpoint = deleteBreakpoint;
        _getSessionState = getSessionState;

        RegisterTool("get_callstack",
            "Get the current call stack (only available when debugger is paused).",
            """{"type":"object","properties":{}}""",
            async (args, ct) => _getCallStack is not null ? await _getCallStack() : new { error = "Debugger not available" });

        RegisterTool("get_variables",
            "Get variables in the current scope.",
            """{"type":"object","properties":{"scope_ref":{"type":"integer","description":"Optional scope reference. Omit for local scope."}}}""",
            async (args, ct) =>
            {
                var scope = args.TryGetProperty("scope_ref", out var s) ? (int?)s.GetInt32() : null;
                return _getVariables is not null ? await _getVariables(scope) : new { error = "Debugger not available" };
            });

        RegisterTool("evaluate_expression",
            "Evaluate an expression in the debugger context.",
            """{"type":"object","properties":{"expression":{"type":"string"},"frame_id":{"type":"integer"}},"required":["expression"]}""",
            async (args, ct) =>
            {
                var expr = args.GetProperty("expression").GetString()!;
                var frameId = args.TryGetProperty("frame_id", out var f) ? (int?)f.GetInt32() : null;
                return _evaluateExpression is not null ? await _evaluateExpression(expr, frameId) : new { error = "Debugger not available" };
            });

        RegisterTool("set_breakpoint",
            "Set or toggle a breakpoint at a file and line.",
            """{"type":"object","properties":{"file_path":{"type":"string"},"line":{"type":"integer"},"condition":{"type":"string"}},"required":["file_path","line"]}""",
            async (args, ct) =>
            {
                var path = args.GetProperty("file_path").GetString()!;
                var line = args.GetProperty("line").GetInt32();
                var cond = args.TryGetProperty("condition", out var c) ? c.GetString() : null;
                return _setBreakpoint is not null ? await _setBreakpoint(path, line, cond) : new { error = "Debugger not available" };
            });

        RegisterTool("get_breakpoints",
            "Get all breakpoints.",
            """{"type":"object","properties":{}}""",
            async (args, ct) => _getBreakpoints is not null ? await _getBreakpoints() : new { error = "Debugger not available" });

        RegisterTool("delete_breakpoint",
            "Delete a breakpoint at a file and line.",
            """{"type":"object","properties":{"file_path":{"type":"string"},"line":{"type":"integer"}},"required":["file_path","line"]}""",
            async (args, ct) =>
            {
                var path = args.GetProperty("file_path").GetString()!;
                var line = args.GetProperty("line").GetInt32();
                return _deleteBreakpoint is not null ? await _deleteBreakpoint(path, line) : new { error = "Debugger not available" };
            });

        RegisterTool("get_session_state",
            "Get the current debugger session state (idle, running, paused, etc.).",
            """{"type":"object","properties":{}}""",
            async (args, ct) => _getSessionState is not null ? await _getSessionState() : new { state = "idle", is_active = false });
    }
}
