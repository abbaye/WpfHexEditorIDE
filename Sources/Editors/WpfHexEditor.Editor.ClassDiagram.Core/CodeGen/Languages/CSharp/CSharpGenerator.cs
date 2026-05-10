// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram.Core
// File: CodeGen/Languages/CSharp/CSharpGenerator.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.7
// Created: 2026-05-08
// Description:
//     ILanguageGenerator implementation that emits C# source via the
//     Roslyn SyntaxFactory. NormalizeWhitespace() guarantees a clean
//     and syntactically valid output regardless of input shape.
//
// Architecture Notes:
//     Composition: each concern (modifiers, parameters, generics, …)
//     lives in its own helper class so this orchestrator stays under
//     ~150 lines and each helper stays under ~25 lines per method.
//     SyntaxFactory output is canonicalised by NormalizeWhitespace
//     and prefixed with an optional banner / nullable directive.
// ==========================================================

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text;
using WpfHexEditor.Editor.ClassDiagram.Core.CodeGen.Abstractions;
using WpfHexEditor.Editor.ClassDiagram.Core.CodeGen.Model;
using WpfHexEditor.Editor.ClassDiagram.Core.CodeGen.Options;

namespace WpfHexEditor.Editor.ClassDiagram.Core.CodeGen.Languages.CSharp;

/// <summary>
/// C# language generator backed by Roslyn <see cref="SyntaxFactory"/>.
/// Output is guaranteed to be syntactically valid C# and idiomatically formatted.
/// </summary>
public sealed class CSharpGenerator : ILanguageGenerator
{
    /// <inheritdoc/>
    public string LanguageId => LanguageIds.CSharp;

    /// <inheritdoc/>
    public string DisplayName => "C#";

    /// <inheritdoc/>
    public string FileExtension => ".cs";

    /// <inheritdoc/>
    public string Generate(CodeModel model, CodeGenOptions options)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(options);

        var typesByNamespace = model.Types
            .GroupBy(t => string.IsNullOrEmpty(t.Namespace) ? model.RootNamespace : t.Namespace,
                StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .ToList();

        var compilationUnit = SyntaxFactory.CompilationUnit()
            .WithUsings(BuildUsings(model.Usings))
            .WithMembers(SyntaxFactory.List(BuildNamespaceMembers(typesByNamespace, options)));

        var rendered = compilationUnit
            .NormalizeWhitespace(indentation: ResolveIndent(options), eol: "\n")
            .ToFullString();

        return PrependPreamble(rendered, options);
    }

    private static SyntaxList<UsingDirectiveSyntax> BuildUsings(IReadOnlyList<CodeUsing> usings) =>
        usings.Count == 0 ? [] : SyntaxFactory.List(usings.Select(BuildOneUsing));

    private static UsingDirectiveSyntax BuildOneUsing(CodeUsing u)
    {
        var directive = SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(u.Namespace));

        if (u.IsStatic)
            directive = directive.WithStaticKeyword(SyntaxFactory.Token(SyntaxKind.StaticKeyword));
        if (u.IsGlobal)
            directive = directive.WithGlobalKeyword(SyntaxFactory.Token(SyntaxKind.GlobalKeyword));
        if (!string.IsNullOrEmpty(u.Alias))
            directive = directive.WithAlias(SyntaxFactory.NameEquals(u.Alias!));

        return directive;
    }

    private static IEnumerable<MemberDeclarationSyntax> BuildNamespaceMembers(
        IReadOnlyList<IGrouping<string, CodeType>> typesByNamespace, CodeGenOptions options)
    {
        foreach (var group in typesByNamespace)
        {
            var typeDecls = group.Select(t => CSharpTypeEmitter.Emit(t, options));
            yield return BuildNamespaceDecl(group.Key, typeDecls, options);
        }
    }

    private static MemberDeclarationSyntax BuildNamespaceDecl(
        string namespaceName, IEnumerable<MemberDeclarationSyntax> typeDecls, CodeGenOptions options)
    {
        var name = SyntaxFactory.ParseName(namespaceName);
        var members = SyntaxFactory.List(typeDecls);

        return UseFileScoped(options)
            ? SyntaxFactory.FileScopedNamespaceDeclaration(name).WithMembers(members)
            : SyntaxFactory.NamespaceDeclaration(name).WithMembers(members);
    }

    private static bool UseFileScoped(CodeGenOptions options)
    {
        if (!options.UseFileScopedNamespace)
            return false;

        return options.CSharpVersion is
            CSharpLanguageVersion.CSharp10 or
            CSharpLanguageVersion.CSharp11 or
            CSharpLanguageVersion.CSharp12 or
            CSharpLanguageVersion.Latest;
    }

    private static string ResolveIndent(CodeGenOptions options) =>
        options.IndentStyle == IndentStyle.Tabs
            ? "\t"
            : new string(' ', Math.Max(1, options.IndentSize));

    private static string PrependPreamble(string body, CodeGenOptions options)
    {
        var emitNullable = options.NullableContextEnabled && options.CSharpVersion != CSharpLanguageVersion.CSharp7_3;
        if (!options.EmitHeader && !emitNullable)
            return body;

        var preamble = new StringBuilder();
        if (options.EmitHeader)
        {
            preamble.Append("// <auto-generated />\n");
            preamble.Append("// Generated by WpfHexEditor ClassDiagram CodeGen.\n");
            preamble.Append('\n');
        }
        if (emitNullable)
        {
            preamble.Append("#nullable enable\n");
            preamble.Append('\n');
        }
        return preamble + body;
    }
}
