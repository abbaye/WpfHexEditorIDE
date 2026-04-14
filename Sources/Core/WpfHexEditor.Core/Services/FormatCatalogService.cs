// ==========================================================
// Project: WpfHexEditor.Core
// File: FormatCatalogService.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude (Anthropic)
// Created: 2026-03-26
// Description:
//     Singleton format catalog service. Loads all 427+ whfmt format
//     definitions ONCE at app startup and sets the static shared catalog
//     on FormatDetectionService so ALL instances can detect formats.
//
// Architecture Notes:
//     First-class IDE service — same level as EventBus, CommandRegistry.
//     Injected via IIDEHostContext.FormatCatalog.
//     Calls FormatDetectionService.SetSharedCatalog() to propagate.
//     Load failures are captured in LoadFailures (never silently discarded).
// ==========================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using WpfHexEditor.Core.FormatDetection;
using WpfHexEditor.Core.Interfaces;

namespace WpfHexEditor.Core.Services
{
    /// <summary>
    /// Singleton format catalog. Loads formats once, shares with all consumers.
    /// </summary>
    public sealed class FormatCatalogService : IFormatCatalogService
    {
        private readonly List<FormatDefinition> _formats = new();
        private readonly List<FormatLoadFailure> _failures = new();
        private bool _initialized;

        public int FormatCount => _formats.Count;
        public bool IsInitialized => _initialized;
        public IReadOnlyList<FormatLoadFailure> LoadFailures => _failures;
        public event EventHandler? CatalogReady;

        /// <summary>
        /// Load all formats from embedded catalog + optional external directory.
        /// Sets <see cref="FormatDetectionService.SetSharedCatalog"/> for all instances.
        /// Safe to call only once — subsequent calls are no-ops.
        /// </summary>
        public void Initialize(
            IEnumerable<(string json, string? category)> embeddedFormats,
            string? externalDirectory = null)
        {
            if (_initialized) return;

            var parser = new FormatDetectionService();

            // Load embedded formats (from EmbeddedFormatCatalog)
            // Note: embeddedFormats already filters to .whfmt entries at the call site
            // (MainWindow.PluginSystem.cs), but we guard here for safety.
            foreach (var (json, category) in embeddedFormats)
            {
                if (string.IsNullOrEmpty(json)) continue;
                try
                {
                    var fmt = parser.ImportFromJson(json);
                    if (fmt is null)
                    {
                        // Null without exception = incompatible schema (syntax-only whfmt).
                        // Not a user-facing error — skip silently.
                        continue;
                    }
                    fmt.Category ??= category;
                    _formats.Add(fmt);
                }
                catch (Exception ex)
                {
                    _failures.Add(new FormatLoadFailure(category ?? "embedded", ex.Message));
                }
            }

            // Load external overrides (user-provided .whfmt files)
            if (!string.IsNullOrEmpty(externalDirectory) && Directory.Exists(externalDirectory))
            {
                foreach (var file in Directory.GetFiles(externalDirectory, "*.whfmt"))
                {
                    try
                    {
                        var json = File.ReadAllText(file);
                        var fmt = parser.ImportFromJson(json);
                        if (fmt is null || !fmt.IsValid())
                        {
                            _failures.Add(new FormatLoadFailure(Path.GetFileName(file), "IsValid() = false"));
                            continue;
                        }
                        _formats.Add(fmt);
                    }
                    catch (Exception ex)
                    {
                        _failures.Add(new FormatLoadFailure(Path.GetFileName(file), ex.Message));
                    }
                }
            }

            // Also check user AppData directory
            var userDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "WpfHexEditor", "FormatDefinitions");
            if (Directory.Exists(userDir))
            {
                foreach (var file in Directory.GetFiles(userDir, "*.whfmt"))
                {
                    try
                    {
                        var json = File.ReadAllText(file);
                        var fmt = parser.ImportFromJson(json);
                        if (fmt is null || !fmt.IsValid())
                        {
                            _failures.Add(new FormatLoadFailure(Path.GetFileName(file), "IsValid() = false"));
                            continue;
                        }
                        _formats.Add(fmt);
                    }
                    catch (Exception ex)
                    {
                        _failures.Add(new FormatLoadFailure(Path.GetFileName(file), ex.Message));
                    }
                }
            }

            // Set static shared catalog — all FormatDetectionService instances now see these
            FormatDetectionService.SetSharedCatalog(_formats);

            _initialized = true;
            CatalogReady?.Invoke(this, EventArgs.Empty);
        }

        public IReadOnlyList<FormatDefinition> GetAllFormats() => _formats;

        public FormatDefinition? FindFormat(string formatName) =>
            _formats.FirstOrDefault(f =>
                string.Equals(f.FormatName, formatName, StringComparison.OrdinalIgnoreCase));

        public IReadOnlyList<FormatDefinition> FindFormatsByExtension(string extension) =>
            _formats.Where(f =>
                f.Extensions?.Contains(extension, StringComparer.OrdinalIgnoreCase) == true)
            .ToList();
    }
}
