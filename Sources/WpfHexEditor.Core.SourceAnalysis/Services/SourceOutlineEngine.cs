// ==========================================================
// Project: WpfHexEditor.Core.SourceAnalysis
// File: Services/SourceOutlineEngine.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude (Anthropic)
// Created: 2026-03-16
// Description:
//     BCL-only regex line-scanner that extracts type and member declarations
//     from .cs and .xaml source files. No Roslyn dependency.
//     Results are cached by (absolute path, last-modified UTC).
//
// Architecture Notes:
//     Pattern: Service with internal ConcurrentDictionary cache.
//     Parser is heuristic — multiline declarations and string-literal braces
//     can cause minor inaccuracies, which is acceptable for a VS-Like outline view.
//     Thread-safe: GetOutlineAsync uses ConfigureAwait(false) and cache is concurrent.
// ==========================================================

using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using WpfHexEditor.Core.SourceAnalysis.Models;

namespace WpfHexEditor.Core.SourceAnalysis.Services;

/// <summary>
/// BCL-only implementation of <see cref="ISourceOutlineService"/>.
/// Parses .cs and .xaml files using pre-compiled regexes and a brace-depth line scanner.
/// Results are cached per (file path, last-write-time).
/// </summary>
public sealed class SourceOutlineEngine : ISourceOutlineService
{
    // -----------------------------------------------------------------------
    // Cache
    // -----------------------------------------------------------------------

    private sealed record CacheEntry(DateTime LastWrite, SourceOutlineModel Model);

    private readonly ConcurrentDictionary<string, CacheEntry> _cache =
        new(StringComparer.OrdinalIgnoreCase);

    // -----------------------------------------------------------------------
    // Pre-compiled regexes
    // -----------------------------------------------------------------------

    // Matches a type declaration keyword preceded by optional modifiers.
    // Named groups: keyword (class|struct|interface|enum|record), name (\w+)
    private static readonly Regex TypeRegex = new(
        @"(?:(?:public|internal|private|protected|file)\s+)?" +
        @"(?:(?:abstract|sealed|static|partial|readonly|unsafe)\s+){0,3}" +
        @"(?<keyword>class|struct|interface|enum|record)\s+(?<name>\w+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // Matches "record struct" as a combined keyword for RecordStruct kind.
    private static readonly Regex RecordStructRegex = new(
        @"record\s+struct\s+(?<name>\w+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // Matches a member declaration: [modifiers] returnType name [(|{|;|=>]
    // Named groups: returnType, name
    private static readonly Regex MemberRegex = new(
        @"^\s*(?:(?:public|private|protected|internal|static|virtual|override|abstract|" +
        @"async|sealed|new|extern|unsafe|readonly|volatile|partial)\s+){1,6}" +
        @"(?<returnType>[\w<>\[\]?,\.\s\*]+?)\s+(?<name>\w+)\s*(?<sig>[({;=<])",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // Constructor: access modifier + TypeName + (
    private static readonly Regex CtorRegex = new(
        @"^\s*(?:public|private|protected|internal)\s+(?<name>[A-Z]\w*)\s*\(",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // Property (auto or expression): [modifiers] type Name { or =>
    private static readonly Regex PropertyRegex = new(
        @"^\s*(?:(?:public|private|protected|internal|static|virtual|override|abstract|" +
        @"sealed|new|readonly)\s+){1,5}" +
        @"(?<returnType>[\w<>\[\]?,\.\s]+?)\s+(?<name>\w+)\s*(?:\{|=>)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // Event field: event Type Name
    private static readonly Regex EventRegex = new(
        @"^\s*(?:(?:public|private|protected|internal|static|virtual|override|abstract|sealed)\s+){0,4}" +
        @"event\s+(?<returnType>[\w<>\[\]?,\.\s]+?)\s+(?<name>\w+)\s*[;{]",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // XAML x:Class attribute
    private static readonly Regex XamlClassRegex = new(
        @"x:Class=""(?<cls>[^""]+)""",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // XAML x:Name attribute with element tag hint
    private static readonly Regex XamlNameRegex = new(
        @"x:Name=""(?<name>\w+)""",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // Extracts XML element tag name from the start of a line, e.g. "<Button" → "Button"
    private static readonly Regex XamlTagRegex = new(
        @"<(?<tag>[A-Za-z][\w.]*)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // -----------------------------------------------------------------------
    // ISourceOutlineService
    // -----------------------------------------------------------------------

    /// <inheritdoc/>
    public bool CanOutline(string filePath)
    {
        var ext = Path.GetExtension(filePath);
        return string.Equals(ext, ".cs",   StringComparison.OrdinalIgnoreCase)
            || string.Equals(ext, ".xaml", StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc/>
    public async Task<SourceOutlineModel?> GetOutlineAsync(
        string filePath, CancellationToken ct = default)
    {
        if (!CanOutline(filePath)) return null;

        // Check cache first (no I/O for stat — just use cached value if freshness is unknown)
        // We do a lightweight stat before reading the full file.
        DateTime lastWrite;
        try { lastWrite = File.GetLastWriteTimeUtc(filePath); }
        catch { return null; }

        if (_cache.TryGetValue(filePath, out var entry) && entry.LastWrite == lastWrite)
            return entry.Model;

        // Parse on background thread
        return await Task.Run(() => ParseAndCache(filePath, lastWrite), ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public void Invalidate(string filePath) => _cache.TryRemove(filePath, out _);

    // -----------------------------------------------------------------------
    // Internal parse + cache
    // -----------------------------------------------------------------------

    private SourceOutlineModel ParseAndCache(string filePath, DateTime lastWrite)
    {
        SourceOutlineModel model;
        try
        {
            var lines = File.ReadAllLines(filePath);
            var ext   = Path.GetExtension(filePath);

            model = string.Equals(ext, ".xaml", StringComparison.OrdinalIgnoreCase)
                ? ParseXaml(filePath, lines)
                : ParseCSharp(filePath, lines);
        }
        catch
        {
            model = new SourceOutlineModel
            {
                FilePath = filePath,
                Kind     = SourceFileKind.CSharp,
                ParsedAt = lastWrite,
            };
        }

        _cache[filePath] = new CacheEntry(lastWrite, model);
        return model;
    }

    // -----------------------------------------------------------------------
    // C# parser
    // -----------------------------------------------------------------------

    private static SourceOutlineModel ParseCSharp(string filePath, string[] lines)
    {
        var topTypes   = new List<SourceTypeModel>();
        var typeStack  = new Stack<TypeBuilder>();
        int braceDepth = 0;

        for (int i = 0; i < lines.Length; i++)
        {
            var line    = lines[i];
            var trimmed = line.TrimStart();
            int lineNum = i + 1;

            // Skip full-line comments and preprocessor directives
            if (trimmed.StartsWith("//", StringComparison.Ordinal) ||
                trimmed.StartsWith("#",  StringComparison.Ordinal))
                continue;

            // Count braces — simplified (ignores string literals, but acceptable heuristic)
            int open  = CountChar(line, '{');
            int close = CountChar(line, '}');

            // Record struct (must be checked before TypeRegex which would match "record")
            var rsMatch = RecordStructRegex.Match(trimmed);
            if (rsMatch.Success)
            {
                var builder = new TypeBuilder(
                    rsMatch.Groups["name"].Value,
                    SourceTypeKind.RecordStruct,
                    lineNum,
                    HasModifier(trimmed, "public"),
                    HasModifier(trimmed, "abstract"),
                    HasModifier(trimmed, "static"),
                    braceDepth + open);
                typeStack.Push(builder);
            }
            else
            {
                var typeMatch = TypeRegex.Match(trimmed);
                if (typeMatch.Success)
                {
                    var keyword = typeMatch.Groups["keyword"].Value;
                    var kind    = keyword switch
                    {
                        "class"     => SourceTypeKind.Class,
                        "struct"    => SourceTypeKind.Struct,
                        "interface" => SourceTypeKind.Interface,
                        "enum"      => SourceTypeKind.Enum,
                        "record"    => SourceTypeKind.Record,
                        _           => SourceTypeKind.Class
                    };

                    var builder = new TypeBuilder(
                        typeMatch.Groups["name"].Value,
                        kind,
                        lineNum,
                        HasModifier(trimmed, "public"),
                        HasModifier(trimmed, "abstract"),
                        HasModifier(trimmed, "static"),
                        braceDepth + open);
                    typeStack.Push(builder);
                }
                else if (typeStack.Count > 0)
                {
                    // Try to detect members inside the current type
                    var member = TryParseMember(trimmed, lineNum);
                    if (member is not null)
                        typeStack.Peek().Members.Add(member);
                }
            }

            // Update brace depth
            braceDepth += open - close;

            // Pop completed types
            while (typeStack.Count > 0 && braceDepth < typeStack.Peek().OpenBraceDepth)
            {
                var finished = typeStack.Pop().Build();
                if (typeStack.Count == 0)
                    topTypes.Add(finished);
                else
                    typeStack.Peek().NestedTypes.Add(finished);
            }
        }

        // Flush any remaining types (e.g. file-scoped namespaces without closing brace)
        while (typeStack.Count > 0)
        {
            var finished = typeStack.Pop().Build();
            topTypes.Add(finished);
        }

        return new SourceOutlineModel
        {
            FilePath = filePath,
            Kind     = SourceFileKind.CSharp,
            Types    = topTypes,
            ParsedAt = DateTime.UtcNow,
        };
    }

    private static SourceMemberModel? TryParseMember(string trimmed, int lineNum)
    {
        // Skip blank, comment, attribute, using, namespace lines
        if (string.IsNullOrWhiteSpace(trimmed))          return null;
        if (trimmed.StartsWith("//", StringComparison.Ordinal))  return null;
        if (trimmed.StartsWith("[",  StringComparison.Ordinal))  return null;
        if (trimmed.StartsWith("using", StringComparison.Ordinal)) return null;
        if (trimmed.StartsWith("namespace", StringComparison.Ordinal)) return null;

        // Event field (highest priority — before general member)
        var evtMatch = EventRegex.Match(trimmed);
        if (evtMatch.Success)
        {
            return new SourceMemberModel
            {
                Name       = evtMatch.Groups["name"].Value,
                ReturnType = evtMatch.Groups["returnType"].Value.Trim(),
                Kind       = SourceMemberKind.Event,
                LineNumber = lineNum,
                IsPublic   = HasModifier(trimmed, "public"),
                IsStatic   = HasModifier(trimmed, "static"),
            };
        }

        // Constructor (no return type — must come before MemberRegex)
        var ctorMatch = CtorRegex.Match(trimmed);
        if (ctorMatch.Success)
        {
            var name = ctorMatch.Groups["name"].Value;
            // Heuristic: constructor names start with uppercase and match the class pattern
            return new SourceMemberModel
            {
                Name       = name,
                ReturnType = string.Empty,
                Kind       = SourceMemberKind.Constructor,
                LineNumber = lineNum,
                IsPublic   = HasModifier(trimmed, "public"),
                IsStatic   = false,
            };
        }

        // Property (detected by trailing { or =>)
        var propMatch = PropertyRegex.Match(trimmed);
        if (propMatch.Success)
        {
            var name = propMatch.Groups["name"].Value;
            if (!IsKeyword(name))
                return new SourceMemberModel
                {
                    Name       = name,
                    ReturnType = propMatch.Groups["returnType"].Value.Trim(),
                    Kind       = SourceMemberKind.Property,
                    LineNumber = lineNum,
                    IsPublic   = HasModifier(trimmed, "public"),
                    IsStatic   = HasModifier(trimmed, "static"),
                    IsOverride = HasModifier(trimmed, "override"),
                };
        }

        // General method or field
        var m = MemberRegex.Match(trimmed);
        if (m.Success)
        {
            var name = m.Groups["name"].Value;
            if (IsKeyword(name)) return null;

            var sig  = m.Groups["sig"].Value;
            var kind = sig == "(" ? SourceMemberKind.Method : SourceMemberKind.Field;

            return new SourceMemberModel
            {
                Name       = name,
                ReturnType = m.Groups["returnType"].Value.Trim(),
                Kind       = kind,
                LineNumber = lineNum,
                IsPublic   = HasModifier(trimmed, "public"),
                IsStatic   = HasModifier(trimmed, "static"),
                IsOverride = HasModifier(trimmed, "override"),
                IsAsync    = HasModifier(trimmed, "async"),
            };
        }

        return null;
    }

    // -----------------------------------------------------------------------
    // XAML parser
    // -----------------------------------------------------------------------

    private static SourceOutlineModel ParseXaml(string filePath, string[] lines)
    {
        string? xClass   = null;
        var elements     = new List<XamlNamedElement>();

        for (int i = 0; i < lines.Length; i++)
        {
            var line    = lines[i];
            int lineNum = i + 1;

            // Extract x:Class from first 50 lines only
            if (xClass is null && lineNum <= 50)
            {
                var clsMatch = XamlClassRegex.Match(line);
                if (clsMatch.Success)
                    xClass = clsMatch.Groups["cls"].Value;
            }

            // Extract x:Name
            var nameMatch = XamlNameRegex.Match(line);
            if (nameMatch.Success)
            {
                var name     = nameMatch.Groups["name"].Value;
                var typeHint = string.Empty;

                var tagMatch = XamlTagRegex.Match(line);
                if (tagMatch.Success)
                    typeHint = tagMatch.Groups["tag"].Value;

                elements.Add(new XamlNamedElement
                {
                    Name     = name,
                    TypeHint = typeHint,
                    LineNumber = lineNum,
                });
            }
        }

        return new SourceOutlineModel
        {
            FilePath     = filePath,
            Kind         = SourceFileKind.Xaml,
            XamlClass    = xClass,
            XamlElements = elements,
            ParsedAt     = DateTime.UtcNow,
        };
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static bool HasModifier(string line, string modifier)
        => line.Contains(modifier + " ", StringComparison.Ordinal)
        || line.Contains(modifier + "\t", StringComparison.Ordinal);

    private static int CountChar(string s, char c)
    {
        int count = 0;
        foreach (var ch in s) if (ch == c) count++;
        return count;
    }

    private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
    {
        "if", "else", "for", "foreach", "while", "do", "switch", "case",
        "try", "catch", "finally", "return", "throw", "new", "var",
        "class", "struct", "interface", "enum", "record", "using", "namespace",
        "base", "this", "null", "true", "false", "default", "typeof", "sizeof",
        "checked", "unchecked", "lock", "fixed", "stackalloc", "await", "yield",
    };

    private static bool IsKeyword(string name) => Keywords.Contains(name);

    // -----------------------------------------------------------------------
    // TypeBuilder (mutable accumulator during parse)
    // -----------------------------------------------------------------------

    private sealed class TypeBuilder
    {
        public string         Name          { get; }
        public SourceTypeKind Kind          { get; }
        public int            LineNumber    { get; }
        public bool           IsPublic      { get; }
        public bool           IsAbstract    { get; }
        public bool           IsStatic      { get; }
        public int            OpenBraceDepth { get; }

        public List<SourceMemberModel> Members     { get; } = [];
        public List<SourceTypeModel>   NestedTypes { get; } = [];

        public TypeBuilder(
            string name, SourceTypeKind kind, int lineNumber,
            bool isPublic, bool isAbstract, bool isStatic, int openBraceDepth)
        {
            Name           = name;
            Kind           = kind;
            LineNumber     = lineNumber;
            IsPublic       = isPublic;
            IsAbstract     = isAbstract;
            IsStatic       = isStatic;
            OpenBraceDepth = openBraceDepth;
        }

        public SourceTypeModel Build()
        {
            // Merge nested types into members list isn't applicable here;
            // for simplicity, nested types are not separately surfaced in the outline.
            // Only the top-level types from a file are shown.
            return new SourceTypeModel
            {
                Name       = Name,
                Kind       = Kind,
                LineNumber = LineNumber,
                IsPublic   = IsPublic,
                IsAbstract = IsAbstract,
                IsStatic   = IsStatic,
                Members    = Members,
            };
        }
    }
}
