//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////
// Project: WpfHexEditor.Editor.StructureEditor
// File: Services/ExpressionContextService.cs
// Description: Per-editor-instance singleton that holds the active IVariableSource.
//              Set once by StructureEditor when the VM is ready; read by any
//              ExpressionTextBox that has no explicit VariableSource binding.
// Architecture Notes:
//     Uses instance-based registration (keyed by StructureEditor instance)
//     rather than a true static singleton to support multiple open editors.
//     ExpressionTextBox walks up its visual tree to find the closest
//     registered StructureEditor and queries its variable source.
//     Falls back to empty list if no registration found.
//////////////////////////////////////////////

using System.Runtime.CompilerServices;

namespace WpfHexEditor.Editor.StructureEditor.Services;

/// <summary>
/// Registry that maps a StructureEditor instance to its active variable source.
/// </summary>
internal static class ExpressionContextService
{
    // ConditionalWeakTable avoids memory leaks when editors are closed.
    private static readonly ConditionalWeakTable<object, IVariableSource> _registry = new();

    /// <summary>Registers a variable source for the given editor instance.</summary>
    internal static void Register(object editorInstance, IVariableSource source) =>
        _registry.AddOrUpdate(editorInstance, source);

    /// <summary>Returns the variable source for the given editor instance, or null.</summary>
    internal static IVariableSource? Get(object editorInstance) =>
        _registry.TryGetValue(editorInstance, out var src) ? src : null;
}
