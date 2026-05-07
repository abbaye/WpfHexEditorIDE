// ==========================================================
// Project: whfmt.Fuzz
// File: FuzzVariant.cs
// Description: Result model for a single generated mutant file.
// ==========================================================

namespace WhfmtFuzz;

/// <summary>Mutation types available in the whfmt.Fuzz engine.</summary>
public enum MutationType
{
    /// <summary>Apply boundary values (0, 1, max-1, max) to the target field.</summary>
    BoundaryValues,
    /// <summary>Sweep all values from the field's valueMap plus invalid values.</summary>
    EnumSweep,
    /// <summary>Overwrite the magic bytes / signature field with garbage.</summary>
    CorruptSignature,
    /// <summary>Flip one random bit in the target field.</summary>
    BitFlip,
    /// <summary>Fill the target field with 0x00 bytes.</summary>
    ZeroField,
    /// <summary>Fill the target field with 0xFF bytes (integer overflow).</summary>
    Overflow,
    /// <summary>Replace the target field with cryptographically random bytes.</summary>
    RandomBytes,
    /// <summary>Truncate the file at the midpoint of the target field.</summary>
    Truncate,
    /// <summary>Duplicate the target field's bytes inline.</summary>
    Duplicate,
}

/// <summary>A single generated mutant file with provenance metadata.</summary>
public sealed class FuzzVariant
{
    /// <summary>Zero-based variant index within the generated batch.</summary>
    public int Index { get; init; }

    /// <summary>Name of the original input file.</summary>
    public string OriginalFile { get; init; } = "";

    /// <summary>Detected or forced format name.</summary>
    public string FormatName { get; init; } = "";

    /// <summary>Mutation strategy applied.</summary>
    public string Strategy { get; init; } = "";

    /// <summary>Target field name from the fuzz strategy.</summary>
    public string Field { get; init; } = "";

    /// <summary>Human-readable description of why this field is interesting to fuzz.</summary>
    public string Description { get; init; } = "";

    /// <summary>Mutated file content.</summary>
    public byte[] Data { get; init; } = [];

    /// <summary>Number of mutations applied (1 for single-mutation corpus).</summary>
    public int MutationCount { get; init; }

    /// <summary>Error message if variant could not be generated, otherwise null.</summary>
    public string? Error { get; init; }

    /// <summary>True when this variant represents an error condition.</summary>
    public bool IsError => Error is not null;

    /// <summary>Suggested output file name for this variant.</summary>
    public string SuggestedFileName =>
        $"{Path.GetFileNameWithoutExtension(OriginalFile)}_fuzz{Index:D4}_{Strategy}{Path.GetExtension(OriginalFile)}";

    internal static FuzzVariant ErrorVariant(string file, string message) => new()
    {
        OriginalFile = file,
        Error        = message,
        Data         = [],
    };
}
