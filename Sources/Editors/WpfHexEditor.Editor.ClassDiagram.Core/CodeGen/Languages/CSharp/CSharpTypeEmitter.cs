// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram.Core
// File: CodeGen/Languages/CSharp/CSharpTypeEmitter.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.7
// Created: 2026-05-08
// Description:
//     Emits TypeDeclarationSyntax (and EnumDeclarationSyntax) from
//     CodeType IR descriptors. Selects the right Roslyn factory call
//     based on CodeTypeKind.
// ==========================================================

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using WpfHexEditor.Editor.ClassDiagram.Core.CodeGen.Model;
using WpfHexEditor.Editor.ClassDiagram.Core.CodeGen.Options;

namespace WpfHexEditor.Editor.ClassDiagram.Core.CodeGen.Languages.CSharp;

internal static class CSharpTypeEmitter
{
    public static MemberDeclarationSyntax Emit(CodeType type, CodeGenOptions options)
    {
        if (type.Kind is CodeTypeKind.Enum)
            return EmitEnum(type, options);

        var members = type.Members.Select(m => CSharpMemberEmitter.Emit(m, type.Kind, options));

        TypeDeclarationSyntax typeDecl = type.Kind switch
        {
            CodeTypeKind.Interface     => BuildInterface(type, members),
            CodeTypeKind.Struct or CodeTypeKind.ReadOnlyStruct => BuildStruct(type, members),
            CodeTypeKind.Record        => BuildRecord(type, members, isStruct: false),
            CodeTypeKind.RecordStruct  => BuildRecord(type, members, isStruct: true),
            _                          => BuildClass(type, members)
        };

        typeDecl = typeDecl
            .WithModifiers(CSharpModifierBuilder.ForType(type))
            .WithBaseList(BuildBaseList(type))
            .WithAttributeLists(BuildAttributes(type, options))
            .WithLeadingTrivia(BuildDocs(type, options));

        var generics = CSharpGenericsBuilder.BuildList(type.GenericParameters);
        if (generics is not null)
            typeDecl = typeDecl
                .WithTypeParameterList(generics)
                .WithConstraintClauses(CSharpGenericsBuilder.BuildConstraints(type.GenericParameters));

        return typeDecl;
    }

    private static ClassDeclarationSyntax BuildClass(CodeType type, IEnumerable<MemberDeclarationSyntax> members) =>
        SyntaxFactory.ClassDeclaration(type.Name).WithMembers(SyntaxFactory.List(members));

    private static InterfaceDeclarationSyntax BuildInterface(CodeType type, IEnumerable<MemberDeclarationSyntax> members) =>
        SyntaxFactory.InterfaceDeclaration(type.Name).WithMembers(SyntaxFactory.List(members));

    private static StructDeclarationSyntax BuildStruct(CodeType type, IEnumerable<MemberDeclarationSyntax> members) =>
        SyntaxFactory.StructDeclaration(type.Name).WithMembers(SyntaxFactory.List(members));

    private static RecordDeclarationSyntax BuildRecord(
        CodeType type, IEnumerable<MemberDeclarationSyntax> members, bool isStruct)
    {
        var classOrStruct = isStruct ? SyntaxFactory.Token(SyntaxKind.StructKeyword) : default;
        var declarationKind = isStruct ? SyntaxKind.RecordStructDeclaration : SyntaxKind.RecordDeclaration;

        var record = SyntaxFactory.RecordDeclaration(
                kind: declarationKind,
                keyword: SyntaxFactory.Token(SyntaxKind.RecordKeyword),
                identifier: SyntaxFactory.Identifier(type.Name))
            .WithClassOrStructKeyword(classOrStruct)
            .WithOpenBraceToken(SyntaxFactory.Token(SyntaxKind.OpenBraceToken))
            .WithCloseBraceToken(SyntaxFactory.Token(SyntaxKind.CloseBraceToken))
            .WithMembers(SyntaxFactory.List(members));

        if (type.PrimaryConstructorParameters.Count > 0)
            record = record.WithParameterList(
                CSharpParameterBuilder.Build(type.PrimaryConstructorParameters));

        return record;
    }

    private static EnumDeclarationSyntax EmitEnum(CodeType type, CodeGenOptions options)
    {
        var members = type.Members
            .Select(m => (EnumMemberDeclarationSyntax)CSharpMemberEmitter.Emit(m, CodeTypeKind.Enum, options));

        var node = SyntaxFactory.EnumDeclaration(type.Name)
            .WithModifiers(CSharpModifierBuilder.ForType(type))
            .WithMembers(SyntaxFactory.SeparatedList(members))
            .WithAttributeLists(BuildAttributes(type, options))
            .WithLeadingTrivia(BuildDocs(type, options));

        if (!string.IsNullOrWhiteSpace(type.EnumUnderlyingType))
        {
            var baseList = SyntaxFactory.BaseList(SyntaxFactory.SingletonSeparatedList<BaseTypeSyntax>(
                SyntaxFactory.SimpleBaseType(SyntaxFactory.ParseTypeName(type.EnumUnderlyingType))));
            node = node.WithBaseList(baseList);
        }

        return node;
    }

    private static BaseListSyntax? BuildBaseList(CodeType type)
    {
        if (type.Kind is CodeTypeKind.Enum)
            return null;

        var entries = new List<BaseTypeSyntax>();

        if (!string.IsNullOrWhiteSpace(type.BaseType))
            entries.Add(SyntaxFactory.SimpleBaseType(SyntaxFactory.ParseTypeName(type.BaseType)));

        foreach (var iface in type.ImplementedInterfaces)
        {
            if (string.IsNullOrWhiteSpace(iface))
                continue;
            entries.Add(SyntaxFactory.SimpleBaseType(SyntaxFactory.ParseTypeName(iface)));
        }

        return entries.Count == 0
            ? null
            : SyntaxFactory.BaseList(SyntaxFactory.SeparatedList(entries));
    }

    private static SyntaxList<AttributeListSyntax> BuildAttributes(CodeType type, CodeGenOptions options) =>
        options.EmitAttributes ? CSharpAttributeBuilder.Build(type.Attributes) : [];

    private static SyntaxTriviaList BuildDocs(CodeType type, CodeGenOptions options) =>
        options.EmitXmlDocs ? CSharpXmlDocBuilder.Build(type.XmlDocSummary) : [];
}
