// ==========================================================
// Project: WpfHexEditor.Editor.ResxEditor
// File: ViewModels/ResxLocaleCompareViewModel.cs
// Description:
//     ViewModel for the side-by-side locale comparison view.
//     Aligns entries from two locale files by key and
//     colour-codes the comparison status for each row.
// ==========================================================

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using WpfHexEditor.Editor.ResxEditor.Models;
using WpfHexEditor.Editor.ResxEditor.Services;
using WpfHexEditor.Core.ViewModels;

namespace WpfHexEditor.Editor.ResxEditor.ViewModels;

/// <summary>Comparison status for a single key across two locales.</summary>
public enum LocaleCompareStatus
{
    /// <summary>Key exists in both locales with identical values.</summary>
    Matched,
    /// <summary>Key exists in both locales but values differ.</summary>
    ValueDiffers,
    /// <summary>Key exists in base but is missing from target.</summary>
    MissingInTarget,
    /// <summary>Key exists in target but is missing from base.</summary>
    ExtraInTarget
}

/// <summary>A single aligned row in the locale comparison view.</summary>
public sealed record LocaleCompareRow(
    string              Key,
    string              BaseValue,
    string              TargetValue,
    LocaleCompareStatus Status);

/// <summary>ViewModel for the side-by-side locale comparison panel.</summary>
public sealed class ResxLocaleCompareViewModel : ViewModelBase
{
    private CultureInfo? _baseCulture;
    private CultureInfo? _targetCulture;

    public CultureInfo? BaseCulture
    {
        get => _baseCulture;
        set { _baseCulture = value; OnPropertyChanged(); }
    }

    public CultureInfo? TargetCulture
    {
        get => _targetCulture;
        set { _targetCulture = value; OnPropertyChanged(); }
    }

    public ObservableCollection<LocaleCompareRow> Rows { get; } = [];

    // ------------------------------------------------------------------

    /// <summary>Populates <see cref="Rows"/> from two parsed documents.</summary>
    public void Compare(ResxDocument baseDoc, ResxDocument targetDoc)
    {
        Rows.Clear();

        var baseMap   = baseDoc.Entries.ToDictionary(e => e.Name, StringComparer.Ordinal);
        var targetMap = targetDoc.Entries.ToDictionary(e => e.Name, StringComparer.Ordinal);

        // Keys present in base
        foreach (var (key, baseEntry) in baseMap.OrderBy(k => k.Key, StringComparer.Ordinal))
        {
            if (targetMap.TryGetValue(key, out var targetEntry))
            {
                var status = string.Equals(baseEntry.Value, targetEntry.Value, StringComparison.Ordinal)
                    ? LocaleCompareStatus.Matched
                    : LocaleCompareStatus.ValueDiffers;
                Rows.Add(new LocaleCompareRow(key, baseEntry.Value, targetEntry.Value, status));
            }
            else
            {
                Rows.Add(new LocaleCompareRow(key, baseEntry.Value, string.Empty, LocaleCompareStatus.MissingInTarget));
            }
        }

        // Keys only in target
        foreach (var (key, targetEntry) in targetMap.OrderBy(k => k.Key, StringComparer.Ordinal))
        {
            if (!baseMap.ContainsKey(key))
                Rows.Add(new LocaleCompareRow(key, string.Empty, targetEntry.Value, LocaleCompareStatus.ExtraInTarget));
        }
    }

}
