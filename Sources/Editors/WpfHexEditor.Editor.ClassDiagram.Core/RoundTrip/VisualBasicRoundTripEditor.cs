// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram.Core
// File: RoundTrip/VisualBasicRoundTripEditor.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.7
// Created: 2026-05-11
// Description:
//     Phase A (ADR-037) — Visual Basic implementation of
//     ILanguageRoundTripEditor. Mirrors CSharpRoundTripEditor's
//     11 MemberEdit cases via Microsoft.CodeAnalysis.VisualBasic
//     SyntaxFactory + Formatter.
//
// Architecture Notes:
//     Stateless — every ApplyAsync call parses, transforms, formats,
//     returns. No instance state, safe to share across threads.
//     Falls back to KeepNoTrivia when removing nodes.
//     Formatting honours AdhocWorkspace defaults; per-project
//     editorconfig support is planned for Phase 6.
// ==========================================================

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.VisualBasic;
using Microsoft.CodeAnalysis.VisualBasic.Syntax;
using WpfHexEditor.Editor.ClassDiagram.Core.RoundTrip.Abstractions;

namespace WpfHexEditor.Editor.ClassDiagram.Core.RoundTrip;

/// <summary>VB implementation of <see cref="ILanguageRoundTripEditor"/>.</summary>
public sealed class VisualBasicRoundTripEditor : ILanguageRoundTripEditor
{
    /// <inheritdoc/>
    public string LanguageId => "vb";

    /// <inheritdoc/>
    public string DisplayName => "Visual Basic";

    /// <inheritdoc/>
    public IReadOnlyList<string> FileExtensions => [".vb"];

    /// <inheritdoc/>
    public async Task<RoundTripResult> ApplyAsync(
        string             filePath,
        string             sourceText,
        MemberEdit         edit,
        CancellationToken  ct = default)
    {
        ArgumentNullException.ThrowIfNull(sourceText);
        ArgumentNullException.ThrowIfNull(edit);

        var tree = VisualBasicSyntaxTree.ParseText(sourceText, cancellationToken: ct);
        var root = await tree.GetRootAsync(ct).ConfigureAwait(false);

        if (root is not CompilationUnitSyntax)
            return RoundTripResult.Fail(filePath, "Source is not a valid VB compilation unit.");

        var target = FindTargetType(root, edit.TargetTypeFullName);

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

        using var workspace = new AdhocWorkspace();
        var formatted = Formatter.Format(newRoot, workspace, cancellationToken: ct);

        return new RoundTripResult(
            Success:       true,
            FilePath:      filePath,
            ContentBefore: sourceText,
            ContentAfter:  formatted.ToFullString());
    }

    // ── Target lookup ────────────────────────────────────────────────────────

    private static TypeBlockSyntax? FindTargetType(SyntaxNode root, string fullName)
    {
        string simple = fullName.Contains('.', StringComparison.Ordinal)
            ? fullName[(fullName.LastIndexOf('.') + 1)..]
            : fullName;

        return root.DescendantNodes()
                   .OfType<TypeBlockSyntax>()
                   .FirstOrDefault(t => t.BlockStatement.Identifier.Text == simple);
    }

    // ── Type-level edits ─────────────────────────────────────────────────────

    private static SyntaxNode ApplyAddType(SyntaxNode root, AddType edit)
    {
        var parsed = SyntaxFactory.ParseCompilationUnit(edit.Snippet);
        var typeBlock = parsed.DescendantNodes().OfType<TypeBlockSyntax>().FirstOrDefault()
                        ?? throw new InvalidOperationException("AddType snippet does not contain a VB type block.");
        if (root is not CompilationUnitSyntax cu)
            throw new InvalidOperationException("Cannot add type — root is not a CompilationUnit.");
        return cu.AddMembers(typeBlock);
    }

    private static SyntaxNode ApplyRemoveType(SyntaxNode root, TypeBlockSyntax target) =>
        root.RemoveNode(target, SyntaxRemoveOptions.KeepNoTrivia)!;

    private static SyntaxNode ApplyRenameType(SyntaxNode root, TypeBlockSyntax target, string newName)
    {
        var stmt = target.BlockStatement;
        var newIdent = SyntaxFactory.Identifier(newName).WithTriviaFrom(stmt.Identifier);
        var newStmt  = stmt.WithIdentifier(newIdent);
        var newTarget = target.WithBlockStatement(newStmt);
        return root.ReplaceNode(target, newTarget);
    }

    private static SyntaxNode ApplyChangeBaseType(SyntaxNode root, TypeBlockSyntax target, string? newBase)
    {
        // VB has separate Inherits and Implements clauses. We treat "base type" as the
        // first Inherits clause; preserve all Implements clauses.
        var inherits = target.Inherits.ToList();
        if (!string.IsNullOrWhiteSpace(newBase))
        {
            var newInherits = SyntaxFactory.InheritsStatement(SyntaxFactory.ParseTypeName(newBase));
            inherits = [newInherits];
        }
        else
        {
            inherits = [];
        }
        var newTarget = target.WithInherits(SyntaxFactory.List(inherits));
        return root.ReplaceNode(target, newTarget);
    }

    private static SyntaxNode ApplyAddInterface(SyntaxNode root, TypeBlockSyntax target, string ifaceName)
    {
        var ifaceType = SyntaxFactory.ParseTypeName(ifaceName);
        var existing = target.Implements;
        ImplementsStatementSyntax addedStmt;
        if (existing.Count > 0)
        {
            addedStmt = existing[0].AddTypes(ifaceType);
            var newImpls = existing.Replace(existing[0], addedStmt);
            return root.ReplaceNode(target, target.WithImplements(newImpls));
        }
        addedStmt = SyntaxFactory.ImplementsStatement(ifaceType);
        var newImpls2 = SyntaxFactory.List(new[] { addedStmt });
        return root.ReplaceNode(target, target.WithImplements(newImpls2));
    }

    private static SyntaxNode ApplyRemoveInterface(SyntaxNode root, TypeBlockSyntax target, string ifaceName)
    {
        if (target.Implements.Count == 0) return root;
        var newImpls = SyntaxFactory.List(target.Implements
            .Select(stmt => stmt.WithTypes(SyntaxFactory.SeparatedList(
                stmt.Types.Where(t => t.ToString() != ifaceName))))
            .Where(stmt => stmt.Types.Count > 0));
        return root.ReplaceNode(target, target.WithImplements(newImpls));
    }

    // ── Member-level edits ───────────────────────────────────────────────────

    private static SyntaxNode ApplyAddMember(SyntaxNode root, TypeBlockSyntax target, string snippet)
    {
        // Wrap the snippet inside a dummy class so VB parser produces a member block.
        string wrapper = $"Class __Tmp\n{snippet}\nEnd Class";
        var parsed = SyntaxFactory.ParseCompilationUnit(wrapper);
        var dummy = parsed.DescendantNodes().OfType<ClassBlockSyntax>().FirstOrDefault()
                    ?? throw new InvalidOperationException("AddMember snippet is not a valid VB member declaration.");
        var newMembers = target.Members.AddRange(dummy.Members);
        var newTarget = target.WithMembers(newMembers);
        return root.ReplaceNode(target, newTarget);
    }

    private static SyntaxNode ApplyRemoveMember(SyntaxNode root, TypeBlockSyntax target, string memberName)
    {
        var toRemove = target.Members.FirstOrDefault(m => GetMemberName(m) == memberName)
                       ?? throw new InvalidOperationException($"Member '{memberName}' not found.");
        var newTarget = target.RemoveNode(toRemove, SyntaxRemoveOptions.KeepNoTrivia)!;
        return root.ReplaceNode(target, newTarget);
    }

    private static SyntaxNode ApplyRenameMember(
        SyntaxNode root, TypeBlockSyntax target, string oldName, string newName)
    {
        var rewriter = new IdentifierRenamer(oldName, newName);
        var newTarget = (TypeBlockSyntax)rewriter.Visit(target);
        return root.ReplaceNode(target, newTarget);
    }

    private static SyntaxNode ApplyChangeVisibility(
        SyntaxNode root, TypeBlockSyntax target, string memberName, MemberVisibilityKind newVisibility)
    {
        var member = target.Members.FirstOrDefault(m => GetMemberName(m) == memberName)
                     ?? throw new InvalidOperationException($"Member '{memberName}' not found.");

        var newModifiers = ReplaceVisibility(GetModifiers(member), newVisibility);
        var newMember = WithModifiers(member, newModifiers);
        if (ReferenceEquals(newMember, member)) return root;
        var newTarget = target.ReplaceNode(member, newMember);
        return root.ReplaceNode(target, newTarget);
    }

    private static SyntaxNode ApplyChangeMemberType(
        SyntaxNode root, TypeBlockSyntax target, string memberName, string newType)
    {
        var member = target.Members.FirstOrDefault(m => GetMemberName(m) == memberName)
                     ?? throw new InvalidOperationException($"Member '{memberName}' not found.");

        // VB type changes vary by member kind: PropertyStatement.AsClause,
        // FieldDeclaration variators' AsClause, MethodStatement.AsClause for Function.
        var newAsClause = SyntaxFactory.SimpleAsClause(SyntaxFactory.ParseTypeName(newType));
        StatementSyntax newMember = member switch
        {
            PropertyStatementSyntax p => p.WithAsClause(newAsClause),
            MethodStatementSyntax   m => m.WithAsClause(newAsClause),
            FieldDeclarationSyntax  f => RetypeFieldDeclaration(f, newType),
            _ => throw new NotSupportedException($"ChangeMemberType not supported on '{member.GetType().Name}'.")
        };

        var newTarget = target.ReplaceNode(member, newMember);
        return root.ReplaceNode(target, newTarget);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string? GetMemberName(StatementSyntax m) => m switch
    {
        MethodStatementSyntax x        => x.Identifier.Text,
        PropertyStatementSyntax x      => x.Identifier.Text,
        FieldDeclarationSyntax x       => x.Declarators.FirstOrDefault()?.Names.FirstOrDefault()?.Identifier.Text,
        EventStatementSyntax x         => x.Identifier.Text,
        SubNewStatementSyntax          => "New",
        MethodBlockSyntax mb           => mb.SubOrFunctionStatement.Identifier.Text,
        ConstructorBlockSyntax cb      => "New",
        PropertyBlockSyntax pb         => pb.PropertyStatement.Identifier.Text,
        EventBlockSyntax eb            => eb.EventStatement.Identifier.Text,
        _                              => null
    };

    private static SyntaxTokenList GetModifiers(StatementSyntax m) => m switch
    {
        MethodStatementSyntax x   => x.Modifiers,
        PropertyStatementSyntax x => x.Modifiers,
        FieldDeclarationSyntax x  => x.Modifiers,
        EventStatementSyntax x    => x.Modifiers,
        MethodBlockSyntax mb      => mb.SubOrFunctionStatement.Modifiers,
        PropertyBlockSyntax pb    => pb.PropertyStatement.Modifiers,
        EventBlockSyntax eb       => eb.EventStatement.Modifiers,
        _                         => default
    };

    private static StatementSyntax WithModifiers(StatementSyntax m, SyntaxTokenList modifiers) => m switch
    {
        MethodStatementSyntax x   => x.WithModifiers(modifiers),
        PropertyStatementSyntax x => x.WithModifiers(modifiers),
        FieldDeclarationSyntax x  => x.WithModifiers(modifiers),
        EventStatementSyntax x    => x.WithModifiers(modifiers),
        MethodBlockSyntax mb      => mb.WithSubOrFunctionStatement(mb.SubOrFunctionStatement.WithModifiers(modifiers)),
        PropertyBlockSyntax pb    => pb.WithPropertyStatement(pb.PropertyStatement.WithModifiers(modifiers)),
        EventBlockSyntax eb       => eb.WithEventStatement(eb.EventStatement.WithModifiers(modifiers)),
        _                         => m
    };

    private static SyntaxTokenList ReplaceVisibility(SyntaxTokenList modifiers, MemberVisibilityKind kind)
    {
        var stripped = modifiers.Where(t => !IsVisibilityToken(t)).ToList();
        var inserted = VisibilityTokens(kind);
        return SyntaxFactory.TokenList(inserted.Concat(stripped));
    }

    private static bool IsVisibilityToken(SyntaxToken t) =>
        t.IsKind(SyntaxKind.PublicKeyword)
        || t.IsKind(SyntaxKind.FriendKeyword)
        || t.IsKind(SyntaxKind.ProtectedKeyword)
        || t.IsKind(SyntaxKind.PrivateKeyword);

    private static IEnumerable<SyntaxToken> VisibilityTokens(MemberVisibilityKind kind) => kind switch
    {
        MemberVisibilityKind.Public            => [SyntaxFactory.Token(SyntaxKind.PublicKeyword)],
        MemberVisibilityKind.Internal          => [SyntaxFactory.Token(SyntaxKind.FriendKeyword)],
        MemberVisibilityKind.Protected         => [SyntaxFactory.Token(SyntaxKind.ProtectedKeyword)],
        MemberVisibilityKind.ProtectedInternal => [SyntaxFactory.Token(SyntaxKind.ProtectedKeyword), SyntaxFactory.Token(SyntaxKind.FriendKeyword)],
        MemberVisibilityKind.PrivateProtected  => [SyntaxFactory.Token(SyntaxKind.PrivateKeyword),   SyntaxFactory.Token(SyntaxKind.ProtectedKeyword)],
        MemberVisibilityKind.Private           => [SyntaxFactory.Token(SyntaxKind.PrivateKeyword)],
        _                                      => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    private static FieldDeclarationSyntax RetypeFieldDeclaration(FieldDeclarationSyntax f, string newType)
    {
        var newAs = SyntaxFactory.SimpleAsClause(SyntaxFactory.ParseTypeName(newType));
        var newDeclarators = SyntaxFactory.SeparatedList(
            f.Declarators.Select(d => d.WithAsClause(newAs)));
        return f.WithDeclarators(newDeclarators);
    }

    // ── Identifier rewriter (file-scoped rename) ────────────────────────────

    private sealed class IdentifierRenamer(string oldName, string newName) : VisualBasicSyntaxRewriter
    {
        public override SyntaxToken VisitToken(SyntaxToken token)
        {
            if (token.IsKind(SyntaxKind.IdentifierToken) && token.Text == oldName)
                return SyntaxFactory.Identifier(newName).WithTriviaFrom(token);
            return base.VisitToken(token);
        }
    }
}
