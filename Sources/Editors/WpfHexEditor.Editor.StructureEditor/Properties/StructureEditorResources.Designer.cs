///////////////////////////////////////////////////////////////
// GNU Affero General Public License v3.0  2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Project     : WpfHexEditor.Editor.StructureEditor
// File        : Properties/StructureEditorResources.Designer.cs
// Description : Auto-generated strongly-typed resource accessor for
//               StructureEditorResources.resx.
// Architecture: Internal static class — one property per resource key.
//               Consumers reference SR.StructureEditorResources.<Key>.
///////////////////////////////////////////////////////////////

namespace WpfHexEditor.Editor.StructureEditor.Properties
{
    using System.Globalization;
    using System.Resources;

    /// <summary>
    /// Strongly-typed accessor for StructureEditorResources.resx.
    /// All members are thread-safe (ResourceManager is thread-safe by design).
    /// </summary>
    internal static class StructureEditorResources
    {
        private static ResourceManager? _resourceManager;
        private static CultureInfo? _resourceCulture;

        internal static ResourceManager ResourceManager
        {
            get
            {
                _resourceManager ??= new ResourceManager(
                    "WpfHexEditor.Editor.StructureEditor.Properties.StructureEditorResources",
                    typeof(StructureEditorResources).Assembly);
                return _resourceManager;
            }
        }

        /// <summary>
        /// Overrides the current thread's culture for all resource lookups.
        /// Leave null to use <see cref="CultureInfo.CurrentUICulture"/>.
        /// </summary>
        internal static CultureInfo? Culture
        {
            get => _resourceCulture;
            set => _resourceCulture = value;
        }

        private static string GetString(string name) =>
            ResourceManager.GetString(name, _resourceCulture) ?? name;

        // ── Add Block Dialog ─────────────────────────────────────────────────

        /// <summary>Add Block</summary>
        internal static string StructureEditor_AddBlockTitle =>
            GetString(nameof(StructureEditor_AddBlockTitle));

        /// <summary>Add</summary>
        internal static string StructureEditor_AddBlockButton =>
            GetString(nameof(StructureEditor_AddBlockButton));

        /// <summary>Cancel</summary>
        internal static string StructureEditor_AddBlockCancel =>
            GetString(nameof(StructureEditor_AddBlockCancel));

        // ── Tabs ─────────────────────────────────────────────────────────────

        /// <summary>Metadata</summary>
        internal static string StructureEditor_TabMetadata =>
            GetString(nameof(StructureEditor_TabMetadata));

        /// <summary>Detection</summary>
        internal static string StructureEditor_TabDetection =>
            GetString(nameof(StructureEditor_TabDetection));

        /// <summary>Blocks</summary>
        internal static string StructureEditor_TabBlocks =>
            GetString(nameof(StructureEditor_TabBlocks));

        /// <summary>Variables</summary>
        internal static string StructureEditor_TabVariables =>
            GetString(nameof(StructureEditor_TabVariables));

        /// <summary>Advanced</summary>
        internal static string StructureEditor_TabAdvanced =>
            GetString(nameof(StructureEditor_TabAdvanced));

        /// <summary>Quality</summary>
        internal static string StructureEditor_TabQuality =>
            GetString(nameof(StructureEditor_TabQuality));

        /// <summary>Test</summary>
        internal static string StructureEditor_TabTest =>
            GetString(nameof(StructureEditor_TabTest));

        // ── Toolbar ToolTips ─────────────────────────────────────────────────

        /// <summary>Save (Ctrl+S)</summary>
        internal static string StructureEditor_SaveToolTip =>
            GetString(nameof(StructureEditor_SaveToolTip));

        /// <summary>Validate (Ctrl+Shift+V)</summary>
        internal static string StructureEditor_ValidateToolTip =>
            GetString(nameof(StructureEditor_ValidateToolTip));

        /// <summary>Undo (Ctrl+Z)</summary>
        internal static string StructureEditor_UndoToolTip =>
            GetString(nameof(StructureEditor_UndoToolTip));

        /// <summary>Redo (Ctrl+Y)</summary>
        internal static string StructureEditor_RedoToolTip =>
            GetString(nameof(StructureEditor_RedoToolTip));

        /// <summary>Toggle Live Code View</summary>
        internal static string StructureEditor_LiveCodeViewToolTip =>
            GetString(nameof(StructureEditor_LiveCodeViewToolTip));

        /// <summary>Add block (Ctrl+N)</summary>
        internal static string StructureEditor_AddBlockToolTip =>
            GetString(nameof(StructureEditor_AddBlockToolTip));

        /// <summary>Duplicate block (Ctrl+D)</summary>
        internal static string StructureEditor_DuplicateBlockToolTip =>
            GetString(nameof(StructureEditor_DuplicateBlockToolTip));

        /// <summary>Unsaved changes</summary>
        internal static string StructureEditor_UnsavedChangesToolTip =>
            GetString(nameof(StructureEditor_UnsavedChangesToolTip));

        /// <summary>Filter blocks by name</summary>
        internal static string StructureEditor_BlocksFilterToolTip =>
            GetString(nameof(StructureEditor_BlocksFilterToolTip));

        // ── Blocks Context Menu ───────────────────────────────────────────────

        /// <summary>Add Block...</summary>
        internal static string StructureEditor_BlocksAddMenuItem =>
            GetString(nameof(StructureEditor_BlocksAddMenuItem));

        /// <summary>Duplicate</summary>
        internal static string StructureEditor_BlocksDuplicateMenuItem =>
            GetString(nameof(StructureEditor_BlocksDuplicateMenuItem));

        /// <summary>Remove</summary>
        internal static string StructureEditor_BlocksRemoveMenuItem =>
            GetString(nameof(StructureEditor_BlocksRemoveMenuItem));

        /// <summary>Move Up</summary>
        internal static string StructureEditor_BlocksMoveUpMenuItem =>
            GetString(nameof(StructureEditor_BlocksMoveUpMenuItem));

        /// <summary>Move Down</summary>
        internal static string StructureEditor_BlocksMoveDownMenuItem =>
            GetString(nameof(StructureEditor_BlocksMoveDownMenuItem));

        /// <summary>Copy as JSON</summary>
        internal static string StructureEditor_BlocksCopyJsonMenuItem =>
            GetString(nameof(StructureEditor_BlocksCopyJsonMenuItem));

        /// <summary>Paste Block</summary>
        internal static string StructureEditor_BlocksPasteMenuItem =>
            GetString(nameof(StructureEditor_BlocksPasteMenuItem));

        // ── Blocks Empty State ────────────────────────────────────────────────

        /// <summary>Select a block to edit its properties,</summary>
        internal static string StructureEditor_BlocksEmptyLine1 =>
            GetString(nameof(StructureEditor_BlocksEmptyLine1));

        /// <summary>or press Ctrl+N / right-click to add a new block.</summary>
        internal static string StructureEditor_BlocksEmptyLine2 =>
            GetString(nameof(StructureEditor_BlocksEmptyLine2));

        // ── Detection Tab ─────────────────────────────────────────────────────

        /// <summary>Hex Signature</summary>
        internal static string StructureEditor_DetectionHexSigLabel =>
            GetString(nameof(StructureEditor_DetectionHexSigLabel));

        /// <summary>Hex bytes without spaces, e.g. 89504E47 for PNG</summary>
        internal static string StructureEditor_DetectionHexBytesToolTip =>
            GetString(nameof(StructureEditor_DetectionHexBytesToolTip));

        /// <summary>Signature</summary>
        internal static string StructureEditor_DetectionSigLabel =>
            GetString(nameof(StructureEditor_DetectionSigLabel));

        /// <summary>v2.0 Signatures</summary>
        internal static string StructureEditor_DetectionV2SigsLabel =>
            GetString(nameof(StructureEditor_DetectionV2SigsLabel));

        /// <summary>Match Mode</summary>
        internal static string StructureEditor_DetectionMatchModeLabel =>
            GetString(nameof(StructureEditor_DetectionMatchModeLabel));

        /// <summary>Hex value</summary>
        internal static string StructureEditor_DetectionHexValueToolTip =>
            GetString(nameof(StructureEditor_DetectionHexValueToolTip));

        /// <summary>Offset</summary>
        internal static string StructureEditor_DetectionOffsetToolTip =>
            GetString(nameof(StructureEditor_DetectionOffsetToolTip));

        /// <summary>Label</summary>
        internal static string StructureEditor_DetectionLabelToolTip =>
            GetString(nameof(StructureEditor_DetectionLabelToolTip));

        /// <summary>＋ Add Signature</summary>
        internal static string StructureEditor_DetectionAddSigButton =>
            GetString(nameof(StructureEditor_DetectionAddSigButton));

        /// <summary>Entropy Hint</summary>
        internal static string StructureEditor_DetectionEntropyLabel =>
            GetString(nameof(StructureEditor_DetectionEntropyLabel));

        /// <summary>Min Entropy</summary>
        internal static string StructureEditor_DetectionMinEntropyLabel =>
            GetString(nameof(StructureEditor_DetectionMinEntropyLabel));

        /// <summary>Max Entropy</summary>
        internal static string StructureEditor_DetectionMaxEntropyLabel =>
            GetString(nameof(StructureEditor_DetectionMaxEntropyLabel));

        /// <summary>Minimum Score</summary>
        internal static string StructureEditor_DetectionMinScoreLabel =>
            GetString(nameof(StructureEditor_DetectionMinScoreLabel));

        /// <summary>Score Threshold</summary>
        internal static string StructureEditor_DetectionScoreThresholdLabel =>
            GetString(nameof(StructureEditor_DetectionScoreThresholdLabel));

        /// <summary>Content Patterns (text formats)</summary>
        internal static string StructureEditor_DetectionContentPatternsLabel =>
            GetString(nameof(StructureEditor_DetectionContentPatternsLabel));

        /// <summary>Regex pattern</summary>
        internal static string StructureEditor_DetectionRegexToolTip =>
            GetString(nameof(StructureEditor_DetectionRegexToolTip));

        /// <summary>＋ Add Pattern</summary>
        internal static string StructureEditor_DetectionAddPatternButton =>
            GetString(nameof(StructureEditor_DetectionAddPatternButton));

        // ── Metadata Tab ──────────────────────────────────────────────────────

        /// <summary>Core</summary>
        internal static string StructureEditor_MetaCoreSection =>
            GetString(nameof(StructureEditor_MetaCoreSection));

        /// <summary>Format Name</summary>
        internal static string StructureEditor_MetaNameLabel =>
            GetString(nameof(StructureEditor_MetaNameLabel));

        /// <summary>Human-readable format name (e.g., 'ZIP Archive', 'PNG Image')</summary>
        internal static string StructureEditor_MetaNameToolTip =>
            GetString(nameof(StructureEditor_MetaNameToolTip));

        /// <summary>Version</summary>
        internal static string StructureEditor_MetaVersionLabel =>
            GetString(nameof(StructureEditor_MetaVersionLabel));

        /// <summary>Version of this definition (e.g., '1.0', '2.0')</summary>
        internal static string StructureEditor_MetaVersionToolTip =>
            GetString(nameof(StructureEditor_MetaVersionToolTip));

        /// <summary>Author</summary>
        internal static string StructureEditor_MetaAuthorLabel =>
            GetString(nameof(StructureEditor_MetaAuthorLabel));

        /// <summary>Author of this definition file</summary>
        internal static string StructureEditor_MetaAuthorToolTip =>
            GetString(nameof(StructureEditor_MetaAuthorToolTip));

        /// <summary>Category</summary>
        internal static string StructureEditor_MetaCategoryLabel =>
            GetString(nameof(StructureEditor_MetaCategoryLabel));

        /// <summary>Category (auto-set from folder structure: Archives, Images, Audio, Video, etc.)</summary>
        internal static string StructureEditor_MetaCategoryToolTip =>
            GetString(nameof(StructureEditor_MetaCategoryToolTip));

        /// <summary>Diff Mode</summary>
        internal static string StructureEditor_MetaDiffModeLabel =>
            GetString(nameof(StructureEditor_MetaDiffModeLabel));

        /// <summary>Preferred diff algorithm for DiffViewer</summary>
        internal static string StructureEditor_MetaDiffModeToolTip =>
            GetString(nameof(StructureEditor_MetaDiffModeToolTip));

        /// <summary>Preferred Editor</summary>
        internal static string StructureEditor_MetaPreferredEditorLabel =>
            GetString(nameof(StructureEditor_MetaPreferredEditorLabel));

        /// <summary>Preferred editor factory ID for files matching this format</summary>
        internal static string StructureEditor_MetaPreferredEditorToolTip =>
            GetString(nameof(StructureEditor_MetaPreferredEditorToolTip));

        /// <summary>Description</summary>
        internal static string StructureEditor_MetaDescriptionLabel =>
            GetString(nameof(StructureEditor_MetaDescriptionLabel));

        /// <summary>Is text format (opens in Code Editor by default)</summary>
        internal static string StructureEditor_MetaIsTextFormat =>
            GetString(nameof(StructureEditor_MetaIsTextFormat));

        /// <summary>File Extensions</summary>
        internal static string StructureEditor_MetaExtensionsLabel =>
            GetString(nameof(StructureEditor_MetaExtensionsLabel));

        /// <summary>＋ Add Extension</summary>
        internal static string StructureEditor_MetaAddExtensionButton =>
            GetString(nameof(StructureEditor_MetaAddExtensionButton));

        /// <summary>MIME Types</summary>
        internal static string StructureEditor_MetaMimeTypesLabel =>
            GetString(nameof(StructureEditor_MetaMimeTypesLabel));

        /// <summary>＋ Add MIME Type</summary>
        internal static string StructureEditor_MetaAddMimeButton =>
            GetString(nameof(StructureEditor_MetaAddMimeButton));

        /// <summary>Software</summary>
        internal static string StructureEditor_MetaSoftwareLabel =>
            GetString(nameof(StructureEditor_MetaSoftwareLabel));

        /// <summary>＋ Add Software</summary>
        internal static string StructureEditor_MetaAddSoftwareButton =>
            GetString(nameof(StructureEditor_MetaAddSoftwareButton));

        // ── Quality Tab ───────────────────────────────────────────────────────

        /// <summary>Computed Metrics</summary>
        internal static string StructureEditor_QualityComputedSection =>
            GetString(nameof(StructureEditor_QualityComputedSection));

        /// <summary>Block Coverage</summary>
        internal static string StructureEditor_QualityBlockCoverage =>
            GetString(nameof(StructureEditor_QualityBlockCoverage));

        /// <summary>Completeness Score</summary>
        internal static string StructureEditor_QualityCompletenessScore =>
            GetString(nameof(StructureEditor_QualityCompletenessScore));

        /// <summary>Type Distribution</summary>
        internal static string StructureEditor_QualityTypeDistribution =>
            GetString(nameof(StructureEditor_QualityTypeDistribution));

        /// <summary>Blocks</summary>
        internal static string StructureEditor_QualityBlocksLabel =>
            GetString(nameof(StructureEditor_QualityBlocksLabel));

        /// <summary>Depth</summary>
        internal static string StructureEditor_QualityDepthLabel =>
            GetString(nameof(StructureEditor_QualityDepthLabel));

        /// <summary>Variables</summary>
        internal static string StructureEditor_QualityVariablesLabel =>
            GetString(nameof(StructureEditor_QualityVariablesLabel));

        /// <summary>Rules</summary>
        internal static string StructureEditor_QualityRulesLabel =>
            GetString(nameof(StructureEditor_QualityRulesLabel));

        /// <summary>Metadata</summary>
        internal static string StructureEditor_QualityMetadataLabel =>
            GetString(nameof(StructureEditor_QualityMetadataLabel));

        /// <summary>Documentation Level</summary>
        internal static string StructureEditor_QualityDocLevel =>
            GetString(nameof(StructureEditor_QualityDocLevel));

        /// <summary>Last Updated</summary>
        internal static string StructureEditor_QualityLastUpdated =>
            GetString(nameof(StructureEditor_QualityLastUpdated));

        /// <summary>Format: YYYY-MM-DD (auto-filled on save)</summary>
        internal static string StructureEditor_QualityLastUpdatedToolTip =>
            GetString(nameof(StructureEditor_QualityLastUpdatedToolTip));

        /// <summary>Priority Format (top 100)</summary>
        internal static string StructureEditor_QualityPriorityFormat =>
            GetString(nameof(StructureEditor_QualityPriorityFormat));

        /// <summary>Auto-refined (enriched by script — read-only)</summary>
        internal static string StructureEditor_QualityAutoRefined =>
            GetString(nameof(StructureEditor_QualityAutoRefined));

        // ── Test Tab ──────────────────────────────────────────────────────────

        /// <summary>Test Format Against File</summary>
        internal static string StructureEditor_TestSection =>
            GetString(nameof(StructureEditor_TestSection));

        /// <summary>Path to the binary file to test</summary>
        internal static string StructureEditor_TestFilePathToolTip =>
            GetString(nameof(StructureEditor_TestFilePathToolTip));

        /// <summary>Browse…</summary>
        internal static string StructureEditor_TestBrowseButton =>
            GetString(nameof(StructureEditor_TestBrowseButton));

        /// <summary>▶ Run Test</summary>
        internal static string StructureEditor_TestRunButton =>
            GetString(nameof(StructureEditor_TestRunButton));

        /// <summary>Copy results to clipboard (TSV)</summary>
        internal static string StructureEditor_TestCopyToolTip =>
            GetString(nameof(StructureEditor_TestCopyToolTip));

        /// <summary>Select a file and click ▶ Run Test to test the current format definition.</summary>
        internal static string StructureEditor_TestEmptyMessage =>
            GetString(nameof(StructureEditor_TestEmptyMessage));

        /// <summary>Status</summary>
        internal static string StructureEditor_TestColStatus =>
            GetString(nameof(StructureEditor_TestColStatus));

        /// <summary>Block Name</summary>
        internal static string StructureEditor_TestColBlockName =>
            GetString(nameof(StructureEditor_TestColBlockName));

        /// <summary>Type</summary>
        internal static string StructureEditor_TestColType =>
            GetString(nameof(StructureEditor_TestColType));

        /// <summary>Offset</summary>
        internal static string StructureEditor_TestColOffset =>
            GetString(nameof(StructureEditor_TestColOffset));

        /// <summary>Len</summary>
        internal static string StructureEditor_TestColLen =>
            GetString(nameof(StructureEditor_TestColLen));

        /// <summary>Raw (hex)</summary>
        internal static string StructureEditor_TestColRaw =>
            GetString(nameof(StructureEditor_TestColRaw));

        /// <summary>Parsed Value</summary>
        internal static string StructureEditor_TestColParsed =>
            GetString(nameof(StructureEditor_TestColParsed));

        /// <summary>Note</summary>
        internal static string StructureEditor_TestColNote =>
            GetString(nameof(StructureEditor_TestColNote));

        /// <summary>✓ OK</summary>
        internal static string StructureEditor_TestFilterOk =>
            GetString(nameof(StructureEditor_TestFilterOk));

        /// <summary>⚠ Warning</summary>
        internal static string StructureEditor_TestFilterWarning =>
            GetString(nameof(StructureEditor_TestFilterWarning));

        /// <summary>✕ Error</summary>
        internal static string StructureEditor_TestFilterError =>
            GetString(nameof(StructureEditor_TestFilterError));

        /// <summary>⊘ Skipped</summary>
        internal static string StructureEditor_TestFilterSkipped =>
            GetString(nameof(StructureEditor_TestFilterSkipped));

        // ── Variables Tab ─────────────────────────────────────────────────────

        /// <summary>Initial variables available to the block interpreter (e.g. loop counters, computed offsets).</summary>
        internal static string StructureEditor_VarsDescription =>
            GetString(nameof(StructureEditor_VarsDescription));

        /// <summary>Filter variables by name</summary>
        internal static string StructureEditor_VarsFilterToolTip =>
            GetString(nameof(StructureEditor_VarsFilterToolTip));

        /// <summary>Variable Name</summary>
        internal static string StructureEditor_VarsColName =>
            GetString(nameof(StructureEditor_VarsColName));

        /// <summary>Type</summary>
        internal static string StructureEditor_VarsColType =>
            GetString(nameof(StructureEditor_VarsColType));

        /// <summary>Initial Value</summary>
        internal static string StructureEditor_VarsColInitialValue =>
            GetString(nameof(StructureEditor_VarsColInitialValue));

        /// <summary>Description</summary>
        internal static string StructureEditor_VarsColDescription =>
            GetString(nameof(StructureEditor_VarsColDescription));

        /// <summary>＋ Add Variable</summary>
        internal static string StructureEditor_VarsAddButton =>
            GetString(nameof(StructureEditor_VarsAddButton));

        // ── Advanced Tab — v2 sections ────────────────────────────────────────

        /// <summary>Assertions</summary>
        internal static string StructureEditor_V2AssertionsHeader =>
            GetString(nameof(StructureEditor_V2AssertionsHeader));

        /// <summary>var:name equals 0 or calc:expr</summary>
        internal static string StructureEditor_V2AssertionPlaceholder =>
            GetString(nameof(StructureEditor_V2AssertionPlaceholder));

        /// <summary>Boolean expression — Ctrl+Space for functions and variables</summary>
        internal static string StructureEditor_V2AssertionToolTip =>
            GetString(nameof(StructureEditor_V2AssertionToolTip));

        /// <summary>Checksums</summary>
        internal static string StructureEditor_V2ChecksumsHeader =>
            GetString(nameof(StructureEditor_V2ChecksumsHeader));

        /// <summary>Versioning (VersionDetection / VersionedBlocks)</summary>
        internal static string StructureEditor_V2VersioningHeader =>
            GetString(nameof(StructureEditor_V2VersioningHeader));

        /// <summary>Imports ($ref Composition)</summary>
        internal static string StructureEditor_V2ImportsHeader =>
            GetString(nameof(StructureEditor_V2ImportsHeader));

        /// <summary>ref = format name (or structs/Name), as = alias used in structRef blocks.</summary>
        internal static string StructureEditor_V2ImportsDescription =>
            GetString(nameof(StructureEditor_V2ImportsDescription));

        /// <summary>Format ref e.g. structs/PE_OptionalHeader</summary>
        internal static string StructureEditor_V2ImportRefToolTip =>
            GetString(nameof(StructureEditor_V2ImportRefToolTip));

        /// <summary>Alias used in structRef e.g. PEOpt</summary>
        internal static string StructureEditor_V2ImportAliasToolTip =>
            GetString(nameof(StructureEditor_V2ImportAliasToolTip));

        /// <summary>+ Add Import</summary>
        internal static string StructureEditor_V2AddImportButton =>
            GetString(nameof(StructureEditor_V2AddImportButton));

        /// <summary>Forensic</summary>
        internal static string StructureEditor_V2ForensicHeader =>
            GetString(nameof(StructureEditor_V2ForensicHeader));

        /// <summary>Navigation Bookmarks</summary>
        internal static string StructureEditor_V2NavBookmarksHeader =>
            GetString(nameof(StructureEditor_V2NavBookmarksHeader));

        /// <summary>variableName</summary>
        internal static string StructureEditor_V2BookmarkPlaceholder =>
            GetString(nameof(StructureEditor_V2BookmarkPlaceholder));

        /// <summary>Variable holding the byte offset — Ctrl+Space for suggestions</summary>
        internal static string StructureEditor_V2BookmarkToolTip =>
            GetString(nameof(StructureEditor_V2BookmarkToolTip));

        /// <summary>＋ Add Bookmark</summary>
        internal static string StructureEditor_V2AddBookmarkButton =>
            GetString(nameof(StructureEditor_V2AddBookmarkButton));

        /// <summary>Inspector Layout</summary>
        internal static string StructureEditor_V2InspectorLayoutHeader =>
            GetString(nameof(StructureEditor_V2InspectorLayoutHeader));

        /// <summary>Export Templates</summary>
        internal static string StructureEditor_V2ExportTemplatesHeader =>
            GetString(nameof(StructureEditor_V2ExportTemplatesHeader));

        /// <summary>＋ Add Template</summary>
        internal static string StructureEditor_V2AddTemplateButton =>
            GetString(nameof(StructureEditor_V2AddTemplateButton));

        /// <summary>AI Hints</summary>
        internal static string StructureEditor_V2AIHintsHeader =>
            GetString(nameof(StructureEditor_V2AIHintsHeader));

        // ── Block Type Hints ─────────────────────────────────────────────────

        /// <summary>A binary field with a fixed or variable offset and length.</summary>
        internal static string StructureEditor_TypeHintField =>
            GetString(nameof(StructureEditor_TypeHintField));

        /// <summary>Magic bytes that identify the format at a specific offset.</summary>
        internal static string StructureEditor_TypeHintSignature =>
            GetString(nameof(StructureEditor_TypeHintSignature));

        /// <summary>Reads a variable-length or symbolic value into a named variable.</summary>
        internal static string StructureEditor_TypeHintMetadata =>
            GetString(nameof(StructureEditor_TypeHintMetadata));

        /// <summary>Conditionally parses blocks based on a field value or variable.</summary>
        internal static string StructureEditor_TypeHintConditional =>
            GetString(nameof(StructureEditor_TypeHintConditional));

        /// <summary>Repeats a block body while a condition is true.</summary>
        internal static string StructureEditor_TypeHintLoop =>
            GetString(nameof(StructureEditor_TypeHintLoop));

        /// <summary>Modifies a variable (increment, decrement, setVariable).</summary>
        internal static string StructureEditor_TypeHintAction =>
            GetString(nameof(StructureEditor_TypeHintAction));

        /// <summary>Evaluates a math expression and stores the result.</summary>
        internal static string StructureEditor_TypeHintCompute =>
            GetString(nameof(StructureEditor_TypeHintCompute));

        /// <summary>Parses a fixed-count array of structured entries.</summary>
        internal static string StructureEditor_TypeHintRepeating =>
            GetString(nameof(StructureEditor_TypeHintRepeating));

        /// <summary>Selects a variant block set based on a discriminant variable.</summary>
        internal static string StructureEditor_TypeHintUnion =>
            GetString(nameof(StructureEditor_TypeHintUnion));

        /// <summary>Embeds an external struct definition by reference.</summary>
        internal static string StructureEditor_TypeHintNested =>
            GetString(nameof(StructureEditor_TypeHintNested));

        /// <summary>Creates a navigation annotation to a pointed-to offset.</summary>
        internal static string StructureEditor_TypeHintPointer =>
            GetString(nameof(StructureEditor_TypeHintPointer));

        // ── Validation / Status ───────────────────────────────────────────────

        /// <summary>{0} error(s)</summary>
        internal static string StructureEditor_ValidationErrorCount =>
            GetString(nameof(StructureEditor_ValidationErrorCount));

        /// <summary>{0} warning(s)</summary>
        internal static string StructureEditor_ValidationWarningCount =>
            GetString(nameof(StructureEditor_ValidationWarningCount));

        // ── Raw JSON Panel ────────────────────────────────────────────────────

        /// <summary>Raw JSON — {0}</summary>
        internal static string StructureEditor_RawJsonTitle =>
            GetString(nameof(StructureEditor_RawJsonTitle));

        /// <summary>Apply</summary>
        internal static string StructureEditor_RawJsonApply =>
            GetString(nameof(StructureEditor_RawJsonApply));

        /// <summary>Cancel</summary>
        internal static string StructureEditor_RawJsonCancel =>
            GetString(nameof(StructureEditor_RawJsonCancel));

        // ── Contributors — Toolbar tooltips ──────────────────────────────────

        /// <summary>Validate (Ctrl+Shift+V)</summary>
        internal static string StrTb_Validate =>
            GetString(nameof(StrTb_Validate));

        /// <summary>Add Block (Ctrl+N)</summary>
        internal static string StrTb_AddBlock =>
            GetString(nameof(StrTb_AddBlock));

        /// <summary>Toggle Live Code View</summary>
        internal static string StrTb_ToggleLiveCode =>
            GetString(nameof(StrTb_ToggleLiveCode));

        /// <summary>Layout</summary>
        internal static string StrTb_LayoutLabel =>
            GetString(nameof(StrTb_LayoutLabel));

        /// <summary>Code view layout</summary>
        internal static string StrTb_LayoutTooltip =>
            GetString(nameof(StrTb_LayoutTooltip));

        // ── Contributors — Toolbar layout dropdown ────────────────────────────

        /// <summary>Code Right</summary>
        internal static string StrLayout_CodeRight =>
            GetString(nameof(StrLayout_CodeRight));

        /// <summary>Code Left</summary>
        internal static string StrLayout_CodeLeft =>
            GetString(nameof(StrLayout_CodeLeft));

        /// <summary>Code Bottom</summary>
        internal static string StrLayout_CodeBottom =>
            GetString(nameof(StrLayout_CodeBottom));

        /// <summary>Code Top</summary>
        internal static string StrLayout_CodeTop =>
            GetString(nameof(StrLayout_CodeTop));

        // ── Contributors — Status bar labels ──────────────────────────────────

        /// <summary>Format</summary>
        internal static string StrSb_FormatLabel =>
            GetString(nameof(StrSb_FormatLabel));

        /// <summary>Tab</summary>
        internal static string StrSb_TabLabel =>
            GetString(nameof(StrSb_TabLabel));

        /// <summary>Blocks</summary>
        internal static string StrSb_BlocksLabel =>
            GetString(nameof(StrSb_BlocksLabel));

        /// <summary>Validation</summary>
        internal static string StrSb_ValidationLabel =>
            GetString(nameof(StrSb_ValidationLabel));

        // ── Factory Registration ──────────────────────────────────────────────

        /// <summary>Structure Editor</summary>
        internal static string StructureEditor_FactoryDisplayName =>
            GetString(nameof(StructureEditor_FactoryDisplayName));

        /// <summary>Visual editor for .whfmt structure definition files. Edit blocks, types, offsets, lengths and colors in a DataGrid.</summary>
        internal static string StructureEditor_FactoryDescription =>
            GetString(nameof(StructureEditor_FactoryDescription));
    }
}
