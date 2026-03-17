// ==========================================================
// Project: WpfHexEditor.Core.AssemblyAnalysis
// File: Services/PdbReader.cs
// Author: Derek Tremblay
// Created: 2026-03-16
// License: GNU Affero General Public License v3.0 (AGPL-3.0)
// Description:
//     BCL-only portable PDB reader.  Opens an embedded or side-by-side
//     <assembly>.pdb file and provides:
//       - Sequence points (IL offset ↔ source line mapping)
//       - Local variable names from scope data
//       - SourceLink map (JSON blob → URL templates)
//
// Architecture Notes:
//     Pattern: Disposable reader (holds a MetadataReaderProvider/file stream).
//     Open once per operation via PdbReader.TryOpen(), use, then dispose.
//     BCL-only: System.Reflection.Metadata + System.Text.Json (both inbox in net8.0).
// ==========================================================

using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Text.Json;

namespace WpfHexEditor.Core.AssemblyAnalysis.Services;

// ── Public models ─────────────────────────────────────────────────────────────

/// <summary>Maps one IL offset to a source location.</summary>
public sealed record SequencePoint(
    int     IlOffset,
    string? DocumentPath,
    int     StartLine,
    int     StartColumn,
    int     EndLine,
    int     EndColumn)
{
    /// <summary>True for hidden sequence points (compiler-generated, no real source).</summary>
    public bool IsHidden => StartLine == 0xFEEFEE;
}

/// <summary>A local variable name entry from the PDB scope data.</summary>
public sealed record LocalVariableInfo(int SlotIndex, string Name);

/// <summary>SourceLink URL mapping: wildcard local path patterns → URL templates.</summary>
public sealed record SourceLinkMap(IReadOnlyDictionary<string, string> Mappings);

// ── PdbReader ─────────────────────────────────────────────────────────────────

/// <summary>
/// Reads a portable PDB file and exposes sequence points, local variable names,
/// and the SourceLink map.  Dispose after use — holds an open file stream.
/// </summary>
public sealed class PdbReader : IDisposable
{
    private static readonly Guid SourceLinkGuid =
        new("CC110556-A091-4D38-9FEC-25AB9A351A6A");

    private readonly MetadataReaderProvider _provider;
    private readonly MetadataReader         _reader;
    private bool                            _disposed;

    private PdbReader(MetadataReaderProvider provider)
    {
        _provider = provider;
        _reader   = provider.GetMetadataReader();
    }

    // ── Factory ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Tries to open the portable PDB that sits next to <paramref name="assemblyFilePath"/>.
    /// Returns false when no .pdb exists or the file is not a portable PDB.
    /// </summary>
    public static bool TryOpen(string assemblyFilePath, out PdbReader? reader)
    {
        reader = null;
        if (string.IsNullOrEmpty(assemblyFilePath)) return false;

        var pdbPath = Path.ChangeExtension(assemblyFilePath, ".pdb");
        if (!File.Exists(pdbPath)) return false;

        try
        {
            var stream   = new FileStream(pdbPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var provider = MetadataReaderProvider.FromPortablePdbStream(stream);
            reader = new PdbReader(provider);
            return true;
        }
        catch
        {
            return false;
        }
    }

    // ── Sequence points ───────────────────────────────────────────────────────

    /// <summary>
    /// Returns the sequence points for the method identified by
    /// <paramref name="methodMetadataToken"/> (e.g. 0x06000012).
    /// Returns an empty list when the token is absent from the PDB.
    /// </summary>
    public IReadOnlyList<SequencePoint> GetSequencePoints(int methodMetadataToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        try
        {
            var defHandle = (MethodDefinitionHandle)MetadataTokens.Handle(methodMetadataToken);
            var mdi       = _reader.GetMethodDebugInformation(defHandle.ToDebugInformationHandle());

            var result = new List<SequencePoint>();

            string? lastDocument = null;
            foreach (var sp in mdi.GetSequencePoints())
            {
                if (!sp.Document.IsNil)
                {
                    var doc = _reader.GetDocument(sp.Document);
                    if (!doc.Name.IsNil)
                        lastDocument = _reader.GetString(doc.Name);
                }

                result.Add(new SequencePoint(
                    IlOffset:    sp.Offset,
                    DocumentPath: lastDocument,
                    StartLine:   sp.StartLine,
                    StartColumn: sp.StartColumn,
                    EndLine:     sp.EndLine,
                    EndColumn:   sp.EndColumn));
            }

            return result;
        }
        catch
        {
            return [];
        }
    }

    // ── Local variable names ──────────────────────────────────────────────────

    /// <summary>
    /// Returns local variable name info for the method identified by
    /// <paramref name="methodMetadataToken"/>.
    /// </summary>
    public IReadOnlyList<LocalVariableInfo> GetLocalVariables(int methodMetadataToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        try
        {
            var defHandle = (MethodDefinitionHandle)MetadataTokens.Handle(methodMetadataToken);
            var scopes    = _reader.GetLocalScopes(defHandle);
            var result    = new List<LocalVariableInfo>();

            foreach (var scopeHandle in scopes)
            {
                var scope = _reader.GetLocalScope(scopeHandle);
                foreach (var varHandle in scope.GetLocalVariables())
                {
                    var lv   = _reader.GetLocalVariable(varHandle);
                    var name = _reader.GetString(lv.Name);
                    if (!string.IsNullOrWhiteSpace(name))
                        result.Add(new LocalVariableInfo(lv.Index, name));
                }
            }

            return result;
        }
        catch
        {
            return [];
        }
    }

    // ── SourceLink ────────────────────────────────────────────────────────────

    /// <summary>
    /// Reads the SourceLink custom debug information blob and parses it as a
    /// JSON document mapping wildcard local paths to URL templates.
    /// Returns null when no SourceLink entry is present in the PDB.
    /// </summary>
    public SourceLinkMap? GetSourceLinkMap()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        try
        {
            foreach (var cdiHandle in _reader.CustomDebugInformation)
            {
                var cdi = _reader.GetCustomDebugInformation(cdiHandle);
                if (_reader.GetGuid(cdi.Kind) != SourceLinkGuid) continue;

                var json = Encoding.UTF8.GetString(_reader.GetBlobBytes(cdi.Value));
                return ParseSourceLinkJson(json);
            }
        }
        catch { /* non-fatal */ }

        return null;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static SourceLinkMap? ParseSourceLinkJson(string json)
    {
        try
        {
            using var doc  = JsonDocument.Parse(json);
            var mappings   = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (!doc.RootElement.TryGetProperty("documents", out var docs)) return null;

            foreach (var prop in docs.EnumerateObject())
            {
                var value = prop.Value.GetString();
                if (value is not null)
                    mappings[prop.Name] = value;
            }

            return new SourceLinkMap(mappings);
        }
        catch
        {
            return null;
        }
    }

    // ── IDisposable ───────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _provider.Dispose();
    }
}
