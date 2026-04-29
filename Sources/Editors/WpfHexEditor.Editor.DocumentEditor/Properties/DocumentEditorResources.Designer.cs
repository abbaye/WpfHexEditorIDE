///////////////////////////////////////////////////////////////
// GNU Affero General Public License v3.0  2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Project     : WpfHexEditor.Editor.DocumentEditor
// File        : DocumentEditorResources.Designer.cs
// Description : Strongly-typed accessor class for DocumentEditorResources.resx.
//               One static property per resource key, using GetString(nameof(...)).
// Architecture: Internal — consumed by DocumentEditorLocalizedDictionary and
//               code-behind files inside WpfHexEditor.Editor.DocumentEditor.
///////////////////////////////////////////////////////////////

#nullable enable

namespace WpfHexEditor.Editor.DocumentEditor.Properties;

using System.Globalization;
using System.Resources;

/// <summary>
/// Strongly-typed resource accessor for
/// <c>WpfHexEditor.Editor.DocumentEditor.Properties.DocumentEditorResources</c>.
/// </summary>
[global::System.CodeDom.Compiler.GeneratedCodeAttribute("DocumentEditorResources.Designer.cs", "1.0.0.0")]
[global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
[global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
internal static class DocumentEditorResources
{
    private static ResourceManager? _resourceMan;
    private static CultureInfo? _resourceCulture;

    /// <summary>Returns the cached <see cref="ResourceManager"/> instance.</summary>
    [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
    internal static ResourceManager ResourceManager
    {
        get
        {
            if (_resourceMan is null)
                _resourceMan = new ResourceManager(
                    "WpfHexEditor.Editor.DocumentEditor.Properties.DocumentEditorResources",
                    typeof(DocumentEditorResources).Assembly);
            return _resourceMan;
        }
    }

    /// <summary>
    /// Overrides the current thread's <see cref="CultureInfo.CurrentUICulture"/> for
    /// all lookups performed by this strongly-typed resource class.
    /// </summary>
    [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
    internal static CultureInfo? Culture
    {
        get => _resourceCulture;
        set => _resourceCulture = value;
    }

    // ─── DocumentFindReplaceDialog.xaml ────────────────────────────────────────

    /// <summary>Gets "Find &amp; Replace"</summary>
    internal static string DocFindReplace_Title
        => ResourceManager.GetString(nameof(DocFindReplace_Title), _resourceCulture)!;

    /// <summary>Gets "Find:"</summary>
    internal static string DocFindReplace_FindLabel
        => ResourceManager.GetString(nameof(DocFindReplace_FindLabel), _resourceCulture)!;

    /// <summary>Gets "Replace:"</summary>
    internal static string DocFindReplace_ReplaceLabel
        => ResourceManager.GetString(nameof(DocFindReplace_ReplaceLabel), _resourceCulture)!;

    /// <summary>Gets "Match case"</summary>
    internal static string DocFindReplace_MatchCase
        => ResourceManager.GetString(nameof(DocFindReplace_MatchCase), _resourceCulture)!;

    /// <summary>Gets "Whole word"</summary>
    internal static string DocFindReplace_WholeWord
        => ResourceManager.GetString(nameof(DocFindReplace_WholeWord), _resourceCulture)!;

    /// <summary>Gets "Regex"</summary>
    internal static string DocFindReplace_Regex
        => ResourceManager.GetString(nameof(DocFindReplace_Regex), _resourceCulture)!;

    /// <summary>Gets "Find All"</summary>
    internal static string DocFindReplace_FindAll
        => ResourceManager.GetString(nameof(DocFindReplace_FindAll), _resourceCulture)!;

    /// <summary>Gets "Find Next"</summary>
    internal static string DocFindReplace_FindNext
        => ResourceManager.GetString(nameof(DocFindReplace_FindNext), _resourceCulture)!;

    /// <summary>Gets "Find Prev"</summary>
    internal static string DocFindReplace_FindPrev
        => ResourceManager.GetString(nameof(DocFindReplace_FindPrev), _resourceCulture)!;

    /// <summary>Gets "Replace"</summary>
    internal static string DocFindReplace_Replace
        => ResourceManager.GetString(nameof(DocFindReplace_Replace), _resourceCulture)!;

    /// <summary>Gets "Replace All"</summary>
    internal static string DocFindReplace_ReplaceAll
        => ResourceManager.GetString(nameof(DocFindReplace_ReplaceAll), _resourceCulture)!;

    /// <summary>Gets "Close"</summary>
    internal static string DocFindReplace_Close
        => ResourceManager.GetString(nameof(DocFindReplace_Close), _resourceCulture)!;

    // ─── DocumentHexPane.xaml ──────────────────────────────────────────────────

    /// <summary>Gets "Hex"</summary>
    internal static string DocHexPane_HexLabel
        => ResourceManager.GetString(nameof(DocHexPane_HexLabel), _resourceCulture)!;

    // ─── DocumentPageSettingsPanel.xaml ────────────────────────────────────────

    /// <summary>Gets "Page"</summary>
    internal static string DocPageSettings_PageTab
        => ResourceManager.GetString(nameof(DocPageSettings_PageTab), _resourceCulture)!;

    /// <summary>Gets "Format:"</summary>
    internal static string DocPageSettings_FormatLabel
        => ResourceManager.GetString(nameof(DocPageSettings_FormatLabel), _resourceCulture)!;

    /// <summary>Gets "A4  (21 × 29.7 cm)"</summary>
    internal static string DocPageSettings_A4Option
        => ResourceManager.GetString(nameof(DocPageSettings_A4Option), _resourceCulture)!;

    /// <summary>Gets "A3  (29.7 × 42 cm)"</summary>
    internal static string DocPageSettings_A3Option
        => ResourceManager.GetString(nameof(DocPageSettings_A3Option), _resourceCulture)!;

    /// <summary>Gets "A5  (14.8 × 21 cm)"</summary>
    internal static string DocPageSettings_A5Option
        => ResourceManager.GetString(nameof(DocPageSettings_A5Option), _resourceCulture)!;

    /// <summary>Gets "Letter (8.5 × 11 in)"</summary>
    internal static string DocPageSettings_LetterOption
        => ResourceManager.GetString(nameof(DocPageSettings_LetterOption), _resourceCulture)!;

    /// <summary>Gets "Legal  (8.5 × 14 in)"</summary>
    internal static string DocPageSettings_LegalOption
        => ResourceManager.GetString(nameof(DocPageSettings_LegalOption), _resourceCulture)!;

    /// <summary>Gets "Custom"</summary>
    internal static string DocPageSettings_CustomOption
        => ResourceManager.GetString(nameof(DocPageSettings_CustomOption), _resourceCulture)!;

    /// <summary>Gets "Orientation:"</summary>
    internal static string DocPageSettings_OrientationLabel
        => ResourceManager.GetString(nameof(DocPageSettings_OrientationLabel), _resourceCulture)!;

    /// <summary>Gets "Portrait"</summary>
    internal static string DocPageSettings_Portrait
        => ResourceManager.GetString(nameof(DocPageSettings_Portrait), _resourceCulture)!;

    /// <summary>Gets "Landscape"</summary>
    internal static string DocPageSettings_Landscape
        => ResourceManager.GetString(nameof(DocPageSettings_Landscape), _resourceCulture)!;

    /// <summary>Gets "Columns"</summary>
    internal static string DocPageSettings_ColumnsTab
        => ResourceManager.GetString(nameof(DocPageSettings_ColumnsTab), _resourceCulture)!;

    /// <summary>Gets "Equal column widths"</summary>
    internal static string DocPageSettings_EqualWidths
        => ResourceManager.GetString(nameof(DocPageSettings_EqualWidths), _resourceCulture)!;

    /// <summary>Gets "Show separator line between columns"</summary>
    internal static string DocPageSettings_ShowSeparator
        => ResourceManager.GetString(nameof(DocPageSettings_ShowSeparator), _resourceCulture)!;

    /// <summary>Gets "Header/Footer"</summary>
    internal static string DocPageSettings_HeaderFooterTab
        => ResourceManager.GetString(nameof(DocPageSettings_HeaderFooterTab), _resourceCulture)!;

    /// <summary>Gets "Header"</summary>
    internal static string DocPageSettings_HeaderGroup
        => ResourceManager.GetString(nameof(DocPageSettings_HeaderGroup), _resourceCulture)!;

    /// <summary>Gets "Enable header"</summary>
    internal static string DocPageSettings_EnableHeader
        => ResourceManager.GetString(nameof(DocPageSettings_EnableHeader), _resourceCulture)!;

    /// <summary>Gets "Different content on first page"</summary>
    internal static string DocPageSettings_DifferentFirstPage
        => ResourceManager.GetString(nameof(DocPageSettings_DifferentFirstPage), _resourceCulture)!;

    /// <summary>Gets "Same header on left/right pages"</summary>
    internal static string DocPageSettings_SameHeaderLR
        => ResourceManager.GetString(nameof(DocPageSettings_SameHeaderLR), _resourceCulture)!;

    /// <summary>Gets "Footer"</summary>
    internal static string DocPageSettings_FooterGroup
        => ResourceManager.GetString(nameof(DocPageSettings_FooterGroup), _resourceCulture)!;

    /// <summary>Gets "Enable footer"</summary>
    internal static string DocPageSettings_EnableFooter
        => ResourceManager.GetString(nameof(DocPageSettings_EnableFooter), _resourceCulture)!;

    /// <summary>Gets "Border"</summary>
    internal static string DocPageSettings_BorderTab
        => ResourceManager.GetString(nameof(DocPageSettings_BorderTab), _resourceCulture)!;

    /// <summary>Gets "None"</summary>
    internal static string DocPageSettings_BorderNone
        => ResourceManager.GetString(nameof(DocPageSettings_BorderNone), _resourceCulture)!;

    /// <summary>Gets "Box"</summary>
    internal static string DocPageSettings_BorderBox
        => ResourceManager.GetString(nameof(DocPageSettings_BorderBox), _resourceCulture)!;

    /// <summary>Gets "Shadow"</summary>
    internal static string DocPageSettings_BorderShadow
        => ResourceManager.GetString(nameof(DocPageSettings_BorderShadow), _resourceCulture)!;

    /// <summary>Gets "Cancel"</summary>
    internal static string DocPageSettings_CancelButton
        => ResourceManager.GetString(nameof(DocPageSettings_CancelButton), _resourceCulture)!;

    /// <summary>Gets "Apply"</summary>
    internal static string DocPageSettings_ApplyButton
        => ResourceManager.GetString(nameof(DocPageSettings_ApplyButton), _resourceCulture)!;

    /// <summary>Gets "Inside:"</summary>
    internal static string DocPageSettings_InsideMargin
        => ResourceManager.GetString(nameof(DocPageSettings_InsideMargin), _resourceCulture)!;

    /// <summary>Gets "Outside:"</summary>
    internal static string DocPageSettings_OutsideMargin
        => ResourceManager.GetString(nameof(DocPageSettings_OutsideMargin), _resourceCulture)!;

    /// <summary>Gets "Left:"</summary>
    internal static string DocPageSettings_LeftMargin
        => ResourceManager.GetString(nameof(DocPageSettings_LeftMargin), _resourceCulture)!;

    /// <summary>Gets "Right:"</summary>
    internal static string DocPageSettings_RightMargin
        => ResourceManager.GetString(nameof(DocPageSettings_RightMargin), _resourceCulture)!;

    // ─── DocumentEditorHost.xaml ───────────────────────────────────────────────

    /// <summary>Gets "Text view"</summary>
    internal static string DocEditorHost_TextViewToolTip
        => ResourceManager.GetString(nameof(DocEditorHost_TextViewToolTip), _resourceCulture)!;

    /// <summary>Gets "Split view (text + hex)"</summary>
    internal static string DocEditorHost_SplitViewToolTip
        => ResourceManager.GetString(nameof(DocEditorHost_SplitViewToolTip), _resourceCulture)!;

    /// <summary>Gets "Hex view"</summary>
    internal static string DocEditorHost_HexViewToolTip
        => ResourceManager.GetString(nameof(DocEditorHost_HexViewToolTip), _resourceCulture)!;

    /// <summary>Gets "Structure tree view"</summary>
    internal static string DocEditorHost_StructureViewToolTip
        => ResourceManager.GetString(nameof(DocEditorHost_StructureViewToolTip), _resourceCulture)!;

    /// <summary>Gets "Full view (text + structure + hex)"</summary>
    internal static string DocEditorHost_FullViewToolTip
        => ResourceManager.GetString(nameof(DocEditorHost_FullViewToolTip), _resourceCulture)!;

    /// <summary>Gets "Focus mode — zen writing (Ctrl+Shift+F)"</summary>
    internal static string DocEditorHost_FocusModeToolTip
        => ResourceManager.GetString(nameof(DocEditorHost_FocusModeToolTip), _resourceCulture)!;

    /// <summary>Gets "Text"</summary>
    internal static string DocEditorHost_TextModeLabel
        => ResourceManager.GetString(nameof(DocEditorHost_TextModeLabel), _resourceCulture)!;

    /// <summary>Gets "Split"</summary>
    internal static string DocEditorHost_SplitModeLabel
        => ResourceManager.GetString(nameof(DocEditorHost_SplitModeLabel), _resourceCulture)!;

    /// <summary>Gets "Hex"</summary>
    internal static string DocEditorHost_HexModeLabel
        => ResourceManager.GetString(nameof(DocEditorHost_HexModeLabel), _resourceCulture)!;

    /// <summary>Gets "Structure"</summary>
    internal static string DocEditorHost_StructureModeLabel
        => ResourceManager.GetString(nameof(DocEditorHost_StructureModeLabel), _resourceCulture)!;

    /// <summary>Gets "Full"</summary>
    internal static string DocEditorHost_FullModeLabel
        => ResourceManager.GetString(nameof(DocEditorHost_FullModeLabel), _resourceCulture)!;

    /// <summary>Gets "Focus"</summary>
    internal static string DocEditorHost_FocusModeLabel
        => ResourceManager.GetString(nameof(DocEditorHost_FocusModeLabel), _resourceCulture)!;

    /// <summary>Gets "Page view — paginated A4/Letter cards"</summary>
    internal static string DocEditorHost_PageViewToolTip
        => ResourceManager.GetString(nameof(DocEditorHost_PageViewToolTip), _resourceCulture)!;

    /// <summary>Gets "Draft view — continuous scroll, compact margins"</summary>
    internal static string DocEditorHost_DraftViewToolTip
        => ResourceManager.GetString(nameof(DocEditorHost_DraftViewToolTip), _resourceCulture)!;

    /// <summary>Gets "Outline view — structure tree centred"</summary>
    internal static string DocEditorHost_OutlineViewToolTip
        => ResourceManager.GetString(nameof(DocEditorHost_OutlineViewToolTip), _resourceCulture)!;

    /// <summary>Gets "Forensic analysis mode"</summary>
    internal static string DocEditorHost_ForensicModeToolTip
        => ResourceManager.GetString(nameof(DocEditorHost_ForensicModeToolTip), _resourceCulture)!;

    /// <summary>Gets "Forensic"</summary>
    internal static string DocEditorHost_ForensicModeLabel
        => ResourceManager.GetString(nameof(DocEditorHost_ForensicModeLabel), _resourceCulture)!;

    /// <summary>Gets "Bold (Ctrl+B)"</summary>
    internal static string DocEditorHost_BoldToolTip
        => ResourceManager.GetString(nameof(DocEditorHost_BoldToolTip), _resourceCulture)!;

    /// <summary>Gets "Italic (Ctrl+I)"</summary>
    internal static string DocEditorHost_ItalicToolTip
        => ResourceManager.GetString(nameof(DocEditorHost_ItalicToolTip), _resourceCulture)!;

    /// <summary>Gets "Underline (Ctrl+U)"</summary>
    internal static string DocEditorHost_UnderlineToolTip
        => ResourceManager.GetString(nameof(DocEditorHost_UnderlineToolTip), _resourceCulture)!;

    /// <summary>Gets "Strikethrough"</summary>
    internal static string DocEditorHost_StrikethroughToolTip
        => ResourceManager.GetString(nameof(DocEditorHost_StrikethroughToolTip), _resourceCulture)!;

    /// <summary>Gets "Align left"</summary>
    internal static string DocEditorHost_AlignLeftToolTip
        => ResourceManager.GetString(nameof(DocEditorHost_AlignLeftToolTip), _resourceCulture)!;

    /// <summary>Gets "Align center"</summary>
    internal static string DocEditorHost_AlignCenterToolTip
        => ResourceManager.GetString(nameof(DocEditorHost_AlignCenterToolTip), _resourceCulture)!;

    /// <summary>Gets "Align right"</summary>
    internal static string DocEditorHost_AlignRightToolTip
        => ResourceManager.GetString(nameof(DocEditorHost_AlignRightToolTip), _resourceCulture)!;

    /// <summary>Gets "Paragraph style"</summary>
    internal static string DocEditorHost_StyleDropdownToolTip
        => ResourceManager.GetString(nameof(DocEditorHost_StyleDropdownToolTip), _resourceCulture)!;

    /// <summary>Gets "Normal"</summary>
    internal static string DocEditorHost_StyleNormal
        => ResourceManager.GetString(nameof(DocEditorHost_StyleNormal), _resourceCulture)!;

    /// <summary>Gets "H1"</summary>
    internal static string DocEditorHost_StyleH1
        => ResourceManager.GetString(nameof(DocEditorHost_StyleH1), _resourceCulture)!;

    /// <summary>Gets "H2"</summary>
    internal static string DocEditorHost_StyleH2
        => ResourceManager.GetString(nameof(DocEditorHost_StyleH2), _resourceCulture)!;

    /// <summary>Gets "H3"</summary>
    internal static string DocEditorHost_StyleH3
        => ResourceManager.GetString(nameof(DocEditorHost_StyleH3), _resourceCulture)!;

    /// <summary>Gets "H4"</summary>
    internal static string DocEditorHost_StyleH4
        => ResourceManager.GetString(nameof(DocEditorHost_StyleH4), _resourceCulture)!;

    /// <summary>Gets "H5"</summary>
    internal static string DocEditorHost_StyleH5
        => ResourceManager.GetString(nameof(DocEditorHost_StyleH5), _resourceCulture)!;

    /// <summary>Gets "H6"</summary>
    internal static string DocEditorHost_StyleH6
        => ResourceManager.GetString(nameof(DocEditorHost_StyleH6), _resourceCulture)!;

    /// <summary>Gets "Quote"</summary>
    internal static string DocEditorHost_StyleQuote
        => ResourceManager.GetString(nameof(DocEditorHost_StyleQuote), _resourceCulture)!;

    /// <summary>Gets "Code"</summary>
    internal static string DocEditorHost_StyleCode
        => ResourceManager.GetString(nameof(DocEditorHost_StyleCode), _resourceCulture)!;

    /// <summary>Gets "Paragraph styles (Ctrl+Alt+S)"</summary>
    internal static string DocEditorHost_StylesPanelToolTip
        => ResourceManager.GetString(nameof(DocEditorHost_StylesPanelToolTip), _resourceCulture)!;

    /// <summary>Gets "Page setup (paper size, margins, columns…)"</summary>
    internal static string DocEditorHost_PageSetupToolTip
        => ResourceManager.GetString(nameof(DocEditorHost_PageSetupToolTip), _resourceCulture)!;

    /// <summary>Gets "Zoom out"</summary>
    internal static string DocEditorHost_ZoomOutToolTip
        => ResourceManager.GetString(nameof(DocEditorHost_ZoomOutToolTip), _resourceCulture)!;

    /// <summary>Gets "100%"</summary>
    internal static string DocEditorHost_ZoomDefault
        => ResourceManager.GetString(nameof(DocEditorHost_ZoomDefault), _resourceCulture)!;

    /// <summary>Gets "Zoom in"</summary>
    internal static string DocEditorHost_ZoomInToolTip
        => ResourceManager.GetString(nameof(DocEditorHost_ZoomInToolTip), _resourceCulture)!;

    /// <summary>Gets "Read Only"</summary>
    internal static string DocEditorHost_ReadOnlyToolTip
        => ResourceManager.GetString(nameof(DocEditorHost_ReadOnlyToolTip), _resourceCulture)!;

    /// <summary>Gets "Document metadata"</summary>
    internal static string DocEditorHost_MetadataToolTip
        => ResourceManager.GetString(nameof(DocEditorHost_MetadataToolTip), _resourceCulture)!;

    /// <summary>Gets "Export document"</summary>
    internal static string DocEditorHost_ExportToolTip
        => ResourceManager.GetString(nameof(DocEditorHost_ExportToolTip), _resourceCulture)!;

    /// <summary>Gets "Save"</summary>
    internal static string DocEditorHost_SaveToolTip
        => ResourceManager.GetString(nameof(DocEditorHost_SaveToolTip), _resourceCulture)!;

    /// <summary>Gets "Title"</summary>
    internal static string DocEditorHost_MetaTitleLabel
        => ResourceManager.GetString(nameof(DocEditorHost_MetaTitleLabel), _resourceCulture)!;

    /// <summary>Gets "Author"</summary>
    internal static string DocEditorHost_MetaAuthorLabel
        => ResourceManager.GetString(nameof(DocEditorHost_MetaAuthorLabel), _resourceCulture)!;

    /// <summary>Gets "Format"</summary>
    internal static string DocEditorHost_MetaFormatLabel
        => ResourceManager.GetString(nameof(DocEditorHost_MetaFormatLabel), _resourceCulture)!;

    /// <summary>Gets "MIME"</summary>
    internal static string DocEditorHost_MetaMimeLabel
        => ResourceManager.GetString(nameof(DocEditorHost_MetaMimeLabel), _resourceCulture)!;

    /// <summary>Gets "Created"</summary>
    internal static string DocEditorHost_MetaCreatedLabel
        => ResourceManager.GetString(nameof(DocEditorHost_MetaCreatedLabel), _resourceCulture)!;

    /// <summary>Gets "Modified"</summary>
    internal static string DocEditorHost_MetaModifiedLabel
        => ResourceManager.GetString(nameof(DocEditorHost_MetaModifiedLabel), _resourceCulture)!;

    /// <summary>Gets "Macros"</summary>
    internal static string DocEditorHost_MetaMacrosLabel
        => ResourceManager.GetString(nameof(DocEditorHost_MetaMacrosLabel), _resourceCulture)!;

    // ─── DocumentPopToolbar.xaml ───────────────────────────────────────────────

    /// <summary>Gets "Bold (Ctrl+B)"</summary>
    internal static string DocPopToolbar_BoldToolTip
        => ResourceManager.GetString(nameof(DocPopToolbar_BoldToolTip), _resourceCulture)!;

    /// <summary>Gets "Italic (Ctrl+I)"</summary>
    internal static string DocPopToolbar_ItalicToolTip
        => ResourceManager.GetString(nameof(DocPopToolbar_ItalicToolTip), _resourceCulture)!;

    /// <summary>Gets "Underline (Ctrl+U)"</summary>
    internal static string DocPopToolbar_UnderlineToolTip
        => ResourceManager.GetString(nameof(DocPopToolbar_UnderlineToolTip), _resourceCulture)!;

    /// <summary>Gets "Copy selected text"</summary>
    internal static string DocPopToolbar_CopyTextToolTip
        => ResourceManager.GetString(nameof(DocPopToolbar_CopyTextToolTip), _resourceCulture)!;

    /// <summary>Gets "Copy as hex bytes"</summary>
    internal static string DocPopToolbar_CopyHexToolTip
        => ResourceManager.GetString(nameof(DocPopToolbar_CopyHexToolTip), _resourceCulture)!;

    /// <summary>Gets "Inspect block in Structure pane"</summary>
    internal static string DocPopToolbar_InspectToolTip
        => ResourceManager.GetString(nameof(DocPopToolbar_InspectToolTip), _resourceCulture)!;

    /// <summary>Gets "Jump to offset in Hex pane"</summary>
    internal static string DocPopToolbar_JumpHexToolTip
        => ResourceManager.GetString(nameof(DocPopToolbar_JumpHexToolTip), _resourceCulture)!;

    // ─── DocumentStatusBar.xaml ────────────────────────────────────────────────

    /// <summary>Gets "Page count"</summary>
    internal static string DocStatusBar_PageCountToolTip
        => ResourceManager.GetString(nameof(DocStatusBar_PageCountToolTip), _resourceCulture)!;

    /// <summary>Gets "Word count"</summary>
    internal static string DocStatusBar_WordCountToolTip
        => ResourceManager.GetString(nameof(DocStatusBar_WordCountToolTip), _resourceCulture)!;

    /// <summary>Gets "Zoom level"</summary>
    internal static string DocStatusBar_ZoomToolTip
        => ResourceManager.GetString(nameof(DocStatusBar_ZoomToolTip), _resourceCulture)!;

    /// <summary>Gets "Click to open forensic panel"</summary>
    internal static string DocStatusBar_ForensicBadgeToolTip
        => ResourceManager.GetString(nameof(DocStatusBar_ForensicBadgeToolTip), _resourceCulture)!;

    // ─── DocumentStructurePane.xaml ────────────────────────────────────────────

    /// <summary>Gets "Structure"</summary>
    internal static string DocStructure_Header
        => ResourceManager.GetString(nameof(DocStructure_Header), _resourceCulture)!;

    /// <summary>Gets "Collapse all"</summary>
    internal static string DocStructure_CollapseAllToolTip
        => ResourceManager.GetString(nameof(DocStructure_CollapseAllToolTip), _resourceCulture)!;

    /// <summary>Gets "Expand all"</summary>
    internal static string DocStructure_ExpandAllToolTip
        => ResourceManager.GetString(nameof(DocStructure_ExpandAllToolTip), _resourceCulture)!;

    /// <summary>Gets "Filter blocks…"</summary>
    internal static string DocStructure_FilterPlaceholder
        => ResourceManager.GetString(nameof(DocStructure_FilterPlaceholder), _resourceCulture)!;

    /// <summary>Gets "Clear filter"</summary>
    internal static string DocStructure_ClearFilterToolTip
        => ResourceManager.GetString(nameof(DocStructure_ClearFilterToolTip), _resourceCulture)!;

    /// <summary>Gets "Navigate to text"</summary>
    internal static string DocStructure_NavigateMenuHeader
        => ResourceManager.GetString(nameof(DocStructure_NavigateMenuHeader), _resourceCulture)!;

    /// <summary>Gets "Jump to hex offset"</summary>
    internal static string DocStructure_JumpHexMenuHeader
        => ResourceManager.GetString(nameof(DocStructure_JumpHexMenuHeader), _resourceCulture)!;

    /// <summary>Gets "Copy block text"</summary>
    internal static string DocStructure_CopyTextMenuHeader
        => ResourceManager.GetString(nameof(DocStructure_CopyTextMenuHeader), _resourceCulture)!;

    /// <summary>Gets "Copy offset"</summary>
    internal static string DocStructure_CopyOffsetMenuHeader
        => ResourceManager.GetString(nameof(DocStructure_CopyOffsetMenuHeader), _resourceCulture)!;

    // ─── Runtime strings (DocumentEditorHost.xaml.cs + DocumentStatusBar.xaml.cs) ──

    /// <summary>Gets "Document"</summary>
    internal static string DocEditorHost_DocumentTitle
        => ResourceManager.GetString(nameof(DocEditorHost_DocumentTitle), _resourceCulture)!;

    /// <summary>Gets "Saved — {0}"</summary>
    internal static string DocEditorHost_SavedStatus
        => ResourceManager.GetString(nameof(DocEditorHost_SavedStatus), _resourceCulture)!;

    /// <summary>Gets "Save failed: {0}"</summary>
    internal static string DocEditorHost_SaveFailedStatus
        => ResourceManager.GetString(nameof(DocEditorHost_SaveFailedStatus), _resourceCulture)!;

    /// <summary>Gets "No saver registered for this file type."</summary>
    internal static string DocEditorHost_NoSaverStatus
        => ResourceManager.GetString(nameof(DocEditorHost_NoSaverStatus), _resourceCulture)!;

    /// <summary>Gets "Loading document…"</summary>
    internal static string DocEditorHost_LoadingMessage
        => ResourceManager.GetString(nameof(DocEditorHost_LoadingMessage), _resourceCulture)!;

    /// <summary>Gets "Waiting for IDE to initialize…"</summary>
    internal static string DocEditorHost_WaitingForIDE
        => ResourceManager.GetString(nameof(DocEditorHost_WaitingForIDE), _resourceCulture)!;

    /// <summary>Gets "Failed to open document: {0}"</summary>
    internal static string DocEditorHost_LoadErrorMessage
        => ResourceManager.GetString(nameof(DocEditorHost_LoadErrorMessage), _resourceCulture)!;

    /// <summary>Gets "Export Document"</summary>
    internal static string DocEditorHost_ExportDialogTitle
        => ResourceManager.GetString(nameof(DocEditorHost_ExportDialogTitle), _resourceCulture)!;

    /// <summary>Gets "Export failed: {0}"</summary>
    internal static string DocEditorHost_ExportFailedStatus
        => ResourceManager.GetString(nameof(DocEditorHost_ExportFailedStatus), _resourceCulture)!;

    /// <summary>Gets "Exported — {0}"</summary>
    internal static string DocEditorHost_ExportedStatus
        => ResourceManager.GetString(nameof(DocEditorHost_ExportedStatus), _resourceCulture)!;

    /// <summary>Gets "No saver registered for '{0}'."</summary>
    internal static string DocEditorHost_NoSaverForExport
        => ResourceManager.GetString(nameof(DocEditorHost_NoSaverForExport), _resourceCulture)!;

    /// <summary>Gets "—"</summary>
    internal static string DocEditorHost_MetaEmptyValue
        => ResourceManager.GetString(nameof(DocEditorHost_MetaEmptyValue), _resourceCulture)!;

    /// <summary>Gets "Yes"</summary>
    internal static string DocEditorHost_MetaYesValue
        => ResourceManager.GetString(nameof(DocEditorHost_MetaYesValue), _resourceCulture)!;

    /// <summary>Gets "No"</summary>
    internal static string DocEditorHost_MetaNoValue
        => ResourceManager.GetString(nameof(DocEditorHost_MetaNoValue), _resourceCulture)!;

    /// <summary>Gets "Text"</summary>
    internal static string DocEditorHost_ViewModeText
        => ResourceManager.GetString(nameof(DocEditorHost_ViewModeText), _resourceCulture)!;

    /// <summary>Gets "Split"</summary>
    internal static string DocEditorHost_ViewModeSplit
        => ResourceManager.GetString(nameof(DocEditorHost_ViewModeSplit), _resourceCulture)!;

    /// <summary>Gets "Hex"</summary>
    internal static string DocEditorHost_ViewModeHex
        => ResourceManager.GetString(nameof(DocEditorHost_ViewModeHex), _resourceCulture)!;

    /// <summary>Gets "Structure"</summary>
    internal static string DocEditorHost_ViewModeStructure
        => ResourceManager.GetString(nameof(DocEditorHost_ViewModeStructure), _resourceCulture)!;

    /// <summary>Gets "Full"</summary>
    internal static string DocEditorHost_ViewModeFull
        => ResourceManager.GetString(nameof(DocEditorHost_ViewModeFull), _resourceCulture)!;

    /// <summary>Gets "Focus"</summary>
    internal static string DocEditorHost_ViewModeFocus
        => ResourceManager.GetString(nameof(DocEditorHost_ViewModeFocus), _resourceCulture)!;

    /// <summary>Gets " | Read Only"</summary>
    internal static string DocEditorHost_ReadOnlySuffix
        => ResourceManager.GetString(nameof(DocEditorHost_ReadOnlySuffix), _resourceCulture)!;

    /// <summary>Gets "Page 1"</summary>
    internal static string DocStatusBar_DefaultPage
        => ResourceManager.GetString(nameof(DocStatusBar_DefaultPage), _resourceCulture)!;

    /// <summary>Gets "0 words"</summary>
    internal static string DocStatusBar_DefaultWordCount
        => ResourceManager.GetString(nameof(DocStatusBar_DefaultWordCount), _resourceCulture)!;

    /// <summary>Gets "100%"</summary>
    internal static string DocStatusBar_DefaultZoom
        => ResourceManager.GetString(nameof(DocStatusBar_DefaultZoom), _resourceCulture)!;

    /// <summary>Gets "Split"</summary>
    internal static string DocStatusBar_DefaultViewMode
        => ResourceManager.GetString(nameof(DocStatusBar_DefaultViewMode), _resourceCulture)!;

    /// <summary>Gets "Format: {0} ({1})"</summary>
    internal static string DocStatusBar_FormatPattern
        => ResourceManager.GetString(nameof(DocStatusBar_FormatPattern), _resourceCulture)!;

    /// <summary>Gets "{0:N0} words"</summary>
    internal static string DocStatusBar_WordCountPattern
        => ResourceManager.GetString(nameof(DocStatusBar_WordCountPattern), _resourceCulture)!;

    /// <summary>Gets "Page 1 / {0}"</summary>
    internal static string DocStatusBar_PagePattern
        => ResourceManager.GetString(nameof(DocStatusBar_PagePattern), _resourceCulture)!;

    /// <summary>Gets "{0:0}%"</summary>
    internal static string DocStatusBar_ZoomPattern
        => ResourceManager.GetString(nameof(DocStatusBar_ZoomPattern), _resourceCulture)!;

    /// <summary>Gets "Hex  |  0x{0:X8}"</summary>
    internal static string DocHexPane_OffsetPattern
        => ResourceManager.GetString(nameof(DocHexPane_OffsetPattern), _resourceCulture)!;

    // ─── DocumentEditorHost.Contributors.cs — status bar labels ──────────────

    /// <summary>Gets "Format"</summary>
    internal static string DocSb_FormatLabel
        => ResourceManager.GetString(nameof(DocSb_FormatLabel), _resourceCulture)!;

    /// <summary>Gets "Version"</summary>
    internal static string DocSb_VersionLabel
        => ResourceManager.GetString(nameof(DocSb_VersionLabel), _resourceCulture)!;

    /// <summary>Gets "Blocks"</summary>
    internal static string DocSb_BlocksLabel
        => ResourceManager.GetString(nameof(DocSb_BlocksLabel), _resourceCulture)!;

    /// <summary>Gets "Selection"</summary>
    internal static string DocSb_SelectionLabel
        => ResourceManager.GetString(nameof(DocSb_SelectionLabel), _resourceCulture)!;

    /// <summary>Gets "Alerts"</summary>
    internal static string DocSb_AlertsLabel
        => ResourceManager.GetString(nameof(DocSb_AlertsLabel), _resourceCulture)!;

    /// <summary>Gets "View"</summary>
    internal static string DocSb_ViewLabel
        => ResourceManager.GetString(nameof(DocSb_ViewLabel), _resourceCulture)!;

    // ─── DocumentEditorHost.Contributors.cs — toolbar tooltips ───────────────

    /// <summary>Gets "Text view"</summary>
    internal static string DocTb_TextView
        => ResourceManager.GetString(nameof(DocTb_TextView), _resourceCulture)!;

    /// <summary>Gets "Split view"</summary>
    internal static string DocTb_SplitView
        => ResourceManager.GetString(nameof(DocTb_SplitView), _resourceCulture)!;

    /// <summary>Gets "Hex view"</summary>
    internal static string DocTb_HexView
        => ResourceManager.GetString(nameof(DocTb_HexView), _resourceCulture)!;

    /// <summary>Gets "Structure view"</summary>
    internal static string DocTb_StructureView
        => ResourceManager.GetString(nameof(DocTb_StructureView), _resourceCulture)!;

    /// <summary>Gets "Forensic mode"</summary>
    internal static string DocTb_ForensicMode
        => ResourceManager.GetString(nameof(DocTb_ForensicMode), _resourceCulture)!;

    /// <summary>Gets "Save"</summary>
    internal static string DocTb_Save
        => ResourceManager.GetString(nameof(DocTb_Save), _resourceCulture)!;

    // ─── DocumentStylesPanel.xaml.cs ──────────────────────────────────────────

    /// <summary>Gets "Normal"</summary>
    internal static string DocStyles_Normal
        => ResourceManager.GetString(nameof(DocStyles_Normal), _resourceCulture)!;

    /// <summary>Gets "Heading 1"</summary>
    internal static string DocStyles_Heading1
        => ResourceManager.GetString(nameof(DocStyles_Heading1), _resourceCulture)!;

    /// <summary>Gets "Heading 2"</summary>
    internal static string DocStyles_Heading2
        => ResourceManager.GetString(nameof(DocStyles_Heading2), _resourceCulture)!;

    /// <summary>Gets "Heading 3"</summary>
    internal static string DocStyles_Heading3
        => ResourceManager.GetString(nameof(DocStyles_Heading3), _resourceCulture)!;

    /// <summary>Gets "Heading 4"</summary>
    internal static string DocStyles_Heading4
        => ResourceManager.GetString(nameof(DocStyles_Heading4), _resourceCulture)!;

    /// <summary>Gets "Heading 5"</summary>
    internal static string DocStyles_Heading5
        => ResourceManager.GetString(nameof(DocStyles_Heading5), _resourceCulture)!;

    /// <summary>Gets "Heading 6"</summary>
    internal static string DocStyles_Heading6
        => ResourceManager.GetString(nameof(DocStyles_Heading6), _resourceCulture)!;

    /// <summary>Gets "Quote"</summary>
    internal static string DocStyles_Quote
        => ResourceManager.GetString(nameof(DocStyles_Quote), _resourceCulture)!;

    /// <summary>Gets "Code"</summary>
    internal static string DocStyles_Code
        => ResourceManager.GetString(nameof(DocStyles_Code), _resourceCulture)!;

    /// <summary>Gets "Caption"</summary>
    internal static string DocStyles_Caption
        => ResourceManager.GetString(nameof(DocStyles_Caption), _resourceCulture)!;

    /// <summary>Gets "List Paragraph"</summary>
    internal static string DocStyles_ListParagraph
        => ResourceManager.GetString(nameof(DocStyles_ListParagraph), _resourceCulture)!;

    /// <summary>Gets "Intense Quote"</summary>
    internal static string DocStyles_IntenseQuote
        => ResourceManager.GetString(nameof(DocStyles_IntenseQuote), _resourceCulture)!;
}
