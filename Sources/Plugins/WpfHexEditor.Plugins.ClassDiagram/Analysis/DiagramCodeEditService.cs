// ==========================================================
// Project: WpfHexEditor.Plugins.ClassDiagram
// File: Analysis/DiagramCodeEditService.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-04-07
// Description:
//     Bidirectional code editing from the class diagram canvas.
//     Uses Roslyn SyntaxRewriter to apply rename/add/delete
//     member operations directly to source files on disk.
//     Live-sync will pick up the saved file and refresh the diagram.
//
// Architecture Notes:
//     Pure Roslyn syntax transforms — no LSP, no MSBuild workspace.
//     All operations are file-scoped (syntax-only, no semantic model).
//     Results are written back to disk; live-sync handles diagram refresh.
// ==========================================================

using System.IO;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using WpfHexEditor.Editor.ClassDiagram.Core.Model;

namespace WpfHexEditor.Plugins.ClassDiagram.Analysis;

/// <summary>
/// Applies code edits (rename / add / delete member) to C# source files
/// using Roslyn syntax transforms.
/// </summary>
public static class DiagramCodeEditService
{
    // ── Rename member ─────────────────────────────────────────────────────────

    /// <summary>
    /// Renames <paramref name="member"/> in the source file referenced by
    /// <see cref="ClassNode.SourceFilePath"/> (or <see cref="ClassMember.SourceFilePath"/>).
    /// The identifier token at the member's declaration is renamed; all other
    /// occurrences in the same file are also renamed.
    /// </summary>
    public static async Task<bool> RenameMemberAsync(
        ClassNode node, ClassMember member, string newName,
        CancellationToken ct = default)
    {
        string? filePath = member.SourceFilePath ?? node.SourceFilePath;
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return false;
        if (string.IsNullOrWhiteSpace(newName))                        return false;

        string src  = await File.ReadAllTextAsync(filePath, ct);
        var    tree = CSharpSyntaxTree.ParseText(src, cancellationToken: ct);
        var    root = await tree.GetRootAsync(ct);

        // Find the declaration of this member by name
        string oldName = member.Name;
        var rewriter   = new RenameRewriter(oldName, newName);
        var newRoot    = rewriter.Visit(root);

        if (newRoot == root) return false; // nothing changed

        await File.WriteAllTextAsync(filePath, newRoot.ToFullString(), ct);
        return true;
    }

    // ── Add member ────────────────────────────────────────────────────────────

    /// <summary>
    /// Appends a raw C# member declaration snippet to the end of the target
    /// class body in the source file.
    /// </summary>
    public static async Task<bool> AddMemberAsync(
        ClassNode node, string memberSnippet,
        CancellationToken ct = default)
    {
        string? filePath = node.SourceFilePath;
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return false;
        if (string.IsNullOrWhiteSpace(memberSnippet))                  return false;

        string src  = await File.ReadAllTextAsync(filePath, ct);
        var    tree = CSharpSyntaxTree.ParseText(src, cancellationToken: ct);
        var    root = await tree.GetRootAsync(ct);

        // Find the target class/record/struct declaration
        TypeDeclarationSyntax? typeDecl = FindTypeDeclaration(root, node.Name);
        if (typeDecl is null) return false;

        // Parse the snippet as a member declaration
        MemberDeclarationSyntax? newMember;
        try
        {
            newMember = SyntaxFactory.ParseMemberDeclaration(memberSnippet);
        }
        catch
        {
            return false;
        }
        if (newMember is null) return false;

        // Indent and append at the end of the class body
        newMember = newMember
            .WithLeadingTrivia(SyntaxFactory.Whitespace("    "))
            .WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed);

        var newTypeDecl  = typeDecl.AddMembers(newMember);
        var newRoot      = root.ReplaceNode(typeDecl, newTypeDecl);

        await File.WriteAllTextAsync(filePath, newRoot.ToFullString(), ct);
        return true;
    }

    // ── Delete member ─────────────────────────────────────────────────────────

    /// <summary>
    /// Removes the member declaration identified by <paramref name="member"/>
    /// from the source file.
    /// </summary>
    public static async Task<bool> DeleteMemberAsync(
        ClassNode node, ClassMember member,
        CancellationToken ct = default)
    {
        string? filePath = member.SourceFilePath ?? node.SourceFilePath;
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return false;

        string src  = await File.ReadAllTextAsync(filePath, ct);
        var    tree = CSharpSyntaxTree.ParseText(src, cancellationToken: ct);
        var    root = await tree.GetRootAsync(ct);

        // Find the declaration by name in the correct class
        TypeDeclarationSyntax? typeDecl = FindTypeDeclaration(root, node.Name);
        if (typeDecl is null) return false;

        var toRemove = typeDecl.Members
            .FirstOrDefault(m => GetMemberName(m) == member.Name);

        if (toRemove is null) return false;

        var newTypeDecl = typeDecl.RemoveNode(toRemove, SyntaxRemoveOptions.KeepNoTrivia)!;
        var newRoot     = root.ReplaceNode(typeDecl, newTypeDecl);

        await File.WriteAllTextAsync(filePath, newRoot.ToFullString(), ct);
        return true;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static TypeDeclarationSyntax? FindTypeDeclaration(SyntaxNode root, string typeName)
    {
        return root.DescendantNodes()
            .OfType<TypeDeclarationSyntax>()
            .FirstOrDefault(t => t.Identifier.Text == typeName);
    }

    private static string? GetMemberName(MemberDeclarationSyntax member) => member switch
    {
        MethodDeclarationSyntax m       => m.Identifier.Text,
        PropertyDeclarationSyntax p     => p.Identifier.Text,
        FieldDeclarationSyntax f        => f.Declaration.Variables.FirstOrDefault()?.Identifier.Text,
        EventDeclarationSyntax e        => e.Identifier.Text,
        EventFieldDeclarationSyntax ef  => ef.Declaration.Variables.FirstOrDefault()?.Identifier.Text,
        ConstructorDeclarationSyntax c  => c.Identifier.Text,
        _                               => null
    };

    // ── Roslyn rewriter ───────────────────────────────────────────────────────

    private sealed class RenameRewriter(string oldName, string newName) : CSharpSyntaxRewriter
    {
        public override SyntaxToken VisitToken(SyntaxToken token)
        {
            if (token.IsKind(SyntaxKind.IdentifierToken) && token.Text == oldName)
                return SyntaxFactory.Identifier(newName).WithTriviaFrom(token);
            return base.VisitToken(token);
        }
    }
}
