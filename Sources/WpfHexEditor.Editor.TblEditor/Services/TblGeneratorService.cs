//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using WpfHexEditor.Core;
using WpfHexEditor.Core.Bytes;
using WpfHexEditor.Core.CharacterTable;
using WpfHexEditor.Core.Search.Models;
using WpfHexEditor.Core.Search.Services;
using WpfHexEditor.Editor.TblEditor.Models;
using WpfHexEditor.Editor.TblEditor.ViewModels;

namespace WpfHexEditor.Editor.TblEditor.Services;

/// <summary>
/// Service for generating TBL entries from sample text using RelativeSearchEngine
/// </summary>
public class TblGeneratorService
{
    private readonly ByteProvider _byteProvider;
    private readonly TblStream _targetTbl;

    public TblGeneratorService(ByteProvider byteProvider, TblStream targetTbl)
    {
        _byteProvider = byteProvider ?? throw new ArgumentNullException(nameof(byteProvider));
        _targetTbl = targetTbl ?? throw new ArgumentNullException(nameof(targetTbl));
    }

    public async Task<TblGenerationResult> GenerateFromTextAsync(TblGenerationOptions options, CancellationToken cancellationToken) =>
        await Task.Run(() => GenerateFromText(options, cancellationToken), cancellationToken);

    public TblGenerationResult GenerateFromText(TblGenerationOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        var result = new TblGenerationResult();
        try
        {
            var searchEngine = new RelativeSearchEngine(_byteProvider, _targetTbl);
            var searchOptions = new RelativeSearchOptions
            {
                SearchText = options.SampleText,
                CaseSensitive = options.CaseSensitive,
                StartPosition = options.StartPosition,
                EndPosition = options.EndPosition,
                MinMatchesRequired = options.MinMatches,
                MaxProposals = 10,
                UseParallelSearch = true
            };

            var searchResult = searchEngine.Search(searchOptions, cancellationToken);
            if (cancellationToken.IsCancellationRequested) { result.Success = false; result.ErrorMessage = "Operation cancelled"; return result; }
            if (!searchResult.Success || searchResult.Proposals.Count == 0) { result.Success = false; result.ErrorMessage = searchResult.ErrorMessage ?? "No proposals found"; return result; }

            result.Proposals = ConvertProposalsToEntries(searchResult.Proposals);
            result.Success = true;
            result.SearchDurationMs = searchResult.DurationMs;
            result.TotalProposals = searchResult.Proposals.Count;
        }
        catch (Exception ex) { result.Success = false; result.ErrorMessage = $"Error generating TBL: {ex.Message}"; }
        return result;
    }

    private List<TblProposal> ConvertProposalsToEntries(List<EncodingProposal> encodingProposals) =>
        encodingProposals.Select(proposal => new TblProposal
        {
            Offset = proposal.Offset,
            Score = proposal.Score,
            MatchCount = proposal.MatchCount,
            SampleText = proposal.SampleText,
            PreviewText = proposal.PreviewText,
            Entries = proposal.CharacterMapping.OrderBy(m => m.Value.actualByte)
                .Select(m => new TblEntryViewModel(new Dte(ByteConverters.ByteToHex(m.Value.actualByte), m.Value.character.ToString())))
                .ToList()
        }).ToList();

    public TblMergeResult MergeProposal(TblProposal proposal, MergeStrategy strategy, IEnumerable<TblEntryViewModel>? existingEntries = null)
    {
        var result = new TblMergeResult();
        foreach (var newEntry in proposal.Entries)
        {
            string hexKey = newEntry.Entry.ToUpperInvariant();
            var existing = _targetTbl.GetEntry(hexKey);
            if (existing != null)
            {
                if (strategy == MergeStrategy.Skip) { result.Skipped.Add(hexKey); continue; }
                if (strategy == MergeStrategy.Ask) { result.Conflicts.Add(new TblMergeConflict { HexKey = hexKey, ExistingValue = existing.Value, NewValue = newEntry.Value }); continue; }
                if (strategy == MergeStrategy.Overwrite) { _targetTbl.Remove(existing); result.Overwritten.Add(hexKey); }
            }
            try { var dte = new Dte(hexKey, newEntry.Value ?? ""); if (dte.IsValid) { _targetTbl.Add(dte); result.Added.Add(hexKey); } }
            catch { result.Skipped.Add(hexKey); }
        }
        return result;
    }

    public TblMergeResult PreviewMerge(TblProposal proposal)
    {
        var result = new TblMergeResult();
        foreach (var newEntry in proposal.Entries)
        {
            string hexKey = newEntry.Entry.ToUpperInvariant();
            var existing = _targetTbl.GetEntry(hexKey);
            if (existing != null) result.Conflicts.Add(new TblMergeConflict { HexKey = hexKey, ExistingValue = existing.Value, NewValue = newEntry.Value });
            else result.Added.Add(hexKey);
        }
        return result;
    }

    public void ResolveConflicts(TblProposal proposal, Dictionary<string, ConflictResolution> resolutions)
    {
        foreach (var newEntry in proposal.Entries)
        {
            string hexKey = newEntry.Entry.ToUpperInvariant();
            if (!resolutions.TryGetValue(hexKey, out var resolution)) continue;
            var existing = _targetTbl.GetEntry(hexKey);
            if (resolution == ConflictResolution.UseNew)
            {
                if (existing != null) _targetTbl.Remove(existing);
                var dte = new Dte(hexKey, newEntry.Value ?? "");
                if (dte.IsValid) _targetTbl.Add(dte);
            }
        }
    }
}

public class TblGenerationResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public List<TblProposal> Proposals { get; set; } = [];
    public long SearchDurationMs { get; set; }
    public int TotalProposals { get; set; }
}

public class TblProposal
{
    public byte Offset { get; set; }
    public double Score { get; set; }
    public int MatchCount { get; set; }
    public string? SampleText { get; set; }
    public string? PreviewText { get; set; }
    public List<TblEntryViewModel> Entries { get; set; } = [];
    public string ScoreDisplay => $"{Score:F1}";
    public string OffsetDisplay => $"+{Offset:D3}";
    public int EntryCount => Entries.Count;
}

public enum ConflictResolution { UseNew, KeepExisting, Skip }
