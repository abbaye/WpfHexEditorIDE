// ==========================================================
// Project: WpfHexEditor.Core.AssemblyAnalysis
// File: Models/TextSpan.cs
// Author: Derek Tremblay
// Created: 2026-03-16
// License: GNU Affero General Public License v3.0 (AGPL-3.0)
// Description:
//     Immutable model representing a character span in a decompiled text output.
//     Produced by DecompiledTextLinker.ExtractTypeNames() and consumed by the
//     plugin layer to build TextLink objects for goto-definition navigation.
//
// Architecture Notes:
//     Pattern: Immutable data model (record).
//     No WPF / no NuGet — safe from the Core layer.
// ==========================================================

namespace WpfHexEditor.Core.AssemblyAnalysis.Models;

/// <summary>
/// An identified span of text within a decompiled source string.
/// Typically represents a type name candidate for goto-definition linking.
/// </summary>
public sealed record TextSpan(
    /// <summary>Zero-based start offset within the full text string.</summary>
    int Start,

    /// <summary>Length of the span in characters.</summary>
    int Length,

    /// <summary>The actual text of the span.</summary>
    string Text);
