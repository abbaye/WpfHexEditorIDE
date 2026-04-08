// ==========================================================
// Project: WpfHexEditor.Core.LSP.Client
// File: Services/ServerCapabilities.cs
// Description:
//     Parses the LSP server's capability flags from the initialize response.
//     Used to gate feature calls so the client never invokes unsupported methods.
//
// Architecture Notes:
//     Value object — immutable, constructed via static Parse factory.
//     LSP spec: capabilities can be bool true/false OR an options object; both are handled.
// ==========================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace WpfHexEditor.Core.LSP.Client.Services;

/// <summary>
/// Capability flags parsed from the LSP <c>initialize</c> response.
/// A <c>false</c> flag means the server does not support that feature —
/// the client must not call the corresponding method.
/// </summary>
internal sealed class ServerCapabilities
{
    internal bool HasCompletionProvider       { get; init; }
    internal bool HasHoverProvider            { get; init; }
    internal bool HasDefinitionProvider       { get; init; }
    internal bool HasReferencesProvider       { get; init; }
    internal bool HasSignatureHelpProvider    { get; init; }
    internal bool HasCodeActionProvider       { get; init; }
    internal bool HasRenameProvider           { get; init; }
    internal bool HasImplementationProvider   { get; init; }
    internal bool HasTypeDefinitionProvider   { get; init; }
    internal bool HasFormattingProvider            { get; init; }
    internal bool HasRangeFormattingProvider       { get; init; }
    internal bool HasDiagnosticProvider            { get; init; }
    internal bool DiagnosticInterFileDependencies  { get; init; }
    internal bool HasLinkedEditingRangeProvider    { get; init; }
    internal bool HasCallHierarchyProvider         { get; init; }
    internal bool HasTypeHierarchyProvider         { get; init; }
    internal bool HasInlayHintsProvider            { get; init; }
    internal bool HasSemanticTokensProvider        { get; init; }
    internal bool HasWorkspaceSymbolsProvider      { get; init; }
    /// <summary>Token type names indexed by LSP integer (from semanticTokensProvider.legend.tokenTypes).</summary>
    internal IReadOnlyList<string> SemanticTokenTypesLegend     { get; init; } = Array.Empty<string>();
    /// <summary>Token modifier names indexed by LSP integer (from semanticTokensProvider.legend.tokenModifiers).</summary>
    internal IReadOnlyList<string> SemanticTokenModifiersLegend { get; init; } = Array.Empty<string>();
    /// <summary>
    /// TextDocumentSyncKind: 0=None, 1=Full, 2=Incremental.
    /// Defaults to 1 (Full) when absent so existing behaviour is preserved.
    /// </summary>
    internal int TextDocumentSyncKind { get; init; } = 1;

    /// <summary>
    /// Parses the capabilities from the raw <c>initialize</c> response node.
    /// Returns an all-<c>true</c> instance when the response is missing,
    /// so a server that omits capability declarations still works.
    /// </summary>
    internal static ServerCapabilities Parse(JsonNode? initializeResult)
    {
        var caps = initializeResult?["capabilities"];
        if (caps is null)
            return AllEnabled();

        return new ServerCapabilities
        {
            HasCompletionProvider     = IsEnabled(caps["completionProvider"]),
            HasHoverProvider          = IsEnabled(caps["hoverProvider"]),
            HasDefinitionProvider     = IsEnabled(caps["definitionProvider"]),
            HasReferencesProvider     = IsEnabled(caps["referencesProvider"]),
            HasSignatureHelpProvider  = IsEnabled(caps["signatureHelpProvider"]),
            HasCodeActionProvider     = IsEnabled(caps["codeActionProvider"]),
            HasRenameProvider         = IsEnabled(caps["renameProvider"]),
            HasImplementationProvider = IsEnabled(caps["implementationProvider"]),
            HasTypeDefinitionProvider = IsEnabled(caps["typeDefinitionProvider"]),
            HasFormattingProvider           = IsEnabled(caps["documentFormattingProvider"]),
            HasRangeFormattingProvider      = IsEnabled(caps["documentRangeFormattingProvider"]),
            HasDiagnosticProvider           = IsEnabled(caps["diagnosticProvider"]),
            DiagnosticInterFileDependencies = caps["diagnosticProvider"]?["interFileDependencies"]
                                              ?.GetValue<bool>() ?? false,
            HasLinkedEditingRangeProvider   = IsEnabled(caps["linkedEditingRangeProvider"]),
            HasCallHierarchyProvider        = IsEnabled(caps["callHierarchyProvider"]),
            HasTypeHierarchyProvider        = IsEnabled(caps["typeHierarchyProvider"]),
            HasInlayHintsProvider           = IsEnabled(caps["inlayHintProvider"]),
            HasSemanticTokensProvider       = IsEnabled(caps["semanticTokensProvider"]),
            HasWorkspaceSymbolsProvider     = IsEnabled(caps["workspaceSymbolProvider"]),
            SemanticTokenTypesLegend        = ParseLegendArray(caps["semanticTokensProvider"]?["legend"]?["tokenTypes"]),
            SemanticTokenModifiersLegend    = ParseLegendArray(caps["semanticTokensProvider"]?["legend"]?["tokenModifiers"]),
            TextDocumentSyncKind            = ParseSyncKind(caps["textDocumentSync"]),
        };
    }

    private static int ParseSyncKind(JsonNode? node) => node switch
    {
        // bare integer: { "textDocumentSync": 2 }
        JsonValue v when v.TryGetValue<int>(out var i) => i,
        // options object: { "textDocumentSync": { "change": 2 } }
        JsonObject obj when obj["change"] is JsonValue cv && cv.TryGetValue<int>(out var c) => c,
        _ => 1, // default: Full
    };

    private static IReadOnlyList<string> ParseLegendArray(JsonNode? node)
    {
        if (node is not JsonArray arr) return Array.Empty<string>();
        var result = new List<string>(arr.Count);
        foreach (var item in arr)
            result.Add(item?.GetValue<string>() ?? string.Empty);
        return result;
    }

    // LSP spec: a capability value is either absent (false), bool true/false,
    // or an options object {} (true).  All three forms must be handled.
    private static bool IsEnabled(JsonNode? node) => node switch
    {
        null       => false,
        JsonObject => true,
        JsonValue v => v.TryGetValue<bool>(out var b) ? b : true,
        _           => true,
    };

    private static ServerCapabilities AllEnabled() => new()
    {
        HasCompletionProvider     = true,
        HasHoverProvider          = true,
        HasDefinitionProvider     = true,
        HasReferencesProvider     = true,
        HasSignatureHelpProvider  = true,
        HasCodeActionProvider     = true,
        HasRenameProvider         = true,
        HasImplementationProvider = true,
        HasTypeDefinitionProvider = true,
        HasFormattingProvider            = true,
        HasRangeFormattingProvider       = true,
        HasDiagnosticProvider            = false, // AllEnabled = push mode (safe default)
        DiagnosticInterFileDependencies  = false,
        HasLinkedEditingRangeProvider    = false, // safe default — opt-in only
        HasCallHierarchyProvider         = false,
        HasTypeHierarchyProvider         = false,
        HasInlayHintsProvider            = false,
        HasSemanticTokensProvider        = false,
        HasWorkspaceSymbolsProvider      = true,
    };
}
