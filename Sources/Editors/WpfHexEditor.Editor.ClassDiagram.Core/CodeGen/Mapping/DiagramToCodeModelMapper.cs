// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram.Core
// File: CodeGen/Mapping/DiagramToCodeModelMapper.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.7
// Created: 2026-05-08
// Description:
//     Translates a DiagramDocument (UI/diagram-oriented) into the
//     language-agnostic CodeModel IR consumed by every generator.
//
// Architecture Notes:
//     Pure transformation — no I/O, no static state. The mapper is
//     deterministic: same DiagramDocument + same CodeGenOptions always
//     produces the same CodeModel. Inheritance edges from the diagram
//     are projected onto CodeType.BaseType / ImplementedInterfaces.
// ==========================================================

using WpfHexEditor.Editor.ClassDiagram.Core.CodeGen.Model;
using WpfHexEditor.Editor.ClassDiagram.Core.CodeGen.Options;
using WpfHexEditor.Editor.ClassDiagram.Core.Model;

namespace WpfHexEditor.Editor.ClassDiagram.Core.CodeGen.Mapping;

/// <summary>
/// Builds an immutable <see cref="CodeModel"/> from a <see cref="DiagramDocument"/>.
/// </summary>
public static class DiagramToCodeModelMapper
{
    /// <summary>
    /// Translates <paramref name="document"/> into a <see cref="CodeModel"/> using
    /// <paramref name="options"/> for default-namespace fallback.
    /// </summary>
    public static CodeModel Map(DiagramDocument document, CodeGenOptions options)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(options);

        var context = MappingContext.Build(document);
        var types = document.Classes.Select(node => MapType(node, context)).ToList();

        return new CodeModel
        {
            RootNamespace = options.RootNamespace,
            Usings = [],
            Types = types
        };
    }

    private sealed record MappingContext(
        IReadOnlyDictionary<string, string> Inheritance,
        IReadOnlyDictionary<string, IReadOnlyList<string>> Interfaces,
        IReadOnlyDictionary<string, ClassNode> NodesById)
    {
        public static MappingContext Build(DiagramDocument document)
        {
            var inheritance = new Dictionary<string, string>(StringComparer.Ordinal);
            var interfaces = new Dictionary<string, List<string>>(StringComparer.Ordinal);

            foreach (var r in document.Relationships)
            {
                switch (r.Kind)
                {
                    case RelationshipKind.Inheritance:
                        inheritance.TryAdd(r.SourceId, r.TargetId);
                        break;
                    case RelationshipKind.Realization:
                        if (!interfaces.TryGetValue(r.SourceId, out var list))
                            interfaces[r.SourceId] = list = [];
                        list.Add(r.TargetId);
                        break;
                }
            }

            return new MappingContext(
                inheritance,
                interfaces.ToDictionary(kv => kv.Key, kv => (IReadOnlyList<string>)kv.Value, StringComparer.Ordinal),
                document.Classes.ToDictionary(c => c.Id, StringComparer.Ordinal));
        }

        public string ResolveName(string id) =>
            NodesById.TryGetValue(id, out var node) ? node.Name : id;
    }

    private static CodeType MapType(ClassNode node, MappingContext ctx)
    {
        var kind = ResolveKind(node);
        return new CodeType
        {
            Name = node.Name,
            Kind = kind,
            Accessibility = CodeAccessibility.Public,
            Namespace = node.Namespace,
            IsPartial = node.IsPartial,
            XmlDocSummary = node.XmlDocSummary,
            BaseType = ResolveBaseType(node, kind, ctx),
            ImplementedInterfaces = ResolveInterfaces(node, ctx),
            Attributes = MapAttributeNames(node.Attributes),
            Members = MapMembers(node, kind)
        };
    }

    private static CodeTypeKind ResolveKind(ClassNode node) => node.Kind switch
    {
        ClassKind.Interface                  => CodeTypeKind.Interface,
        ClassKind.Enum                       => CodeTypeKind.Enum,
        ClassKind.Struct when node.IsRecord  => CodeTypeKind.RecordStruct,
        ClassKind.Struct                     => CodeTypeKind.Struct,
        ClassKind.Abstract                   => CodeTypeKind.AbstractClass,
        _ when node.IsAbstract               => CodeTypeKind.AbstractClass,
        _ when node.IsRecord                 => CodeTypeKind.Record,
        _ when node.IsSealed                 => CodeTypeKind.SealedClass,
        _                                    => CodeTypeKind.Class
    };

    private static string? ResolveBaseType(ClassNode node, CodeTypeKind kind, MappingContext ctx)
    {
        if (kind is CodeTypeKind.Enum or CodeTypeKind.Interface)
            return null;
        return ctx.Inheritance.TryGetValue(node.Id, out var baseId) ? ctx.ResolveName(baseId) : null;
    }

    private static IReadOnlyList<string> ResolveInterfaces(ClassNode node, MappingContext ctx) =>
        ctx.Interfaces.TryGetValue(node.Id, out var ids)
            ? ids.Select(ctx.ResolveName).ToList()
            : [];

    private static IReadOnlyList<CodeMember> MapMembers(ClassNode node, CodeTypeKind kind)
    {
        if (kind is CodeTypeKind.Enum)
            return MapEnumMembers(node);

        return node.Members.Select(m => MapMember(m, kind)).ToList();
    }

    private static IReadOnlyList<CodeMember> MapEnumMembers(ClassNode node) =>
        node.Members.Select(m => new CodeMember
        {
            Name = m.Name,
            Kind = CodeMemberKind.EnumMember,
            Accessibility = CodeAccessibility.NotApplicable,
            XmlDocSummary = m.XmlDocSummary
        }).ToList();

    private static CodeMember MapMember(ClassMember m, CodeTypeKind owningTypeKind)
    {
        var memberKind = MapMemberKind(m.Kind);
        return new CodeMember
        {
            Name = m.Name,
            Kind = memberKind,
            ReturnType = m.TypeName,
            Accessibility = MapVisibility(m.Visibility, owningTypeKind),
            IsStatic = m.IsStatic,
            IsAbstract = m.IsAbstract || owningTypeKind == CodeTypeKind.Interface,
            IsAsync = m.IsAsync,
            IsOverride = m.IsOverride,
            HasSetter = memberKind == CodeMemberKind.Property,
            XmlDocSummary = m.XmlDocSummary,
            Parameters = MapParameters(m.Parameters),
            GenericParameters = MapGenericConstraints(m.GenericConstraints),
            Attributes = MapAttributeNames(m.Attributes)
        };
    }

    private static CodeMemberKind MapMemberKind(MemberKind kind) => kind switch
    {
        MemberKind.Field    => CodeMemberKind.Field,
        MemberKind.Property => CodeMemberKind.Property,
        MemberKind.Event    => CodeMemberKind.Event,
        _                   => CodeMemberKind.Method
    };

    private static CodeAccessibility MapVisibility(MemberVisibility v, CodeTypeKind owner)
    {
        if (owner is CodeTypeKind.Interface)
            return CodeAccessibility.NotApplicable;

        return v switch
        {
            MemberVisibility.Private   => CodeAccessibility.Private,
            MemberVisibility.Protected => CodeAccessibility.Protected,
            MemberVisibility.Internal  => CodeAccessibility.Internal,
            _                          => CodeAccessibility.Public
        };
    }

    private static IReadOnlyList<CodeParameter> MapParameters(IReadOnlyList<string> raw)
    {
        if (raw.Count == 0)
            return [];

        var parameters = new List<CodeParameter>(raw.Count);
        foreach (var token in raw)
        {
            var trimmed = token.Trim();
            if (trimmed.Length == 0)
                continue;

            // Heuristic: "Type name" -> two tokens; bare token = type only with synthetic name.
            var lastSpace = trimmed.LastIndexOf(' ');
            if (lastSpace > 0)
            {
                parameters.Add(new CodeParameter
                {
                    Type = trimmed[..lastSpace].Trim(),
                    Name = trimmed[(lastSpace + 1)..].Trim()
                });
            }
            else
            {
                parameters.Add(new CodeParameter
                {
                    Type = trimmed,
                    Name = $"arg{parameters.Count}"
                });
            }
        }

        return parameters;
    }

    private static IReadOnlyList<CodeGenericParameter> MapGenericConstraints(string? raw)
    {
        // The diagram model packs all where-clauses into a single string, e.g.
        //     "where T : IDisposable where U : class, new()"
        // Splitting on `where` produces one CodeGenericParameter per clause.
        if (string.IsNullOrWhiteSpace(raw))
            return [];

        var clauses = raw
            .Split("where", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var result = new List<CodeGenericParameter>(clauses.Length);
        foreach (var clause in clauses)
        {
            var colon = clause.IndexOf(':');
            if (colon < 0)
                continue;

            var name = clause[..colon].Trim();
            var constraintList = clause[(colon + 1)..]
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();

            result.Add(new CodeGenericParameter
            {
                Name = name,
                Constraints = constraintList
            });
        }

        return result;
    }

    private static IReadOnlyList<CodeAttribute> MapAttributeNames(IReadOnlyList<string> names)
    {
        if (names.Count == 0)
            return [];

        return names
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => new CodeAttribute { Name = n.Trim() })
            .ToList();
    }
}
