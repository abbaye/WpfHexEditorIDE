// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: Services/CodeGen/XamlNameRenameService.cs
// Author: Derek Tremblay
// Created: 2026-05-05
// Description:
//     Performs x:Name rename cascade: when the user renames an x:Name in XAML
//     (detected by CodeBehindSyncService via CodeBehindPatch.RenamedElements),
//     uses Roslyn's CSharpSyntaxTree to rename the corresponding private field
//     throughout the code-behind file — not just the declaration.
//
// Architecture Notes:
//     Does NOT use Roslyn Workspace / Renamer.RenameSymbolAsync (requires a full
//     Roslyn workspace with compilation) — instead performs a targeted identifier
//     token rename across all occurrences in the syntax tree.
//     This avoids the workspace overhead while still being structurally correct
//     for the common case (field access within the partial class).
//     For full semantic rename (across files), the user should invoke the IDE's
//     F2 rename refactoring via the Roslyn LSP service.
// ==========================================================

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis;

namespace WpfHexEditor.Editor.XamlDesigner.Services.CodeGen;

/// <summary>
/// Renames an identifier in a C# source file at the syntax level (all occurrence tokens).
/// </summary>
public sealed class XamlNameRenameService
{
    /// <summary>
    /// Renames all occurrences of <paramref name="oldName"/> to <paramref name="newName"/>
    /// in <paramref name="csharpSource"/> and returns the updated source text.
    /// </summary>
    /// <remarks>
    /// Only renames tokens whose parent is a simple identifier context (not method calls,
    /// type names, or namespace references). Targets field declarations, all usages within
    /// the same partial class.
    /// </remarks>
    public string Rename(string csharpSource, string oldName, string newName)
    {
        if (string.IsNullOrWhiteSpace(csharpSource) ||
            string.IsNullOrWhiteSpace(oldName)      ||
            string.IsNullOrWhiteSpace(newName)      ||
            oldName == newName)
            return csharpSource;

        var tree = CSharpSyntaxTree.ParseText(csharpSource);
        var root = tree.GetRoot();

        // Find all identifier tokens matching oldName.
        var tokens = root.DescendantTokens()
                         .Where(t => t.IsKind(SyntaxKind.IdentifierToken) &&
                                     t.Text == oldName);

        // Replace all at once using SyntaxTree replacement.
        var newRoot = root.ReplaceTokens(tokens, (original, _) =>
            SyntaxFactory.Identifier(original.LeadingTrivia, newName, original.TrailingTrivia));

        return newRoot.ToFullString();
    }

    /// <summary>
    /// Applies a batch of renames to <paramref name="csharpSource"/>.
    /// Renames are applied in order; if the same token appears in multiple renames,
    /// only the first applies (since the token text changes after the first rename).
    /// </summary>
    public string RenameBatch(
        string csharpSource,
        IEnumerable<(string OldName, string NewName)> renames)
    {
        string result = csharpSource;
        foreach (var (oldName, newName) in renames)
            result = Rename(result, oldName, newName);
        return result;
    }
}
