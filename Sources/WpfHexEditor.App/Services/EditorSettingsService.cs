// ==========================================================
// Project: WpfHexEditor.App
// File: Services/EditorSettingsService.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-29
// Description:
//     Applies user preferences from AppSettings to newly created editor controls.
//     Extracted from MainWindow.ApplyEditorSettings + ApplyHexEditorDefaults static
//     methods to keep MainWindow lean and allow future DI-based invocation.
//
// Architecture Notes:
//     Single public method: Apply(IDocumentEditor).
//     Type-dispatches to per-editor helpers (CodeEditor, TextEditor, HexEditor).
//     Reads AppSettingsService.Instance.Current — no constructor args required.
//     Registered as singleton in AppServiceCollection.
// ==========================================================

using System.Windows.Media;
using WpfHexEditor.Core.Options;
using AppSettings = WpfHexEditor.Core.Options.AppSettings;
using WpfHexEditor.Editor.Core;
using WpfHexEditor.Core.ProjectSystem.Languages;
using HexEditorControl = WpfHexEditor.HexEditor.HexEditor;

namespace WpfHexEditor.App.Services;

/// <summary>
/// Applies per-editor-type settings from <see cref="AppSettingsService"/> to
/// freshly-created <see cref="IDocumentEditor"/> instances.
/// </summary>
public sealed class EditorSettingsService
{
    /// <summary>
    /// Applies settings to <paramref name="editor"/> based on its concrete type.
    /// Safe to call with any <see cref="IDocumentEditor"/> — unknown types are ignored.
    /// </summary>
    public void Apply(IDocumentEditor editor)
    {
        var settings = AppSettingsService.Instance.Current;

        switch (editor)
        {
            case WpfHexEditor.Editor.CodeEditor.Controls.CodeEditor ce:
                ApplyCodeEditor(ce, settings);
                break;
            case WpfHexEditor.Editor.TextEditor.Controls.TextEditor te:
                ApplyTextEditor(te, settings);
                break;
            case HexEditorControl hex:
                ApplyHexEditor(hex, settings);
                break;
        }
        // CodeEditorSplitHost wraps two CodeEditor instances
        if (editor is WpfHexEditor.Editor.CodeEditor.Controls.CodeEditorSplitHost splitHost)
        {
            if (splitHost.RefreshTimeStatusBarItem is { } shRt)
                shRt.IsVisible = settings.CodeEditorDefaults.ShowRefreshRateInStatusBar;
            splitHost.ShowMinimap = settings.CodeEditorDefaults.ShowMinimap;
            splitHost.MinimapRenderCharacters = settings.CodeEditorDefaults.MinimapRenderCharacters;
            splitHost.MinimapVerticalSize = (WpfHexEditor.Editor.CodeEditor.Controls.MinimapVerticalSize)settings.CodeEditorDefaults.MinimapVerticalSize;
            splitHost.MinimapSliderMode = (WpfHexEditor.Editor.CodeEditor.Controls.MinimapSliderMode)settings.CodeEditorDefaults.MinimapSliderMode;
            splitHost.MinimapSide = (WpfHexEditor.Editor.CodeEditor.Controls.MinimapSide)settings.CodeEditorDefaults.MinimapSide;
        }
    }

    /// <summary>Applies hex-editor defaults from settings to a bare <see cref="HexEditorControl"/>.</summary>
    public void ApplyHexDefaults(HexEditorControl hex)
        => ApplyHexEditor(hex, AppSettingsService.Instance.Current);

    // ── Per-editor helpers ─────────────────────────────────────────────────────

    private static void ApplyCodeEditor(
        WpfHexEditor.Editor.CodeEditor.Controls.CodeEditor ce,
        AppSettings settings)
    {
        var d = settings.CodeEditorDefaults;
        ce.MouseWheelSpeed          = d.MouseWheelSpeed;
        ce.ZoomLevel                = d.DefaultZoom;
        ce.FoldToggleOnDoubleClick  = d.FoldToggleOnDoubleClick;
        ce.IsWordWrapEnabled        = d.WordWrap;
        ce.ShowColumnRulers         = d.ShowColumnRulers;
        ce.ShowInlineHints          = d.ShowInlineHints;
        ce.InlineHintsVisibleKinds  = d.InlineHintsVisibleKinds == 0
            ? WpfHexEditor.Editor.Core.InlineHintsSymbolKinds.All
            : (WpfHexEditor.Editor.Core.InlineHintsSymbolKinds)d.InlineHintsVisibleKinds;
        ce.InlineHintsSource        = d.InlineHintsSource;
        ce.ShowVarTypeHints         = d.ShowVarTypeHints;
        ce.ShowLambdaReturnTypeHints = d.ShowLambdaReturnTypeHints;
        ce.ShowLspInlayHints        = d.ShowLspInlayHints;
        ce.ShowLspDeclarationHints          = d.ShowLspDeclarationHints;
        ce.ShowEndOfBlockHint       = d.EndOfBlockHintEnabled;
        ce.EndOfBlockHintDelayMs    = d.EndOfBlockHintDelayMs;
        ce.ClickableLinksEnabled    = d.ClickableLinksEnabled;
        ce.ClickableEmailsEnabled   = d.ClickableEmailsEnabled;
        ce.EnableAutoClosingBrackets = d.AutoClosingBrackets;
        ce.EnableAutoClosingQuotes   = d.AutoClosingQuotes;
        ce.SkipOverClosingChar       = d.SkipOverClosingChar;
        ce.WrapSelectionInPairs          = d.WrapSelectionInPairs;
        ce.BracketPairColorizationEnabled = d.BracketPairColorization;
        ce.RainbowScopeGuidesEnabled      = d.RainbowScopeGuides;
        ce.ColorSwatchPreviewEnabled      = d.ColorSwatchPreview;
        ce.FormatOnSave                   = d.FormatOnSave;
        ce.XmlAttributeIndentLevels       = d.XmlAttributeIndentLevels;
        ce.XmlOneAttributePerLine         = d.XmlOneAttributePerLine;

        var ss = d.StickyScroll;
        ce.ApplyStickyScrollSettings(
            ss.Enabled, ss.MaxLines, ss.SyntaxHighlight,
            ss.ClickToNavigate, ss.Opacity, ss.MinScopeLines);

        ApplySyntaxColorOverrides(ce, d);
    }

    private static void ApplyTextEditor(
        WpfHexEditor.Editor.TextEditor.Controls.TextEditor te,
        AppSettings settings)
    {
        var td = settings.TextEditorDefaults;
        te.MouseWheelSpeed      = td.MouseWheelSpeed;
        te.ZoomLevel            = td.DefaultZoom;
        te.IsWordWrapEnabled    = td.WordWrap;
        te.ClickableLinksEnabled  = td.ClickableLinksEnabled;
        te.ClickableEmailsEnabled = td.ClickableEmailsEnabled;
    }

    private static void ApplyHexEditor(HexEditorControl hex, AppSettings settings)
    {
        var d = settings.HexEditorDefaults;

        // Display
        hex.BytePerLine            = d.BytePerLine;
        hex.ShowOffset             = d.ShowOffset;
        hex.ShowAscii              = d.ShowAscii;
        hex.DataStringVisual       = d.DataStringVisual;
        hex.OffSetStringVisual     = d.OffSetStringVisual;
        hex.ByteGrouping           = d.ByteGrouping;
        hex.ByteSpacerPositioning  = d.ByteSpacerPositioning;

        // Editing
        hex.EditMode               = d.DefaultEditMode;
        hex.AllowZoom              = d.AllowZoom;
        hex.MouseWheelSpeed        = d.MouseWheelSpeed;
        hex.AllowFileDrop          = d.AllowFileDrop;

        // Data interpretation
        hex.ByteSize                    = d.ByteSize;
        hex.ByteOrder                   = d.ByteOrder;
        hex.DefaultCopyToClipboardMode  = d.DefaultCopyToClipboardMode;
        hex.ByteSpacerVisualStyle       = d.ByteSpacerVisualStyle;

        // Scroll markers
        hex.ShowBookmarkMarkers         = d.ShowBookmarkMarkers;
        hex.ShowModifiedMarkers         = d.ShowModifiedMarkers;
        hex.ShowInsertedMarkers         = d.ShowInsertedMarkers;
        hex.ShowDeletedMarkers          = d.ShowDeletedMarkers;
        hex.ShowSearchResultMarkers     = d.ShowSearchResultMarkers;

        // Status bar visibility
        hex.ShowStatusMessage           = d.ShowStatusMessage;
        hex.ShowFileSizeInStatusBar     = d.ShowFileSizeInStatusBar;
        hex.ShowSelectionInStatusBar    = d.ShowSelectionInStatusBar;
        hex.ShowPositionInStatusBar     = d.ShowPositionInStatusBar;
        hex.ShowEditModeInStatusBar     = d.ShowEditModeInStatusBar;
        hex.ShowBytesPerLineInStatusBar = d.ShowBytesPerLineInStatusBar;

        // Advanced behaviour
        hex.AllowAutoHighLightSelectionByte      = d.AllowAutoHighLightSelectionByte;
        hex.AllowAutoSelectSameByteAtDoubleClick = d.AllowAutoSelectSameByteAtDoubleClick;
        hex.AllowContextMenu                     = d.AllowContextMenu;
        hex.AllowDeleteByte                      = d.AllowDeleteByte;
        hex.AllowExtend                          = d.AllowExtend;
        hex.FileDroppingConfirmation             = d.FileDroppingConfirmation;
        hex.PreloadByteInEditorMode              = d.PreloadByteInEditorMode;

        hex.ByteToolTipDisplayMode               = d.ByteToolTipDisplayMode;

        // Column / row highlight
        hex.ShowColumnHighlight      = d.ShowColumnHighlight;
        hex.ShowAsciiColumnHighlight = d.ShowAsciiColumnHighlight;
        hex.ShowRowHighlight         = d.ShowRowHighlight;

        // Breadcrumb bar
        hex.ShowBreadcrumbBar             = d.ShowBreadcrumbBar;
        hex.BreadcrumbOffsetFormat        = (WpfHexEditor.HexEditor.Controls.BreadcrumbOffsetFormat)d.BreadcrumbOffsetFormat;
        hex.BreadcrumbShowFormatInfo      = d.BreadcrumbShowFormatInfo;
        hex.BreadcrumbShowFieldPath       = d.BreadcrumbShowFieldPath;
        hex.BreadcrumbShowSelectionLength = d.BreadcrumbShowSelectionLength;
        hex.BreadcrumbFontSize            = d.BreadcrumbFontSize;
    }

    private static void ApplySyntaxColorOverrides(
        WpfHexEditor.Editor.CodeEditor.Controls.CodeEditor editor,
        CodeEditorDefaultSettings ce)
    {
        Apply(SyntaxTokenKind.Keyword,    ce.KeywordColor);
        Apply(SyntaxTokenKind.String,     ce.StringColor);
        Apply(SyntaxTokenKind.Number,     ce.NumberColor);
        Apply(SyntaxTokenKind.Comment,    ce.CommentColor);
        Apply(SyntaxTokenKind.Type,       ce.TypeColor);
        Apply(SyntaxTokenKind.Identifier, ce.IdentifierColor);
        Apply(SyntaxTokenKind.Operator,   ce.OperatorColor);
        Apply(SyntaxTokenKind.Bracket,    ce.BracketColor);
        Apply(SyntaxTokenKind.Attribute,  ce.AttributeColor);

        void Apply(WpfHexEditor.Core.ProjectSystem.Languages.SyntaxTokenKind k, string? hex)
            => editor.SetSyntaxColorOverride(k, TryParseColor(hex, out var c) ? c : null);
    }

    private static bool TryParseColor(string? hex, out Color color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(hex)) return false;
        try
        {
            color = (Color)ColorConverter.ConvertFromString(hex.Trim());
            return true;
        }
        catch { return false; }
    }
}
