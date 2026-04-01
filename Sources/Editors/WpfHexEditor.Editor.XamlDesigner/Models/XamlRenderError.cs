// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: Models/XamlRenderError.cs
// Author: Derek Tremblay
// Created: 2026-03-18
// Description:
//     Carries structured parse/render error information emitted by DesignCanvas
//     to subscribers (XamlDesignerSplitHost → ErrorList pipeline).
//     Replaces the raw string? payload of the legacy RenderError event so that
//     line/column can be forwarded to the IDE ErrorPanel as a DiagnosticEntry.
// ==========================================================

namespace WpfHexEditor.Editor.XamlDesigner.Models;

/// <summary>Discriminator for <see cref="XamlRenderError"/> so the split host can distinguish
/// actionable parse failures from informational "no preview available" states.</summary>
public enum XamlRenderErrorKind
{
    /// <summary>A XAML or XML parse/layout exception — show the error card.</summary>
    ParseError,
    /// <summary>The root element is a non-UIElement type (ResourceDictionary, Style, etc.) —
    /// auto-switch to CodeOnly and show a neutral info banner instead of the error card.</summary>
    NonRenderableRoot,
}

/// <summary>
/// Structured render-error payload raised by <see cref="Controls.DesignCanvas.RenderError"/>.
/// </summary>
/// <param name="Message">Human-readable error message (never null or empty).</param>
/// <param name="Line">1-based source line number, or -1 when not available.</param>
/// <param name="Column">1-based column number, or -1 when not available.</param>
/// <param name="FilePath">Absolute path of the XAML file, or null.</param>
/// <param name="Kind">Discriminator for how the split host should present this error.</param>
public sealed record XamlRenderError(
    string              Message,
    int                 Line     = -1,
    int                 Column   = -1,
    string?             FilePath = null,
    XamlRenderErrorKind Kind     = XamlRenderErrorKind.ParseError);
