// ==========================================================
// Project: WpfHexEditor.Plugins.AssemblyExplorer
// File: Services/IlSpyDecompilerBackend.cs
// Author: Derek Tremblay
// Created: 2026-03-16
// Description:
//     Optional decompiler backend that wraps ICSharpCode.Decompiler (ILSpy).
//     Produces real C# code with method bodies. Falls back to the skeleton
//     backend when the NuGet package is absent or decompilation fails.
//
//     Enable by setting DecompilerBackend = "ILSpy" in AssemblyExplorerOptions.
//     Add the NuGet package via: <PackageReference Include="ICSharpCode.Decompiler" Version="9.*"/>
//     (or conditionally via <Condition="'$(EnableILSpyBackend)'=='true'"/>).
//
// Architecture Notes:
//     Pattern: Strategy + Proxy (lazy reflection-based loading).
//     All ICSharpCode.Decompiler types are accessed via reflection so that the
//     plugin binary does NOT hard-link to the NuGet DLL.
//     If the DLL is missing the plugin still loads and IsAvailable returns false.
// ==========================================================

using System.Reflection;
using WpfHexEditor.Core.AssemblyAnalysis.Models;

namespace WpfHexEditor.Plugins.AssemblyExplorer.Services;

/// <summary>
/// Optional backend using ICSharpCode.Decompiler (ILSpy) for full C# decompilation.
/// Loaded via reflection so the plugin binary compiles without the NuGet dependency.
/// </summary>
public sealed class IlSpyDecompilerBackend : IDecompilerBackend
{
    private readonly SkeletonDecompilerBackend _fallback;
    private static   bool?                    _available;

    // Lazy-resolved ILSpy entry points (null when the NuGet is absent).
    private static Type?   _decompilerType;
    private static Type?   _settingsType;
    private static MethodInfo? _decompileTypeMethod;
    private static MethodInfo? _decompileMethod;

    public IlSpyDecompilerBackend(SkeletonDecompilerBackend fallback)
        => _fallback = fallback;

    public string Name => "ILSpy (ICSharpCode.Decompiler)";

    public bool IsAvailable
    {
        get
        {
            if (_available.HasValue) return _available.Value;
            _available = TryLoadIlSpy();
            return _available.Value;
        }
    }

    // ── IDecompilerBackend ────────────────────────────────────────────────────

    public string DecompileAssembly(AssemblyModel model, string filePath)
        => _fallback.DecompileAssembly(model, filePath); // Assembly-level info stays as skeleton.

    public string DecompileType(TypeModel type, string filePath)
    {
        if (!IsAvailable || string.IsNullOrEmpty(filePath)) return _fallback.DecompileType(type, filePath);
        try { return IlSpyDecompileType(type, filePath); }
        catch { return _fallback.DecompileType(type, filePath) + "\n\n// ILSpy decompilation failed — showing skeleton."; }
    }

    public string DecompileMethod(MemberModel member, string filePath)
    {
        if (!IsAvailable || string.IsNullOrEmpty(filePath)) return _fallback.DecompileMethod(member, filePath);
        try { return IlSpyDecompileMethod(member, filePath); }
        catch { return _fallback.DecompileMethod(member, filePath) + "\n\n// ILSpy decompilation failed — showing skeleton."; }
    }

    public string GetIlText(MemberModel member, string filePath)
        => _fallback.GetIlText(member, filePath); // IL disassembly keeps using BCL IlTextEmitter.

    // ── Reflection-based ILSpy calls ─────────────────────────────────────────

    private static string IlSpyDecompileType(TypeModel type, string filePath)
    {
        // ICSharpCode.Decompiler.CSharp.CSharpDecompiler decompiler = new(filePath, settings);
        // return decompiler.DecompileTypeAsString(new ICSharpCode.Decompiler.TypeSystem.FullTypeName(type.FullName));
        // Implemented via reflection to avoid hard NuGet dependency.
        var settings    = Activator.CreateInstance(_settingsType!);
        var decompiler  = Activator.CreateInstance(_decompilerType!, filePath, settings);
        var fullTypeName = CreateFullTypeName(type.FullName);
        return (string)_decompileTypeMethod!.Invoke(decompiler, [fullTypeName])!;
    }

    private static string IlSpyDecompileMethod(MemberModel member, string filePath)
    {
        var settings    = Activator.CreateInstance(_settingsType!);
        var decompiler  = Activator.CreateInstance(_decompilerType!, filePath, settings);
        var fullTypeName = CreateFullTypeName(member.Name);
        return (string)_decompileMethod!.Invoke(decompiler, [fullTypeName])!;
    }

    private static object CreateFullTypeName(string fullName)
    {
        // new ICSharpCode.Decompiler.TypeSystem.FullTypeName(fullName)
        var fullTypeNameType = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => { try { return a.GetTypes(); } catch { return []; } })
            .FirstOrDefault(t => t.FullName == "ICSharpCode.Decompiler.TypeSystem.FullTypeName");
        return fullTypeNameType is null
            ? fullName
            : Activator.CreateInstance(fullTypeNameType, fullName)!;
    }

    private static bool TryLoadIlSpy()
    {
        try
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            var ilspyAsm   = assemblies.FirstOrDefault(
                a => a.GetName().Name == "ICSharpCode.Decompiler");

            if (ilspyAsm is null)
            {
                // Attempt to load from the plugin directory.
                var pluginDir = AppDomain.CurrentDomain.BaseDirectory;
                var candidate = System.IO.Path.Combine(pluginDir, "ICSharpCode.Decompiler.dll");
                if (System.IO.File.Exists(candidate))
                    ilspyAsm = Assembly.LoadFrom(candidate);
            }

            if (ilspyAsm is null) return false;

            _decompilerType = ilspyAsm.GetType("ICSharpCode.Decompiler.CSharp.CSharpDecompiler");
            _settingsType   = ilspyAsm.GetType("ICSharpCode.Decompiler.DecompilerSettings");

            if (_decompilerType is null || _settingsType is null) return false;

            _decompileTypeMethod = _decompilerType.GetMethod("DecompileTypeAsString");
            _decompileMethod     = _decompilerType.GetMethod("DecompileAsString");

            return _decompileTypeMethod is not null;
        }
        catch { return false; }
    }
}
