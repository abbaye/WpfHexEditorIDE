// ==========================================================
// Project: WpfHexEditor.SDK
// File: TemplateApplyRequestedEvent.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-07
// Description:
//     EventBus message published by the CustomParserTemplatePlugin when the user
//     clicks "Apply" in the Custom Parser panel. MainWindow subscribes to this
//     and forwards the parsed blocks to the ParsedFieldsPanel (which stays in
//     MainWindow and cannot be migrated to a plugin).
//
// Architecture Notes:
//     Decoupled via IPluginEventBus — CustomParser plugin has no direct reference
//     to MainWindow or ParsedFieldsPanel.
// ==========================================================

namespace WpfHexEditor.SDK.Events;

/// <summary>
/// Represents a single parsed block produced by a custom parser template.
/// Used in <see cref="TemplateApplyRequestedEvent"/> payload.
/// </summary>
public sealed class ParsedBlockInfo
{
    public string  Name        { get; init; } = string.Empty;
    public long    Offset      { get; init; }
    public int     Length      { get; init; }
    public string? TypeHint    { get; init; }
    public string? DisplayValue { get; init; }
}

/// <summary>
/// EventBus message raised when the CustomParser plugin applies a template.
/// Consumed by MainWindow to populate ParsedFieldsPanel.
/// </summary>
public sealed class TemplateApplyRequestedEvent
{
    public string                       TemplateName { get; init; } = string.Empty;
    public IReadOnlyList<ParsedBlockInfo> Blocks     { get; init; } = [];
}
