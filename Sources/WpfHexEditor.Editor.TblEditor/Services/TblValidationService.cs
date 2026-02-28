using System.Text.RegularExpressions;
using WpfHexEditor.TblEditor.Models;
using WpfHexEditor.TblEditor.ViewModels;

namespace WpfHexEditor.TblEditor.Services;

/// <summary>Service for validating TBL entries</summary>
public class TblValidationService
{
    public TblValidationResult ValidateEntry(string? entry, string? value)
    {
        if (!IsValidHexEntry(entry)) return TblValidationResult.Error("Invalid hex format");
        if (!HasValidLength(entry)) return TblValidationResult.Error($"Length must be 2-16 characters, got {entry?.Length ?? 0}");
        if (!IsValidValue(value)) return TblValidationResult.Error("Value cannot be empty");
        return TblValidationResult.Success();
    }

    public bool IsValidHexEntry(string? entry)
    {
        if (string.IsNullOrWhiteSpace(entry)) return false;
        if (entry.Length % 2 != 0) return false;
        return Regex.IsMatch(entry, "^[0-9A-Fa-f]+$");
    }

    public bool HasValidLength(string? entry)
    {
        if (string.IsNullOrEmpty(entry)) return false;
        return entry.Length >= 2 && entry.Length <= 16 && entry.Length % 2 == 0;
    }

    public bool IsValidValue(string? value) => !string.IsNullOrEmpty(value);

    public bool HasDuplicateEntry(string entry, IEnumerable<TblEntryViewModel> existingEntries)
    {
        if (string.IsNullOrWhiteSpace(entry)) return false;
        var upperEntry = entry.ToUpperInvariant();
        return existingEntries.Any(e => e.Entry.ToUpperInvariant() == upperEntry);
    }

    public async Task<Dictionary<TblEntryViewModel, TblValidationResult>> ValidateAllAsync(
        IEnumerable<TblEntryViewModel> entries, CancellationToken cancellationToken)
    {
        var results = new Dictionary<TblEntryViewModel, TblValidationResult>();
        await Task.Run(() =>
        {
            foreach (var entry in entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var result = ValidateEntry(entry.Entry, entry.Value);
                results[entry] = result;
                entry.IsValid = result.IsValid;
                entry.ValidationError = result.ErrorMessage;
            }
        }, cancellationToken);
        return results;
    }
}
