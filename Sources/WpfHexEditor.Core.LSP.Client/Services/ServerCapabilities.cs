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
        };
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
    };
}
