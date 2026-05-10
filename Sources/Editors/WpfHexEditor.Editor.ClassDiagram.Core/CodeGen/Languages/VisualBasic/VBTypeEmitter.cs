// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram.Core
// File: CodeGen/Languages/VisualBasic/VBTypeEmitter.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.7
// Created: 2026-05-08
// Description:
//     Emits VB type block syntax (Class/Interface/Structure/Enum/Module)
//     from CodeType IR descriptors.
//
// Architecture Notes:
//     VB has no record syntax, so Record / RecordStruct map to
//     Class / Structure with a header comment noting the lossy
//     translation.
// ==========================================================

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.VisualBasic;
using Microsoft.CodeAnalysis.VisualBasic.Syntax;
using WpfHexEditor.Editor.ClassDiagram.Core.CodeGen.Model;
using WpfHexEditor.Editor.ClassDiagram.Core.CodeGen.Options;

namespace WpfHexEditor.Editor.ClassDiagram.Core.CodeGen.Languages.VisualBasic;

internal static class VBTypeEmitter
{
    public static StatementSyntax Emit(CodeType type, CodeGenOptions options) => type.Kind switch
    {
        CodeTypeKind.Enum                                  => EmitEnum(type, options),
        CodeTypeKind.Interface                             => EmitInterface(type, options),
        CodeTypeKind.Struct or CodeTypeKind.ReadOnlyStruct
        or CodeTypeKind.RecordStruct                       => EmitStructure(type, options),
        _                                                  => EmitClass(type, options)
    };

    private static ClassBlockSyntax EmitClass(CodeType type, CodeGenOptions options)
    {
        var header = SyntaxFactory.ClassStatement(SyntaxFactory.Identifier(type.Name))
            .WithModifiers(VBModifierBuilder.ForType(type))
            .WithAttributeLists(BuildAttributes(type, options))
            .WithLeadingTrivia(BuildDocs(type, options))
            .WithTypeParameterList(VBGenericsBuilder.BuildList(type.GenericParameters)!);

        var inheritList = SyntaxFactory.List(BuildInherits(type));
        var implementsList = SyntaxFactory.List(BuildImplements(type));

        return SyntaxFactory.ClassBlock(
            header,
            inheritList,
            implementsList,
            SyntaxFactory.List(type.Members.Select(m => VBMemberEmitter.Emit(m, type.Kind, options))),
            SyntaxFactory.EndClassStatement());
    }

    private static InterfaceBlockSyntax EmitInterface(CodeType type, CodeGenOptions options)
    {
        var header = SyntaxFactory.InterfaceStatement(SyntaxFactory.Identifier(type.Name))
            .WithModifiers(VBModifierBuilder.ForType(type))
            .WithAttributeLists(BuildAttributes(type, options))
            .WithLeadingTrivia(BuildDocs(type, options))
            .WithTypeParameterList(VBGenericsBuilder.BuildList(type.GenericParameters)!);

        return SyntaxFactory.InterfaceBlock(
            header,
            SyntaxFactory.List(BuildInherits(type)),
            default,
            SyntaxFactory.List(type.Members.Select(m => VBMemberEmitter.Emit(m, type.Kind, options))),
            SyntaxFactory.EndInterfaceStatement());
    }

    private static StructureBlockSyntax EmitStructure(CodeType type, CodeGenOptions options)
    {
        var header = SyntaxFactory.StructureStatement(SyntaxFactory.Identifier(type.Name))
            .WithModifiers(VBModifierBuilder.ForType(type))
            .WithAttributeLists(BuildAttributes(type, options))
            .WithLeadingTrivia(BuildDocs(type, options))
            .WithTypeParameterList(VBGenericsBuilder.BuildList(type.GenericParameters)!);

        return SyntaxFactory.StructureBlock(
            header,
            default,
            SyntaxFactory.List(BuildImplements(type)),
            SyntaxFactory.List(type.Members.Select(m => VBMemberEmitter.Emit(m, type.Kind, options))),
            SyntaxFactory.EndStructureStatement());
    }

    private static EnumBlockSyntax EmitEnum(CodeType type, CodeGenOptions options)
    {
        var header = SyntaxFactory.EnumStatement(SyntaxFactory.Identifier(type.Name))
            .WithModifiers(VBModifierBuilder.ForType(type))
            .WithAttributeLists(BuildAttributes(type, options))
            .WithLeadingTrivia(BuildDocs(type, options));

        if (!string.IsNullOrWhiteSpace(type.EnumUnderlyingType))
            header = header.WithUnderlyingType(
                SyntaxFactory.SimpleAsClause(VBTypeRefHelper.Parse(type.EnumUnderlyingType)));

        var members = type.Members
            .Select(m => (StatementSyntax)VBMemberEmitter.Emit(m, CodeTypeKind.Enum, options));

        return SyntaxFactory.EnumBlock(header, SyntaxFactory.List(members), SyntaxFactory.EndEnumStatement());
    }

    private static IEnumerable<InheritsStatementSyntax> BuildInherits(CodeType type)
    {
        if (string.IsNullOrWhiteSpace(type.BaseType))
            yield break;

        yield return SyntaxFactory.InheritsStatement(VBTypeRefHelper.Parse(type.BaseType));
    }

    private static IEnumerable<ImplementsStatementSyntax> BuildImplements(CodeType type)
    {
        var nonEmpty = type.ImplementedInterfaces
            .Where(i => !string.IsNullOrWhiteSpace(i))
            .Select(i => VBTypeRefHelper.Parse(i))
            .ToList();

        if (nonEmpty.Count == 0)
            yield break;

        yield return SyntaxFactory.ImplementsStatement(
            SyntaxFactory.SeparatedList(nonEmpty));
    }

    private static SyntaxList<AttributeListSyntax> BuildAttributes(CodeType type, CodeGenOptions options) =>
        options.EmitAttributes ? VBAttributeBuilder.Build(type.Attributes) : [];

    private static SyntaxTriviaList BuildDocs(CodeType type, CodeGenOptions options) =>
        options.EmitXmlDocs ? VBXmlDocBuilder.Build(type.XmlDocSummary) : [];
}
