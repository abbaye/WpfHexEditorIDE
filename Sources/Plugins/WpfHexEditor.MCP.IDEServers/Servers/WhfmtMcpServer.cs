// ==========================================================
// Project: WpfHexEditor.MCP.IDEServers
// File: WhfmtMcpServer.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.6
// Created: 2026-03-31
// License: GNU Affero General Public License v3.0 (AGPL-3.0)
// Description:
//     MCP server exposing WHFMT binary format parsing tools.
//     Tools: parse_format, get_fields, get_forensic_alerts, get_format_catalog_summary.
// ==========================================================
using System.Text.Json;
using WpfHexEditor.MCP.IDEServers.Base;

namespace WpfHexEditor.MCP.IDEServers.Servers;

public sealed class WhfmtMcpServer : IdeMcpServerBase
{
    public override string ServerId => "whfmt";
    public override string DisplayName => "WHFMT Format Parser";

    // Service references injected from IIDEHostContext at runtime
    private readonly Func<string?, Task<object>>? _parseFormat;
    private readonly Func<long?, long?, int?, Task<object>>? _getFields;
    private readonly Func<Task<object>>? _getForensicAlerts;

    public WhfmtMcpServer(
        Func<string?, Task<object>>? parseFormat = null,
        Func<long?, long?, int?, Task<object>>? getFields = null,
        Func<Task<object>>? getForensicAlerts = null)
    {
        _parseFormat = parseFormat;
        _getFields = getFields;
        _getForensicAlerts = getForensicAlerts;

        RegisterTool("parse_format",
            "Parse the binary format of the active file or a specified file path.",
            """{"type":"object","properties":{"file_path":{"type":"string","description":"Optional file path. Uses active hex editor file if omitted."}}}""",
            async (args, ct) =>
            {
                var path = args.TryGetProperty("file_path", out var fp) ? fp.GetString() : null;
                return _parseFormat is not null
                    ? await _parseFormat(path)
                    : new { error = "FormatParsingService not available" };
            });

        RegisterTool("get_fields",
            "Get parsed binary fields from the active file.",
            """{"type":"object","properties":{"offset_start":{"type":"integer"},"offset_end":{"type":"integer"},"limit":{"type":"integer","default":50}}}""",
            async (args, ct) =>
            {
                var start = args.TryGetProperty("offset_start", out var s) ? (long?)s.GetInt64() : null;
                var end = args.TryGetProperty("offset_end", out var e) ? (long?)e.GetInt64() : null;
                var limit = args.TryGetProperty("limit", out var l) ? l.GetInt32() : 50;
                return _getFields is not null
                    ? await _getFields(start, end, limit)
                    : new { error = "FormatParsingService not available" };
            });

        RegisterTool("get_forensic_alerts",
            "Get forensic alerts (failed assertions) from the active file's format analysis.",
            """{"type":"object","properties":{}}""",
            async (args, ct) =>
            {
                return _getForensicAlerts is not null
                    ? await _getForensicAlerts()
                    : new { error = "FormatParsingService not available" };
            });
    }
}
