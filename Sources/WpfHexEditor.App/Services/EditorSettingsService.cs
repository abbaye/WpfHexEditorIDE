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
    }

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
        ce.ShowInlineHints          = d.ShowInlineHints;
        ce.InlineHintsVisibleKinds  = d.InlineHintsVisibleKinds == 0
            ? WpfHexEditor.Editor.Core.InlineHintsSymbolKinds.All
            : (WpfHexEditor.Editor.Core.InlineHintsSymbolKinds)d.InlineHintsVisibleKinds;
        ce.ShowEndOfBlockHint       = d.EndOfBlockHintEnabled;
        ce.EndOfBlockHintDelayMs    = d.EndOfBlockHintDelayMs;
        ce.EnableAutoClosingBrackets = d.AutoClosingBrackets;
        ce.EnableAutoClosingQuotes   = d.AutoClosingQuotes;
        ce.SkipOverClosingChar       = d.SkipOverClosingChar;
        ce.WrapSelectionInPairs      = d.WrapSelectionInPairs;

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
        te.MouseWheelSpeed   = settings.TextEditorDefaults.MouseWheelSpeed;
        te.ZoomLevel         = settings.TextEditorDefaults.DefaultZoom;
        te.IsWordWrapEnabled = settings.TextEditorDefaults.WordWrap;
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

        // Tooltip
        hex.ByteToolTipDisplayMode               = d.ByteToolTipDisplayMode;
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

        void Apply(WpfHexEditor.Editor.Core.SyntaxTokenKind k, string? hex)
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
