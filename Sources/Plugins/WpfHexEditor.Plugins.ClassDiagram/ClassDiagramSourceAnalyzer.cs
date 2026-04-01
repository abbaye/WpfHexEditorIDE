// ==========================================================
// Project: WpfHexEditor.Plugins.ClassDiagram
// File: ClassDiagramSourceAnalyzer.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-19
// Description:
//     Lightweight regex-based source analyzer that extracts type declarations,
//     members and inheritance relationships from C# (.cs) and
//     VB.NET (.vb) source files — without any Roslyn dependency.
//
// Architecture Notes:
//     Pattern: Static Service + Strategy (language dispatch).
//     Public entry points delegate to language-specific pipelines:
//       C#    → ProcessCSharpSource  (brace-delimited bodies)
//       VB.NET → ProcessVbSource     (End-keyword-delimited bodies)
//     All regex patterns are compiled once (static readonly) to avoid
//     per-call recompilation overhead.
//     File language is detected from extension: .cs → C#, .vb → VB.NET.
//     Unknown extensions default to C# (best-effort).
//     IncludePrivateMembers + AutoLayout controlled by ClassDiagramOptions.
// ==========================================================

using System.IO;
using System.Text.RegularExpressions;
using WpfHexEditor.Editor.ClassDiagram.Core.Model;
using WpfHexEditor.Plugins.ClassDiagram.Options;

namespace WpfHexEditor.Plugins.ClassDiagram;

/// <summary>
/// Parses C# and VB.NET source files using regex and produces a
/// <see cref="DiagramDocument"/> without requiring Roslyn or any compiler.
/// </summary>
public static class ClassDiagramSourceAnalyzer
{
    // ── Language detection ────────────────────────────────────────────────────

    private enum SourceLanguage { CSharp, VisualBasic }

    private static SourceLanguage DetectLanguage(string filePath) =>
        Path.GetExtension(filePath).Equals(".vb", StringComparison.OrdinalIgnoreCase)
            ? SourceLanguage.VisualBasic
            : SourceLanguage.CSharp;

    // ══════════════════════════════════════════════════════════════════════════
    //  C# PATTERNS
    // ══════════════════════════════════════════════════════════════════════════

    private static readonly Regex CsTypeDeclaration = new(
        @"(?:^|\n)\s*" +
        @"(public|internal|private|protected)?\s*" +
        @"(abstract|sealed|static|partial|readonly)?\s*" +
        @"(partial\s+)?" +
        @"(class|interface|enum|struct|record\s+struct|record)\s+" +
        @"(\w+)" +
        @"(?:<[^>]+>)?" +
        @"(?:\s*:\s*([^{]+?))?(?:\s*\{|\s*$|\s*where\s)",
        RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.IgnoreCase);

    private static readonly Regex CsEventDeclaration = new(
        @"^\s*(?:public|private|protected|internal)\s+event\s+" +
        @"([\w][\w<>]*)\s+(\w+)\s*[;{]",
        RegexOptions.Compiled | RegexOptions.Multiline);

    private static readonly Regex CsPropertyDeclaration = new(
        @"^\s*(public|private|protected|internal)\s+" +
        @"(?:static\s+)?(?:virtual\s+|override\s+|abstract\s+|new\s+)?" +
        @"([\w][\w<>,?\[\]\s]*?)\s+(\w+)\s*(?:\{|=>)",
        RegexOptions.Compiled | RegexOptions.Multiline);

    private static readonly Regex CsMethodDeclaration = new(
        @"^\s*(public|private|protected|internal)\s+" +
        @"(?:static\s+)?(?:virtual\s+|override\s+|abstract\s+|async\s+|new\s+|extern\s+)?" +
        @"(?:async\s+)?([\w][\w<>,?\[\]\s]*?)\s+(\w+)\s*\(([^)]*)\)",
        RegexOptions.Compiled | RegexOptions.Multiline);

    private static readonly Regex CsFieldDeclaration = new(
        @"^\s*(public|private|protected|internal)\s+" +
        @"(?:static\s+)?(?:readonly\s+)?(?:volatile\s+)?" +
        @"([\w][\w<>,?\[\]]*?)\s+(\w+)\s*(?:=|;)",
        RegexOptions.Compiled | RegexOptions.Multiline);

    // ══════════════════════════════════════════════════════════════════════════
    //  VB.NET PATTERNS
    // ══════════════════════════════════════════════════════════════════════════

    // Matches: [Public|Friend|...] [MustInherit|NotInheritable|Partial] [Class|Interface|Enum|Structure|Module] Name
    private static readonly Regex VbTypeDeclaration = new(
        @"^\s*(Public|Friend|Private|Protected)?\s*" +
        @"(MustInherit|NotInheritable|Partial|Shared|Static)?\s*" +
        @"(Partial\s+)?" +
        @"(Class|Interface|Enum|Structure|Module)\s+(\w+)",
        RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.IgnoreCase);

    // Matches: Inherits BaseClass
    private static readonly Regex VbInherits = new(
        @"^\s*Inherits\s+(\w[\w.]*)",
        RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.IgnoreCase);

    // Matches: Implements IFoo, IBar
    private static readonly Regex VbImplements = new(
        @"^\s*Implements\s+([\w,.\s]+)",
        RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.IgnoreCase);

    // Matches: [Public|...] Event Name As EventHandlerType
    private static readonly Regex VbEventDeclaration = new(
        @"^\s*(Public|Private|Protected|Friend)\s+Event\s+(\w+)\s+As\s+([\w<>]+)",
        RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.IgnoreCase);

    // Matches: [Public|...] [ReadOnly|...] Property Name As Type
    private static readonly Regex VbPropertyDeclaration = new(
        @"^\s*(Public|Private|Protected|Friend)\s+" +
        @"(?:Shared\s+)?(?:ReadOnly\s+|WriteOnly\s+)?(?:Overridable\s+|Overrides\s+|MustOverride\s+|NotOverridable\s+)?" +
        @"Property\s+(\w+)(?:\([^)]*\))?\s+As\s+([\w<>\[\]]+)",
        RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.IgnoreCase);

    // Matches: [Public|...] Function Name([params]) As ReturnType
    private static readonly Regex VbFunctionDeclaration = new(
        @"^\s*(Public|Private|Protected|Friend)\s+" +
        @"(?:Shared\s+)?(?:Overridable\s+|Overrides\s+|MustOverride\s+|NotOverridable\s+)?(?:Async\s+)?" +
        @"Function\s+(\w+)\s*\(([^)]*)\)\s+As\s+([\w<>\[\]]+)",
        RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.IgnoreCase);

    // Matches: [Public|...] Sub Name([params])
    private static readonly Regex VbSubDeclaration = new(
        @"^\s*(Public|Private|Protected|Friend)\s+" +
        @"(?:Shared\s+)?(?:Overridable\s+|Overrides\s+|MustOverride\s+|NotOverridable\s+)?(?:Async\s+)?" +
        @"Sub\s+(\w+)\s*\(([^)]*)\)",
        RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.IgnoreCase);

    // Matches: [Private|...] [ReadOnly|Shared] _name As Type [= value]
    private static readonly Regex VbFieldDeclaration = new(
        @"^\s*(Public|Private|Protected|Friend)\s+" +
        @"(?:Shared\s+)?(?:ReadOnly\s+)?" +
        @"(\w+)\s+As\s+([\w<>\[\]]+)",
        RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.IgnoreCase);

    // End-keyword patterns for body boundary detection
    private static readonly Regex VbEndKeyword = new(
        @"^\s*End\s+(Class|Interface|Enum|Structure|Module)\b",
        RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.IgnoreCase);

    // Shared base-type token splitter
    private static readonly Regex BaseTypeToken = new(
        @"[\w][\w.<>[\],\s]*",
        RegexOptions.Compiled);

    // ── Public entry points ───────────────────────────────────────────────────

    /// <summary>
    /// Analyzes one or more source files (.cs and/or .vb) and builds
    /// a <see cref="DiagramDocument"/> with all discovered types and relationships.
    /// </summary>
    public static DiagramDocument AnalyzeFiles(
        IEnumerable<string> filePaths,
        ClassDiagramOptions? options = null)
    {
        options ??= new ClassDiagramOptions();

        var document       = new DiagramDocument();
        var allNodes       = new Dictionary<string, ClassNode>(StringComparer.Ordinal);
        var inheritanceMap = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        foreach (string path in filePaths)
        {
            if (!File.Exists(path)) continue;

            string source;
            try { source = File.ReadAllText(path); }
            catch { continue; }

            var lang = DetectLanguage(path);

            if (lang == SourceLanguage.VisualBasic)
                ProcessVbSource(source, options, allNodes, inheritanceMap);
            else
                ProcessCSharpSource(source, options, allNodes, inheritanceMap);
        }

        foreach (var node in allNodes.Values)
            document.Classes.Add(node);

        BuildRelationships(document, allNodes, inheritanceMap);

        if (options.AutoLayout)
            ApplyGridAutoLayout(document.Classes, options);

        return document;
    }

    /// <summary>
    /// Convenience overload for a single file (.cs or .vb).
    /// </summary>
    public static DiagramDocument AnalyzeFile(
        string filePath,
        ClassDiagramOptions? options = null)
        => AnalyzeFiles([filePath], options);

    // ── C# pipeline ──────────────────────────────────────────────────────────

    private static void ProcessCSharpSource(
        string source,
        ClassDiagramOptions options,
        Dictionary<string, ClassNode> allNodes,
        Dictionary<string, List<string>> inheritanceMap)
    {
        foreach (Match typeMatch in CsTypeDeclaration.Matches(source))
        {
            string keyword  = typeMatch.Groups[4].Value.Trim().ToLowerInvariant();
            string name     = typeMatch.Groups[5].Value.Trim();
            string modifier = typeMatch.Groups[2].Value.Trim().ToLowerInvariant();
            string baseList = typeMatch.Groups[6].Value.Trim();

            if (string.IsNullOrEmpty(name) || allNodes.ContainsKey(name)) continue;

            ClassKind kind = keyword switch
            {
                "interface"      => ClassKind.Interface,
                "enum"           => ClassKind.Enum,
                "struct"         => ClassKind.Struct,
                "record struct"  => ClassKind.Struct,
                "record"         => ClassKind.Class,
                _                => modifier == "abstract" ? ClassKind.Abstract : ClassKind.Class
            };

            var node = ClassNode.Create(name, kind);
            node.IsAbstract = modifier == "abstract";
            node.Width  = options.DefaultNodeWidth;
            node.Height = options.DefaultNodeHeight;

            int bodyStart     = typeMatch.Index + typeMatch.Length;
            string bodySnippet = ExtractCsBodySnippet(source, bodyStart);
            ExtractCsMembers(bodySnippet, node, options);

            allNodes[name] = node;

            if (!string.IsNullOrEmpty(baseList))
                inheritanceMap[name] = ParseBaseList(baseList);
        }
    }

    private static string ExtractCsBodySnippet(string source, int startIndex)
    {
        int bracePos = source.IndexOf('{', startIndex);
        if (bracePos < 0) return string.Empty;

        int depth = 1;
        int pos   = bracePos + 1;
        int limit = Math.Min(pos + 8_000, source.Length);

        while (pos < limit && depth > 0)
        {
            char c = source[pos++];
            if (c == '{') depth++;
            else if (c == '}') depth--;
        }

        return source.Substring(bracePos + 1, pos - bracePos - 2);
    }

    private static void ExtractCsMembers(string body, ClassNode node, ClassDiagramOptions options)
    {
        // Events first to avoid false-positive method matches
        foreach (Match m in CsEventDeclaration.Matches(body))
        {
            if (string.IsNullOrEmpty(m.Groups[2].Value)) continue;
            node.Members.Add(new ClassMember
            {
                Name       = m.Groups[2].Value.Trim(),
                TypeName   = m.Groups[1].Value.Trim(),
                Kind       = MemberKind.Event,
                Visibility = MemberVisibility.Public
            });
        }

        foreach (Match m in CsPropertyDeclaration.Matches(body))
        {
            var vis = ParseVisibilityCSharp(m.Groups[1].Value);
            if (!options.IncludePrivateMembers
                && vis is MemberVisibility.Private or MemberVisibility.Protected) continue;

            string typeName = m.Groups[2].Value.Trim();
            string propName = m.Groups[3].Value.Trim();
            if (string.IsNullOrEmpty(propName) || typeName == node.Name) continue;
            if (node.Members.Any(x => x.Name == propName && x.Kind == MemberKind.Event)) continue;

            node.Members.Add(new ClassMember { Name = propName, TypeName = typeName,
                Kind = MemberKind.Property, Visibility = vis });
        }

        foreach (Match m in CsMethodDeclaration.Matches(body))
        {
            var vis = ParseVisibilityCSharp(m.Groups[1].Value);
            if (!options.IncludePrivateMembers
                && vis is MemberVisibility.Private or MemberVisibility.Protected) continue;

            string returnType = m.Groups[2].Value.Trim();
            string methodName = m.Groups[3].Value.Trim();
            if (string.IsNullOrEmpty(methodName)) continue;
            if (node.Members.Any(x => x.Name == methodName && x.Kind == MemberKind.Property)) continue;

            node.Members.Add(new ClassMember
            {
                Name       = methodName,
                TypeName   = returnType,
                Kind       = MemberKind.Method,
                Visibility = vis,
                Parameters = ParseCsParameterList(m.Groups[4].Value.Trim())
            });
        }

        foreach (Match m in CsFieldDeclaration.Matches(body))
        {
            var vis = ParseVisibilityCSharp(m.Groups[1].Value);
            if (!options.IncludePrivateMembers
                && vis is MemberVisibility.Private or MemberVisibility.Protected) continue;

            string fieldName = m.Groups[3].Value.Trim();
            if (string.IsNullOrEmpty(fieldName) || node.Members.Any(x => x.Name == fieldName)) continue;

            node.Members.Add(new ClassMember { Name = fieldName, TypeName = m.Groups[2].Value.Trim(),
                Kind = MemberKind.Field, Visibility = vis });
        }
    }

    // ── VB.NET pipeline ───────────────────────────────────────────────────────

    private static void ProcessVbSource(
        string source,
        ClassDiagramOptions options,
        Dictionary<string, ClassNode> allNodes,
        Dictionary<string, List<string>> inheritanceMap)
    {
        var lines = source.Split('\n');

        for (int i = 0; i < lines.Length; i++)
        {
            var typeMatch = VbTypeDeclaration.Match(lines[i]);
            if (!typeMatch.Success) continue;

            string keyword  = typeMatch.Groups[4].Value.Trim().ToLowerInvariant();
            string name     = typeMatch.Groups[5].Value.Trim();
            string modifier = typeMatch.Groups[2].Value.Trim().ToLowerInvariant();

            if (string.IsNullOrEmpty(name) || allNodes.ContainsKey(name)) continue;

            ClassKind kind = keyword switch
            {
                "interface" => ClassKind.Interface,
                "enum"      => ClassKind.Enum,
                "structure" => ClassKind.Struct,
                "module"    => ClassKind.Class,    // Module → Class in diagram
                _           => modifier == "mustinherit" ? ClassKind.Abstract : ClassKind.Class
            };

            var node = ClassNode.Create(name, kind);
            node.IsAbstract = modifier == "mustinherit";
            node.Width  = options.DefaultNodeWidth;
            node.Height = options.DefaultNodeHeight;

            // Collect the body lines until matching End <keyword>
            string endKeyword = keyword == "structure" ? "Structure"
                              : keyword == "module"    ? "Module"
                              : char.ToUpper(keyword[0]) + keyword[1..];

            int bodyStart = i + 1;
            int bodyEnd   = FindVbEndLine(lines, bodyStart, endKeyword);
            string bodyText = string.Join("\n", lines[bodyStart..bodyEnd]);

            ExtractVbMembers(bodyText, node, options);

            // Inherits / Implements inside the body
            var bases = new List<string>();
            foreach (Match inh in VbInherits.Matches(bodyText))
                bases.Add(inh.Groups[1].Value.Trim());
            foreach (Match impl in VbImplements.Matches(bodyText))
                foreach (string iface in impl.Groups[1].Value.Split(',', StringSplitOptions.RemoveEmptyEntries))
                    bases.Add(iface.Trim().Split('.')[^1]); // strip namespace prefix

            allNodes[name] = node;
            if (bases.Count > 0)
                inheritanceMap[name] = bases;

            i = bodyEnd; // skip the processed body
        }
    }

    /// <summary>
    /// Returns the 0-based index of the "End &lt;keyword&gt;" line or the last line
    /// if not found (handles malformed files gracefully).
    /// </summary>
    private static int FindVbEndLine(string[] lines, int startIndex, string keyword)
    {
        var pattern = new Regex(
            @"^\s*End\s+" + Regex.Escape(keyword) + @"\b",
            RegexOptions.IgnoreCase);

        for (int i = startIndex; i < lines.Length; i++)
            if (pattern.IsMatch(lines[i]))
                return i;

        return lines.Length - 1;
    }

    private static void ExtractVbMembers(string body, ClassNode node, ClassDiagramOptions options)
    {
        // Events
        foreach (Match m in VbEventDeclaration.Matches(body))
        {
            var vis = ParseVisibilityVb(m.Groups[1].Value);
            if (!options.IncludePrivateMembers
                && vis is MemberVisibility.Private or MemberVisibility.Protected) continue;

            node.Members.Add(new ClassMember
            {
                Name       = m.Groups[2].Value.Trim(),
                TypeName   = m.Groups[3].Value.Trim(),
                Kind       = MemberKind.Event,
                Visibility = vis
            });
        }

        // Properties
        foreach (Match m in VbPropertyDeclaration.Matches(body))
        {
            var vis = ParseVisibilityVb(m.Groups[1].Value);
            if (!options.IncludePrivateMembers
                && vis is MemberVisibility.Private or MemberVisibility.Protected) continue;

            string propName = m.Groups[2].Value.Trim();
            string typeName = m.Groups[3].Value.Trim();
            if (string.IsNullOrEmpty(propName)) continue;
            if (node.Members.Any(x => x.Name == propName && x.Kind == MemberKind.Event)) continue;

            node.Members.Add(new ClassMember { Name = propName, TypeName = typeName,
                Kind = MemberKind.Property, Visibility = vis });
        }

        // Functions (return a value → Method with return type)
        foreach (Match m in VbFunctionDeclaration.Matches(body))
        {
            var vis = ParseVisibilityVb(m.Groups[1].Value);
            if (!options.IncludePrivateMembers
                && vis is MemberVisibility.Private or MemberVisibility.Protected) continue;

            string methodName  = m.Groups[2].Value.Trim();
            string returnType  = m.Groups[4].Value.Trim();
            if (string.IsNullOrEmpty(methodName)) continue;
            if (node.Members.Any(x => x.Name == methodName && x.Kind == MemberKind.Property)) continue;

            node.Members.Add(new ClassMember
            {
                Name       = methodName + "()",
                TypeName   = returnType,
                Kind       = MemberKind.Method,
                Visibility = vis,
                Parameters = ParseVbParameterList(m.Groups[3].Value.Trim())
            });
        }

        // Subs (no return value → Method with void / "")
        foreach (Match m in VbSubDeclaration.Matches(body))
        {
            var vis = ParseVisibilityVb(m.Groups[1].Value);
            if (!options.IncludePrivateMembers
                && vis is MemberVisibility.Private or MemberVisibility.Protected) continue;

            string methodName = m.Groups[2].Value.Trim();
            if (string.IsNullOrEmpty(methodName)) continue;
            if (node.Members.Any(x => x.Name == methodName + "()")) continue;

            // Skip property getters/setters emitted by the compiler (Get/Set/Init)
            if (methodName is "Get" or "Set" or "Init") continue;

            node.Members.Add(new ClassMember
            {
                Name       = methodName + "()",
                TypeName   = "",
                Kind       = MemberKind.Method,
                Visibility = vis,
                Parameters = ParseVbParameterList(m.Groups[3].Value.Trim())
            });
        }

        // Fields (last — lowest priority)
        foreach (Match m in VbFieldDeclaration.Matches(body))
        {
            // Skip lines that matched a Property declaration already
            if (body[m.Index..].TrimStart().StartsWith("Property ", StringComparison.OrdinalIgnoreCase))
                continue;

            var vis = ParseVisibilityVb(m.Groups[1].Value);
            if (!options.IncludePrivateMembers
                && vis is MemberVisibility.Private or MemberVisibility.Protected) continue;

            string fieldName = m.Groups[2].Value.Trim();
            string typeName  = m.Groups[3].Value.Trim();
            if (string.IsNullOrEmpty(fieldName)) continue;
            if (node.Members.Any(x => x.Name == fieldName)) continue;

            // Skip keywords that alias to built-in constructs
            if (fieldName is "Function" or "Sub" or "Property" or "Event" or "Class"
                          or "Interface" or "Enum" or "Structure" or "Module")
                continue;

            node.Members.Add(new ClassMember { Name = fieldName, TypeName = typeName,
                Kind = MemberKind.Field, Visibility = vis });
        }
    }

    // ── Shared helpers ────────────────────────────────────────────────────────

    private static void BuildRelationships(
        DiagramDocument document,
        Dictionary<string, ClassNode> allNodes,
        Dictionary<string, List<string>> inheritanceMap)
    {
        foreach ((string sourceName, List<string> bases) in inheritanceMap)
        {
            if (!allNodes.TryGetValue(sourceName, out var sourceNode)) continue;

            foreach (string baseName in bases)
            {
                if (!allNodes.TryGetValue(baseName, out var targetNode)) continue;

                document.Relationships.Add(new ClassRelationship
                {
                    SourceId = sourceNode.Id,
                    TargetId = targetNode.Id,
                    Kind     = RelationshipKind.Inheritance
                });
            }
        }
    }

    private static void ApplyGridAutoLayout(List<ClassNode> nodes, ClassDiagramOptions options)
    {
        if (nodes.Count == 0) return;
        const double colGap = 40.0, rowGap = 40.0;
        int cols = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(nodes.Count)));

        var ordered = nodes.OrderBy(n => n.Kind).ThenBy(n => n.Name).ToList();
        for (int i = 0; i < ordered.Count; i++)
        {
            ordered[i].X = (i % cols) * (options.DefaultNodeWidth  + colGap) + 20;
            ordered[i].Y = (i / cols) * (options.DefaultNodeHeight + rowGap)  + 20;
        }
    }

    private static List<string> ParseBaseList(string rawBaseList)
    {
        var result = new List<string>();
        foreach (Match m in BaseTypeToken.Matches(rawBaseList))
        {
            string token = m.Value.Trim();
            int anglePos = token.IndexOf('<');
            if (anglePos > 0) token = token[..anglePos].Trim();
            if (!string.IsNullOrEmpty(token))
                result.Add(token);
        }
        return result;
    }

    private static MemberVisibility ParseVisibilityCSharp(string raw) =>
        raw.Trim().ToLowerInvariant() switch
        {
            "public"    => MemberVisibility.Public,
            "protected" => MemberVisibility.Protected,
            "internal"  => MemberVisibility.Internal,
            _           => MemberVisibility.Private
        };

    private static MemberVisibility ParseVisibilityVb(string raw) =>
        raw.Trim().ToLowerInvariant() switch
        {
            "public"    => MemberVisibility.Public,
            "protected" => MemberVisibility.Protected,
            "friend"    => MemberVisibility.Internal,   // Friend ≡ Internal
            _           => MemberVisibility.Private
        };

    private static List<string> ParseCsParameterList(string rawParams)
    {
        if (string.IsNullOrWhiteSpace(rawParams)) return [];
        var result = new List<string>();
        foreach (string part in rawParams.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            string t = part.Trim();
            int eq = t.IndexOf('=');
            if (eq > 0) t = t[..eq].Trim();
            int sp = t.LastIndexOf(' ');
            result.Add(sp > 0 ? t[..sp].Trim() : t);
        }
        return result;
    }

    /// <summary>
    /// Parses a VB.NET parameter list (each param: "name As Type [= default]").
    /// Returns a list of type names.
    /// </summary>
    private static List<string> ParseVbParameterList(string rawParams)
    {
        if (string.IsNullOrWhiteSpace(rawParams)) return [];
        var result = new List<string>();
        foreach (string part in rawParams.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            // "ByVal name As Type" or "name As Type = default"
            var asMatch = Regex.Match(part, @"\bAs\s+([\w<>\[\]]+)", RegexOptions.IgnoreCase);
            result.Add(asMatch.Success ? asMatch.Groups[1].Value.Trim() : part.Trim());
        }
        return result;
    }
}
