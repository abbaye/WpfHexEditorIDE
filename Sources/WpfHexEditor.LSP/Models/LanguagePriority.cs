// ==========================================================
// Project: WpfHexEditor.LSP
// File: Models/LanguagePriority.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Ordering enum for language definition sources.
//     Higher numeric value = higher priority (wins in extension conflicts).
// ==========================================================

namespace WpfHexEditor.LSP.Models;

/// <summary>Source priority for a <see cref="LanguageDefinition"/>.</summary>
public enum LanguagePriority
{
    /// <summary>Shipped with the WpfHexEditor application (lowest priority).</summary>
    BuiltIn = 0,

    /// <summary>Imported by the user from an external source (e.g. marketplace plugin).</summary>
    Imported = 1,

    /// <summary>Created by the user directly in the active workspace (highest priority).</summary>
    UserCreated = 2,
}
