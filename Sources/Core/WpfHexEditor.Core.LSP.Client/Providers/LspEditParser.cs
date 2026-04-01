// ==========================================================
// Project: WpfHexEditor.Core.LSP.Client
// File: Providers/LspEditParser.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-23
// Description:
//     Shared JSON-to-DTO parser for LSP workspace edit responses.
//     Used by both LspCodeActionProvider and LspRenameProvider.
//
// Architecture Notes:
//     Pattern: Static utility — pure functions, no state.
//     Supports both `changes` (uri-keyed) and `documentChanges` (TextDocumentEdit array) formats.
// ==========================================================

using System.Collections.Generic;
using System.Text.Json.Nodes;
using WpfHexEditor.Editor.Core.LSP;
using WpfHexEditor.Core.LSP.Client.Transport;

namespace WpfHexEditor.Core.LSP.Client.Providers;

internal static class LspEditParser
{
    /// <summary>
    /// Parses a WorkspaceEdit JSON node into an <see cref="LspWorkspaceEdit"/>.
    /// Returns null when the node is null or has no recognised changes.
    /// </summary>
    internal static LspWorkspaceEdit? ParseWorkspaceEdit(JsonNode? node)
    {
        if (node is null) return null;

        var dict = new Dictionary<string, IReadOnlyList<LspTextEdit>>(System.StringComparer.OrdinalIgnoreCase);

        // Format 1: changes { "uri": [TextEdit] }
        if (node["changes"] is JsonObject changesObj)
        {
            foreach (var kv in changesObj)
            {
                var edits = ParseTextEdits(kv.Value as JsonArray);
                if (edits.Count > 0)
                    dict[LspDocumentSync.FromUri(kv.Key)] = edits;
            }
        }

        // Format 2: documentChanges [{ textDocument: { uri }, edits: [TextEdit] }]
        if (dict.Count == 0 && node["documentChanges"] is JsonArray docChanges)
        {
            foreach (var dc in docChanges)
            {
                if (dc is null) continue;
                var uri   = dc["textDocument"]?["uri"]?.GetValue<string>();
                if (uri is null) continue;
                var edits = ParseTextEdits(dc["edits"] as JsonArray);
                if (edits.Count > 0)
                    dict[LspDocumentSync.FromUri(uri)] = edits;
            }
        }

        return dict.Count == 0 ? null : new LspWorkspaceEdit { Changes = dict };
    }

    internal static IReadOnlyList<LspTextEdit> ParseTextEdits(JsonArray? arr)
    {
        if (arr is null) return System.Array.Empty<LspTextEdit>();

        var list = new List<LspTextEdit>(arr.Count);
        foreach (var e in arr)
        {
            if (e is null) continue;
            list.Add(new LspTextEdit
            {
                StartLine   = e["range"]?["start"]?["line"]?.GetValue<int>()      ?? 0,
                StartColumn = e["range"]?["start"]?["character"]?.GetValue<int>() ?? 0,
                EndLine     = e["range"]?["end"]?["line"]?.GetValue<int>()        ?? 0,
                EndColumn   = e["range"]?["end"]?["character"]?.GetValue<int>()   ?? 0,
                NewText     = e["newText"]?.GetValue<string>()                     ?? string.Empty,
            });
        }
        return list;
    }
}
