// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram.Core
// File: Model/ClassNode.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-19
// Description:
//     Mutable model node representing a single type (class, interface,
//     enum, struct, or abstract class) in a class diagram document.
//     Stores both structural (members, kind) and layout (X, Y, Width,
//     Height) state.
//
// Architecture Notes:
//     Regular mutable class rather than a record because the layout
//     engine and drag-move operations mutate X/Y/Width/Height in place.
//     Id defaults to Name at construction time but can be overridden
//     for round-trip serialisation stability.
//     Filtered member views (Fields, Properties, Methods, Events) use
//     LINQ — callers should cache results for hot paths.
//     Semantic properties (IsPartial, IsRecord, IsSealed, XmlDocSummary,
//     Metrics, Namespace, SourceFilePath, SourceLineOneBased) are populated
//     by RoslynClassDiagramAnalyzer and left at defaults by the regex fallback.
// ==========================================================

namespace WpfHexEditor.Editor.ClassDiagram.Core.Model;

/// <summary>
/// Represents a single type node inside a <see cref="DiagramDocument"/>.
/// </summary>
public sealed class ClassNode
{
    // -------------------------------------------------------
    // Identity & Structural Properties
    // -------------------------------------------------------

    /// <summary>
    /// Stable identifier used to reference this node from relationships.
    /// Defaults to <see cref="Name"/> when not explicitly assigned.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>The declared type name shown in the diagram header.</summary>
    public required string Name { get; init; }

    /// <summary>Structural kind of this type node.</summary>
    public ClassKind Kind { get; set; } = ClassKind.Class;

    /// <summary>
    /// Whether this type carries an explicit <c>abstract</c> modifier.
    /// When <see langword="true"/> and <see cref="Kind"/> is <see cref="ClassKind.Class"/>,
    /// the generator emits <c>abstract class</c>.
    /// </summary>
    public bool IsAbstract { get; set; }

    /// <summary>Whether this type is declared <c>partial</c> across multiple source files.</summary>
    public bool IsPartial { get; set; }

    /// <summary>Whether this type is a C# 9+ <c>record</c> or <c>record struct</c>.</summary>
    public bool IsRecord { get; set; }

    /// <summary>Whether this type carries the <c>sealed</c> modifier.</summary>
    public bool IsSealed { get; set; }

    /// <summary>
    /// Fully-qualified namespace of this type (e.g. <c>MyApp.Services</c>).
    /// Empty string when not resolvable (regex fallback path).
    /// </summary>
    public string Namespace { get; set; } = string.Empty;

    /// <summary>
    /// First-line XML documentation summary for this type.
    /// <see langword="null"/> when not present or when using the regex fallback.
    /// </summary>
    public string? XmlDocSummary { get; set; }

    /// <summary>
    /// Absolute path of the primary source file containing this type declaration.
    /// <see langword="null"/> when not resolvable.
    /// </summary>
    public string? SourceFilePath { get; set; }

    /// <summary>
    /// 1-based line number of the type declaration in <see cref="SourceFilePath"/>.
    /// 0 when not resolvable.
    /// </summary>
    public int SourceLineOneBased { get; set; }

    /// <summary>
    /// Coupling and complexity metrics computed by the Roslyn analyzer.
    /// Defaults to <see cref="ClassMetrics.Empty"/> when not computed.
    /// </summary>
    public ClassMetrics Metrics { get; set; } = ClassMetrics.Empty;

    /// <summary>All members declared inside this node.</summary>
    public List<ClassMember> Members { get; init; } = [];

    /// <summary>
    /// Attribute names applied to this type (e.g. "Serializable", "DataContract").
    /// Populated by <c>RoslynClassDiagramAnalyzer</c>; empty when using the regex fallback.
    /// The "Attribute" suffix is stripped for display brevity.
    /// </summary>
    public List<string> Attributes { get; init; } = [];

    // -------------------------------------------------------
    // Layout Properties (mutated by AutoLayoutEngine and drag)
    // -------------------------------------------------------

    /// <summary>Canvas X-coordinate of the top-left corner of this node box.</summary>
    public double X { get; set; }

    /// <summary>Canvas Y-coordinate of the top-left corner of this node box.</summary>
    public double Y { get; set; }

    /// <summary>Rendered box width in logical pixels. Default 180.</summary>
    public double Width { get; set; } = 180;

    /// <summary>Rendered box height in logical pixels. Grows with member count.</summary>
    public double Height { get; set; } = 80;

    /// <summary>Optional user-chosen accent color override for this node (R,G,B). Null = automatic.</summary>
    public (byte R, byte G, byte B)? CustomColor { get; set; }

    // -------------------------------------------------------
    // Filtered Member Views
    // -------------------------------------------------------

    /// <summary>All <see cref="MemberKind.Field"/> members.</summary>
    public IEnumerable<ClassMember> Fields =>
        Members.Where(m => m.Kind == MemberKind.Field);

    /// <summary>All <see cref="MemberKind.Property"/> members.</summary>
    public IEnumerable<ClassMember> Properties =>
        Members.Where(m => m.Kind == MemberKind.Property);

    /// <summary>All <see cref="MemberKind.Method"/> members.</summary>
    public IEnumerable<ClassMember> Methods =>
        Members.Where(m => m.Kind == MemberKind.Method);

    /// <summary>All <see cref="MemberKind.Event"/> members.</summary>
    public IEnumerable<ClassMember> Events =>
        Members.Where(m => m.Kind == MemberKind.Event);

    // -------------------------------------------------------
    // Factory helper
    // -------------------------------------------------------

    /// <summary>
    /// Creates a node and ensures <see cref="Id"/> is set to <see cref="Name"/>
    /// when no explicit Id is provided.
    /// </summary>
    public static ClassNode Create(string name, ClassKind kind = ClassKind.Class)
    {
        var node = new ClassNode { Name = name, Kind = kind };
        node.Id = name;
        return node;
    }

    /// <summary>
    /// Returns a shallow-cloned copy of this node with a new independent <see cref="Members"/> list.
    /// Layout position is preserved; the caller is responsible for assigning a new Id and offset.
    /// </summary>
    public ClassNode DeepClone()
    {
        var clone = new ClassNode
        {
            Name               = Name,
            Kind               = Kind,
            IsAbstract         = IsAbstract,
            IsPartial          = IsPartial,
            IsRecord           = IsRecord,
            IsSealed           = IsSealed,
            Namespace          = Namespace,
            XmlDocSummary      = XmlDocSummary,
            SourceFilePath     = SourceFilePath,
            SourceLineOneBased = SourceLineOneBased,
            Metrics            = Metrics,
            X                  = X,
            Y                  = Y,
            Width              = Width,
            Height             = Height,
            CustomColor        = CustomColor
        };
        clone.Id = Name;  // caller replaces with Guid
        clone.Members.AddRange(Members);
        clone.Attributes.AddRange(Attributes);
        return clone;
    }
}
