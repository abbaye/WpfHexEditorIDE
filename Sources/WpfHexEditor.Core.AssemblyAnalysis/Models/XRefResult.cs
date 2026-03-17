// ==========================================================
// Project: WpfHexEditor.Core.AssemblyAnalysis
// File: Models/XRefResult.cs
// Author: Derek Tremblay
// Created: 2026-03-16
// License: GNU Affero General Public License v3.0 (AGPL-3.0)
// Description:
//     Immutable models for cross-reference analysis results.
//     Produced by XRefService; consumed by XRefViewModel in the plugin layer.
//
// Architecture Notes:
//     Pattern: Immutable data records.
//     XRefEntry identifies a caller/callee member and where it lives.
//     XRefResult groups entries by relationship kind (CalledBy / Calls /
//     FieldReads / FieldWrites / Implementors).
// ==========================================================

namespace WpfHexEditor.Core.AssemblyAnalysis.Models;

/// <summary>
/// A single cross-reference entry: a member that references or is referenced by the target.
/// </summary>
public sealed record XRefEntry(
    /// <summary>Full name of the declaring type, e.g. "MyApp.Utils.StringHelper".</summary>
    string TypeFullName,

    /// <summary>Human-readable member signature, e.g. "string Trim(string s)".</summary>
    string MemberSignature,

    /// <summary>ECMA-335 metadata token of the referencing/referenced member.</summary>
    int MetadataToken,

    /// <summary>Metadata token of the declaring type that owns this member.</summary>
    int OwnerTypeToken);

/// <summary>
/// All cross-reference results for a single target member.
/// </summary>
public sealed record XRefResult(
    /// <summary>Methods that call the target method.</summary>
    IReadOnlyList<XRefEntry> CalledBy,

    /// <summary>Methods called by the target method (callees).</summary>
    IReadOnlyList<XRefEntry> Calls,

    /// <summary>Methods that read the target field.</summary>
    IReadOnlyList<XRefEntry> FieldReads,

    /// <summary>Methods that write the target field.</summary>
    IReadOnlyList<XRefEntry> FieldWrites,

    /// <summary>Types that implement the target interface or override the target virtual method.</summary>
    IReadOnlyList<XRefEntry> Implementors);
