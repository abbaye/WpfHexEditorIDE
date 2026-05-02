///////////////////////////////////////////////////////////////
// GNU Affero General Public License v3.0  2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Project     : WpfHexEditor.Core.Localization
// File        : CultureChangedEventArgs.cs
// Description : Event args for ILocalizationService.CultureChanged.
//               Single definition — replaces the duplicates that existed
//               in LocalizedResourceDictionary and DynamicResourceManager.
///////////////////////////////////////////////////////////////

using System;
using System.Globalization;

namespace WpfHexEditor.Core.Localization.Services;

/// <summary>
/// Provides culture transition data for <see cref="ILocalizationService.CultureChanged"/>.
/// </summary>
public sealed class CultureChangedEventArgs(CultureInfo previousCulture, CultureInfo newCulture) : EventArgs
{
    /// <summary>The culture that was active before the change.</summary>
    public CultureInfo PreviousCulture { get; } = previousCulture;

    /// <summary>The culture now active.</summary>
    public CultureInfo NewCulture { get; } = newCulture;
}
