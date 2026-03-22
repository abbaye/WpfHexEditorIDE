// ==========================================================
// Project: WpfHexEditor.Editor.MarkdownEditor
// File: MarkdownEditorFactory.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-19
// Description:
//     IEditorFactory that creates MarkdownEditorHost instances for
//     Markdown files (.md, .markdown, .mkd, .mdx).
//     Must be registered BEFORE TextEditorFactory in the editor registry
//     so that Markdown files are intercepted first.
//
// Architecture Notes:
//     Factory Pattern — stateless, registered at app startup via
//     _editorRegistry.Register(new MarkdownEditorFactory()).
// ==========================================================

using System.Collections.Generic;
using System.IO;
using WpfHexEditor.Editor.Core;
using WpfHexEditor.Editor.MarkdownEditor.Controls;

namespace WpfHexEditor.Editor.MarkdownEditor;

/// <summary>
/// <see cref="IEditorFactory"/> that creates <see cref="MarkdownEditorHost"/> instances
/// for GitHub-Flavored Markdown files.
/// </summary>
public sealed class MarkdownEditorFactory : IEditorFactory
{
    private static readonly IEditorDescriptor _descriptor = new MarkdownEditorDescriptor();

    /// <inheritdoc/>
    public IEditorDescriptor Descriptor => _descriptor;

    /// <inheritdoc/>
    public bool CanOpen(string filePath)
    {
        var ext = Path.GetExtension(filePath)?.ToLowerInvariant();
        return _descriptor.SupportedExtensions.Contains(ext ?? string.Empty);
    }

    /// <inheritdoc/>
    public IDocumentEditor Create() => new MarkdownEditorHost();
}

file sealed class MarkdownEditorDescriptor : IEditorDescriptor
{
    public string Id          => "markdown-editor";
    public string DisplayName => "Markdown Editor";
    public string Description => "GitHub-Flavored Markdown editor with live preview, Mermaid diagrams, code highlighting, and emoji support.";
    public IReadOnlyList<string> SupportedExtensions => [".md", ".markdown", ".mkd", ".mdx"];
}
