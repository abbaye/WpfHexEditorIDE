//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.Editor.Core;

/// <summary>
/// Optionally implemented by document editors (<see cref="IDocumentEditor"/>
/// implementors) that can supply a <see cref="IPropertyProvider"/> reflecting
/// the editor's current selection.
/// <para>
/// The host calls <see cref="GetPropertyProvider"/> whenever the active
/// document tab changes and passes the result to the Properties panel.
/// </para>
/// </summary>
public interface IPropertyProviderSource
{
    /// <summary>
    /// Returns a provider for the current selection/context, or
    /// <see langword="null"/> if no meaningful properties are available.
    /// The provider may be a long-lived object that raises
    /// <see cref="IPropertyProvider.PropertiesChanged"/> on selection changes.
    /// </summary>
    IPropertyProvider? GetPropertyProvider();
}
