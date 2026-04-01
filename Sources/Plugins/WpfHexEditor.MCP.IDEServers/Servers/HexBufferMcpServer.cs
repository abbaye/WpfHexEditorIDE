// ==========================================================
// Project: WpfHexEditor.MCP.IDEServers
// File: HexBufferMcpServer.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.6
// Created: 2026-03-31
// License: GNU Affero General Public License v3.0 (AGPL-3.0)
// Description:
//     MCP server exposing hex editor buffer tools.
//     Tools: read_bytes, get_selection, get_bookmarks, search_hex, navigate_to.
// ==========================================================
using System.Text.Json;
using WpfHexEditor.MCP.IDEServers.Base;

namespace WpfHexEditor.MCP.IDEServers.Servers;

public sealed class HexBufferMcpServer : IdeMcpServerBase
{
    public override string ServerId => "hex-buffer";
    public override string DisplayName => "Hex Buffer";

    private readonly Func<long, int, Task<object>>? _readBytes;
    private readonly Func<Task<object>>? _getSelection;
    private readonly Func<Task<object>>? _getBookmarks;
    private readonly Func<long, Task<object>>? _navigateTo;

    public HexBufferMcpServer(
        Func<long, int, Task<object>>? readBytes = null,
        Func<Task<object>>? getSelection = null,
        Func<Task<object>>? getBookmarks = null,
        Func<long, Task<object>>? navigateTo = null)
    {
        _readBytes = readBytes;
        _getSelection = getSelection;
        _getBookmarks = getBookmarks;
        _navigateTo = navigateTo;

        RegisterTool("read_bytes",
            "Read bytes from the active hex editor at a given offset.",
            """{"type":"object","properties":{"offset":{"type":"integer"},"length":{"type":"integer","default":256}},"required":["offset"]}""",
            async (args, ct) =>
            {
                var offset = args.GetProperty("offset").GetInt64();
                var length = args.TryGetProperty("length", out var l) ? l.GetInt32() : 256;
                return _readBytes is not null
                    ? await _readBytes(offset, length)
                    : new { error = "HexEditorService not available" };
            });

        RegisterTool("get_selection",
            "Get the current selection in the hex editor (start, end, hex, ascii).",
            """{"type":"object","properties":{}}""",
            async (args, ct) => _getSelection is not null
                ? await _getSelection()
                : new { error = "HexEditorService not available" });

        RegisterTool("get_bookmarks",
            "Get all bookmarks in the active hex editor.",
            """{"type":"object","properties":{}}""",
            async (args, ct) => _getBookmarks is not null
                ? await _getBookmarks()
                : new { error = "HexEditorService not available" });

        RegisterTool("navigate_to",
            "Navigate the hex editor to a specific byte offset.",
            """{"type":"object","properties":{"offset":{"type":"integer"}},"required":["offset"]}""",
            async (args, ct) =>
            {
                var offset = args.GetProperty("offset").GetInt64();
                return _navigateTo is not null
                    ? await _navigateTo(offset)
                    : new { error = "HexEditorService not available" };
            });
    }
}
