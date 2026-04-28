///////////////////////////////////////////////////////////////
// GNU Affero General Public License v3.0  2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Project     : WpfHexEditor.Core.Localization
// File        : ILocalizationService.cs
// Description : Contract for runtime culture switching.
//               Implement in the host app (IDE or standalone) and inject
//               where needed. The micro-package provides LocalizationService
//               as a default static implementation.
// Architecture: Each NuGet UI package receives culture-change notifications
//               via CultureChanged. No coupling to WpfHexEditor.Core.
///////////////////////////////////////////////////////////////

using System;
using System.Globalization;

namespace WpfHexEditor.Core.Localization.Services;

/// <summary>
/// Provides runtime culture switching without application restart.
/// </summary>
public interface ILocalizationService
{
    /// <summary>Gets the currently active culture.</summary>
    CultureInfo CurrentCulture { get; }

    /// <summary>
    /// Switches the application culture and notifies all registered
    /// <see cref="LocalizedResourceDictionary"/> instances.
    /// </summary>
    /// <param name="culture">The target culture.</param>
    /// <param name="persistent">
    /// When <see langword="true"/>, the preference is persisted so it
    /// survives application restart (implementation-defined storage).
    /// </param>
    void ChangeCulture(CultureInfo culture, bool persistent = true);

    /// <summary>
    /// Fired after a culture change has been applied to all resource dictionaries.
    /// </summary>
    event EventHandler<CultureChangedEventArgs> CultureChanged;
}
