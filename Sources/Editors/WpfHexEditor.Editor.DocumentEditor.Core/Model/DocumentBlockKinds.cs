// ==========================================================
// Project: WpfHexEditor.Editor.DocumentEditor.Core
// File: Model/DocumentBlockKinds.cs
// Description:
//     Well-known string values for DocumentBlock.Kind, shared
//     across loaders, mappers, renderer and services so that
//     a typo can't silently break the cascade or filtering.
// ==========================================================

namespace WpfHexEditor.Editor.DocumentEditor.Core.Model;

/// <summary>Canonical Kind strings used throughout the DocumentEditor pipeline.</summary>
public static class DocumentBlockKinds
{
    public const string Paragraph     = "paragraph";
    public const string Heading       = "heading";
    public const string Run           = "run";
    public const string Hyperlink     = "hyperlink";
    public const string StructuredTag = "structured-tag";
    public const string ListItem      = "list-item";
    public const string List          = "list";
    public const string Table         = "table";
    public const string TableRow      = "table-row";
    public const string TableCell     = "table-cell";
    public const string Image         = "image";
    public const string ObjectEmbed   = "object";
    public const string Section       = "section";
    public const string Header        = "header";
    public const string Footer        = "footer";
    public const string Macro         = "macro";
}
