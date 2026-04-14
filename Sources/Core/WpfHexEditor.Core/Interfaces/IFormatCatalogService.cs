// ==========================================================
// Project: WpfHexEditor.Core
// File: IFormatCatalogService.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude (Anthropic)
// Created: 2026-03-26
// Description:
//     First-class IDE service for the shared whfmt format definition catalog.
//     Loaded ONCE at app startup; used by all FormatDetectionService instances.
//
// Architecture Notes:
//     Defined in Core (not SDK) because FormatCatalogService implementation
//     is also in Core and Core cannot reference SDK.
//     SDK consumers access it transitively via SDK → Core dependency.
// ==========================================================

using System;
using System.Collections.Generic;
using WpfHexEditor.Core.FormatDetection;

namespace WpfHexEditor.Core.Interfaces
{
    /// <summary>
    /// Shared format definition catalog. Loaded once at app startup.
    /// All format detection consumers use this catalog transparently.
    /// </summary>
    public interface IFormatCatalogService
    {
        /// <summary>Number of loaded format definitions.</summary>
        int FormatCount { get; }

        /// <summary>
        /// Formats that failed to load during <see cref="Initialize"/>. Empty on a healthy catalog.
        /// Check this after initialization to surface broken whfmt files to the user.
        /// </summary>
        IReadOnlyList<FormatLoadFailure> LoadFailures { get; }

        /// <summary>Whether the catalog has been initialized.</summary>
        bool IsInitialized { get; }

        /// <summary>Get all loaded format definitions (read-only snapshot).</summary>
        IReadOnlyList<FormatDefinition> GetAllFormats();

        /// <summary>Find a format definition by FormatName (case-insensitive).</summary>
        FormatDefinition? FindFormat(string formatName);

        /// <summary>Find all formats matching a file extension (e.g. ".exe").</summary>
        IReadOnlyList<FormatDefinition> FindFormatsByExtension(string extension);

        /// <summary>Raised after the catalog is fully loaded.</summary>
        event EventHandler? CatalogReady;
    }
}
