///////////////////////////////////////////////////////////////
// GNU Affero General Public License v3.0  2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Project     : WpfHexEditor.Core.Localization
// File        : LocalizationService.cs
// Description : Default implementation of ILocalizationService.
//               Wraps LocalizedResourceDictionary (static gateway) to provide
//               a DI-friendly, testable service object.
//               The host app (IDE or standalone) creates one instance and
//               stores it wherever needed. Plugins may consume
//               LocalizedResourceDictionary.CultureChanged directly (no DI
//               required) or request ILocalizationService via the host context.
//
// Architecture Notes:
//               Persistence (saving PreferredLanguage to AppSettings) is
//               intentionally NOT done here to keep the micro-package
//               dependency-free. The host app handles persistence inside
//               its own OnLanguageSelectionChanged / AppSettings.Flush path.
///////////////////////////////////////////////////////////////

using System;
using System.Globalization;

namespace WpfHexEditor.Core.Localization.Services;

/// <summary>
/// Default implementation of <see cref="ILocalizationService"/>.
/// Delegates all culture changes to <see cref="LocalizedResourceDictionary.ChangeCulture"/>
/// and forwards its <see cref="LocalizedResourceDictionary.CultureChanged"/> event.
/// </summary>
public sealed class LocalizationService : ILocalizationService
{
    /// <summary>
    /// Shared singleton instance. Set by the host application at startup
    /// (<c>LocalizationService.Instance = new LocalizationService();</c>).
    /// Plugins may read this to obtain the current culture or subscribe to changes.
    /// </summary>
    public static ILocalizationService? Instance { get; set; }

    /// <inheritdoc/>
    public CultureInfo CurrentCulture => LocalizedResourceDictionary.CurrentCulture;

    /// <inheritdoc/>
    public void ChangeCulture(CultureInfo culture, bool persistent = true)
    {
        ArgumentNullException.ThrowIfNull(culture);
        LocalizedResourceDictionary.ChangeCulture(culture);
    }

    /// <inheritdoc/>
    public event EventHandler<CultureChangedEventArgs> CultureChanged
    {
        add    => LocalizedResourceDictionary.CultureChanged += value;
        remove => LocalizedResourceDictionary.CultureChanged -= value;
    }
}
