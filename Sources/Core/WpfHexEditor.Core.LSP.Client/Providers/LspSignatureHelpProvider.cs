// ==========================================================
// Project: WpfHexEditor.Core.LSP.Client
// File: Providers/LspSignatureHelpProvider.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-31
// Description:
//     Sends textDocument/signatureHelp requests and parses the full LSP response
//     including all overloads, active signature/parameter indices, and parameter labels
//     (both string and [start,end] offset formats are handled).
//
// Architecture Notes:
//     Stateless — called by LspClientImpl on '(' and ',' keystrokes.
//     Handles both MarkupContent { kind, value } and plain-string documentation.
// ==========================================================

using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using WpfHexEditor.Core.LSP.Client.Transport;
using WpfHexEditor.Editor.Core.LSP;

namespace WpfHexEditor.Core.LSP.Client.Providers;

internal sealed class LspSignatureHelpProvider
{
    private readonly LspJsonRpcChannel _channel;

    internal LspSignatureHelpProvider(LspJsonRpcChannel channel)
        => _channel = channel;

    internal async Task<LspSignatureHelpResult?> GetAsync(
        string filePath, int line, int column, CancellationToken ct)
    {
        var uri     = LspDocumentSync.ToUri(filePath);
        var @params = new
        {
            textDocument = new { uri },
            position     = new { line, character = column },
        };

        JsonNode? result;
        try
        {
            result = await _channel.CallAsync("textDocument/signatureHelp", @params, ct)
                                   .ConfigureAwait(false);
        }
        catch { return null; }

        if (result is null) return null;

        var signaturesNode  = result["signatures"]?.AsArray();
        if (signaturesNode is null || signaturesNode.Count == 0) return null;

        var activeSignature = result["activeSignature"]?.GetValue<int?>() ?? 0;
        var activeParameter = result["activeParameter"]?.GetValue<int?>() ?? 0;

        var signatures = new List<LspSignatureInfo>(signaturesNode.Count);
        foreach (var sigNode in signaturesNode)
        {
            if (sigNode is null) continue;

            var label         = sigNode["label"]?.GetValue<string>() ?? string.Empty;
            var documentation = ExtractDocumentation(sigNode["documentation"]);

            // Parse per-signature activeParameter override
            var sigActiveParam = sigNode["activeParameter"]?.GetValue<int?>() ?? activeParameter;

            // Parse parameters array
            var paramsNode = sigNode["parameters"]?.AsArray();
            List<LspParameterInfo>? parameters = null;
            if (paramsNode is { Count: > 0 })
            {
                parameters = new List<LspParameterInfo>(paramsNode.Count);
                foreach (var p in paramsNode)
                {
                    if (p is null) continue;
                    var paramLabel = ExtractParameterLabel(p["label"], label);
                    var paramDoc   = ExtractDocumentation(p["documentation"]);
                    parameters.Add(new LspParameterInfo { Label = paramLabel, Documentation = paramDoc });
                }
            }

            signatures.Add(new LspSignatureInfo
            {
                Label         = label,
                Documentation = documentation,
                Parameters    = parameters,
            });
        }

        // Clamp indices to valid range
        activeSignature = Math.Clamp(activeSignature, 0, signatures.Count - 1);
        var activeSig   = signatures[activeSignature];
        if (activeSig.Parameters is not null)
            activeParameter = Math.Clamp(activeParameter, 0, activeSig.Parameters.Count - 1);

        return new LspSignatureHelpResult
        {
            Signatures          = signatures,
            ActiveSignatureIndex = activeSignature,
            ActiveParameterIndex = activeParameter,
        };
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────

    private static string? ExtractDocumentation(JsonNode? node)
    {
        if (node is null) return null;
        // MarkupContent: { kind: "markdown"|"plaintext", value: "..." }
        if (node is JsonObject obj)
            return obj["value"]?.GetValue<string>();
        // Plain string
        return node.GetValue<string?>();
    }

    /// <summary>
    /// LSP parameter labels are either:
    ///   - a string: the literal parameter text
    ///   - a [start, end] array: byte offsets within the parent signature label
    /// Both are mapped to a string here for UI use.
    /// </summary>
    private static string ExtractParameterLabel(JsonNode? labelNode, string signatureLabel)
    {
        if (labelNode is null) return string.Empty;

        // Array format: [startOffset, endOffset]
        if (labelNode is JsonArray arr && arr.Count == 2)
        {
            var start = arr[0]?.GetValue<int?>() ?? 0;
            var end   = arr[1]?.GetValue<int?>() ?? 0;
            if (start >= 0 && end > start && end <= signatureLabel.Length)
                return signatureLabel.Substring(start, end - start);
            return string.Empty;
        }

        // String format
        return labelNode.GetValue<string?>() ?? string.Empty;
    }
}
