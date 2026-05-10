// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram.Core
// File: CodeGen/Languages/VisualBasic/VBMemberEmitter.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.7
// Created: 2026-05-08
// Description:
//     Emits VB StatementSyntax instances for each CodeMember kind.
//     Sub vs Function discrimination is driven by whether the IR
//     return type is empty/void.
// ==========================================================

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.VisualBasic;
using Microsoft.CodeAnalysis.VisualBasic.Syntax;
using WpfHexEditor.Editor.ClassDiagram.Core.CodeGen.Model;
using WpfHexEditor.Editor.ClassDiagram.Core.CodeGen.Options;

namespace WpfHexEditor.Editor.ClassDiagram.Core.CodeGen.Languages.VisualBasic;

internal static class VBMemberEmitter
{
    public static StatementSyntax Emit(CodeMember member, CodeTypeKind owningTypeKind, CodeGenOptions options) =>
        member.Kind switch
        {
            CodeMemberKind.Field       => EmitField(member, owningTypeKind, options),
            CodeMemberKind.Property    => EmitProperty(member, owningTypeKind, options),
            CodeMemberKind.Event       => EmitEvent(member, owningTypeKind, options),
            CodeMemberKind.Constructor => EmitConstructor(member, owningTypeKind, options),
            CodeMemberKind.EnumMember  => EmitEnumMember(member, options),
            _                          => EmitMethod(member, owningTypeKind, options)
        };

    private static FieldDeclarationSyntax EmitField(CodeMember m, CodeTypeKind owner, CodeGenOptions options)
    {
        var declarator = SyntaxFactory.VariableDeclarator(
            SyntaxFactory.SingletonSeparatedList(SyntaxFactory.ModifiedIdentifier(m.Name)))
            .WithAsClause(SyntaxFactory.SimpleAsClause(VBTypeRefHelper.Parse(m.ReturnType)));

        if (!string.IsNullOrWhiteSpace(m.InitializerExpression))
            declarator = declarator.WithInitializer(
                SyntaxFactory.EqualsValue(SyntaxFactory.ParseExpression(m.InitializerExpression)));

        return SyntaxFactory.FieldDeclaration(
                SyntaxFactory.SingletonSeparatedList(declarator))
            .WithModifiers(VBModifierBuilder.ForMember(m, owner))
            .WithAttributeLists(EmitAttributes(m, options))
            .WithLeadingTrivia(EmitDocs(m, options));
    }

    private static PropertyBlockSyntax EmitProperty(CodeMember m, CodeTypeKind owner, CodeGenOptions options)
    {
        var statement = SyntaxFactory.PropertyStatement(SyntaxFactory.Identifier(m.Name))
            .WithAsClause(SyntaxFactory.SimpleAsClause(VBTypeRefHelper.Parse(m.ReturnType)))
            .WithModifiers(VBModifierBuilder.ForMember(m, owner))
            .WithAttributeLists(EmitAttributes(m, options))
            .WithLeadingTrivia(EmitDocs(m, options));

        if (!string.IsNullOrWhiteSpace(m.InitializerExpression))
            statement = statement.WithInitializer(
                SyntaxFactory.EqualsValue(SyntaxFactory.ParseExpression(m.InitializerExpression)));

        return SyntaxFactory.PropertyBlock(statement, SyntaxFactory.List(BuildAccessors(m)));
    }

    private static IEnumerable<AccessorBlockSyntax> BuildAccessors(CodeMember m)
    {
        yield return SyntaxFactory.AccessorBlock(
            SyntaxKind.GetAccessorBlock,
            SyntaxFactory.GetAccessorStatement(),
            SyntaxFactory.EndGetStatement());

        if (m.HasSetter)
            yield return SyntaxFactory.AccessorBlock(
                SyntaxKind.SetAccessorBlock,
                SyntaxFactory.SetAccessorStatement(),
                SyntaxFactory.EndSetStatement());
    }

    private static EventStatementSyntax EmitEvent(CodeMember m, CodeTypeKind owner, CodeGenOptions options) =>
        SyntaxFactory.EventStatement(SyntaxFactory.Identifier(m.Name))
            .WithAsClause(SyntaxFactory.SimpleAsClause(VBTypeRefHelper.Parse(m.ReturnType, VBTypeRefHelper.DefaultEventType)))
            .WithModifiers(VBModifierBuilder.ForMember(m, owner))
            .WithAttributeLists(EmitAttributes(m, options))
            .WithLeadingTrivia(EmitDocs(m, options));

    private static EnumMemberDeclarationSyntax EmitEnumMember(CodeMember m, CodeGenOptions options)
    {
        var node = SyntaxFactory.EnumMemberDeclaration(SyntaxFactory.Identifier(m.Name))
            .WithAttributeLists(EmitAttributes(m, options))
            .WithLeadingTrivia(EmitDocs(m, options));

        if (!string.IsNullOrWhiteSpace(m.InitializerExpression))
            node = node.WithInitializer(SyntaxFactory.EqualsValue(SyntaxFactory.ParseExpression(m.InitializerExpression)));

        return node;
    }

    private static ConstructorBlockSyntax EmitConstructor(CodeMember m, CodeTypeKind owner, CodeGenOptions options)
    {
        var subNew = SyntaxFactory.SubNewStatement()
            .WithParameterList(VBParameterBuilder.Build(m.Parameters))
            .WithModifiers(VBModifierBuilder.ForMember(m, owner))
            .WithAttributeLists(EmitAttributes(m, options))
            .WithLeadingTrivia(EmitDocs(m, options));

        return SyntaxFactory.ConstructorBlock(subNew, default, SyntaxFactory.EndSubStatement());
    }

    private static StatementSyntax EmitMethod(CodeMember m, CodeTypeKind owner, CodeGenOptions options)
    {
        var isAbstract = owner == CodeTypeKind.Interface || m.IsAbstract;
        var isVoid = VBTypeRefHelper.IsVoid(m.ReturnType) && !m.IsAsync;

        return isVoid
            ? EmitSubMethod(m, owner, options, isAbstract)
            : EmitFunctionMethod(m, owner, options, isAbstract);
    }

    private static StatementSyntax EmitSubMethod(
        CodeMember m, CodeTypeKind owner, CodeGenOptions options, bool isAbstract)
    {
        var statement = SyntaxFactory.SubStatement(SyntaxFactory.Identifier(m.Name))
            .WithParameterList(VBParameterBuilder.Build(m.Parameters))
            .WithModifiers(VBModifierBuilder.ForMember(m, owner))
            .WithAttributeLists(EmitAttributes(m, options))
            .WithLeadingTrivia(EmitDocs(m, options));

        var generics = VBGenericsBuilder.BuildList(m.GenericParameters);
        if (generics is not null)
            statement = statement.WithTypeParameterList(generics);

        return isAbstract
            ? statement
            : SyntaxFactory.SubBlock(statement, default, SyntaxFactory.EndSubStatement());
    }

    private static StatementSyntax EmitFunctionMethod(
        CodeMember m, CodeTypeKind owner, CodeGenOptions options, bool isAbstract)
    {
        var returnType = VBTypeRefHelper.ForFunctionReturn(m.ReturnType, m.IsAsync, options);
        var statement = SyntaxFactory.FunctionStatement(SyntaxFactory.Identifier(m.Name))
            .WithParameterList(VBParameterBuilder.Build(m.Parameters))
            .WithAsClause(SyntaxFactory.SimpleAsClause(returnType))
            .WithModifiers(VBModifierBuilder.ForMember(m, owner))
            .WithAttributeLists(EmitAttributes(m, options))
            .WithLeadingTrivia(EmitDocs(m, options));

        var generics = VBGenericsBuilder.BuildList(m.GenericParameters);
        if (generics is not null)
            statement = statement.WithTypeParameterList(generics);

        if (isAbstract)
            return statement;

        var body = SyntaxFactory.SingletonList<StatementSyntax>(
            SyntaxFactory.ThrowStatement(SyntaxFactory.ParseExpression("New NotImplementedException()")));
        return SyntaxFactory.FunctionBlock(statement, body, SyntaxFactory.EndFunctionStatement());
    }

    private static SyntaxList<AttributeListSyntax> EmitAttributes(CodeMember m, CodeGenOptions options) =>
        options.EmitAttributes ? VBAttributeBuilder.Build(m.Attributes) : [];

    private static SyntaxTriviaList EmitDocs(CodeMember m, CodeGenOptions options) =>
        options.EmitXmlDocs ? VBXmlDocBuilder.Build(m.XmlDocSummary) : [];
}
