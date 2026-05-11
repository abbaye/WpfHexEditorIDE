// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram.Core
// File: RoundTrip/CSharpRoundTripEditor.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.7
// Created: 2026-05-10
// Description:
//     C# implementation of ILanguageRoundTripEditor. Uses Roslyn
//     CSharpSyntaxTree + SyntaxFactory + Formatter to surgically patch a
//     source file in response to a MemberEdit emitted by the diagram
//     surface (ADR-022 Phase 1B).
//
// Architecture Notes:
//     Stateless — every ApplyAsync call parses, transforms, formats,
//     returns. No instance state, safe to share across threads.
//     Falls back to KeepNoTrivia when removing nodes to avoid stray
//     blank lines.
//     Formatting honours the AdhocWorkspace defaults; per-project
//     editorconfig support is planned for Phase 6.
// ==========================================================

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using WpfHexEditor.Editor.ClassDiagram.Core.RoundTrip.Abstractions;

namespace WpfHexEditor.Editor.ClassDiagram.Core.RoundTrip;

/// <summary>
/// C# implementation of <see cref="ILanguageRoundTripEditor"/>.
/// </summary>
public sealed class CSharpRoundTripEditor : ILanguageRoundTripEditor
{
    /// <inheritdoc/>
    public string LanguageId => "csharp";

    /// <inheritdoc/>
    public string DisplayName => "C#";

    /// <inheritdoc/>
    public IReadOnlyList<string> FileExtensions => [".cs"];

    /// <inheritdoc/>
    public async Task<RoundTripResult> ApplyAsync(
        string             filePath,
        string             sourceText,
        MemberEdit         edit,
        CancellationToken  ct = default)
    {
        ArgumentNullException.ThrowIfNull(sourceText);
        ArgumentNullException.ThrowIfNull(edit);

        var tree = CSharpSyntaxTree.ParseText(sourceText, cancellationToken: ct);
        var root = await tree.GetRootAsync(ct).ConfigureAwait(false);

        if (root is not CompilationUnitSyntax)
            return RoundTripResult.Fail(filePath, "Source is not a valid C# compilation unit.");

        var target = FindTargetType(root, edit.TargetTypeFullName);

        // AddType is the only edit that does not require an existing target.
        if (target is null && edit is not AddType)
            return RoundTripResult.Fail(filePath, $"Target type '{edit.TargetTypeFullName}' not found.");

        SyntaxNode newRoot;
        try
        {
            newRoot = edit switch
            {
                AddType         a => ApplyAddType(root, a),
                RemoveType        => ApplyRemoveType(root, target!),
                RenameType      r => ApplyRenameType(root, target!, r.NewName),
                ChangeBaseType  c => ApplyChangeBaseType(root, target!, c.NewBaseType),
                AddInterface    a => ApplyAddInterface(root, target!, a.InterfaceName),
                RemoveInterface r => ApplyRemoveInterface(root, target!, r.InterfaceName),
                AddMember       a => ApplyAddMember(root, target!, a.Snippet),
                RemoveMember    r => ApplyRemoveMember(root, target!, r.MemberName),
                RenameMember    r => ApplyRenameMember(root, target!, r.OldName, r.NewName),
                ChangeVisibility c => ApplyChangeVisibility(root, target!, c.MemberName, c.NewVisibility),
                ChangeMemberType c => ApplyChangeMemberType(root, target!, c.MemberName, c.NewType),
                _ => throw new NotSupportedException($"MemberEdit '{edit.GetType().Name}' is not implemented.")
            };
        }
        catch (Exception ex)
        {
            return RoundTripResult.Fail(filePath, ex.Message);
        }

        if (ReferenceEquals(newRoot, root))
            return RoundTripResult.Fail(filePath, "Edit produced no change.");

        // Formatter requires a Workspace; AdhocWorkspace is the lightweight option.
        using var workspace = new AdhocWorkspace();
        var formatted = Formatter.Format(newRoot, workspace, cancellationToken: ct);

        return new RoundTripResult(
            Success:       true,
            FilePath:      filePath,
            ContentBefore: sourceText,
            ContentAfter:  formatted.ToFullString());
    }

    // ── Target lookup ────────────────────────────────────────────────────────

    private static TypeDeclarationSyntax? FindTargetType(SyntaxNode root, string fullName)
    {
        // Accept either a simple name or a dotted form Namespace.TypeName.
        string simple = fullName.Contains('.', StringComparison.Ordinal)
            ? fullName[(fullName.LastIndexOf('.') + 1)..]
            : fullName;

        return root.DescendantNodes()
                   .OfType<TypeDeclarationSyntax>()
                   .FirstOrDefault(t => t.Identifier.Text == simple);
    }

    // ── Type-level edits ─────────────────────────────────────────────────────

    private static SyntaxNode ApplyAddType(SyntaxNode root, AddType edit)
    {
        var member = SyntaxFactory.ParseMemberDeclaration(edit.Snippet)
                     ?? throw new InvalidOperationException("AddType snippet is not a valid member declaration.");
        if (root is not CompilationUnitSyntax cu)
            throw new InvalidOperationException("Cannot add type — root is not a CompilationUnit.");
        return cu.AddMembers(member);
    }

    private static SyntaxNode ApplyRemoveType(SyntaxNode root, TypeDeclarationSyntax target) =>
        root.RemoveNode(target, SyntaxRemoveOptions.KeepNoTrivia)!;

    private static SyntaxNode ApplyRenameType(SyntaxNode root, TypeDeclarationSyntax target, string newName)
    {
        var renamed = target.WithIdentifier(SyntaxFactory.Identifier(newName).WithTriviaFrom(target.Identifier));
        return root.ReplaceNode(target, renamed);
    }

    private static SyntaxNode ApplyChangeBaseType(SyntaxNode root, TypeDeclarationSyntax target, string? newBase)
    {
        var existing = target.BaseList;
        var interfaces = existing?.Types
            .Skip(1)        // first entry is treated as base class (C# convention)
            .ToList() ?? [];

        BaseListSyntax? newList = null;
        if (!string.IsNullOrWhiteSpace(newBase))
        {
            var baseType = SyntaxFactory.SimpleBaseType(SyntaxFactory.ParseTypeName(newBase));
            var all = new List<BaseTypeSyntax> { baseType };
            all.AddRange(interfaces);
            newList = SyntaxFactory.BaseList(SyntaxFactory.SeparatedList(all));
        }
        else if (interfaces.Count > 0)
        {
            newList = SyntaxFactory.BaseList(SyntaxFactory.SeparatedList(interfaces));
        }

        var newTarget = target.WithBaseList(newList);
        return root.ReplaceNode(target, newTarget);
    }

    private static SyntaxNode ApplyAddInterface(SyntaxNode root, TypeDeclarationSyntax target, string ifaceName)
    {
        var iface = SyntaxFactory.SimpleBaseType(SyntaxFactory.ParseTypeName(ifaceName));
        BaseListSyntax newList = target.BaseList is null
            ? SyntaxFactory.BaseList(SyntaxFactory.SingletonSeparatedList<BaseTypeSyntax>(iface))
            : target.BaseList.AddTypes(iface);
        var newTarget = target.WithBaseList(newList);
        return root.ReplaceNode(target, newTarget);
    }

    private static SyntaxNode ApplyRemoveInterface(SyntaxNode root, TypeDeclarationSyntax target, string ifaceName)
    {
        if (target.BaseList is null) return root;
        var kept = target.BaseList.Types
            .Where(t => t.Type.ToString() != ifaceName)
            .ToList();
        BaseListSyntax? newList = kept.Count == 0
            ? null
            : SyntaxFactory.BaseList(SyntaxFactory.SeparatedList(kept));
        var newTarget = target.WithBaseList(newList);
        return root.ReplaceNode(target, newTarget);
    }

    // ── Member-level edits ───────────────────────────────────────────────────

    private static SyntaxNode ApplyAddMember(SyntaxNode root, TypeDeclarationSyntax target, string snippet)
    {
        var member = SyntaxFactory.ParseMemberDeclaration(snippet)
                     ?? throw new InvalidOperationException("AddMember snippet is not a valid member declaration.");
        var newTarget = target.AddMembers(member);
        return root.ReplaceNode(target, newTarget);
    }

    private static SyntaxNode ApplyRemoveMember(SyntaxNode root, TypeDeclarationSyntax target, string memberName)
    {
        var toRemove = target.Members.FirstOrDefault(m => GetMemberName(m) == memberName)
                       ?? throw new InvalidOperationException($"Member '{memberName}' not found.");
        var newTarget = target.RemoveNode(toRemove, SyntaxRemoveOptions.KeepNoTrivia)!;
        return root.ReplaceNode(target, newTarget);
    }

    private static SyntaxNode ApplyRenameMember(
        SyntaxNode root, TypeDeclarationSyntax target, string oldName, string newName)
    {
        // File-scoped rename: every identifier token matching oldName inside the target
        // is renamed. Solution-wide rename (Renamer.RenameSymbolAsync) is deferred to a
        // separate code path that has access to a Workspace.
        var rewriter = new IdentifierRenamer(oldName, newName);
        var newTarget = (TypeDeclarationSyntax)rewriter.Visit(target);
        return root.ReplaceNode(target, newTarget);
    }

    private static SyntaxNode ApplyChangeVisibility(
        SyntaxNode root, TypeDeclarationSyntax target, string memberName, MemberVisibilityKind newVisibility)
    {
        var member = target.Members.FirstOrDefault(m => GetMemberName(m) == memberName)
                     ?? throw new InvalidOperationException($"Member '{memberName}' not found.");

        var newModifiers = ReplaceVisibility(member.Modifiers, newVisibility);
        var newMember = member.WithModifiers(newModifiers);
        var newTarget = target.ReplaceNode(member, newMember);
        return root.ReplaceNode(target, newTarget);
    }

    private static SyntaxNode ApplyChangeMemberType(
        SyntaxNode root, TypeDeclarationSyntax target, string memberName, string newType)
    {
        var member = target.Members.FirstOrDefault(m => GetMemberName(m) == memberName)
                     ?? throw new InvalidOperationException($"Member '{memberName}' not found.");

        MemberDeclarationSyntax newMember = member switch
        {
            PropertyDeclarationSyntax p => p.WithType(SyntaxFactory.ParseTypeName(newType).WithTrailingTrivia(SyntaxFactory.Space)),
            FieldDeclarationSyntax    f => f.WithDeclaration(f.Declaration.WithType(SyntaxFactory.ParseTypeName(newType).WithTrailingTrivia(SyntaxFactory.Space))),
            MethodDeclarationSyntax   m => m.WithReturnType(SyntaxFactory.ParseTypeName(newType).WithTrailingTrivia(SyntaxFactory.Space)),
            _ => throw new NotSupportedException($"ChangeMemberType not supported on '{member.GetType().Name}'.")
        };

        var newTarget = target.ReplaceNode(member, newMember);
        return root.ReplaceNode(target, newTarget);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string? GetMemberName(MemberDeclarationSyntax m) => m switch
    {
        MethodDeclarationSyntax x       => x.Identifier.Text,
        PropertyDeclarationSyntax x     => x.Identifier.Text,
        FieldDeclarationSyntax x        => x.Declaration.Variables.FirstOrDefault()?.Identifier.Text,
        EventDeclarationSyntax x        => x.Identifier.Text,
        EventFieldDeclarationSyntax x   => x.Declaration.Variables.FirstOrDefault()?.Identifier.Text,
        ConstructorDeclarationSyntax x  => x.Identifier.Text,
        _                               => null
    };

    private static SyntaxTokenList ReplaceVisibility(SyntaxTokenList modifiers, MemberVisibilityKind kind)
    {
        var stripped = modifiers
            .Where(t => !IsVisibilityToken(t))
            .ToList();

        var inserted = VisibilityTokens(kind);
        return SyntaxFactory.TokenList(inserted.Concat(stripped));
    }

    private static bool IsVisibilityToken(SyntaxToken t) =>
        t.IsKind(SyntaxKind.PublicKeyword)
        || t.IsKind(SyntaxKind.InternalKeyword)
        || t.IsKind(SyntaxKind.ProtectedKeyword)
        || t.IsKind(SyntaxKind.PrivateKeyword);

    private static IEnumerable<SyntaxToken> VisibilityTokens(MemberVisibilityKind kind) => kind switch
    {
        MemberVisibilityKind.Public            => [SyntaxFactory.Token(SyntaxKind.PublicKeyword)],
        MemberVisibilityKind.Internal          => [SyntaxFactory.Token(SyntaxKind.InternalKeyword)],
        MemberVisibilityKind.Protected         => [SyntaxFactory.Token(SyntaxKind.ProtectedKeyword)],
        MemberVisibilityKind.ProtectedInternal => [SyntaxFactory.Token(SyntaxKind.ProtectedKeyword), SyntaxFactory.Token(SyntaxKind.InternalKeyword)],
        MemberVisibilityKind.PrivateProtected  => [SyntaxFactory.Token(SyntaxKind.PrivateKeyword),   SyntaxFactory.Token(SyntaxKind.ProtectedKeyword)],
        MemberVisibilityKind.Private           => [SyntaxFactory.Token(SyntaxKind.PrivateKeyword)],
        _                                      => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    // ── Roslyn rewriter (file-scoped identifier rename) ──────────────────────

    private sealed class IdentifierRenamer(string oldName, string newName) : CSharpSyntaxRewriter
    {
        public override SyntaxToken VisitToken(SyntaxToken token)
        {
            if (token.IsKind(SyntaxKind.IdentifierToken) && token.Text == oldName)
                return SyntaxFactory.Identifier(newName).WithTriviaFrom(token);
            return base.VisitToken(token);
        }
    }
}
