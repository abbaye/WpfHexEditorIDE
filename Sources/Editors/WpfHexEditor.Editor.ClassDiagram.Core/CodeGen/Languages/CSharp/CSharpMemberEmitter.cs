// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram.Core
// File: CodeGen/Languages/CSharp/CSharpMemberEmitter.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.7
// Created: 2026-05-08
// Description:
//     Emits MemberDeclarationSyntax instances for every CodeMember
//     kind. Decoupled from the parent type so unit tests can drive
//     it directly.
// ==========================================================

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using WpfHexEditor.Editor.ClassDiagram.Core.CodeGen.Model;
using WpfHexEditor.Editor.ClassDiagram.Core.CodeGen.Options;

namespace WpfHexEditor.Editor.ClassDiagram.Core.CodeGen.Languages.CSharp;

internal static class CSharpMemberEmitter
{
    public static MemberDeclarationSyntax Emit(
        CodeMember member, CodeTypeKind owningTypeKind, CodeGenOptions options) =>
        member.Kind switch
        {
            CodeMemberKind.Field       => EmitField(member, owningTypeKind, options),
            CodeMemberKind.Property    => EmitProperty(member, owningTypeKind, options),
            CodeMemberKind.Event       => EmitEvent(member, owningTypeKind, options),
            CodeMemberKind.Constructor => EmitConstructor(member, owningTypeKind, options),
            CodeMemberKind.EnumMember  => EmitEnumMember(member, options),
            _                          => EmitMethod(member, owningTypeKind, options)
        };

    private static FieldDeclarationSyntax EmitField(
        CodeMember m, CodeTypeKind owningTypeKind, CodeGenOptions options)
    {
        var type = CSharpTypeRefHelper.Parse(m.ReturnType);
        var declarator = SyntaxFactory.VariableDeclarator(SyntaxFactory.Identifier(m.Name));
        if (!string.IsNullOrWhiteSpace(m.InitializerExpression))
        {
            declarator = declarator.WithInitializer(
                SyntaxFactory.EqualsValueClause(SyntaxFactory.ParseExpression(m.InitializerExpression)));
        }

        var declaration = SyntaxFactory.VariableDeclaration(type,
            SyntaxFactory.SingletonSeparatedList(declarator));

        return Decorate(SyntaxFactory.FieldDeclaration(declaration), m, owningTypeKind, options);
    }

    private static PropertyDeclarationSyntax EmitProperty(
        CodeMember m, CodeTypeKind owningTypeKind, CodeGenOptions options)
    {
        var type = CSharpTypeRefHelper.Parse(m.ReturnType);
        var prop = Decorate(
            SyntaxFactory.PropertyDeclaration(type, SyntaxFactory.Identifier(m.Name))
                .WithAccessorList(BuildAccessorList(m)),
            m, owningTypeKind, options);

        if (!string.IsNullOrWhiteSpace(m.InitializerExpression))
        {
            prop = prop
                .WithInitializer(SyntaxFactory.EqualsValueClause(
                    SyntaxFactory.ParseExpression(m.InitializerExpression)))
                .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));
        }

        return prop;
    }

    private static AccessorListSyntax BuildAccessorList(CodeMember m)
    {
        var accessors = new List<AccessorDeclarationSyntax>
        {
            SyntaxFactory
                .AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken))
        };

        if (m.HasSetter)
        {
            var setterKind = m.IsInitOnly
                ? SyntaxKind.InitAccessorDeclaration
                : SyntaxKind.SetAccessorDeclaration;
            accessors.Add(SyntaxFactory
                .AccessorDeclaration(setterKind)
                .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)));
        }

        return SyntaxFactory.AccessorList(SyntaxFactory.List(accessors));
    }

    private static EventFieldDeclarationSyntax EmitEvent(
        CodeMember m, CodeTypeKind owningTypeKind, CodeGenOptions options)
    {
        var type = CSharpTypeRefHelper.Parse(m.ReturnType, fallback: CSharpTypeRefHelper.DefaultEventType);
        var declarator = SyntaxFactory.VariableDeclarator(SyntaxFactory.Identifier(m.Name));
        var declaration = SyntaxFactory.VariableDeclaration(type,
            SyntaxFactory.SingletonSeparatedList(declarator));

        return Decorate(SyntaxFactory.EventFieldDeclaration(declaration), m, owningTypeKind, options);
    }

    private static EnumMemberDeclarationSyntax EmitEnumMember(CodeMember m, CodeGenOptions options)
    {
        var node = SyntaxFactory.EnumMemberDeclaration(SyntaxFactory.Identifier(m.Name))
            .WithAttributeLists(EmitAttributes(m, options))
            .WithLeadingTrivia(EmitDocs(m, options));

        if (!string.IsNullOrWhiteSpace(m.InitializerExpression))
        {
            node = node.WithEqualsValue(
                SyntaxFactory.EqualsValueClause(SyntaxFactory.ParseExpression(m.InitializerExpression)));
        }

        return node;
    }

    private static ConstructorDeclarationSyntax EmitConstructor(
        CodeMember m, CodeTypeKind owningTypeKind, CodeGenOptions options) =>
        Decorate(
            SyntaxFactory.ConstructorDeclaration(SyntaxFactory.Identifier(m.Name))
                .WithParameterList(CSharpParameterBuilder.Build(m.Parameters))
                .WithBody(SyntaxFactory.Block()),
            m, owningTypeKind, options);

    private static MethodDeclarationSyntax EmitMethod(
        CodeMember m, CodeTypeKind owningTypeKind, CodeGenOptions options)
    {
        var returnType = CSharpTypeRefHelper.ForMethodReturn(m.ReturnType, m.IsAsync, options);
        var method = Decorate(
            SyntaxFactory.MethodDeclaration(returnType, SyntaxFactory.Identifier(m.Name))
                .WithParameterList(CSharpParameterBuilder.Build(m.Parameters)),
            m, owningTypeKind, options);

        var typeParameterList = CSharpGenericsBuilder.BuildList(m.GenericParameters);
        if (typeParameterList is not null)
            method = method.WithTypeParameterList(typeParameterList)
                .WithConstraintClauses(CSharpGenericsBuilder.BuildConstraints(m.GenericParameters));

        return owningTypeKind == CodeTypeKind.Interface || m.IsAbstract
            ? method.WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken))
            : method.WithBody(BuildBody(m, options));
    }

    private static BlockSyntax BuildBody(CodeMember m, CodeGenOptions options)
    {
        if (!string.IsNullOrWhiteSpace(m.BodyText))
            return SyntaxFactory.ParseStatement("{ " + m.BodyText + " }") as BlockSyntax
                ?? SyntaxFactory.Block();

        var isVoid = string.IsNullOrEmpty(m.ReturnType) || m.ReturnType == "void";
        var isAsyncTask = m.IsAsync && options.EmitAsyncSignatures && isVoid;

        if (isVoid && !isAsyncTask)
            return SyntaxFactory.Block();

        var stmt = isAsyncTask
            ? "throw new NotImplementedException();"
            : "return default!;";

        return SyntaxFactory.Block(SyntaxFactory.ParseStatement(stmt));
    }

    private static T Decorate<T>(T node, CodeMember m, CodeTypeKind owningTypeKind, CodeGenOptions options)
        where T : MemberDeclarationSyntax =>
        (T)node
            .WithModifiers(CSharpModifierBuilder.ForMember(m, owningTypeKind))
            .WithAttributeLists(EmitAttributes(m, options))
            .WithLeadingTrivia(EmitDocs(m, options));

    private static SyntaxList<AttributeListSyntax> EmitAttributes(CodeMember m, CodeGenOptions options) =>
        options.EmitAttributes ? CSharpAttributeBuilder.Build(m.Attributes) : [];

    private static SyntaxTriviaList EmitDocs(CodeMember m, CodeGenOptions options) =>
        options.EmitXmlDocs ? CSharpXmlDocBuilder.Build(m.XmlDocSummary) : [];
}
