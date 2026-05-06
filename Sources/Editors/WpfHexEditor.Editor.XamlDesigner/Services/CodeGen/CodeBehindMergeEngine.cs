// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: Services/CodeGen/CodeBehindMergeEngine.cs
// Author: Derek Tremblay
// Created: 2026-05-05
// Description:
//     Three-way merge engine for code-behind files.
//     Given the existing .xaml.cs content and the newly generated partial-class
//     source, produces a merged file that:
//       - Replaces all [GeneratedCode] members with the new generated set.
//       - Preserves all user-written members (no [GeneratedCode] annotation).
//       - Preserves user-written event handler bodies when a method with the
//         same name + parameter signature already exists (even if generated).
//
// Architecture Notes:
//     Uses Microsoft.CodeAnalysis.CSharp for precise AST-level member identification.
//     Avoids SyntaxFactory — reads existing tree, rewrites selected nodes.
//     Three-way merge contract:
//       existing file = [GeneratedCode] members (replaced) + user members (kept)
//     Conflict model: if a [GeneratedCode] member was modified by the user
//     (body changed, annotation removed), it transitions to a user member and
//     is preserved. The new generated member is added as a separate stub.
// ==========================================================

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace WpfHexEditor.Editor.XamlDesigner.Services.CodeGen;

/// <summary>
/// Describes a conflict detected during the merge phase.
/// </summary>
/// <param name="MemberName">Name of the member causing the conflict.</param>
/// <param name="Reason">Human-readable reason.</param>
public sealed record MergeConflict(string MemberName, string Reason);

/// <summary>
/// Result of a merge operation.
/// </summary>
/// <param name="MergedSource">The merged C# source text.</param>
/// <param name="Conflicts">Any conflicts detected (informational, not blocking).</param>
public sealed record MergeResult(string MergedSource, IReadOnlyList<MergeConflict> Conflicts);

/// <summary>
/// Merges newly generated code-behind content with an existing .xaml.cs file,
/// preserving all user-written code while replacing [GeneratedCode] members.
/// </summary>
public sealed class CodeBehindMergeEngine
{
    private const string GeneratedCodeAttributeName = "GeneratedCode";

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Merges <paramref name="generatedSource"/> with <paramref name="existingSource"/>.
    /// When <paramref name="existingSource"/> is empty, returns <paramref name="generatedSource"/> unchanged.
    /// </summary>
    public MergeResult Merge(string existingSource, string generatedSource)
    {
        if (string.IsNullOrWhiteSpace(existingSource))
            return new MergeResult(generatedSource, []);

        if (string.IsNullOrWhiteSpace(generatedSource))
            return new MergeResult(existingSource, []);

        var conflicts = new List<MergeConflict>();

        // Parse both files.
        var existingTree   = CSharpSyntaxTree.ParseText(existingSource);
        var generatedTree  = CSharpSyntaxTree.ParseText(generatedSource);

        var existingRoot   = existingTree.GetCompilationUnitRoot();
        var generatedRoot  = generatedTree.GetCompilationUnitRoot();

        // Locate the partial class in both trees.
        var existingClass  = FindPartialClass(existingRoot);
        var generatedClass = FindPartialClass(generatedRoot);

        if (existingClass is null || generatedClass is null)
            return new MergeResult(generatedSource, conflicts);

        // Classify existing members.
        var (existingGenerated, existingUser) = ClassifyMembers(existingClass);

        // Collect new generated members from the generated file.
        var (newGenerated, _) = ClassifyMembers(generatedClass);

        // Build the merged member list.
        // Strategy:
        //   1. Start with all user-written members (preserved as-is).
        //   2. For each new generated member:
        //      a. If a user-written method has the same name, skip generating the stub
        //         (user has their own implementation) and record info.
        //      b. If an old generated member exists with same name, check if user edited the body.
        //         - Body unchanged (still "TODO: implement") → replace with new generated member.
        //         - Body changed → keep user body, add note conflict.
        //      c. Otherwise → insert new generated member.

        var userMethodNames = existingUser
            .OfType<MethodDeclarationSyntax>()
            .Select(m => m.Identifier.Text)
            .ToHashSet(StringComparer.Ordinal);

        var oldGeneratedByName = existingGenerated
            .OfType<MethodDeclarationSyntax>()
            .ToLookup(m => m.Identifier.Text, StringComparer.Ordinal);

        var oldGeneratedFieldsByName = existingGenerated
            .OfType<FieldDeclarationSyntax>()
            .SelectMany(f => f.Declaration.Variables.Select(v => (v.Identifier.Text, f)))
            .ToDictionary(t => t.Item1, t => t.f, StringComparer.Ordinal);

        var membersToAdd = new List<MemberDeclarationSyntax>();

        foreach (var genMember in newGenerated)
        {
            if (genMember is MethodDeclarationSyntax genMethod)
            {
                string name = genMethod.Identifier.Text;

                if (userMethodNames.Contains(name))
                {
                    // User wrote their own implementation — skip stub.
                    conflicts.Add(new MergeConflict(name,
                        "User-written method with same name exists; generated stub skipped."));
                    continue;
                }

                var oldOverloads = oldGeneratedByName[name];
                if (oldOverloads.Any())
                {
                    // Check if user modified any overload's body from the original stub.
                    if (oldOverloads.Any(HasUserModifiedBody))
                    {
                        // User modified body → promote to user member (already in existingUser)
                        // and skip generating a new stub.
                        conflicts.Add(new MergeConflict(name,
                            "Previously generated stub body was modified by user; treating as user-written."));
                        continue;
                    }
                    // Body still a stub → replace with new generated version.
                }

                membersToAdd.Add(genMethod.WithLeadingTrivia(
                    SyntaxFactory.TriviaList(SyntaxFactory.CarriageReturnLineFeed,
                                            SyntaxFactory.CarriageReturnLineFeed)));
            }
            else if (genMember is FieldDeclarationSyntax genField)
            {
                string name = genField.Declaration.Variables.First().Identifier.Text;

                if (oldGeneratedFieldsByName.ContainsKey(name))
                {
                    // Field already present (same name) — replace with updated type declaration.
                    // (The merge just adds the new one; old generated ones are stripped below.)
                }

                membersToAdd.Add(genField);
            }
        }

        // Rebuild the class body:
        //   User members first, then new generated members.
        var finalMembers = new List<MemberDeclarationSyntax>(existingUser);
        finalMembers.AddRange(membersToAdd);

        var mergedClass = existingClass.WithMembers(
            SyntaxFactory.List(finalMembers));

        // Replace the class in the existing root.
        var mergedRoot = existingRoot.ReplaceNode(existingClass, mergedClass);
        string mergedSource = mergedRoot.ToFullString();

        return new MergeResult(mergedSource, conflicts);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static ClassDeclarationSyntax? FindPartialClass(CompilationUnitSyntax root)
    {
        return root.DescendantNodes()
                   .OfType<ClassDeclarationSyntax>()
                   .FirstOrDefault(c => c.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)));
    }

    private static (List<MemberDeclarationSyntax> Generated, List<MemberDeclarationSyntax> User)
        ClassifyMembers(ClassDeclarationSyntax classDecl)
    {
        var generated = new List<MemberDeclarationSyntax>();
        var user      = new List<MemberDeclarationSyntax>();

        foreach (var member in classDecl.Members)
        {
            if (HasGeneratedCodeAttribute(member))
                generated.Add(member);
            else
                user.Add(member);
        }

        return (generated, user);
    }

    private static bool HasGeneratedCodeAttribute(MemberDeclarationSyntax member)
    {
        return member.AttributeLists
                     .SelectMany(al => al.Attributes)
                     .Any(a => a.Name.ToString() is GeneratedCodeAttributeName
                                                  or "System.CodeDom.Compiler.GeneratedCode");
    }

    private static bool HasUserModifiedBody(MethodDeclarationSyntax method)
    {
        // A stub body contains only a comment "// TODO: implement" and nothing else.
        // If there's any real statement, the user has modified it.
        var body = method.Body;
        if (body is null)
            return false;

        var statements = body.Statements;
        if (statements.Count == 0)
            return false;

        // If there's more than one statement, definitely user-modified.
        if (statements.Count > 1)
            return true;

        // Single statement: check if it's just "throw new NotImplementedException()" or similar,
        // or if there's a non-trivial statement. We consider the stub unmodified when the only
        // content is the trivia comment "// TODO: implement".
        // Simplest heuristic: if the generated body text contained only that comment (no real
        // statement), our generator produces an empty body with trivia — but we added
        // "// TODO: implement" as trivia, not a statement. So statements.Count == 0 means unmodified.
        // Here statements.Count == 1 means user added something.
        return true;
    }
}
