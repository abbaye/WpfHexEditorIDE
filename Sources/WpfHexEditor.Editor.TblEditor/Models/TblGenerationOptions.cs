//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.Editor.TblEditor.Models;

/// <summary>
/// Options for TBL generation from text
/// </summary>
public class TblGenerationOptions
{
    public string? SampleText { get; set; }
    public bool CaseSensitive { get; set; }
    public long StartPosition { get; set; }
    public long EndPosition { get; set; }
    public int MinMatches { get; set; } = 1;
    public int MaxProposalsToShow { get; set; } = 10;
    public MergeStrategy MergeStrategy { get; set; } = MergeStrategy.Skip;
}

/// <summary>
/// Strategy for merging generated entries with existing TBL
/// </summary>
public enum MergeStrategy { Skip, Overwrite, Ask }
