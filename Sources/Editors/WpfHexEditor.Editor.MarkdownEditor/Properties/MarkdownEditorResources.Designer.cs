///////////////////////////////////////////////////////////////
// GNU Affero General Public License v3.0  2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Project     : WpfHexEditor.Editor.MarkdownEditor
// File        : MarkdownEditorResources.Designer.cs
// Description : Strongly-typed accessor for MarkdownEditor-specific UI strings.
///////////////////////////////////////////////////////////////

using System.Globalization;
using System.Resources;

namespace WpfHexEditor.Editor.MarkdownEditor.Properties;

internal static class MarkdownEditorResources
{
    private static ResourceManager? _resourceManager;
    private static CultureInfo? _resourceCulture;

    internal static ResourceManager ResourceManager =>
        _resourceManager ??= new ResourceManager(
            "WpfHexEditor.Editor.MarkdownEditor.Properties.MarkdownEditorResources",
            typeof(MarkdownEditorResources).Assembly);

    internal static CultureInfo? Culture
    {
        get => _resourceCulture;
        set => _resourceCulture = value;
    }

    private static string GetString(string name) =>
        ResourceManager.GetString(name, _resourceCulture) ?? name;

    // Status bar labels / tooltips
    internal static string MdSb_ViewLabel     => GetString(nameof(MdSb_ViewLabel));
    internal static string MdSb_ViewTooltip   => GetString(nameof(MdSb_ViewTooltip));
    internal static string MdSb_WordsLabel    => GetString(nameof(MdSb_WordsLabel));
    internal static string MdSb_WordsTooltip  => GetString(nameof(MdSb_WordsTooltip));
    internal static string MdSb_LinesLabel    => GetString(nameof(MdSb_LinesLabel));
    internal static string MdSb_LinesTooltip  => GetString(nameof(MdSb_LinesTooltip));
    internal static string MdSb_ReadLabel     => GetString(nameof(MdSb_ReadLabel));
    internal static string MdSb_ReadTooltip   => GetString(nameof(MdSb_ReadTooltip));
    internal static string MdSb_ZoomLabel     => GetString(nameof(MdSb_ZoomLabel));
    internal static string MdSb_ZoomTooltip   => GetString(nameof(MdSb_ZoomTooltip));

    // Toolbar labels / tooltips
    internal static string MdTb_ViewLabel        => GetString(nameof(MdTb_ViewLabel));
    internal static string MdTb_ViewTooltip      => GetString(nameof(MdTb_ViewTooltip));
    internal static string MdTb_LayoutLabel      => GetString(nameof(MdTb_LayoutLabel));
    internal static string MdTb_LayoutTooltip    => GetString(nameof(MdTb_LayoutTooltip));
    internal static string MdTb_WrapLabel        => GetString(nameof(MdTb_WrapLabel));
    internal static string MdTb_WrapTooltip      => GetString(nameof(MdTb_WrapTooltip));
    internal static string MdTb_RefreshLabel     => GetString(nameof(MdTb_RefreshLabel));
    internal static string MdTb_RefreshTooltip   => GetString(nameof(MdTb_RefreshTooltip));
    internal static string MdTb_InsertLabel      => GetString(nameof(MdTb_InsertLabel));
    internal static string MdTb_InsertTooltip    => GetString(nameof(MdTb_InsertTooltip));
    internal static string MdTb_FormatLabel      => GetString(nameof(MdTb_FormatLabel));
    internal static string MdTb_FormatTooltip    => GetString(nameof(MdTb_FormatTooltip));
    internal static string MdTb_FullscreenLabel  => GetString(nameof(MdTb_FullscreenLabel));
    internal static string MdTb_FullscreenTooltip => GetString(nameof(MdTb_FullscreenTooltip));

    // Toolbar dropdown items – view modes
    internal static string MdView_SourceOnly  => GetString(nameof(MdView_SourceOnly));
    internal static string MdView_Split       => GetString(nameof(MdView_Split));
    internal static string MdView_PreviewOnly => GetString(nameof(MdView_PreviewOnly));

    // Toolbar dropdown items – layout
    internal static string MdLayout_PreviewRight  => GetString(nameof(MdLayout_PreviewRight));
    internal static string MdLayout_PreviewLeft   => GetString(nameof(MdLayout_PreviewLeft));
    internal static string MdLayout_PreviewBottom => GetString(nameof(MdLayout_PreviewBottom));
    internal static string MdLayout_PreviewTop    => GetString(nameof(MdLayout_PreviewTop));

    // Toolbar dropdown items – insert
    internal static string MdInsert_Table          => GetString(nameof(MdInsert_Table));
    internal static string MdInsert_CodeBlock      => GetString(nameof(MdInsert_CodeBlock));
    internal static string MdInsert_Image          => GetString(nameof(MdInsert_Image));
    internal static string MdInsert_HorizontalRule => GetString(nameof(MdInsert_HorizontalRule));
    internal static string MdInsert_TableOfContents => GetString(nameof(MdInsert_TableOfContents));

    // Toolbar dropdown items – format
    internal static string MdFmt_Bold          => GetString(nameof(MdFmt_Bold));
    internal static string MdFmt_Italic        => GetString(nameof(MdFmt_Italic));
    internal static string MdFmt_Strikethrough => GetString(nameof(MdFmt_Strikethrough));
    internal static string MdFmt_InlineCode    => GetString(nameof(MdFmt_InlineCode));

    // Context menu – source editor (MarkdownEditorHost)
    internal static string MdCtx_Bold           => GetString(nameof(MdCtx_Bold));
    internal static string MdCtx_Italic         => GetString(nameof(MdCtx_Italic));
    internal static string MdCtx_Strikethrough  => GetString(nameof(MdCtx_Strikethrough));
    internal static string MdCtx_InlineCode     => GetString(nameof(MdCtx_InlineCode));
    internal static string MdCtx_Table          => GetString(nameof(MdCtx_Table));
    internal static string MdCtx_CodeBlock      => GetString(nameof(MdCtx_CodeBlock));
    internal static string MdCtx_Link           => GetString(nameof(MdCtx_Link));
    internal static string MdCtx_Image          => GetString(nameof(MdCtx_Image));
    internal static string MdCtx_HorizontalRule => GetString(nameof(MdCtx_HorizontalRule));
    internal static string MdCtx_WordWrap       => GetString(nameof(MdCtx_WordWrap));
    internal static string MdCtx_ExportHtml     => GetString(nameof(MdCtx_ExportHtml));
    internal static string MdCtx_ExportPdf      => GetString(nameof(MdCtx_ExportPdf));

    // Context menu – preview pane (MarkdownPreviewPane)
    internal static string MdPrev_SourceOnly     => GetString(nameof(MdPrev_SourceOnly));
    internal static string MdPrev_SplitView      => GetString(nameof(MdPrev_SplitView));
    internal static string MdPrev_PreviewOnly    => GetString(nameof(MdPrev_PreviewOnly));
    internal static string MdPrev_Fullscreen     => GetString(nameof(MdPrev_Fullscreen));
    internal static string MdPrev_ExitFullscreen => GetString(nameof(MdPrev_ExitFullscreen));
    internal static string MdPrev_RefreshPreview => GetString(nameof(MdPrev_RefreshPreview));
    internal static string MdPrev_CycleLayout    => GetString(nameof(MdPrev_CycleLayout));
    internal static string MdPrev_ZoomIn         => GetString(nameof(MdPrev_ZoomIn));
    internal static string MdPrev_ZoomOut        => GetString(nameof(MdPrev_ZoomOut));
    internal static string MdPrev_ResetZoom      => GetString(nameof(MdPrev_ResetZoom));
    internal static string MdPrev_Copy           => GetString(nameof(MdPrev_Copy));

    // Export dialog titles
    internal static string MdDlg_ExportHtmlTitle => GetString(nameof(MdDlg_ExportHtmlTitle));
    internal static string MdDlg_ExportPdfTitle  => GetString(nameof(MdDlg_ExportPdfTitle));
}
