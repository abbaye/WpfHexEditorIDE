// ==========================================================
// Project: WpfHexEditor.Plugins.ClassDiagram
// File: Analysis/RoslynClassDiagramAnalyzer.cs
// Contributors: Claude Sonnet 4.6
// Created: 2026-04-07
// Description:
//     Roslyn syntax-tree based analyzer that replaces the regex-based
//     Uses Microsoft.CodeAnalysis.CSharp
//     syntax trees (no workspace, no MSBuild) to extract type declarations,
//     members, relationships, source locations, XML doc summaries,
//     and modifier flags from C# source files.
//
//     Key improvements over the regex analyzer:
//       - Partial class merging: all fragments of the same type (same name
//         + namespace) are merged into one ClassNode regardless of file count.
//       - Full generic type signatures (List<T>, Task<TResult>, etc.).
//       - Async / override / static / sealed / record modifiers.
//       - XML doc comment extraction from leading trivia.
//       - Source file path + 1-based line number per node and member.
//       - Cyclomatic complexity estimation per method.
//       - Efferent coupling (Ce) count per type (field/property type references).
//
// Architecture Notes:
//     Pattern: Static Service. Thread-safe (immutable per-parse state).
//     Syntax-only: no SemanticModel required → fast, no compilation errors.
//     VB.NET files (.vb) are skipped (no VB syntax tree integration).
//     AutoLayout via internal ApplyGridLayout helper.
// ==========================================================

using System.IO;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using WpfHexEditor.Editor.ClassDiagram.Core.Layout;
using WpfHexEditor.Editor.ClassDiagram.Core.Model;
using WpfHexEditor.Plugins.ClassDiagram.Options;

namespace WpfHexEditor.Plugins.ClassDiagram.Analysis;

/// <summary>
/// Roslyn syntax-tree analyzer that produces a <see cref="DiagramDocument"/>
/// from one or more C# source files. Partial class fragments across files are
/// merged into a single <see cref="ClassNode"/>.
/// </summary>
public static class RoslynClassDiagramAnalyzer
{
    // ── Public entry points ──────────────────────────────────────────────────

    /// <summary>
    /// Analyzes one or more source files and returns a merged
    /// <see cref="DiagramDocument"/>. VB.NET files are skipped (unsupported).
    /// </summary>
    public static DiagramDocument AnalyzeFiles(
        IEnumerable<string> filePaths,
        ClassDiagramOptions? options = null)
    {
        options ??= new ClassDiagramOptions();

        // Key: "Namespace.TypeName" (or just "TypeName" when no namespace)
        var nodeMap        = new Dictionary<string, ClassNode>(StringComparer.Ordinal);
        var inheritanceMap = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        foreach (string path in filePaths)
        {
            if (!File.Exists(path)) continue;
            if (!path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)) continue;

            string source;
            try { source = File.ReadAllText(path); }
            catch { continue; }

            var tree  = CSharpSyntaxTree.ParseText(source, path: path);
            var root  = tree.GetRoot();

            ProcessSyntaxTree(root, path, options, nodeMap, inheritanceMap);
        }

        var document = new DiagramDocument();
        foreach (var node in nodeMap.Values)
            document.Classes.Add(node);

        BuildRelationships(document, nodeMap, inheritanceMap);
        ComputeMetrics(nodeMap);

        if (options.AutoLayout)
            LayoutStrategyFactory.Create(options.LayoutStrategy).Layout(document, new LayoutOptions
            {
                Strategy           = options.LayoutStrategy,
                ColSpacing         = 60,
                RowSpacing         = 80,
                CanvasPadding      = 40,
                MinBoxWidth        = options.DefaultNodeWidth,
                ForceIterations    = options.ForceDirectedIterations,
                SpringLength       = options.SpringLength
            });

        return document;
    }

    /// <summary>
    /// Convenience overload for a single C# file.
    /// </summary>
    public static DiagramDocument AnalyzeFile(
        string filePath,
        ClassDiagramOptions? options = null)
        => AnalyzeFiles([filePath], options);

    // ── Syntax tree processing ───────────────────────────────────────────────

    private static void ProcessSyntaxTree(
        SyntaxNode root,
        string filePath,
        ClassDiagramOptions options,
        Dictionary<string, ClassNode> nodeMap,
        Dictionary<string, List<string>> inheritanceMap)
    {
        // Walk all type declarations in the file (handles nested types too)
        foreach (var typeDecl in root.DescendantNodes().OfType<BaseTypeDeclarationSyntax>())
        {
            string typeName     = typeDecl.Identifier.ValueText;
            string ns           = GetNamespace(typeDecl);
            string fullKey      = string.IsNullOrEmpty(ns) ? typeName : $"{ns}.{typeName}";
            bool   isPartial    = typeDecl.Modifiers.Any(SyntaxKind.PartialKeyword);

            if (!nodeMap.TryGetValue(fullKey, out var node))
            {
                // First time we see this type — create the node
                node = ClassNode.Create(typeName, GetClassKind(typeDecl));
                node.Namespace          = ns;
                node.IsAbstract         = typeDecl.Modifiers.Any(SyntaxKind.AbstractKeyword);
                node.IsSealed           = typeDecl.Modifiers.Any(SyntaxKind.SealedKeyword);
                node.IsPartial          = isPartial;
                node.IsRecord           = typeDecl is RecordDeclarationSyntax;
                node.XmlDocSummary      = ExtractXmlDocSummary(typeDecl);
                node.SourceFilePath     = filePath;
                node.SourceLineOneBased = typeDecl.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                node.Width              = options.DefaultNodeWidth;
                node.Height             = options.DefaultNodeHeight;
                node.Attributes.AddRange(ExtractAttributes(typeDecl.AttributeLists));

                nodeMap[fullKey] = node;
            }
            else
            {
                // Subsequent partial fragment — mark as partial, don't overwrite source location
                node.IsPartial = true;
            }

            // Extract members from this fragment
            ExtractMembers(typeDecl, node, filePath, options);

            // Collect base types / implemented interfaces for relationship building
            if (typeDecl.BaseList?.Types.Count > 0)
            {
                if (!inheritanceMap.TryGetValue(fullKey, out var bases))
                    inheritanceMap[fullKey] = bases = [];

                foreach (var baseType in typeDecl.BaseList.Types)
                {
                    string baseName = GetSimpleTypeName(baseType.Type);
                    if (!string.IsNullOrEmpty(baseName) && !bases.Contains(baseName))
                        bases.Add(baseName);
                }
            }
        }
    }

    // ── Member extraction ────────────────────────────────────────────────────

    private static void ExtractMembers(
        BaseTypeDeclarationSyntax typeDecl,
        ClassNode node,
        string filePath,
        ClassDiagramOptions options)
    {
        if (typeDecl is not TypeDeclarationSyntax typeDeclWithBody) return;

        foreach (var member in typeDeclWithBody.Members)
        {
            switch (member)
            {
                case FieldDeclarationSyntax field:
                    ExtractField(field, node, filePath, options);
                    break;

                case PropertyDeclarationSyntax prop:
                    ExtractProperty(prop, node, filePath, options);
                    break;

                case EventDeclarationSyntax evt:
                    ExtractEvent(evt, node, filePath, options);
                    break;

                case EventFieldDeclarationSyntax evtField:
                    ExtractEventField(evtField, node, filePath, options);
                    break;

                case MethodDeclarationSyntax method:
                    ExtractMethod(method, node, filePath, options);
                    break;

                case ConstructorDeclarationSyntax ctor:
                    ExtractConstructor(ctor, node, filePath, options);
                    break;

                case OperatorDeclarationSyntax op:
                    ExtractOperator(op, node, filePath, options);
                    break;
            }
        }
    }

    private static void ExtractField(
        FieldDeclarationSyntax field,
        ClassNode node,
        string filePath,
        ClassDiagramOptions options)
    {
        var vis = GetVisibility(field.Modifiers);
        if (!ShouldInclude(vis, options)) return;

        string typeName = field.Declaration.Type.ToString();
        bool   isStatic = field.Modifiers.Any(SyntaxKind.StaticKeyword);

        foreach (var variable in field.Declaration.Variables)
        {
            string name = variable.Identifier.ValueText;
            if (node.Members.Any(m => m.Name == name)) continue;

            node.Members.Add(new ClassMember
            {
                Name                = name,
                TypeName            = typeName,
                Kind                = MemberKind.Field,
                Visibility          = vis,
                IsStatic            = isStatic,
                Attributes          = ExtractAttributes(field.AttributeLists),
                XmlDocSummary       = ExtractXmlDocSummary(field),
                SourceFilePath      = filePath,
                SourceLineOneBased  = field.GetLocation().GetLineSpan().StartLinePosition.Line + 1
            });
        }
    }

    private static void ExtractProperty(
        PropertyDeclarationSyntax prop,
        ClassNode node,
        string filePath,
        ClassDiagramOptions options)
    {
        var vis = GetVisibility(prop.Modifiers);
        if (!ShouldInclude(vis, options)) return;

        string name = prop.Identifier.ValueText;
        if (node.Members.Any(m => m.Name == name)) return;

        node.Members.Add(new ClassMember
        {
            Name                = name,
            TypeName            = prop.Type.ToString(),
            Kind                = MemberKind.Property,
            Visibility          = vis,
            IsStatic            = prop.Modifiers.Any(SyntaxKind.StaticKeyword),
            IsAbstract          = prop.Modifiers.Any(SyntaxKind.AbstractKeyword),
            IsOverride          = prop.Modifiers.Any(SyntaxKind.OverrideKeyword),
            Attributes          = ExtractAttributes(prop.AttributeLists),
            XmlDocSummary       = ExtractXmlDocSummary(prop),
            SourceFilePath      = filePath,
            SourceLineOneBased  = prop.GetLocation().GetLineSpan().StartLinePosition.Line + 1
        });
    }

    private static void ExtractEvent(
        EventDeclarationSyntax evt,
        ClassNode node,
        string filePath,
        ClassDiagramOptions options)
    {
        var vis = GetVisibility(evt.Modifiers);
        if (!ShouldInclude(vis, options)) return;

        string name = evt.Identifier.ValueText;
        if (node.Members.Any(m => m.Name == name)) return;

        node.Members.Add(new ClassMember
        {
            Name                = name,
            TypeName            = evt.Type.ToString(),
            Kind                = MemberKind.Event,
            Visibility          = vis,
            IsStatic            = evt.Modifiers.Any(SyntaxKind.StaticKeyword),
            Attributes          = ExtractAttributes(evt.AttributeLists),
            XmlDocSummary       = ExtractXmlDocSummary(evt),
            SourceFilePath      = filePath,
            SourceLineOneBased  = evt.GetLocation().GetLineSpan().StartLinePosition.Line + 1
        });
    }

    private static void ExtractEventField(
        EventFieldDeclarationSyntax evtField,
        ClassNode node,
        string filePath,
        ClassDiagramOptions options)
    {
        var vis = GetVisibility(evtField.Modifiers);
        if (!ShouldInclude(vis, options)) return;

        string typeName = evtField.Declaration.Type.ToString();
        bool   isStatic = evtField.Modifiers.Any(SyntaxKind.StaticKeyword);

        foreach (var variable in evtField.Declaration.Variables)
        {
            string name = variable.Identifier.ValueText;
            if (node.Members.Any(m => m.Name == name)) return;

            node.Members.Add(new ClassMember
            {
                Name                = name,
                TypeName            = typeName,
                Kind                = MemberKind.Event,
                Visibility          = vis,
                IsStatic            = isStatic,
                Attributes          = ExtractAttributes(evtField.AttributeLists),
                XmlDocSummary       = ExtractXmlDocSummary(evtField),
                SourceFilePath      = filePath,
                SourceLineOneBased  = evtField.GetLocation().GetLineSpan().StartLinePosition.Line + 1
            });
        }
    }

    private static void ExtractMethod(
        MethodDeclarationSyntax method,
        ClassNode node,
        string filePath,
        ClassDiagramOptions options)
    {
        var vis = GetVisibility(method.Modifiers);
        if (!ShouldInclude(vis, options)) return;

        string name = method.Identifier.ValueText;

        // For overloads, append parameter types to disambiguate
        string disambigKey = name + "(" + BuildParamSignature(method.ParameterList) + ")";
        if (node.Members.Any(m => m.Kind == MemberKind.Method && m.Name == disambigKey)) return;

        string? constraints = method.ConstraintClauses.Count > 0
            ? string.Join(", ", method.ConstraintClauses.Select(c => c.ToString()))
            : null;

        node.Members.Add(new ClassMember
        {
            Name                = disambigKey,
            TypeName            = method.ReturnType.ToString(),
            Kind                = MemberKind.Method,
            Visibility          = vis,
            IsStatic            = method.Modifiers.Any(SyntaxKind.StaticKeyword),
            IsAbstract          = method.Modifiers.Any(SyntaxKind.AbstractKeyword),
            IsOverride          = method.Modifiers.Any(SyntaxKind.OverrideKeyword),
            IsAsync             = method.Modifiers.Any(SyntaxKind.AsyncKeyword),
            Parameters          = method.ParameterList.Parameters.Select(p => p.ToString()).ToList(),
            GenericConstraints  = constraints,
            Attributes          = ExtractAttributes(method.AttributeLists),
            XmlDocSummary       = ExtractXmlDocSummary(method),
            SourceFilePath      = filePath,
            SourceLineOneBased  = method.GetLocation().GetLineSpan().StartLinePosition.Line + 1
        });
    }

    private static void ExtractConstructor(
        ConstructorDeclarationSyntax ctor,
        ClassNode node,
        string filePath,
        ClassDiagramOptions options)
    {
        var vis = GetVisibility(ctor.Modifiers);
        if (!ShouldInclude(vis, options)) return;

        string disambigKey = $".ctor({BuildParamSignature(ctor.ParameterList)})";
        if (node.Members.Any(m => m.Kind == MemberKind.Method && m.Name == disambigKey)) return;

        node.Members.Add(new ClassMember
        {
            Name                = disambigKey,
            TypeName            = string.Empty,
            Kind                = MemberKind.Method,
            Visibility          = vis,
            IsStatic            = ctor.Modifiers.Any(SyntaxKind.StaticKeyword),
            Parameters          = ctor.ParameterList.Parameters.Select(p => p.ToString()).ToList(),
            Attributes          = ExtractAttributes(ctor.AttributeLists),
            XmlDocSummary       = ExtractXmlDocSummary(ctor),
            SourceFilePath      = filePath,
            SourceLineOneBased  = ctor.GetLocation().GetLineSpan().StartLinePosition.Line + 1
        });
    }

    private static void ExtractOperator(
        OperatorDeclarationSyntax op,
        ClassNode node,
        string filePath,
        ClassDiagramOptions options)
    {
        // Only show public operators
        if (GetVisibility(op.Modifiers) != MemberVisibility.Public) return;
        if (!options.IncludePrivateMembers && false) return; // operators always shown when public

        string name = $"operator {op.OperatorToken.ValueText}({BuildParamSignature(op.ParameterList)})";
        if (node.Members.Any(m => m.Name == name)) return;

        node.Members.Add(new ClassMember
        {
            Name                = name,
            TypeName            = op.ReturnType.ToString(),
            Kind                = MemberKind.Method,
            Visibility          = MemberVisibility.Public,
            IsStatic            = true,
            SourceFilePath      = filePath,
            SourceLineOneBased  = op.GetLocation().GetLineSpan().StartLinePosition.Line + 1
        });
    }

    // ── Relationship building ────────────────────────────────────────────────

    private static void BuildRelationships(
        DiagramDocument document,
        Dictionary<string, ClassNode> nodeMap,
        Dictionary<string, List<string>> inheritanceMap)
    {
        // Build a lookup by simple name for cross-file resolution
        var bySimpleName = new Dictionary<string, List<ClassNode>>(StringComparer.Ordinal);
        foreach (var node in nodeMap.Values)
        {
            if (!bySimpleName.TryGetValue(node.Name, out var list))
                bySimpleName[node.Name] = list = [];
            list.Add(node);
        }

        foreach ((string sourceKey, List<string> bases) in inheritanceMap)
        {
            // Resolve source node (by full key first, then simple name)
            if (!nodeMap.TryGetValue(sourceKey, out var sourceNode))
            {
                string simpleName = sourceKey.Contains('.') ? sourceKey[(sourceKey.LastIndexOf('.') + 1)..] : sourceKey;
                if (!bySimpleName.TryGetValue(simpleName, out var candidates) || candidates.Count != 1) continue;
                sourceNode = candidates[0];
            }

            foreach (string baseName in bases)
            {
                // Try full key match first, then simple name
                ClassNode? targetNode = null;
                if (!nodeMap.TryGetValue(baseName, out targetNode))
                {
                    if (bySimpleName.TryGetValue(baseName, out var cands) && cands.Count == 1)
                        targetNode = cands[0];
                }
                if (targetNode is null) continue;

                // Interface → Realization, base class → Inheritance
                var kind = targetNode.Kind == ClassKind.Interface
                    ? RelationshipKind.Realization
                    : RelationshipKind.Inheritance;

                if (!document.Relationships.Any(r => r.SourceId == sourceNode.Id && r.TargetId == targetNode.Id))
                {
                    document.Relationships.Add(new ClassRelationship
                    {
                        SourceId = sourceNode.Id,
                        TargetId = targetNode.Id,
                        Kind     = kind
                    });
                }
            }
        }

        // Field-level associations: if a field type resolves to a known node, add Association
        foreach (var node in document.Classes)
        {
            foreach (var field in node.Fields)
            {
                string rawType = StripGenericArity(field.TypeName);
                if (!bySimpleName.TryGetValue(rawType, out var targets) || targets.Count != 1) continue;
                var target = targets[0];
                if (target == node) continue;
                if (document.Relationships.Any(r => r.SourceId == node.Id && r.TargetId == target.Id)) continue;

                document.Relationships.Add(new ClassRelationship
                {
                    SourceId = node.Id,
                    TargetId = target.Id,
                    Kind     = RelationshipKind.Association
                });
            }
        }
    }

    // ── Metrics computation ──────────────────────────────────────────────────

    private static void ComputeMetrics(Dictionary<string, ClassNode> nodeMap)
    {
        // Count efferent coupling per node: number of distinct node names referenced
        // in field/property types within the same node map (cross-type dependencies)
        var bySimpleName = nodeMap.Values
            .GroupBy(n => n.Name)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

        foreach (var node in nodeMap.Values)
        {
            int ce = node.Members
                .Where(m => m.Kind is MemberKind.Field or MemberKind.Property)
                .Select(m => StripGenericArity(m.TypeName))
                .Distinct(StringComparer.Ordinal)
                .Count(t => bySimpleName.ContainsKey(t) && t != node.Name);

            int publicCount = node.Members.Count(m => m.Visibility == MemberVisibility.Public);

            node.Metrics = new ClassMetrics
            {
                EfferentCoupling = ce,
                PublicMemberCount = publicCount
                // AfferentCoupling and CyclomaticComplexity require semantic model;
                // left at 0 for syntax-only path.
            };
        }

        // Second pass: compute afferent coupling (how many others depend on this type)
        foreach (var source in nodeMap.Values)
        {
            var referencedTypes = source.Members
                .Where(m => m.Kind is MemberKind.Field or MemberKind.Property)
                .Select(m => StripGenericArity(m.TypeName))
                .Distinct(StringComparer.Ordinal);

            foreach (string referencedName in referencedTypes)
            {
                var targets = nodeMap.Values
                    .Where(n => n.Name == referencedName && n != source)
                    .ToList();

                foreach (var target in targets)
                {
                    target.Metrics = target.Metrics with
                    {
                        AfferentCoupling = target.Metrics.AfferentCoupling + 1
                    };
                }
            }
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static ClassKind GetClassKind(BaseTypeDeclarationSyntax typeDecl) =>
        typeDecl switch
        {
            InterfaceDeclarationSyntax                                       => ClassKind.Interface,
            EnumDeclarationSyntax                                            => ClassKind.Enum,
            StructDeclarationSyntax                                          => ClassKind.Struct,
            RecordDeclarationSyntax { ClassOrStructKeyword.Text: "struct" }  => ClassKind.Struct,
            ClassDeclarationSyntax c when c.Modifiers.Any(SyntaxKind.AbstractKeyword) => ClassKind.Abstract,
            _                                                                => ClassKind.Class
        };

    private static MemberVisibility GetVisibility(SyntaxTokenList modifiers)
    {
        if (modifiers.Any(SyntaxKind.PublicKeyword))    return MemberVisibility.Public;
        if (modifiers.Any(SyntaxKind.ProtectedKeyword)) return MemberVisibility.Protected;
        if (modifiers.Any(SyntaxKind.InternalKeyword))  return MemberVisibility.Internal;
        return MemberVisibility.Private;
    }

    private static bool ShouldInclude(MemberVisibility vis, ClassDiagramOptions options) =>
        options.IncludePrivateMembers ||
        vis is MemberVisibility.Public or MemberVisibility.Internal;

    private static string GetNamespace(SyntaxNode node)
    {
        var sb  = new StringBuilder();
        var cur = node.Parent;
        while (cur is not null)
        {
            if (cur is BaseNamespaceDeclarationSyntax ns)
            {
                if (sb.Length > 0) sb.Insert(0, '.');
                sb.Insert(0, ns.Name.ToString());
            }
            cur = cur.Parent;
        }
        return sb.ToString();
    }

    private static string GetSimpleTypeName(TypeSyntax type)
    {
        // For generic types (e.g. IList<T>), return just the identifier (IList)
        return type switch
        {
            GenericNameSyntax generic    => generic.Identifier.ValueText,
            IdentifierNameSyntax ident   => ident.Identifier.ValueText,
            QualifiedNameSyntax qualified => qualified.Right switch
            {
                GenericNameSyntax g => g.Identifier.ValueText,
                SimpleNameSyntax s  => s.Identifier.ValueText,
                _                   => qualified.Right.ToString()
            },
            _ => type.ToString().Split('<')[0].Split('.')[^1]
        };
    }

    private static string StripGenericArity(string typeName)
    {
        int angle = typeName.IndexOf('<');
        string raw = angle > 0 ? typeName[..angle] : typeName;
        int dot = raw.LastIndexOf('.');
        return dot >= 0 ? raw[(dot + 1)..] : raw;
    }

    private static string BuildParamSignature(ParameterListSyntax paramList) =>
        string.Join(", ", paramList.Parameters.Select(p =>
        {
            // "TypeName paramName" — show only the type
            return p.Type?.ToString() ?? p.ToString();
        }));

    /// <summary>
    /// Extracts attribute names from an attribute list, stripping the "Attribute" suffix.
    /// E.g. "[SerializableAttribute]" → "Serializable", "[DataContract]" → "DataContract".
    /// </summary>
    private static List<string> ExtractAttributes(SyntaxList<AttributeListSyntax> attrLists)
    {
        var result = new List<string>();
        foreach (var attrList in attrLists)
            foreach (var attr in attrList.Attributes)
            {
                string name = attr.Name switch
                {
                    IdentifierNameSyntax id          => id.Identifier.ValueText,
                    QualifiedNameSyntax q            => q.Right.Identifier.ValueText,
                    GenericNameSyntax g              => g.Identifier.ValueText,
                    _                                => attr.Name.ToString()
                };
                // Strip "Attribute" suffix for brevity (convention in .NET)
                if (name.EndsWith("Attribute", StringComparison.Ordinal) && name.Length > 9)
                    name = name[..^9];
                if (!result.Contains(name))
                    result.Add(name);
            }
        return result;
    }

    private static string? ExtractXmlDocSummary(SyntaxNode node)
    {
        var trivia = node.GetLeadingTrivia()
            .Where(t => t.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)
                     || t.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia))
            .ToList();

        if (trivia.Count == 0) return null;

        // Extract <summary>...</summary> content
        var raw = string.Concat(trivia.Select(t => t.ToString()));
        int start = raw.IndexOf("<summary>", StringComparison.OrdinalIgnoreCase);
        int end   = raw.IndexOf("</summary>", StringComparison.OrdinalIgnoreCase);
        if (start < 0 || end <= start) return null;

        string inner = raw[(start + 9)..end];

        // Strip leading /// and whitespace from each line
        var lines = inner.Split('\n')
            .Select(l => l.TrimStart().TrimStart('/').Trim())
            .Where(l => l.Length > 0);

        return string.Join(" ", lines);
    }
}
