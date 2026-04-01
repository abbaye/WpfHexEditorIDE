// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: XamlDocument.cs
// Author: Derek Tremblay
// Created: 2026-03-16
// Description:
//     Pure domain model for the active XAML text.
//     Holds raw XAML text, the last successfully parsed XElement tree,
//     and the last parse error message. No WPF rendering dependency.
//
// Architecture Notes:
//     Separation of Concerns — XamlReader.Parse() happens only in DesignCanvas;
//     this model only uses System.Xml.Linq for structural parsing.
// ==========================================================

using System.Xml.Linq;

namespace WpfHexEditor.Editor.XamlDesigner.Models;

/// <summary>
/// Domain model for an open XAML document.
/// Tracks raw text, parsed element tree, and parse error state.
/// No WPF rendering is performed here.
/// </summary>
public sealed class XamlDocument
{
    // ── State ─────────────────────────────────────────────────────────────────

    private string _rawXaml = string.Empty;

    // ── Properties ────────────────────────────────────────────────────────────

    /// <summary>Current XAML text content.</summary>
    public string RawXaml => _rawXaml;

    /// <summary>Root XElement from last successful parse; null if not yet parsed or invalid.</summary>
    public XElement? ParsedRoot { get; private set; }

    /// <summary>Error message from last failed parse; null when last parse succeeded.</summary>
    public string? LastParseError { get; private set; }

    /// <summary>True when the last parse succeeded and a root element is available.</summary>
    public bool IsValid => LastParseError is null && ParsedRoot is not null;

    // ── Events ────────────────────────────────────────────────────────────────

    /// <summary>Raised after <see cref="SetXaml"/> updates the content.</summary>
    public event EventHandler? XamlChanged;

    // ── Mutation ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Updates the raw XAML text and attempts an XML parse to refresh
    /// <see cref="ParsedRoot"/> and <see cref="LastParseError"/>.
    /// Fires <see cref="XamlChanged"/> regardless of parse outcome.
    /// </summary>
    public void SetXaml(string xaml)
    {
        _rawXaml = xaml ?? string.Empty;
        TryParse();
        XamlChanged?.Invoke(this, EventArgs.Empty);
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private void TryParse()
    {
        if (string.IsNullOrWhiteSpace(_rawXaml))
        {
            ParsedRoot     = null;
            LastParseError = null;
            return;
        }

        try
        {
            var doc        = XDocument.Parse(_rawXaml, LoadOptions.PreserveWhitespace);
            ParsedRoot     = doc.Root;
            LastParseError = null;
        }
        catch (Exception ex)
        {
            ParsedRoot     = null;
            LastParseError = ex.Message;
        }
    }
}
