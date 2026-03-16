// ==========================================================
// Project: WpfHexEditor.Core.AssemblyAnalysis
// File: Services/AssemblyAnalysisEngine.cs
// Author: Derek Tremblay
// Created: 2026-03-08
// Updated: 2026-03-16 — Phase 2: IsReadOnly, IsOverride, ConstantValue,
//     GenericParameters, XmlDocComment populated from metadata + companion XML.
// License: GNU Affero General Public License v3.0 (AGPL-3.0)
// Description:
//     Concrete implementation of IAssemblyAnalysisEngine using only BCL types:
//     System.Reflection.Metadata.PEReader + MetadataReader + SignatureDecoder.
//     No external NuGet packages required.
//
// Architecture Notes:
//     Pattern: Strategy (implements IAssemblyAnalysisEngine).
//     All heavy work runs on the caller-supplied thread (Task.Run in VM).
//     PEReader must stay open for the entire enumeration session.
//     ct.ThrowIfCancellationRequested() called before each foreach body
//     to support cancellation on large assemblies (e.g. System.Runtime ~40K types).
//     Upgraded from plugin stub:
//       - SignatureDecoder now populates MemberModel.Signature
//       - PeOffsetResolver now resolves real file offsets
//       - BaseTypeName + InterfaceNames + CustomAttributes populated
//       - TargetFramework detected from TargetFrameworkAttribute
//       - IsReadOnly/IsOverride/ConstantValue/GenericParameters (Phase 2)
//       - XmlDocComment from companion .xml (Phase 2, optional)
// ==========================================================

using System.IO;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using WpfHexEditor.Core.AssemblyAnalysis.Models;

namespace WpfHexEditor.Core.AssemblyAnalysis.Services;

/// <summary>
/// Parses PE files using the BCL MetadataReader + PEReader pipeline.
/// Produces an immutable <see cref="AssemblyModel"/> suitable for marshalling to the UI thread.
/// </summary>
public sealed class AssemblyAnalysisEngine : IAssemblyAnalysisEngine
{
    private readonly PeOffsetResolver  _offsetResolver  = new();
    private readonly SignatureDecoder  _sigDecoder      = new();

    // ── IAssemblyAnalysisEngine ───────────────────────────────────────────────

    /// <inheritdoc/>
    public bool CanAnalyze(string filePath)
    {
        if (!File.Exists(filePath)) return false;

        // Check MZ magic bytes — valid for both managed and native PE.
        // Use FileShare.ReadWrite so the check succeeds even when HexEditor holds the file open.
        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        Span<byte> header = stackalloc byte[2];
        return fs.Read(header) == 2 && header[0] == 0x4D && header[1] == 0x5A; // 'MZ'
    }

    /// <inheritdoc/>
    public bool HasManagedMetadata(string filePath) => CheckManagedMetadata(filePath);

    /// <summary>
    /// Static helper — usable without an engine instance (e.g. in dialog filtering logic).
    /// Opens the PE file and checks whether a .NET CLR header is present.
    /// Uses <c>FileShare.ReadWrite</c> so it works while the HexEditor holds the file open.
    /// Returns false on any I/O or format error.
    /// </summary>
    public static bool CheckManagedMetadata(string filePath)
    {
        if (!File.Exists(filePath)) return false;
        try
        {
            using var fs       = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var peReader = new PEReader(fs);
            return peReader.HasMetadata;
        }
        catch { return false; }
    }

    /// <inheritdoc/>
    public Task<AssemblyModel> AnalyzeAsync(string filePath, CancellationToken ct = default)
        => Task.Run(() => Analyze(filePath, ct), ct);

    // ── Core analysis ─────────────────────────────────────────────────────────

    private AssemblyModel Analyze(string filePath, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        // FileShare.ReadWrite allows concurrent access when HexEditor holds the file open.
        using var stream   = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
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

        var targetFramework = DetectTargetFramework(mdReader);
        var xmlDoc          = XmlDocReader.TryLoad(filePath);
        var types           = ReadTypes(peReader, mdReader, xmlDoc, ct);
        var references      = ReadReferences(mdReader, ct);
        var resources       = ReadResources(mdReader, ct);
        var modules         = ReadModules(mdReader, ct);
        var forwarders      = ReadExportedTypes(mdReader, ct);

        return new AssemblyModel
        {
            Name            = name,
            FilePath        = filePath,
            Version         = version,
            Culture         = string.IsNullOrEmpty(culture) ? null : culture,
            PublicKeyToken  = pkt,
            IsManaged       = true,
            TargetFramework = targetFramework,
            Types           = types,
            References      = references,
            Resources       = resources,
            Modules         = modules,
            Sections        = sections,
            TypeForwarders  = forwarders
        };
    }

    // ── Target framework detection ────────────────────────────────────────────

    private static string? DetectTargetFramework(MetadataReader mdReader)
    {
        try
        {
            // Scan assembly-level custom attributes for TargetFrameworkAttribute.
            foreach (var handle in mdReader.GetAssemblyDefinition().GetCustomAttributes())
            {
                var attr      = mdReader.GetCustomAttribute(handle);
                var ctorHandle = attr.Constructor;

                string? ctorTypeName = null;
                if (ctorHandle.Kind == HandleKind.MemberReference)
                {
                    var mref = mdReader.GetMemberReference((MemberReferenceHandle)ctorHandle);
                    if (mref.Parent.Kind == HandleKind.TypeReference)
                    {
                        var typeRef = mdReader.GetTypeReference((TypeReferenceHandle)mref.Parent);
                        ctorTypeName = mdReader.GetString(typeRef.Name);
                    }
                }

                if (ctorTypeName != "TargetFrameworkAttribute") continue;

                // Blob: prolog(2) + string argument
                var blob   = mdReader.GetBlobBytes(attr.Value);
                if (blob.Length < 4) continue;

                // Skip 2-byte prolog (0x01 0x00), then read a packed string.
                var strLen = blob[2];
                if (strLen == 0xFF || blob.Length < 3 + strLen) continue;

                var tfm = System.Text.Encoding.UTF8.GetString(blob, 3, strLen);
                return FormatTargetFramework(tfm);
            }
        }
        catch { /* Non-fatal — return null */ }

        return null;
    }

    private static string FormatTargetFramework(string tfm)
    {
        // ".NETCoreApp,Version=v8.0"   → ".NET 8.0"
        // ".NETFramework,Version=v4.8" → ".NET Framework 4.8"
        // ".NETStandard,Version=v2.0"  → ".NET Standard 2.0"
        if (tfm.StartsWith(".NETCoreApp,Version=v", StringComparison.OrdinalIgnoreCase))
            return ".NET " + tfm[".NETCoreApp,Version=v".Length..];
        if (tfm.StartsWith(".NETFramework,Version=v", StringComparison.OrdinalIgnoreCase))
            return ".NET Framework " + tfm[".NETFramework,Version=v".Length..];
        if (tfm.StartsWith(".NETStandard,Version=v", StringComparison.OrdinalIgnoreCase))
            return ".NET Standard " + tfm[".NETStandard,Version=v".Length..];
        return tfm;
    }

    // ── Type enumeration ─────────────────────────────────────────────────────

    private List<TypeModel> ReadTypes(
        PEReader peReader, MetadataReader mdReader, XmlDocReader? xmlDoc, CancellationToken ct)
    {
        var types = new List<TypeModel>();

        foreach (var handle in mdReader.TypeDefinitions)
        {
            ct.ThrowIfCancellationRequested();

            var typeDef  = mdReader.GetTypeDefinition(handle);
            var ns       = mdReader.GetString(typeDef.Namespace);
            var typeName = mdReader.GetString(typeDef.Name);

            // Skip the synthetic <Module> type.
            if (string.IsNullOrEmpty(typeName) || typeName == "<Module>") continue;

            var kind       = ResolveTypeKind(typeDef.Attributes);
            var isPublic   = (typeDef.Attributes & TypeAttributes.Public) != 0
                          || (typeDef.Attributes & TypeAttributes.NestedPublic) != 0;
            var isAbstract = (typeDef.Attributes & TypeAttributes.Abstract) != 0;
            var isSealed   = (typeDef.Attributes & TypeAttributes.Sealed) != 0;
            var token      = MetadataTokens.GetToken(handle);
            var offset     = _offsetResolver.Resolve(handle, peReader, mdReader);
            var baseName   = ResolveBaseTypeName(typeDef.BaseType, mdReader);
            var ifaces     = ReadInterfaceNames(typeDef, mdReader);
            var attrs      = ReadCustomAttributeNames(typeDef.GetCustomAttributes(), mdReader);
            var genParams  = ReadGenericParamNames(typeDef.GetGenericParameters(), mdReader);
            var fullName   = string.IsNullOrEmpty(ns) ? typeName : $"{ns}.{typeName}";
            var xmlComment = xmlDoc?.GetSummary(XmlDocReader.TypeDocId(ns, typeName));

            types.Add(new TypeModel
            {
                Namespace         = ns,
                Name              = typeName,
                Kind              = kind,
                IsPublic          = isPublic,
                IsAbstract        = isAbstract,
                IsSealed          = isSealed,
                PeOffset          = offset,
                MetadataToken     = token,
                BaseTypeName      = baseName,
                InterfaceNames    = ifaces,
                CustomAttributes  = attrs,
                GenericParameters = genParams,
                XmlDocComment     = xmlComment,
                Methods           = ReadMethods(typeDef, peReader, mdReader, xmlDoc, fullName, ct),
                Fields            = ReadFields(typeDef, peReader, mdReader, xmlDoc, fullName, ct),
                Properties        = ReadProperties(typeDef, mdReader, xmlDoc, fullName, ct),
                Events            = ReadEvents(typeDef, mdReader, xmlDoc, fullName, ct)
            });
        }

        return types;
    }

    // ── Member enumeration ────────────────────────────────────────────────────

    private List<MemberModel> ReadMethods(
        TypeDefinition typeDef, PEReader peReader, MetadataReader mdReader,
        XmlDocReader? xmlDoc, string typeFullName, CancellationToken ct)
    {
        var methods = new List<MemberModel>();

        foreach (var handle in typeDef.GetMethods())
        {
            ct.ThrowIfCancellationRequested();

            var mDef      = mdReader.GetMethodDefinition(handle);
            var mName     = mdReader.GetString(mDef.Name);
            var pub       = (mDef.Attributes & MethodAttributes.Public) != 0;
            var stat      = (mDef.Attributes & MethodAttributes.Static) != 0;
            var abstr     = (mDef.Attributes & MethodAttributes.Abstract) != 0;
            var virt      = (mDef.Attributes & MethodAttributes.Virtual) != 0;
            var newSlot   = (mDef.Attributes & MethodAttributes.NewSlot) != 0;
            var offset    = _offsetResolver.Resolve(handle, peReader, mdReader);
            var byteLen   = ResolveMethodByteLength(mDef, peReader);
            var sig       = TryDecodeMethodSignature(mDef, mdReader);
            var attrs     = ReadCustomAttributeNames(mDef.GetCustomAttributes(), mdReader);
            var genParams = ReadGenericParamNames(mDef.GetGenericParameters(), mdReader);
            var xmlDoc_   = xmlDoc?.GetSummary(XmlDocReader.MethodDocId(typeFullName, mName));

            methods.Add(new MemberModel
            {
                Name              = mName,
                Kind              = MemberKind.Method,
                IsPublic          = pub,
                IsStatic          = stat,
                IsAbstract        = abstr,
                IsVirtual         = virt && newSlot && !abstr,  // new virtual slot only
                IsOverride        = virt && !newSlot && !abstr, // overrides inherited slot
                MetadataToken     = MetadataTokens.GetToken(handle),
                PeOffset          = offset,
                ByteLength        = byteLen,
                Signature         = sig,
                CustomAttributes  = attrs,
                GenericParameters = genParams,
                XmlDocComment     = xmlDoc_
            });
        }

        return methods;
    }

    private List<MemberModel> ReadFields(
        TypeDefinition typeDef, PEReader peReader, MetadataReader mdReader,
        XmlDocReader? xmlDoc, string typeFullName, CancellationToken ct)
    {
        var fields = new List<MemberModel>();

        foreach (var handle in typeDef.GetFields())
        {
            ct.ThrowIfCancellationRequested();

            var fDef          = mdReader.GetFieldDefinition(handle);
            var fName         = mdReader.GetString(fDef.Name);
            var pub           = (fDef.Attributes & FieldAttributes.Public) != 0;
            var stat          = (fDef.Attributes & FieldAttributes.Static) != 0;
            var isReadOnly    = (fDef.Attributes & FieldAttributes.InitOnly) != 0;
            var typeSig       = TryDecodeFieldSignature(fDef, mdReader);
            var attrs         = ReadCustomAttributeNames(fDef.GetCustomAttributes(), mdReader);
            var constantValue = TryGetConstantValue(fDef, mdReader);
            var xmlDoc_       = xmlDoc?.GetSummary(XmlDocReader.FieldDocId(typeFullName, fName));

            fields.Add(new MemberModel
            {
                Name             = fName,
                Kind             = MemberKind.Field,
                IsPublic         = pub,
                IsStatic         = stat,
                IsReadOnly       = isReadOnly,
                ConstantValue    = constantValue,
                MetadataToken    = MetadataTokens.GetToken(handle),
                PeOffset         = 0L, // Field rows don't have IL bodies
                Signature        = typeSig,
                CustomAttributes = attrs,
                XmlDocComment    = xmlDoc_
            });
        }

        return fields;
    }

    private List<MemberModel> ReadProperties(
        TypeDefinition typeDef, MetadataReader mdReader,
        XmlDocReader? xmlDoc, string typeFullName, CancellationToken ct)
    {
        var props = new List<MemberModel>();

        foreach (var handle in typeDef.GetProperties())
        {
            ct.ThrowIfCancellationRequested();

            var pDef    = mdReader.GetPropertyDefinition(handle);
            var pName   = mdReader.GetString(pDef.Name);
            var typeSig = TryDecodePropertySignature(pDef, mdReader);
            var xmlDoc_ = xmlDoc?.GetSummary(XmlDocReader.PropertyDocId(typeFullName, pName));

            props.Add(new MemberModel
            {
                Name          = pName,
                Kind          = MemberKind.Property,
                MetadataToken = MetadataTokens.GetToken(handle),
                Signature     = typeSig,
                XmlDocComment = xmlDoc_
            });
        }

        return props;
    }

    private static List<MemberModel> ReadEvents(
        TypeDefinition typeDef, MetadataReader mdReader,
        XmlDocReader? xmlDoc, string typeFullName, CancellationToken ct)
    {
        var events = new List<MemberModel>();

        foreach (var handle in typeDef.GetEvents())
        {
            ct.ThrowIfCancellationRequested();

            var eDef    = mdReader.GetEventDefinition(handle);
            var eName   = mdReader.GetString(eDef.Name);
            var xmlDoc_ = xmlDoc?.GetSummary(XmlDocReader.EventDocId(typeFullName, eName));

            events.Add(new MemberModel
            {
                Name          = eName,
                Kind          = MemberKind.Event,
                MetadataToken = MetadataTokens.GetToken(handle),
                XmlDocComment = xmlDoc_
            });
        }

        return events;
    }

    // ── References, Resources, Modules ───────────────────────────────────────

    private static List<AssemblyRef> ReadReferences(MetadataReader mdReader, CancellationToken ct)
    {
        var refs = new List<AssemblyRef>();

        foreach (var handle in mdReader.AssemblyReferences)
        {
            ct.ThrowIfCancellationRequested();

            var asmRef  = mdReader.GetAssemblyReference(handle);
            var refName = mdReader.GetString(asmRef.Name);
            var pkt     = BuildPublicKeyToken(mdReader.GetBlobBytes(asmRef.PublicKeyOrToken));

            refs.Add(new AssemblyRef(refName, asmRef.Version, pkt));
        }

        return refs;
    }

    private static List<ResourceEntry> ReadResources(MetadataReader mdReader, CancellationToken ct)
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

    private static List<ModuleEntry> ReadModules(MetadataReader mdReader, CancellationToken ct)
    {
        var modules = new List<ModuleEntry>();
        ct.ThrowIfCancellationRequested();

        var moduleDef = mdReader.GetModuleDefinition();
        modules.Add(new ModuleEntry(mdReader.GetString(moduleDef.Name), mdReader.GetGuid(moduleDef.Mvid)));

        for (int i = 1; i <= mdReader.GetTableRowCount(TableIndex.ModuleRef); i++)
        {
            ct.ThrowIfCancellationRequested();
            var handle = MetadataTokens.ModuleReferenceHandle(i);
            var modRef = mdReader.GetModuleReference(handle);
            modules.Add(new ModuleEntry(mdReader.GetString(modRef.Name), Guid.Empty));
        }

        return modules;
    }

    // ── Type forwarders (ExportedType table) ─────────────────────────────────

    /// <summary>
    /// Reads the ExportedType metadata table and returns only type-forwarder entries.
    /// Facade assemblies like Microsoft.Win32.Primitives or Microsoft.VisualBasic
    /// use this table to redirect callers to the real implementation assembly.
    /// </summary>
    private static List<TypeForwarderEntry> ReadExportedTypes(
        MetadataReader mdReader, CancellationToken ct)
    {
        var forwarders = new List<TypeForwarderEntry>();

        foreach (var handle in mdReader.ExportedTypes)
        {
            ct.ThrowIfCancellationRequested();

            var et = mdReader.GetExportedType(handle);
            if (!et.IsForwarder) continue; // skip re-exported nested types etc.

            var ns   = mdReader.GetString(et.Namespace);
            var name = mdReader.GetString(et.Name);
            if (string.IsNullOrEmpty(name)) continue;

            forwarders.Add(new TypeForwarderEntry(ns, name));
        }

        return forwarders;
    }

    // ── PE Sections ───────────────────────────────────────────────────────────

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

    // ── Native PE stub ────────────────────────────────────────────────────────

    private static AssemblyModel BuildNativePeModel(
        string filePath, IReadOnlyList<PeSectionEntry> sections)
        => new()
        {
            Name      = Path.GetFileNameWithoutExtension(filePath),
            FilePath  = filePath,
            IsManaged = false,
            Sections  = sections
        };

    // ── Base type + interface + attribute helpers ─────────────────────────────

    private static string? ResolveBaseTypeName(EntityHandle baseHandle, MetadataReader mdReader)
    {
        if (baseHandle.IsNil) return null;
        try
        {
            return baseHandle.Kind switch
            {
                HandleKind.TypeDefinition =>
                    mdReader.GetString(mdReader.GetTypeDefinition((TypeDefinitionHandle)baseHandle).Name),
                HandleKind.TypeReference =>
                    BuildFullNameFromRef((TypeReferenceHandle)baseHandle, mdReader),
                _ => null
            };
        }
        catch { return null; }
    }

    private static string BuildFullNameFromRef(TypeReferenceHandle handle, MetadataReader mdReader)
    {
        var typeRef = mdReader.GetTypeReference(handle);
        var ns      = mdReader.GetString(typeRef.Namespace);
        var name    = mdReader.GetString(typeRef.Name);
        return string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
    }

    private static IReadOnlyList<string> ReadInterfaceNames(TypeDefinition typeDef, MetadataReader mdReader)
    {
        var ifaces = new List<string>();
        try
        {
            foreach (var handle in typeDef.GetInterfaceImplementations())
            {
                var impl  = mdReader.GetInterfaceImplementation(handle);
                var iface = impl.Interface;
                if (iface.IsNil) continue;

                var name = iface.Kind switch
                {
                    HandleKind.TypeDefinition =>
                        mdReader.GetString(mdReader.GetTypeDefinition((TypeDefinitionHandle)iface).Name),
                    HandleKind.TypeReference =>
                        mdReader.GetString(mdReader.GetTypeReference((TypeReferenceHandle)iface).Name),
                    _ => null
                };

                if (!string.IsNullOrEmpty(name)) ifaces.Add(name!);
            }
        }
        catch { /* Non-fatal */ }
        return ifaces;
    }

    private static IReadOnlyList<string> ReadCustomAttributeNames(
        CustomAttributeHandleCollection handles, MetadataReader mdReader)
    {
        var names = new List<string>();
        try
        {
            foreach (var handle in handles)
            {
                var attr = mdReader.GetCustomAttribute(handle);
                var ctor = attr.Constructor;
                if (ctor.Kind != HandleKind.MemberReference) continue;

                var mref = mdReader.GetMemberReference((MemberReferenceHandle)ctor);
                if (mref.Parent.Kind != HandleKind.TypeReference) continue;

                var typeRef = mdReader.GetTypeReference((TypeReferenceHandle)mref.Parent);
                var attrName = mdReader.GetString(typeRef.Name);

                // Strip "Attribute" suffix for display.
                if (attrName.EndsWith("Attribute", StringComparison.Ordinal) && attrName.Length > "Attribute".Length)
                    attrName = attrName[..^"Attribute".Length];

                names.Add(attrName);
            }
        }
        catch { /* Non-fatal */ }
        return names;
    }

    // ── Method body byte length ───────────────────────────────────────────────

    /// <summary>
    /// Returns the raw byte length of the method body (header + code + exception clauses),
    /// or 0 for abstract/extern/interface methods with no RVA.
    /// Uses PEReader.GetMethodBody which already handles fat vs tiny header parsing.
    /// </summary>
    private static int ResolveMethodByteLength(MethodDefinition mDef, PEReader peReader)
    {
        var rva = mDef.RelativeVirtualAddress;
        if (rva == 0) return 0;
        try
        {
            var body = peReader.GetMethodBody(rva);
            // Tiny header: 1 byte header + code. Fat header: 12 bytes header + code + padding + clauses.
            // body.Size gives us code size; we add the header size heuristically.
            // A more precise approach: read the header byte at the RVA to detect fat vs tiny.
            var firstByte = peReader.PEHeaders.SectionHeaders
                .Select(s => new { s.PointerToRawData, s.VirtualAddress, s.SizeOfRawData })
                .FirstOrDefault(s => rva >= s.VirtualAddress && rva < s.VirtualAddress + s.SizeOfRawData);
            if (firstByte is null) return body.Size;

            var fileOffset = rva - firstByte.VirtualAddress + firstByte.PointerToRawData;
            var header     = peReader.GetEntireImage();
            if (fileOffset < 0 || fileOffset >= header.Length) return body.Size;

            var b = header.GetContent(fileOffset, 1)[0];
            // Tiny format: bits [1:0] = 0b10, max 63 bytes code, no locals/exception clauses
            var isTiny     = (b & 0x03) == 0x02;
            var headerSize = isTiny ? 1 : 12;
            return headerSize + body.Size;
        }
        catch { return 0; }
    }

    // ── Signature decode wrappers (non-throwing) ──────────────────────────────

    private string TryDecodeMethodSignature(MethodDefinition mDef, MetadataReader mdReader)
    {
        try { return _sigDecoder.DecodeMethodSignature(mDef, mdReader); }
        catch { return mdReader.GetString(mDef.Name) + "(?)"; }
    }

    private string TryDecodeFieldSignature(FieldDefinition fDef, MetadataReader mdReader)
    {
        try { return _sigDecoder.DecodeFieldSignature(fDef, mdReader); }
        catch { return "?"; }
    }

    private string TryDecodePropertySignature(PropertyDefinition pDef, MetadataReader mdReader)
    {
        try { return _sigDecoder.DecodePropertySignature(pDef, mdReader); }
        catch { return "?"; }
    }

    // ── Type kind resolution ──────────────────────────────────────────────────

    private static TypeKind ResolveTypeKind(TypeAttributes attributes)
    {
        if ((attributes & TypeAttributes.ClassSemanticsMask) == TypeAttributes.Interface)
            return TypeKind.Interface;

        if ((attributes & TypeAttributes.Sealed) != 0
            && (attributes & TypeAttributes.Abstract) != 0)
            return TypeKind.Delegate; // abstract+sealed = static class or delegate

        if ((attributes & TypeAttributes.SequentialLayout) != 0
            || (attributes & TypeAttributes.ExplicitLayout) != 0)
            return TypeKind.Struct;

        return TypeKind.Class;
    }

    // ── Generic parameter names ───────────────────────────────────────────────

    /// <summary>
    /// Reads the simple names of all generic parameters declared on a type or method
    /// (e.g. ["T", "TKey", "TValue"]).  Returns an empty list for non-generic rows.
    /// </summary>
    private static IReadOnlyList<string> ReadGenericParamNames(
        GenericParameterHandleCollection handles, MetadataReader mdReader)
    {
        if (handles.Count == 0) return [];
        var names = new List<string>(handles.Count);
        try
        {
            foreach (var h in handles)
                names.Add(mdReader.GetString(mdReader.GetGenericParameter(h).Name));
        }
        catch { /* Non-fatal — return partial list */ }
        return names;
    }

    // ── Constant value formatter ──────────────────────────────────────────────

    /// <summary>
    /// Returns the C# literal representation of a <c>const</c> field's default value,
    /// e.g. <c>"42"</c>, <c>"\"hello\""</c>, <c>"true"</c>, <c>"null"</c>.
    /// Returns <c>null</c> for non-const fields or on any metadata read error.
    /// </summary>
    private static string? TryGetConstantValue(FieldDefinition fDef, MetadataReader mdReader)
    {
        if ((fDef.Attributes & FieldAttributes.Literal) == 0) return null;
        try
        {
            var constantHandle = fDef.GetDefaultValue();
            if (constantHandle.IsNil) return null;

            var constant = mdReader.GetConstant(constantHandle);
            var blob     = mdReader.GetBlobReader(constant.Value);

            return constant.TypeCode switch
            {
                ConstantTypeCode.Boolean     => blob.ReadBoolean() ? "true" : "false",
                ConstantTypeCode.Byte        => blob.ReadByte().ToString(),
                ConstantTypeCode.SByte       => blob.ReadSByte().ToString(),
                ConstantTypeCode.Int16       => blob.ReadInt16().ToString(),
                ConstantTypeCode.UInt16      => blob.ReadUInt16().ToString(),
                ConstantTypeCode.Int32       => blob.ReadInt32().ToString(),
                ConstantTypeCode.UInt32      => blob.ReadUInt32().ToString() + "U",
                ConstantTypeCode.Int64       => blob.ReadInt64().ToString() + "L",
                ConstantTypeCode.UInt64      => blob.ReadUInt64().ToString() + "UL",
                ConstantTypeCode.Single      => blob.ReadSingle().ToString("R") + "f",
                ConstantTypeCode.Double      => blob.ReadDouble().ToString("R") + "d",
                ConstantTypeCode.Char        => $"'\\u{blob.ReadUInt16():X4}'",
                ConstantTypeCode.String      => blob.RemainingBytes == 0
                                                    ? "\"\""
                                                    : $"\"{blob.ReadUTF16(blob.RemainingBytes)}\"",
                ConstantTypeCode.NullReference => "null",
                _                            => null
            };
        }
        catch { return null; }
    }

    // ── Public key token ─────────────────────────────────────────────────────

    private static string? BuildPublicKeyToken(byte[] bytes)
    {
        if (bytes.Length == 0) return null;
        var len = Math.Min(bytes.Length, 8);
        return Convert.ToHexString(bytes[..len]).ToLowerInvariant();
    }
}
