// ==========================================================
// Project: WpfHexEditor.SDK
// File: ExtensionPoints/XamlDesigner/ICodeBehindGeneratorService.cs
// Author: Derek Tremblay
// Created: 2026-05-05
// Description:
//     SDK bridge that exposes the XAML Designer's code-behind generation
//     pipeline to plugin consumers and the CodeGen panel.
//     Uses lightweight SDK-level DTOs to avoid a circular dependency between
//     SDK and WpfHexEditor.Editor.XamlDesigner.
//
// Architecture Notes:
//     SDK cannot reference XamlDesigner (XamlDesigner already references SDK).
//     CodeBehindSummary / CodeBehindNamedElement are thin SDK-level DTOs.
//     The XamlDesignerPlugin maps XamlCodeModel → CodeBehindSummary on publish.
// ==========================================================

namespace WpfHexEditor.SDK.ExtensionPoints.XamlDesigner;

/// <summary>A named element extracted from x:Name in the XAML source.</summary>
/// <param name="Name">C#-safe identifier (sanitized from the x:Name value).</param>
/// <param name="WpfTypeName">WPF element type (e.g. "Button", "Grid").</param>
/// <param name="SourceLine">1-based XAML source line, or -1 when unknown.</param>
public sealed record CodeBehindNamedElement(string Name, string WpfTypeName, int SourceLine = -1);

/// <summary>An event handler sink extracted from an event attribute (e.g. Click="OnClick").</summary>
/// <param name="ElementName">x:Name of the element, or null for anonymous elements.</param>
/// <param name="EventAttributeName">XAML event attribute (e.g. "Click", "Loaded").</param>
/// <param name="HandlerName">C# method name for the handler.</param>
/// <param name="SourceLine">1-based source line of the element.</param>
public sealed record CodeBehindEventSink(
    string? ElementName,
    string  EventAttributeName,
    string  HandlerName,
    int     SourceLine = -1);

/// <summary>
/// Lightweight SDK-level snapshot of the code-generation surface of the active XAML document.
/// </summary>
public sealed record CodeBehindSummary(
    string?                                 Namespace,
    string?                                 ClassName,
    string                                  RootTypeName,
    IReadOnlyList<CodeBehindNamedElement>   NamedElements,
    IReadOnlyList<CodeBehindEventSink>      EventSinks)
{
    /// <summary>True when code generation is applicable (x:Class present).</summary>
    public bool IsCodeGenEnabled => Namespace is not null && ClassName is not null;

    /// <summary>An empty summary used as initial state.</summary>
    public static readonly CodeBehindSummary Empty = new(
        null, null, "",
        [],
        []);
}

/// <summary>
/// Event arguments for <see cref="ICodeBehindGeneratorService.CodeBehindRegenerated"/>.
/// </summary>
public sealed class CodeBehindRegenEventArgs : EventArgs
{
    /// <summary>The latest code-behind summary after regeneration.</summary>
    public CodeBehindSummary Summary { get; init; } = CodeBehindSummary.Empty;

    /// <summary>True when the file was written successfully.</summary>
    public bool Success { get; init; }

    /// <summary>Error description when <see cref="Success"/> is false.</summary>
    public string? Error { get; init; }
}

/// <summary>
/// SDK bridge exposing the active XAML document's code-behind generation state
/// to plugin panels and consumers.
/// </summary>
public interface ICodeBehindGeneratorService
{
    /// <summary>Enables or disables live XAML → code-behind synchronization.</summary>
    bool IsEnabled { get; set; }

    /// <summary>The latest code-behind summary, or null before the first scan.</summary>
    CodeBehindSummary? CurrentSummary { get; }

    /// <summary>Fires on the UI thread after each regeneration attempt.</summary>
    event EventHandler<CodeBehindRegenEventArgs>? CodeBehindRegenerated;

    /// <summary>
    /// Returns a preview of the generated C# for the given XAML without writing to disk.
    /// </summary>
    Task<string> GeneratePreviewAsync(string xamlSource, CancellationToken ct = default);

    /// <summary>Forces immediate regeneration, bypassing the debounce timer.</summary>
    Task ForceRegenerateAsync(CancellationToken ct = default);
}
