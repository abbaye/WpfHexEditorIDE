// ==========================================================
// Project: WpfHexEditor.MCP.IDEServers
// File: SolutionMcpServer.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.6
// Created: 2026-03-31
// License: GNU Affero General Public License v3.0 (AGPL-3.0)
// Description:
//     MCP server exposing solution/project management tools.
//     Tools: list_files, read_file, write_file, create_file, open_file, get_symbols, find_references.
// ==========================================================
using System.Text.Json;
using WpfHexEditor.MCP.IDEServers.Base;

namespace WpfHexEditor.MCP.IDEServers.Servers;

public sealed class SolutionMcpServer : IdeMcpServerBase
{
    public override string ServerId => "solution";
    public override string DisplayName => "Solution Explorer";

    private readonly Func<string?, string?, Task<object>>? _listFiles;
    private readonly Func<string, Task<object>>? _readFile;
    private readonly Func<string, string, Task<object>>? _writeFile;
    private readonly Func<string, string, string, string?, Task<object>>? _createFile;
    private readonly Func<string, Task<object>>? _openFile;

    public SolutionMcpServer(
        Func<string?, string?, Task<object>>? listFiles = null,
        Func<string, Task<object>>? readFile = null,
        Func<string, string, Task<object>>? writeFile = null,
        Func<string, string, string, string?, Task<object>>? createFile = null,
        Func<string, Task<object>>? openFile = null)
    {
        _listFiles = listFiles;
        _readFile = readFile;
        _writeFile = writeFile;
        _createFile = createFile;
        _openFile = openFile;

        RegisterTool("list_files",
            "List files in the current solution, optionally filtered by project or extension.",
            """{"type":"object","properties":{"project_name":{"type":"string"},"extension_filter":{"type":"string"}}}""",
            async (args, ct) =>
            {
                var proj = args.TryGetProperty("project_name", out var p) ? p.GetString() : null;
                var ext = args.TryGetProperty("extension_filter", out var e) ? e.GetString() : null;
                return _listFiles is not null ? await _listFiles(proj, ext) : new { error = "SolutionManager not available" };
            });

        RegisterTool("read_file",
            "Read the content of a file in the solution.",
            """{"type":"object","properties":{"file_path":{"type":"string"}},"required":["file_path"]}""",
            async (args, ct) =>
            {
                var path = args.GetProperty("file_path").GetString()!;
                return _readFile is not null ? await _readFile(path) : new { error = "SolutionManager not available" };
            });

        RegisterTool("write_file",
            "Write content to a file in the solution.",
            """{"type":"object","properties":{"file_path":{"type":"string"},"content":{"type":"string"}},"required":["file_path","content"]}""",
            async (args, ct) =>
            {
                var path = args.GetProperty("file_path").GetString()!;
                var content = args.GetProperty("content").GetString()!;
                return _writeFile is not null ? await _writeFile(path, content) : new { error = "SolutionManager not available" };
            });

        RegisterTool("create_file",
            "Create a new file in a project.",
            """{"type":"object","properties":{"project_name":{"type":"string"},"file_name":{"type":"string"},"content":{"type":"string"},"folder":{"type":"string"}},"required":["project_name","file_name","content"]}""",
            async (args, ct) =>
            {
                var proj = args.GetProperty("project_name").GetString()!;
                var name = args.GetProperty("file_name").GetString()!;
                var content = args.GetProperty("content").GetString()!;
                var folder = args.TryGetProperty("folder", out var f) ? f.GetString() : null;
                return _createFile is not null ? await _createFile(proj, name, content, folder) : new { error = "SolutionManager not available" };
            });

        RegisterTool("open_file",
            "Open a file in the IDE editor.",
            """{"type":"object","properties":{"file_path":{"type":"string"}},"required":["file_path"]}""",
            async (args, ct) =>
            {
                var path = args.GetProperty("file_path").GetString()!;
                return _openFile is not null ? await _openFile(path) : new { error = "DocumentHost not available" };
            });
    }
}
