//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////
// Project: WpfHexEditor.Editor.StructureEditor
// File: Services/IVariableSource.cs
// Description: Abstraction over the live variable name collection.
//              Allows ExpressionTextBox and ExpressionCompletionProvider to
//              consume variable names without a compile-time dependency on
//              VariablesViewModel or BlocksViewModel.
//////////////////////////////////////////////

namespace WpfHexEditor.Editor.StructureEditor.Services;

internal interface IVariableSource
{
    /// <summary>Returns all known variable names at the current moment.</summary>
    IReadOnlyList<string> GetVariableNames();
}
