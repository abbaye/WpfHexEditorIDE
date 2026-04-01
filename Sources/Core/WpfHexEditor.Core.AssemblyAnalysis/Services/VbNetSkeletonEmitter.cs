// ==========================================================
// Project: WpfHexEditor.Core.AssemblyAnalysis
// File: Services/VbNetSkeletonEmitter.cs
// Author: Derek Tremblay
// Created: 2026-03-16
// License: GNU Affero General Public License v3.0 (AGPL-3.0)
// Description:
//     BCL-only VB.NET structure skeleton emitter. Produces human-readable
//     VB.NET type/member declarations without control-flow reconstruction.
//     Mirrors CSharpSkeletonEmitter with identical public API and VB.NET syntax.
//     No external NuGet dependency.
//
// Architecture Notes:
//     Pattern: Service (stateless).
//     Emits VB.NET keyword declarations with stub bodies for method members.
//     Enum members and Const fields use their literal value where available.
//     XmlDocComment on models is emitted as a single ''' <summary> line.
//     Syntax: Public Class / End Class, Public Sub / End Sub,
//             Public Function As / End Function, Inherits, Implements,
//             ReadOnly, Const, Shared, Property / Get / Set / End Property.
// ==========================================================

using System.Text;
using WpfHexEditor.Core.AssemblyAnalysis.Models;

namespace WpfHexEditor.Core.AssemblyAnalysis.Services;

/// <summary>
/// Emits VB.NET-style structure skeleton text for assembly/type/member models.
/// Does not perform control-flow reconstruction — method bodies are stubs.
/// </summary>
public sealed class VbNetSkeletonEmitter
{
    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Emits an AssemblyInfo.vb-style summary for the entire assembly.
    /// </summary>
    public string EmitAssemblyInfo(AssemblyModel model)
    {
        var sb = new StringBuilder();
        sb.AppendLine("' ================================================================");
        sb.AppendLine($"' Assembly : {model.Name}");
        if (model.Version is not null)
            sb.AppendLine($"' Version  : {model.Version}");
        if (!string.IsNullOrEmpty(model.TargetFramework))
            sb.AppendLine($"' Framework: {model.TargetFramework}");
        if (!string.IsNullOrEmpty(model.Culture))
            sb.AppendLine($"' Culture  : {model.Culture}");
        if (!string.IsNullOrEmpty(model.PublicKeyToken))
            sb.AppendLine($"' PKT      : {model.PublicKeyToken}");
        sb.AppendLine($"' Types    : {model.Types.Count}");
        sb.AppendLine($"' Refs     : {model.References.Count}");
        sb.AppendLine("' ================================================================");
        sb.AppendLine();

        if (model.References.Count > 0)
        {
            sb.AppendLine("' Assembly references:");
            foreach (var r in model.References.OrderBy(r => r.Name))
            {
                var ver = r.Version is not null ? $" v{r.Version}" : string.Empty;
                sb.AppendLine($"'   {r.Name}{ver}");
            }
            sb.AppendLine();
        }

        sb.AppendLine("' Sections:");
        foreach (var s in model.Sections)
            sb.AppendLine($"'   {s.Name,-8} RVA=0x{s.VirtualAddress:X8}  Size={s.VirtualSize}");

        return sb.ToString();
    }

    /// <summary>
    /// Emits a VB.NET type declaration skeleton including all members.
    /// </summary>
    public string EmitType(TypeModel model)
    {
        var sb = new StringBuilder();

        // XML doc comment
        if (!string.IsNullOrEmpty(model.XmlDocComment))
            sb.AppendLine($"''' <summary>{model.XmlDocComment}</summary>");

        // Custom attributes
        foreach (var attr in model.CustomAttributes)
            sb.AppendLine($"<{attr}>");

        // Type declaration header
        var visibility = model.IsPublic ? "Public" : "Friend";
        var modifiers  = BuildTypeModifiers(model);
        var keyword    = TypeKindToKeyword(model.Kind);
        var typeName   = BuildTypeNameWithGenerics(model.Name, model.GenericParameters);

        sb.AppendLine($"{visibility}{modifiers} {keyword} {typeName}");

        // Inherits / Implements
        var bases = BuildBaseList(model);
        if (!string.IsNullOrEmpty(bases))
        {
            // VB.NET separates Inherits (base class) from Implements (interfaces).
            if (!string.IsNullOrEmpty(model.BaseTypeName)
                && model.BaseTypeName != "System.Object"
                && model.BaseTypeName != "System.ValueType"
                && model.BaseTypeName != "System.Enum"
                && model.BaseTypeName != "System.MulticastDelegate")
            {
                sb.AppendLine($"    Inherits {SimplifyTypeName(model.BaseTypeName)}");
            }

            var ifaces = model.InterfaceNames.Select(SimplifyTypeName).ToList();
            if (ifaces.Count > 0)
                sb.AppendLine($"    Implements {string.Join(", ", ifaces)}");
        }

        sb.AppendLine();

        // Fields
        if (model.Fields.Count > 0)
        {
            sb.AppendLine("    ' ── Fields ──");
            foreach (var f in model.Fields)
            {
                if (!string.IsNullOrEmpty(f.XmlDocComment))
                    sb.AppendLine($"    ''' <summary>{f.XmlDocComment}</summary>");
                sb.AppendLine($"    {FormatField(f)}");
            }
            sb.AppendLine();
        }

        // Properties
        if (model.Properties.Count > 0)
        {
            sb.AppendLine("    ' ── Properties ──");
            foreach (var p in model.Properties)
            {
                if (!string.IsNullOrEmpty(p.XmlDocComment))
                    sb.AppendLine($"    ''' <summary>{p.XmlDocComment}</summary>");
                sb.AppendLine($"    {FormatProperty(p)}");
            }
            sb.AppendLine();
        }

        // Events
        if (model.Events.Count > 0)
        {
            sb.AppendLine("    ' ── Events ──");
            foreach (var e in model.Events)
            {
                if (!string.IsNullOrEmpty(e.XmlDocComment))
                    sb.AppendLine($"    ''' <summary>{e.XmlDocComment}</summary>");
                sb.AppendLine($"    {FormatEvent(e)}");
            }
            sb.AppendLine();
        }

        // Methods
        if (model.Methods.Count > 0)
        {
            sb.AppendLine("    ' ── Methods ──");
            foreach (var m in model.Methods)
            {
                if (!string.IsNullOrEmpty(m.XmlDocComment))
                    sb.AppendLine($"    ''' <summary>{m.XmlDocComment}</summary>");
                sb.AppendLine($"    {FormatMethod(m, model.Kind == TypeKind.Interface)}");
                sb.AppendLine();
            }
        }

        sb.Append($"End {keyword}");
        return sb.ToString();
    }

    /// <summary>
    /// Emits a standalone method signature (used in the detail pane Code tab for Method nodes).
    /// </summary>
    public string EmitMethod(MemberModel model)
    {
        var sb = new StringBuilder();
        if (!string.IsNullOrEmpty(model.XmlDocComment))
            sb.AppendLine($"''' <summary>{model.XmlDocComment}</summary>");
        foreach (var attr in model.CustomAttributes)
            sb.AppendLine($"<{attr}>");
        sb.AppendLine(FormatMethod(model, false));
        return sb.ToString();
    }

    // ── Formatting helpers ────────────────────────────────────────────────────

    private static string BuildTypeModifiers(TypeModel model)
    {
        if (model.Kind != TypeKind.Class) return string.Empty;

        if (model.IsAbstract && model.IsSealed)  return " Shared";   // VB: Module-like
        if (model.IsAbstract && !model.IsSealed) return " MustInherit";
        if (model.IsSealed   && !model.IsAbstract) return " NotInheritable";
        return string.Empty;
    }

    private static string TypeKindToKeyword(TypeKind kind) => kind switch
    {
        TypeKind.Struct    => "Structure",
        TypeKind.Interface => "Interface",
        TypeKind.Enum      => "Enum",
        TypeKind.Delegate  => "Delegate",
        _                  => "Class"
    };

    /// <summary>Returns a combined base/interface display string for comment purposes.</summary>
    private static string BuildBaseList(TypeModel model)
    {
        // Used only to decide whether to emit Inherits/Implements blocks.
        // The actual formatting is done inline in EmitType.
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(model.BaseTypeName)
            && model.BaseTypeName != "System.Object"
            && model.BaseTypeName != "System.ValueType"
            && model.BaseTypeName != "System.Enum"
            && model.BaseTypeName != "System.MulticastDelegate")
        {
            parts.Add(model.BaseTypeName);
        }
        foreach (var iface in model.InterfaceNames) parts.Add(iface);
        return string.Join(", ", parts);
    }

    private static string FormatField(MemberModel f)
    {
        var vis        = f.IsPublic ? "Public" : "Private";
        var stat       = f.IsStatic ? " Shared" : string.Empty;
        var type       = f.Signature ?? "Object";
        var attrs      = f.CustomAttributes.Count > 0
                         ? string.Join(" ", f.CustomAttributes.Select(a => $"<{a}>")) + " "
                         : string.Empty;

        if (f.ConstantValue is not null)
        {
            // Const field — VB: Public [Shared] Const Name As Type = value
            return $"{attrs}{vis}{stat} Const {f.Name} As {type} = {f.ConstantValue}";
        }

        var readonlyMod = f.IsReadOnly ? " ReadOnly" : string.Empty;
        return $"{attrs}{vis}{stat}{readonlyMod} {f.Name} As {type}";
    }

    private static string FormatProperty(MemberModel p)
    {
        var vis  = p.IsPublic ? "Public" : "Private";
        var stat = p.IsStatic ? " Shared" : string.Empty;
        var type = p.Signature ?? "Object";
        // VB.NET auto-property: Public [Shared] Property Name As Type
        return $"{vis}{stat} Property {p.Name} As {type}";
    }

    private static string FormatEvent(MemberModel e)
    {
        var vis         = e.IsPublic ? "Public" : "Private";
        var stat        = e.IsStatic ? " Shared" : string.Empty;
        var handlerType = e.Signature ?? "EventHandler";
        return $"{vis}{stat} Event {e.Name} As {handlerType}";
    }

    private static string FormatMethod(MemberModel m, bool isInterface)
    {
        var vis       = m.IsPublic ? "Public" : "Private";
        var stat      = m.IsStatic ? " Shared" : string.Empty;
        var mustOvr   = m.IsAbstract && !isInterface ? " MustOverride" : string.Empty;
        var overrides = m.IsOverride && !isInterface  ? " Overrides" : string.Empty;
        var overridable = m.IsVirtual && !m.IsAbstract && !isInterface && !m.IsOverride
                          ? " Overridable" : string.Empty;

        // Determine Sub vs Function from signature.
        // Signature may be "void MethodName(...)" or "ReturnType MethodName(...)".
        var sig = m.Signature ?? $"Sub {m.Name}()";

        string vbSig;
        bool   isVoid;
        if (sig.StartsWith("void ", StringComparison.OrdinalIgnoreCase))
        {
            // C# void → VB Sub
            vbSig  = "Sub " + sig[5..];
            isVoid = true;
        }
        else if (sig.Contains(' '))
        {
            // "ReturnType MethodName(...)" → "Function MethodName(...) As ReturnType"
            var spaceIdx   = sig.IndexOf(' ');
            var returnType = sig[..spaceIdx];
            var rest       = sig[(spaceIdx + 1)..];
            vbSig  = $"Function {rest} As {returnType}";
            isVoid = false;
        }
        else
        {
            vbSig  = $"Sub {sig}";
            isVoid = true;
        }

        var subOrFunc = isVoid ? "Sub" : "Function";

        if (isInterface || m.IsAbstract)
        {
            // Interface members and MustOverride — no body.
            return $"{vis}{stat}{mustOvr} {vbSig}";
        }

        return $"{vis}{stat}{mustOvr}{overridable}{overrides} {vbSig}{Environment.NewLine}" +
               $"        ' IL available in the IL tab{Environment.NewLine}" +
               $"    End {subOrFunc}";
    }

    private static string BuildTypeNameWithGenerics(string rawName, IReadOnlyList<string> genParams)
    {
        if (genParams.Count == 0) return rawName;

        // Strip the backtick-arity suffix (e.g. "List`1" → "List") then append (Of T, ...)
        var backtick = rawName.IndexOf('`');
        var baseName = backtick >= 0 ? rawName[..backtick] : rawName;
        return $"{baseName}(Of {string.Join(", ", genParams)})";
    }

    private static string SimplifyTypeName(string fullName)
    {
        var lastDot = fullName.LastIndexOf('.');
        return lastDot >= 0 ? fullName[(lastDot + 1)..] : fullName;
    }
}
