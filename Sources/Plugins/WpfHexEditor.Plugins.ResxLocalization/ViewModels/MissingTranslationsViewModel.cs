// ==========================================================
// Project: WpfHexEditor.Plugins.ResxLocalization
// File: ViewModels/MissingTranslationsViewModel.cs
// Description:
//     ViewModel for the Missing Translations panel.
//     Produces a matrix: rows = resource keys, columns = cultures.
//     A cell is marked as missing when the key is absent or the
//     value is empty in that locale.
// ==========================================================

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using WpfHexEditor.Editor.ResxEditor.Models;
using WpfHexEditor.Editor.ResxEditor.Services;
using WpfHexEditor.Core.ViewModels;

namespace WpfHexEditor.Plugins.ResxLocalization.ViewModels;

/// <summary>Represents one cell in the missing-translations matrix.</summary>
public sealed class TranslationCellViewModel : ViewModelBase
{
    private string _value    = string.Empty;
    private bool   _isMissing;

    public string CultureCode { get; init; } = string.Empty;

    public string Value
    {
        get => _value;
        set { _value = value; OnPropertyChanged(); }
    }

    public bool IsMissing
    {
        get => _isMissing;
        set { _isMissing = value; OnPropertyChanged(); }
    }

}

/// <summary>Represents one row (one resource key) in the missing-translations matrix.</summary>
public sealed class TranslationRowViewModel
{
    public string                                       Key   { get; init; } = string.Empty;
    public ObservableCollection<TranslationCellViewModel> Cells { get; } = [];
}

/// <summary>
/// Backing ViewModel for <see cref="Panels.MissingTranslationsPanel"/>.
/// </summary>
public sealed class MissingTranslationsViewModel : ViewModelBase
{
    private int    _missingCount;
    private string _statusText = "No locale data loaded";

    public ObservableCollection<TranslationRowViewModel> Rows         { get; } = [];
    public ObservableCollection<string>                   CultureCodes { get; } = [];

    public int MissingCount
    {
        get => _missingCount;
        private set { _missingCount = value; OnPropertyChanged(); }
    }

    public string StatusText
    {
        get => _statusText;
        set { _statusText = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Rebuilds the matrix from a list of (culture, document) pairs.
    /// The first entry should be the base/neutral document.
    /// </summary>
    public void Refresh(IReadOnlyList<(string CultureCode, ResxDocument Doc)> locales)
    {
        Rows.Clear();
        CultureCodes.Clear();
        MissingCount = 0;

        if (locales.Count == 0)
        {
            StatusText = "No locale data loaded";
            return;
        }

        // Collect all unique keys from all locales (preserving base order)
        var allKeys = locales
            .SelectMany(l => l.Doc.Entries.Select(e => e.Name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Build culture column list (skip first = base for row header label)
        foreach (var (code, _) in locales)
            CultureCodes.Add(code);

        // Build lookup: culture â†’ key â†’ value
        var lookup = locales.ToDictionary(
            l => l.CultureCode,
            l => l.Doc.Entries.ToDictionary(
                e => e.Name, e => e.Value,
                StringComparer.OrdinalIgnoreCase),
            StringComparer.OrdinalIgnoreCase);

        int missing = 0;

        foreach (var key in allKeys)
        {
            var row = new TranslationRowViewModel { Key = key };

            foreach (var (code, _) in locales)
            {
                var hasValue  = lookup[code].TryGetValue(key, out var val)
                             && !string.IsNullOrEmpty(val);
                var cell = new TranslationCellViewModel
                {
                    CultureCode = code,
                    Value       = val ?? string.Empty,
                    IsMissing   = !hasValue
                };
                if (!hasValue) missing++;
                row.Cells.Add(cell);
            }

            Rows.Add(row);
        }

        MissingCount = missing;
        StatusText   = missing == 0
            ? $"All keys translated across {locales.Count} locale(s)"
            : $"{missing} missing translation(s) across {locales.Count} locale(s)";
    }

}
