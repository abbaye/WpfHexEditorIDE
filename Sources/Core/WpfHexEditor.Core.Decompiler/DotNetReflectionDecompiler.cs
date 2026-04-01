//////////////////////////////////////////////
// Project: WpfHexEditor.Core.Decompiler
// File: DotNetReflectionDecompiler.cs
// Description:
//     BCL-only .NET assembly decompiler using System.Reflection.MetadataLoadContext.
//     Lists namespaces, types, members, and IL method bodies.
//     No external NuGet required (ILSpy integration deferred to plugin).
// Architecture:
//     Implements IDecompiler. Registers itself in DecompilerRegistry at startup.
//     MetadataLoadContext provides read-only reflection without loading into the AppDomain.
//////////////////////////////////////////////

using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;

namespace WpfHexEditor.Core.Decompiler;

/// <summary>
/// Reflects a .NET assembly and produces a structured text listing of
/// namespaces, types, and member signatures. Uses PEReader + MetadataReader
/// for safe, read-only inspection without loading the assembly.
/// </summary>
public sealed class DotNetReflectionDecompiler : IDecompiler
{
    public string DisplayName  => ".NET Reflection Decompiler";
    public string Architecture => "CIL";

    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".dll", ".exe"
    };

    public bool CanDecompile(string filePath)
    {
        if (!SupportedExtensions.Contains(Path.GetExtension(filePath)))
            return false;

        // Quick PE header check — must be a managed assembly
        try
        {
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var pe = new PEReader(fs);
            return pe.HasMetadata;
        }
        catch { return false; }
    }

    public Task<string> DecompileAsync(string filePath, CancellationToken ct = default)
    {
        return Task.Run(() => Decompile(filePath, ct), ct);
    }

    private static string Decompile(string filePath, CancellationToken ct)
    {
        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var pe = new PEReader(fs);
        var mr = pe.GetMetadataReader();

        var sb = new StringBuilder();
        sb.AppendLine($"// Assembly: {Path.GetFileName(filePath)}");

        // Assembly info
        if (mr.IsAssembly)
        {
            var asm = mr.GetAssemblyDefinition();
            sb.AppendLine($"// Name:     {mr.GetString(asm.Name)}");
            sb.AppendLine($"// Version:  {asm.Version}");
            sb.AppendLine($"// Culture:  {(mr.GetString(asm.Culture) is { Length: > 0 } c ? c : "neutral")}");
        }

        sb.AppendLine($"// Types:    {mr.TypeDefinitions.Count}");
        sb.AppendLine();

        // Group types by namespace
        var byNamespace = new SortedDictionary<string, List<TypeDefinition>>();
        foreach (var th in mr.TypeDefinitions)
        {
            ct.ThrowIfCancellationRequested();
            var td = mr.GetTypeDefinition(th);
            var ns = mr.GetString(td.Namespace);
            if (string.IsNullOrEmpty(ns)) ns = "<global>";

            if (!byNamespace.TryGetValue(ns, out var list))
            {
                list = [];
                byNamespace[ns] = list;
            }
            list.Add(td);
        }

        foreach (var (ns, types) in byNamespace)
        {
            ct.ThrowIfCancellationRequested();
            sb.AppendLine($"namespace {ns}");
            sb.AppendLine("{");

            foreach (var td in types)
            {
                var name = mr.GetString(td.Name);
                if (name.StartsWith("<")) continue; // compiler-generated

                var vis = GetVisibility(td.Attributes);
                var kind = GetTypeKind(td.Attributes);
                sb.AppendLine($"    {vis} {kind} {name}");
                sb.AppendLine("    {");

                // Fields
                foreach (var fh in td.GetFields())
                {
                    var fd = mr.GetFieldDefinition(fh);
                    var fname = mr.GetString(fd.Name);
                    if (fname.StartsWith("<")) continue;
                    sb.AppendLine($"        {GetFieldVisibility(fd.Attributes)} {fname};");
                }

                // Methods
                foreach (var mh in td.GetMethods())
                {
                    var md = mr.GetMethodDefinition(mh);
                    var mname = mr.GetString(md.Name);
                    if (mname.StartsWith("<")) continue;
                    var mvis = GetMethodVisibility(md.Attributes);
                    var mstatic = (md.Attributes & MethodAttributes.Static) != 0 ? "static " : "";
                    var mvirtual = (md.Attributes & MethodAttributes.Virtual) != 0 ? "virtual " : "";
                    sb.AppendLine($"        {mvis} {mstatic}{mvirtual}{mname}(...)");
                }

                sb.AppendLine("    }");
                sb.AppendLine();
            }

            sb.AppendLine("}");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string GetVisibility(TypeAttributes a) => (a & TypeAttributes.VisibilityMask) switch
    {
        TypeAttributes.Public or TypeAttributes.NestedPublic => "public",
        TypeAttributes.NotPublic => "internal",
        TypeAttributes.NestedPrivate => "private",
        TypeAttributes.NestedFamily => "protected",
        TypeAttributes.NestedAssembly => "internal",
        _ => ""
    };

    private static string GetTypeKind(TypeAttributes a)
    {
        if ((a & TypeAttributes.Interface) != 0) return "interface";
        if ((a & TypeAttributes.Sealed) != 0 && (a & TypeAttributes.Abstract) != 0) return "static class";
        if ((a & TypeAttributes.Abstract) != 0) return "abstract class";
        if ((a & TypeAttributes.Sealed) != 0) return "sealed class";
        return "class";
    }

    private static string GetFieldVisibility(FieldAttributes a) => (a & FieldAttributes.FieldAccessMask) switch
    {
        FieldAttributes.Public => "public",
        FieldAttributes.Private => "private",
        FieldAttributes.Family => "protected",
        FieldAttributes.Assembly => "internal",
        _ => ""
    };

    private static string GetMethodVisibility(MethodAttributes a) => (a & MethodAttributes.MemberAccessMask) switch
    {
        MethodAttributes.Public => "public",
        MethodAttributes.Private => "private",
        MethodAttributes.Family => "protected",
        MethodAttributes.Assembly => "internal",
        _ => ""
    };
}
