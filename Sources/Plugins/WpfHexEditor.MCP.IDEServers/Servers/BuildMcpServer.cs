// ==========================================================
// Project: WpfHexEditor.MCP.IDEServers
// File: BuildMcpServer.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.6
// Created: 2026-03-31
// License: GNU Affero General Public License v3.0 (AGPL-3.0)
// Description:
//     MCP server exposing build system tools.
//     Tools: get_errors, run_build, cancel_build, is_build_running, get_output.
// ==========================================================
using System.Text.Json;
using WpfHexEditor.MCP.IDEServers.Base;

namespace WpfHexEditor.MCP.IDEServers.Servers;

public sealed class BuildMcpServer : IdeMcpServerBase
{
    public override string ServerId => "build";
    public override string DisplayName => "Build System";

    private readonly Func<string?, Task<object>>? _getErrors;
    private readonly Func<string?, Task<object>>? _runBuild;
    private readonly Func<Task<object>>? _cancelBuild;
    private readonly Func<Task<object>>? _isBuildRunning;
    private readonly Func<int?, Task<object>>? _getOutput;

    public BuildMcpServer(
        Func<string?, Task<object>>? getErrors = null,
        Func<string?, Task<object>>? runBuild = null,
        Func<Task<object>>? cancelBuild = null,
        Func<Task<object>>? isBuildRunning = null,
        Func<int?, Task<object>>? getOutput = null)
    {
        _getErrors = getErrors;
        _runBuild = runBuild;
        _cancelBuild = cancelBuild;
        _isBuildRunning = isBuildRunning;
        _getOutput = getOutput;

        RegisterTool("get_errors",
            "Get build errors and warnings from the last build.",
            """{"type":"object","properties":{"severity_filter":{"type":"string","enum":["error","warning","all"],"default":"all"}}}""",
            async (args, ct) =>
            {
                var filter = args.TryGetProperty("severity_filter", out var f) ? f.GetString() : "all";
                return _getErrors is not null ? await _getErrors(filter) : new { error = "BuildSystem not available" };
            });

        RegisterTool("run_build",
            "Run a build (solution, dirty, or specific project).",
            """{"type":"object","properties":{"target":{"type":"string","enum":["solution","dirty","project"],"default":"solution"}}}""",
            async (args, ct) =>
            {
                var target = args.TryGetProperty("target", out var t) ? t.GetString() : "solution";
                return _runBuild is not null ? await _runBuild(target) : new { error = "BuildSystem not available" };
            });

        RegisterTool("cancel_build",
            "Cancel the currently running build.",
            """{"type":"object","properties":{}}""",
            async (args, ct) => _cancelBuild is not null ? await _cancelBuild() : new { error = "BuildSystem not available" });

        RegisterTool("is_build_running",
            "Check if a build is currently in progress.",
            """{"type":"object","properties":{}}""",
            async (args, ct) => _isBuildRunning is not null ? await _isBuildRunning() : new { error = "BuildSystem not available" });

        RegisterTool("get_output",
            "Get the build output log.",
            """{"type":"object","properties":{"last_n_lines":{"type":"integer","default":100}}}""",
            async (args, ct) =>
            {
                var lines = args.TryGetProperty("last_n_lines", out var n) ? (int?)n.GetInt32() : 100;
                return _getOutput is not null ? await _getOutput(lines) : new { error = "BuildSystem not available" };
            });
    }
}
