//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Collections.Generic;

namespace WpfHexEditor.Editor.Core;

/// <summary>
/// Centralized registry of document editors available in the application.
///
/// Typical usage (host at startup):
/// <code>
/// registry.Register(new TblEditorFactory());
/// registry.Register(new CodeEditorFactory());
/// // …
/// // When opening a file:
/// var factory = registry.FindFactory(filePath) ?? registry.GetFallback();
/// var editor  = factory.Create();
/// dockingHost.OpenDocument((FrameworkElement)editor, editor.TitleChanged);
/// </code>
/// </summary>
public interface IEditorRegistry
{
    /// <summary>
    /// Registers an available editor.
    /// </summary>
    void Register(IEditorFactory factory);

    /// <summary>
    /// Returns the most appropriate factory for <paramref name="filePath"/>,
    /// or <c>null</c> if no editor supports this file.
    /// </summary>
    IEditorFactory? FindFactory(string filePath);

    /// <summary>
    /// Returns all registered factories.
    /// </summary>
    IReadOnlyList<IEditorFactory> GetAll();
}
