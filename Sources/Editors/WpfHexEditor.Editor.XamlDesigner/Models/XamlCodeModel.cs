// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: Models/XamlCodeModel.cs
// Author: Derek Tremblay
// Created: 2026-05-05
// Description:
//     Immutable snapshot of the code-generation-relevant semantic content
//     extracted from a XAML document: class identity, named elements,
//     event sinks, and style-level EventSetter sinks.
//     Used as input to XamlCodeBehindGenerator and as a diff target for
//     CodeBehindSyncService to detect when regeneration is needed.
//
// Architecture Notes:
//     Value Object / Record pattern — fully immutable.
//     IEquatable<XamlCodeModel> is derived automatically from record
//     positional equality, enabling cheap value-equality diff in SyncService.
// ==========================================================

using System.Collections.Immutable;

namespace WpfHexEditor.Editor.XamlDesigner.Models;

/// <summary>
/// A named WPF element extracted via <c>x:Name</c> in the XAML source.
/// </summary>
/// <param name="Name">The x:Name attribute value (C#-safe identifier after sanitization).</param>
/// <param name="WpfTypeName">Local type name of the element (e.g. "Button", "Grid").</param>
/// <param name="SourceLine">1-based line where the element appears in the XAML source.</param>
public sealed record XamlNamedElement(
    string Name,
    string WpfTypeName,
    int    SourceLine = -1);

/// <summary>
/// An event handler sink extracted from an attribute such as <c>Click="OnClick"</c>.
/// </summary>
/// <param name="ElementName">x:Name of the element that owns the event, or null for anonymous elements.</param>
/// <param name="ElementTypeName">WPF type of the element.</param>
/// <param name="EventAttributeName">XAML attribute name of the event (e.g. "Click", "Loaded").</param>
/// <param name="HandlerName">C# method name assigned as handler value.</param>
/// <param name="SourceLine">1-based source line of the element.</param>
public sealed record XamlEventSink(
    string? ElementName,
    string  ElementTypeName,
    string  EventAttributeName,
    string  HandlerName,
    int     SourceLine = -1);

/// <summary>
/// An event handler wired via <c>EventSetter</c> inside a <c>Style</c>.
/// </summary>
/// <param name="EventAttributeName">Event name from EventSetter.Event attribute.</param>
/// <param name="HandlerName">Handler method name from EventSetter.Handler attribute.</param>
/// <param name="SourceLine">1-based line of the EventSetter element.</param>
public sealed record XamlStyleEventSink(
    string EventAttributeName,
    string HandlerName,
    int    SourceLine = -1);

/// <summary>
/// Full semantic snapshot of a XAML document's code-generation surface.
/// </summary>
/// <param name="Namespace">C# namespace extracted from <c>x:Class</c>, or null when absent.</param>
/// <param name="ClassName">C# class name extracted from <c>x:Class</c>, or null when absent.</param>
/// <param name="RootTypeName">Local WPF type name of the XAML root element (e.g. "Window", "UserControl").</param>
/// <param name="NamedElements">All elements with <c>x:Name</c> attributes, in document order.</param>
/// <param name="EventSinks">All element-level event handler attribute sinks.</param>
/// <param name="StyleEventSinks">All <c>EventSetter</c> sinks inside Style blocks.</param>
public sealed record XamlCodeModel(
    string?                           Namespace,
    string?                           ClassName,
    string                            RootTypeName,
    ImmutableArray<XamlNamedElement>  NamedElements,
    ImmutableArray<XamlEventSink>     EventSinks,
    ImmutableArray<XamlStyleEventSink> StyleEventSinks)
{
    /// <summary>True when the model has enough information to drive code generation.</summary>
    public bool IsCodeGenEnabled => Namespace is not null && ClassName is not null;

    /// <summary>Fully qualified class name (e.g. "MyApp.Views.MainWindow").</summary>
    public string? FullClassName => IsCodeGenEnabled ? $"{Namespace}.{ClassName}" : null;

    /// <summary>An empty model used as initial state before the first scan.</summary>
    public static readonly XamlCodeModel Empty = new(
        null, null, "",
        ImmutableArray<XamlNamedElement>.Empty,
        ImmutableArray<XamlEventSink>.Empty,
        ImmutableArray<XamlStyleEventSink>.Empty);
}

/// <summary>
/// Delta between two <see cref="XamlCodeModel"/> snapshots, computed by
/// <see cref="Services.CodeGen.XamlCodeBehindScanner.Diff"/>.
/// </summary>
/// <param name="AddedElements">Named elements present in the new model but not the old.</param>
/// <param name="RemovedElements">Named elements in the old model absent from the new one.</param>
/// <param name="RenamedElements">Elements whose name changed (old name → new name, same type).</param>
/// <param name="AddedSinks">Event sinks added in the new model.</param>
/// <param name="RemovedSinks">Event sinks removed in the new model.</param>
/// <param name="ClassChanged">True when the x:Class value (namespace or class name) changed.</param>
public sealed record CodeBehindPatch(
    ImmutableArray<XamlNamedElement>  AddedElements,
    ImmutableArray<XamlNamedElement>  RemovedElements,
    ImmutableArray<(XamlNamedElement Old, XamlNamedElement New)> RenamedElements,
    ImmutableArray<XamlEventSink>     AddedSinks,
    ImmutableArray<XamlEventSink>     RemovedSinks,
    bool                              ClassChanged)
{
    /// <summary>True when no code-behind changes are required.</summary>
    public bool IsEmpty =>
        AddedElements.IsEmpty &&
        RemovedElements.IsEmpty &&
        RenamedElements.IsEmpty &&
        AddedSinks.IsEmpty &&
        RemovedSinks.IsEmpty &&
        !ClassChanged;

    /// <summary>An empty patch — no changes.</summary>
    public static readonly CodeBehindPatch None = new(
        ImmutableArray<XamlNamedElement>.Empty,
        ImmutableArray<XamlNamedElement>.Empty,
        ImmutableArray<(XamlNamedElement, XamlNamedElement)>.Empty,
        ImmutableArray<XamlEventSink>.Empty,
        ImmutableArray<XamlEventSink>.Empty,
        false);
}
