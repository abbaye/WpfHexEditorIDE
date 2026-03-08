// ==========================================================
// Project: WpfHexEditor.Plugins.AssemblyExplorer
// File: Services/AssemblyAnalysisService.cs
// Author: Derek Tremblay
// Created: 2026-03-08
// Description:
//     Concrete implementation of IAssemblyAnalysisService using only
//     BCL types: System.Reflection.Metadata.PEReader + MetadataReader.
//     No external NuGet packages are required.
//
// Architecture Notes:
//     Pattern: Strategy (implements IAssemblyAnalysisService).
//     All heavy work runs on the caller-supplied thread (Task.Run in VM).
//     PEReader must stay open for the entire enumeration session —
//     it is disposed only after AssemblyModel construction is complete.
//     ct.ThrowIfCancellationRequested() called before each foreach body
//     to support cancellation on large assemblies (e.g. System.Runtime ~40K types).
// ==========================================================

using System.IO;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using WpfHexEditor.Plugins.AssemblyExplorer.Models;

namespace WpfHexEditor.Plugins.AssemblyExplorer.Services;

/// <summary>
/// Parses PE files using the BCL MetadataReader + PEReader pipeline.
/// Produces an immutable <see cref="AssemblyModel"/> suitable for marshalling
/// to the WPF UI thread.
/// </summary>
public sealed class AssemblyAnalysisService : IAssemblyAnalysisService
{
    private readonly PeOffsetResolver _offsetResolver;

    public AssemblyAnalysisService(PeOffsetResolver offsetResolver)
        => _offsetResolver = offsetResolver;

    // ── IAssemblyAnalysisService ──────────────────────────────────────────────

    /// <inheritdoc/>
    public bool CanAnalyze(string filePath)
    {
        if (!File.Exists(filePath)) return false;

        // Check MZ magic bytes — valid for both managed and native PE.
        using var fs = File.OpenRead(filePath);
        Span<byte> header = stackalloc byte[2];
        return fs.Read(header) == 2 && header[0] == 0x4D && header[1] == 0x5A; // 'MZ'
    }

    /// <inheritdoc/>
    public Task<AssemblyModel> AnalyzeAsync(string filePath, CancellationToken ct = default)
        => Task.Run(() => Analyze(filePath, ct), ct);

    // ── Core analysis ─────────────────────────────────────────────────────────

    private AssemblyModel Analyze(string filePath, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        using var stream   = File.OpenRead(filePath);
        using var peReader = new PEReader(stream);

        var sections = ReadSections(peReader);

        if (!peReader.HasMetadata)
            return BuildNativePeModel(filePath, sections);

        var mdReader = peReader.GetMetadataReader();
        return BuildManagedModel(filePath, peReader, mdReader, sections, ct);
    }

    // ── Managed PE parsing ────────────────────────────────────────────────────

    private AssemblyModel BuildManagedModel(
        string         filePath,
        PEReader       peReader,
        MetadataReader mdReader,
        IReadOnlyList<PeSectionEntry> sections,
        CancellationToken ct)
    {
        var asmDef  = mdReader.GetAssemblyDefinition();
        var name    = mdReader.GetString(asmDef.Name);
        var version = asmDef.Version;
        var culture = mdReader.GetString(asmDef.Culture);
        var pkt     = BuildPublicKeyToken(mdReader.GetBlobBytes(asmDef.PublicKey));

        var types      = ReadTypes(peReader, mdReader, ct);
        var references = ReadReferences(peReader, mdReader, ct);
        var resources  = ReadResources(mdReader, ct);
        var modules    = ReadModules(mdReader, ct);

        return new AssemblyModel
        {
            Name           = name,
            FilePath       = filePath,
            Version        = version,
            Culture        = string.IsNullOrEmpty(culture) ? null : culture,
            PublicKeyToken = pkt,
            IsManaged      = true,
            Types          = types,
            References     = references,
            Resources      = resources,
            Modules        = modules,
            Sections       = sections
        };
    }

    // ── Type enumeration ─────────────────────────────────────────────────────

    private List<TypeModel> ReadTypes(
        PEReader       peReader,
        MetadataReader mdReader,
        CancellationToken ct)
    {
        var types = new List<TypeModel>();

        foreach (var handle in mdReader.TypeDefinitions)
        {
            ct.ThrowIfCancellationRequested();

            var typeDef = mdReader.GetTypeDefinition(handle);
            var ns      = mdReader.GetString(typeDef.Namespace);
            var typeName = mdReader.GetString(typeDef.Name);

            // Skip the synthetic <Module> type that always appears as the first row.
            if (string.IsNullOrEmpty(typeName) || typeName == "<Module>") continue;

            var kind    = ResolveTypeKind(typeDef.Attributes);
            var isPublic = (typeDef.Attributes & TypeAttributes.Public) != 0
                        || (typeDef.Attributes & TypeAttributes.NestedPublic) != 0;
            var token   = MetadataTokens.GetToken(handle);
            var offset  = _offsetResolver.Resolve(handle, peReader, mdReader);

            types.Add(new TypeModel
            {
                Namespace     = ns,
                Name          = typeName,
                Kind          = kind,
                IsPublic      = isPublic,
                PeOffset      = offset,
                MetadataToken = token,
                Methods       = ReadMethods(typeDef, peReader, mdReader, ct),
                Fields        = ReadFields(typeDef, peReader, mdReader, ct),
                Properties    = ReadProperties(typeDef, mdReader, ct),
                Events        = ReadEvents(typeDef, mdReader, ct)
            });
        }

        return types;
    }

    private static List<MemberModel> ReadMethods(
        TypeDefinition typeDef,
        PEReader       peReader,
        MetadataReader mdReader,
        CancellationToken ct)
    {
        var methods = new List<MemberModel>();

        foreach (var handle in typeDef.GetMethods())
        {
            ct.ThrowIfCancellationRequested();

            var mDef  = mdReader.GetMethodDefinition(handle);
            var mName = mdReader.GetString(mDef.Name);
            var pub   = (mDef.Attributes & MethodAttributes.Public) != 0;
            var stat  = (mDef.Attributes & MethodAttributes.Static) != 0;

            methods.Add(new MemberModel
            {
                Name          = mName,
                Kind          = MemberKind.Method,
                IsPublic      = pub,
                IsStatic      = stat,
                MetadataToken = MetadataTokens.GetToken(handle),
                PeOffset      = 0L // Phase 2: _offsetResolver.Resolve(handle, peReader, mdReader)
            });
        }

        return methods;
    }

    private static List<MemberModel> ReadFields(
        TypeDefinition typeDef,
        PEReader       peReader,
        MetadataReader mdReader,
        CancellationToken ct)
    {
        var fields = new List<MemberModel>();

        foreach (var handle in typeDef.GetFields())
        {
            ct.ThrowIfCancellationRequested();

            var fDef  = mdReader.GetFieldDefinition(handle);
            var fName = mdReader.GetString(fDef.Name);
            var pub   = (fDef.Attributes & FieldAttributes.Public) != 0;
            var stat  = (fDef.Attributes & FieldAttributes.Static) != 0;

            fields.Add(new MemberModel
            {
                Name          = fName,
                Kind          = MemberKind.Field,
                IsPublic      = pub,
                IsStatic      = stat,
                MetadataToken = MetadataTokens.GetToken(handle),
                PeOffset      = 0L
            });
        }

        return fields;
    }

    private static List<MemberModel> ReadProperties(
        TypeDefinition typeDef,
        MetadataReader mdReader,
        CancellationToken ct)
    {
        var props = new List<MemberModel>();

        foreach (var handle in typeDef.GetProperties())
        {
            ct.ThrowIfCancellationRequested();

            var pDef  = mdReader.GetPropertyDefinition(handle);
            var pName = mdReader.GetString(pDef.Name);

            props.Add(new MemberModel
            {
                Name          = pName,
                Kind          = MemberKind.Property,
                MetadataToken = MetadataTokens.GetToken(handle),
                PeOffset      = 0L
            });
        }

        return props;
    }

    private static List<MemberModel> ReadEvents(
        TypeDefinition typeDef,
        MetadataReader mdReader,
        CancellationToken ct)
    {
        var events = new List<MemberModel>();

        foreach (var handle in typeDef.GetEvents())
        {
            ct.ThrowIfCancellationRequested();

            var eDef  = mdReader.GetEventDefinition(handle);
            var eName = mdReader.GetString(eDef.Name);

            events.Add(new MemberModel
            {
                Name          = eName,
                Kind          = MemberKind.Event,
                MetadataToken = MetadataTokens.GetToken(handle),
                PeOffset      = 0L
            });
        }

        return events;
    }

    // ── References ───────────────────────────────────────────────────────────

    private static List<AssemblyRef> ReadReferences(
        PEReader       peReader,
        MetadataReader mdReader,
        CancellationToken ct)
    {
        var refs = new List<AssemblyRef>();

        foreach (var handle in mdReader.AssemblyReferences)
        {
            ct.ThrowIfCancellationRequested();

            var asmRef = mdReader.GetAssemblyReference(handle);
            var refName = mdReader.GetString(asmRef.Name);
            var pktBytes = mdReader.GetBlobBytes(asmRef.PublicKeyOrToken);
            var pkt     = BuildPublicKeyToken(pktBytes);

            refs.Add(new AssemblyRef(refName, asmRef.Version, pkt));
        }

        return refs;
    }

    // ── Resources ────────────────────────────────────────────────────────────

    private static List<ResourceEntry> ReadResources(
        MetadataReader mdReader,
        CancellationToken ct)
    {
        var resources = new List<ResourceEntry>();

        foreach (var handle in mdReader.ManifestResources)
        {
            ct.ThrowIfCancellationRequested();

            var res   = mdReader.GetManifestResource(handle);
            var rName = mdReader.GetString(res.Name);

            resources.Add(new ResourceEntry(rName, res.Offset, 0));
        }

        return resources;
    }

    // ── Modules ──────────────────────────────────────────────────────────────

    private static List<ModuleEntry> ReadModules(
        MetadataReader mdReader,
        CancellationToken ct)
    {
        var modules = new List<ModuleEntry>();
        ct.ThrowIfCancellationRequested();

        var moduleDef = mdReader.GetModuleDefinition();
        modules.Add(new ModuleEntry(
            mdReader.GetString(moduleDef.Name),
            mdReader.GetGuid(moduleDef.Mvid)));

        // ModuleRef table (unmanaged module references, e.g. P/Invoke targets)
        for (int i = 1; i <= mdReader.GetTableRowCount(TableIndex.ModuleRef); i++)
        {
            ct.ThrowIfCancellationRequested();
            var handle = MetadataTokens.ModuleReferenceHandle(i);
            var modRef = mdReader.GetModuleReference(handle);
            modules.Add(new ModuleEntry(mdReader.GetString(modRef.Name), Guid.Empty));
        }

        return modules;
    }

    // ── PE sections (managed + native) ────────────────────────────────────────

    private static List<PeSectionEntry> ReadSections(PEReader peReader)
    {
        var sections = new List<PeSectionEntry>();

        foreach (var sec in peReader.PEHeaders.SectionHeaders)
        {
            sections.Add(new PeSectionEntry(
                sec.Name,
                sec.VirtualAddress,
                sec.VirtualSize,
                sec.PointerToRawData,
                sec.SizeOfRawData));
        }

        return sections;
    }

    // ── Native PE stub ───────────────────────────────────────────────────────

    private static AssemblyModel BuildNativePeModel(
        string filePath,
        IReadOnlyList<PeSectionEntry> sections)
        => new()
        {
            Name      = Path.GetFileNameWithoutExtension(filePath),
            FilePath  = filePath,
            IsManaged = false,
            Sections  = sections
        };

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static TypeKind ResolveTypeKind(TypeAttributes attributes)
    {
        var layout = attributes & TypeAttributes.ClassSemanticsMask;

        if (layout == TypeAttributes.Interface)
            return TypeKind.Interface;

        // Sealed + no interface = likely enum or struct (heuristic — base type walk deferred)
        if ((attributes & TypeAttributes.Sealed) != 0
            && (attributes & TypeAttributes.Abstract) != 0)
            return TypeKind.Delegate; // static class (abstract+sealed) — could be delegate

        if ((attributes & TypeAttributes.SequentialLayout) != 0
            || (attributes & TypeAttributes.ExplicitLayout) != 0)
            return TypeKind.Struct;

        return TypeKind.Class;
    }

    private static string? BuildPublicKeyToken(byte[] bytes)
    {
        if (bytes.Length == 0) return null;

        // A full public key needs SHA-1 hashing to get the 8-byte token.
        // For display purposes in this stub, show first 8 bytes as hex.
        var len = Math.Min(bytes.Length, 8);
        return Convert.ToHexString(bytes[..len]).ToLowerInvariant();
    }
}
