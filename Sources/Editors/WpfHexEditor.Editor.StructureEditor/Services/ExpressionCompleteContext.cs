//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////
// Project: WpfHexEditor.Editor.StructureEditor
// File: Services/ExpressionCompleteContext.cs
// Description: Immutable context record describing the expression state at
//              the moment a completion is triggered.
//              Carries data only — no logic, no WPF dependency.
//////////////////////////////////////////////

namespace WpfHexEditor.Editor.StructureEditor.Services;

/// <summary>
/// Snapshot of the expression input state at the moment completion is triggered.
/// </summary>
/// <param name="ExpressionText">Full text currently in the expression box.</param>
/// <param name="CaretIndex">Caret position within <see cref="ExpressionText"/>.</param>
/// <param name="ActivePrefix">
///   Detected prefix left of the caret: <c>"var:"</c>, <c>"calc:"</c>, <c>"offset:"</c>,
///   or <c>null</c> when no prefix is active.
/// </param>
/// <param name="Token">
///   Text after <see cref="ActivePrefix"/> up to the caret (the current filter word).
///   Empty string when no characters have been typed after the prefix.
/// </param>
/// <param name="VariableSource">Live variable name provider.</param>
internal sealed record ExpressionCompleteContext(
    string          ExpressionText,
    int             CaretIndex,
    string?         ActivePrefix,
    string          Token,
    IVariableSource VariableSource);
