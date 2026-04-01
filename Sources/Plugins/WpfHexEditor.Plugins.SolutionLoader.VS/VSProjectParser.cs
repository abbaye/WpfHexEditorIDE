// ==========================================================
// Project: WpfHexEditor.Plugins.SolutionLoader.VS
// File: VSProjectParser.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Parses Visual Studio project files (.csproj / .vbproj).
//     Handles both SDK-style (implicit globs) and legacy
//     (explicit <Compile Include>) formats.
//
// Architecture Notes:
//     - Strategy: SDK-style detection via <Project Sdk="..."> attribute
//     - Legacy: enumerates explicit <Compile>, <Content>, <None> includes
//       using an allowlist to skip non-file elements (BootstrapperPackage, etc.)
//     - SDK-style: enumerates physical files on disk matching glob patterns,
//       then applies <Remove> and <Update> directives
//     - Both styles: parse <Reference>, <PackageReference Version>, <Analyzer>
// ==========================================================

using System.Xml.Linq;
using WpfHexEditor.Editor.Core;
using WpfHexEditor.Plugins.SolutionLoader.VS.VsModels;

namespace WpfHexEditor.Plugins.SolutionLoader.VS;

/// <summary>
/// Parses a .csproj or .vbproj file and returns a <see cref="VsProject"/> model.
/// </summary>
internal static class VSProjectParser
{
    private static readonly XNamespace MsBuildNs = "http://schemas.microsoft.com/developer/msbuild/2003";

    /// <summary>
    /// Item element names that represent actual files in a legacy project.
    /// Any element NOT in this list is ignored (e.g. BootstrapperPackage, COM, FrameworkAssembly).
    /// </summary>
    private static readonly HashSet<string> FileItemTypes = new(StringComparer.Ordinal)
    {
        "Compile", "Content", "None", "EmbeddedResource", "Resource",
        "Page", "ApplicationDefinition", "SplashScreen", "EntityDeploy",
        "XamlAppDef", "TypeScriptCompile", "ClCompile", "ClInclude",
        "PRIResource", "Natvis",
    };

    // -----------------------------------------------------------------------
    // Public API
    // -----------------------------------------------------------------------

    /// <summary>
    /// Parses the project file at <paramref name="projectFilePath"/> and returns
    /// a populated <see cref="VsProject"/>.
    /// </summary>
    public static VsProject Parse(string projectFilePath)
    {
        if (!File.Exists(projectFilePath))
            throw new FileNotFoundException("Project file not found.", projectFilePath);

        var doc      = XDocument.Load(projectFilePath);
        var root     = doc.Root!;
        var isSdk    = root.Attribute("Sdk") != null;
        var language = DetectLanguage(projectFilePath);

        if (isSdk)
            return ParseSdkStyle(projectFilePath, root, language);
        else
            return ParseLegacyStyle(projectFilePath, root, language);
    }

    // -----------------------------------------------------------------------
    // SDK-style parser
    // -----------------------------------------------------------------------

    private static VsProject ParseSdkStyle(string filePath, XElement root, string language)
    {
        var dir            = System.IO.Path.GetDirectoryName(filePath)!;
        var propertyGroups = root.Descendants("PropertyGroup").ToList();

        var targetFramework = GetProperty(propertyGroups, "TargetFramework")
                           ?? GetProperty(propertyGroups, "TargetFrameworks")?.Split(';')[0]
                           ?? "net8.0";
        var outputType   = GetProperty(propertyGroups, "OutputType") ?? "Library";
        var assemblyName = GetProperty(propertyGroups, "AssemblyName")
                        ?? System.IO.Path.GetFileNameWithoutExtension(filePath);
        var rootNs       = GetProperty(propertyGroups, "RootNamespace") ?? assemblyName;
        var projectGuid  = GetProperty(propertyGroups, "ProjectGuid") ?? string.Empty;

        var (items, folders) = CollectSdkItems(dir, root);

        var projectReferences = root.Descendants("ProjectReference")
            .Select(e => e.Attribute("Include")?.Value ?? string.Empty)
            .Where(v => v.Length > 0)
            .Select(v => System.IO.Path.GetFullPath(System.IO.Path.Combine(dir, v)))
            .ToList();

        var packageReferences  = ParsePackageReferences(root, XNamespace.None, dir);
        var assemblyReferences = ParseAssemblyReferences(root, XNamespace.None, dir);
        var analyzerReferences = ParseAnalyzerReferences(root, XNamespace.None, dir);

        return new VsProject
        {
            Name               = assemblyName,
            ProjectFilePath    = filePath,
            Items              = items,
            RootFolders        = folders,
            ProjectType        = System.IO.Path.GetExtension(filePath).TrimStart('.').ToLowerInvariant(),
            TargetFramework    = targetFramework,
            Language           = language,
            OutputType         = outputType,
            AssemblyName       = assemblyName,
            RootNamespace      = rootNs,
            ProjectGuid        = projectGuid,
            ProjectReferences  = projectReferences,
            PackageReferences  = packageReferences,
            AssemblyReferences = assemblyReferences,
            AnalyzerReferences = analyzerReferences,
        };
    }

    /// <summary>
    /// Enumerates physical files in the project directory, applies Remove/Exclude
    /// directives, and maps items to virtual folders.
    /// </summary>
    private static (IReadOnlyList<IProjectItem> Items, IReadOnlyList<IVirtualFolder> Folders)
        CollectSdkItems(string projectDir, XElement root)
    {
        // Collect explicit removes/excludes so we can skip those files.
        var removes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var remove in root.Descendants("Compile").Concat(root.Descendants("Content"))
                                   .Concat(root.Descendants("None")))
        {
            var removeAttr = remove.Attribute("Remove")?.Value;
            if (removeAttr != null)
                removes.Add(System.IO.Path.GetFullPath(System.IO.Path.Combine(projectDir, removeAttr)));
        }

        // Walk the directory tree, skipping obj/bin and hidden folders.
        var physicalFiles = EnumerateProjectFiles(projectDir);

        var items   = new List<IProjectItem>();
        var folders = new Dictionary<string, VsVirtualFolder>(StringComparer.OrdinalIgnoreCase);

        foreach (var absPath in physicalFiles)
        {
            if (removes.Contains(absPath)) continue;

            var relativePath = System.IO.Path.GetRelativePath(projectDir, absPath);
            var dir          = System.IO.Path.GetDirectoryName(relativePath);
            string? folderId = null;

            if (!string.IsNullOrEmpty(dir) && dir != ".")
            {
                folderId = EnsureFolder(dir, folders);
            }

            items.Add(new VsProjectItem
            {
                Name           = System.IO.Path.GetFileName(absPath),
                AbsolutePath   = absPath,
                RelativePath   = relativePath,
                ItemType       = MapItemType(absPath),
                VirtualFolderId = folderId,
            });
        }

        // Build folder hierarchy.
        var rootFolders = BuildFolderTree(folders);
        AttachItemsToFolders(items, folders);

        return (items, rootFolders);
    }

    // -----------------------------------------------------------------------
    // Legacy parser (.csproj with explicit includes)
    // -----------------------------------------------------------------------

    private static VsProject ParseLegacyStyle(string filePath, XElement root, string language)
    {
        var dir            = System.IO.Path.GetDirectoryName(filePath)!;
        var ns             = root.Name.Namespace;
        var propertyGroups = root.Descendants(ns + "PropertyGroup").ToList();

        var targetFramework = GetPropertyNs(propertyGroups, ns, "TargetFrameworkVersion") ?? "v4.8";
        var outputType      = GetPropertyNs(propertyGroups, ns, "OutputType") ?? "Library";
        var assemblyName    = GetPropertyNs(propertyGroups, ns, "AssemblyName")
                           ?? System.IO.Path.GetFileNameWithoutExtension(filePath);
        var rootNs          = GetPropertyNs(propertyGroups, ns, "RootNamespace") ?? assemblyName;
        var projectGuid     = GetPropertyNs(propertyGroups, ns, "ProjectGuid") ?? string.Empty;

        var items   = new List<IProjectItem>();
        var folders = new Dictionary<string, VsVirtualFolder>(StringComparer.OrdinalIgnoreCase);

        foreach (var itemGroup in root.Descendants(ns + "ItemGroup"))
        {
            foreach (var element in itemGroup.Elements())
            {
                var include = element.Attribute("Include")?.Value;
                if (string.IsNullOrEmpty(include)) continue;

                // Only process known file-bearing element types.
                // Skips BootstrapperPackage, FrameworkAssembly, COM, Reference, PackageReference, etc.
                var localName = element.Name.LocalName;
                if (!FileItemTypes.Contains(localName)) continue;

                var absPath      = System.IO.Path.GetFullPath(System.IO.Path.Combine(dir, include));
                var relativePath = include.Replace('/', System.IO.Path.DirectorySeparatorChar);
                var dirPart      = System.IO.Path.GetDirectoryName(relativePath);
                string? folderId = null;

                if (!string.IsNullOrEmpty(dirPart) && dirPart != ".")
                    folderId = EnsureFolder(dirPart, folders);

                items.Add(new VsProjectItem
                {
                    Name            = System.IO.Path.GetFileName(include),
                    AbsolutePath    = absPath,
                    RelativePath    = relativePath,
                    ItemType        = MapItemType(include),
                    VirtualFolderId = folderId,
                });
            }
        }

        var projectReferences = root.Descendants(ns + "ProjectReference")
            .Select(e => e.Attribute("Include")?.Value ?? string.Empty)
            .Where(v => v.Length > 0)
            .Select(v => System.IO.Path.GetFullPath(System.IO.Path.Combine(dir, v)))
            .ToList();

        var packageReferences  = ParsePackageReferences(root, ns, dir);
        var assemblyReferences = ParseAssemblyReferences(root, ns, dir);
        var analyzerReferences = ParseAnalyzerReferences(root, ns, dir);

        var rootFolders = BuildFolderTree(folders);
        AttachItemsToFolders(items, folders);

        return new VsProject
        {
            Name               = assemblyName,
            ProjectFilePath    = filePath,
            Items              = items,
            RootFolders        = rootFolders,
            ProjectType        = System.IO.Path.GetExtension(filePath).TrimStart('.').ToLowerInvariant(),
            TargetFramework    = targetFramework,
            Language           = language,
            OutputType         = outputType,
            AssemblyName       = assemblyName,
            RootNamespace      = rootNs,
            ProjectGuid        = projectGuid,
            ProjectReferences  = projectReferences,
            PackageReferences  = packageReferences,
            AssemblyReferences = assemblyReferences,
            AnalyzerReferences = analyzerReferences,
        };
    }

    // -----------------------------------------------------------------------
    // Reference parsers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Parses all <c>&lt;PackageReference&gt;</c> elements, capturing Id and Version.
    /// Version is read from the <c>Version</c> attribute first, then from a child
    /// <c>&lt;Version&gt;</c> element (used in some Directory.Build.props patterns).
    /// </summary>
    private static IReadOnlyList<PackageReferenceInfo> ParsePackageReferences(
        XElement root, XNamespace ns, string _)
    {
        return root.Descendants(ns + "PackageReference")
            .Select(e =>
            {
                var id = e.Attribute("Include")?.Value ?? string.Empty;
                if (id.Length == 0) return null;
                var version = e.Attribute("Version")?.Value
                           ?? e.Element(ns + "Version")?.Value;
                return new PackageReferenceInfo(id, string.IsNullOrEmpty(version) ? null : version);
            })
            .Where(r => r != null)
            .Select(r => r!)
            .ToList();
    }

    /// <summary>
    /// Parses all <c>&lt;Reference Include="..."&gt;</c> elements.
    /// Strips the full assembly identity (version/culture/token) from the Include value,
    /// extracts an optional <c>&lt;HintPath&gt;</c> child (resolved to absolute path),
    /// and sets <see cref="AssemblyReferenceInfo.IsFrameworkRef"/> when no HintPath is present.
    /// </summary>
    private static IReadOnlyList<AssemblyReferenceInfo> ParseAssemblyReferences(
        XElement root, XNamespace ns, string projectDir)
    {
        var result = new List<AssemblyReferenceInfo>();

        foreach (var el in root.Descendants(ns + "Reference"))
        {
            var include = el.Attribute("Include")?.Value;
            if (string.IsNullOrEmpty(include)) continue;

            // Strip version/culture/publicKeyToken qualifiers (everything after the first comma).
            var commaIdx = include.IndexOf(',');
            var name     = commaIdx >= 0
                ? include[..commaIdx].Trim()
                : include.Trim();

            if (name.Length == 0) continue;

            // Extract version from the Include identity string if present.
            string? version = null;
            if (commaIdx >= 0)
            {
                var rest = include[(commaIdx + 1)..];
                var parts = rest.Split(',');
                foreach (var part in parts)
                {
                    var kv = part.Trim().Split('=');
                    if (kv.Length == 2 &&
                        kv[0].Trim().Equals("Version", StringComparison.OrdinalIgnoreCase))
                    {
                        version = kv[1].Trim();
                        break;
                    }
                }
            }

            // HintPath child element — resolve to absolute path.
            var hintPathRaw = el.Element(ns + "HintPath")?.Value
                           ?? el.Element("HintPath")?.Value;
            string? hintPath = null;
            if (!string.IsNullOrEmpty(hintPathRaw))
            {
                hintPath = System.IO.Path.IsPathRooted(hintPathRaw)
                    ? hintPathRaw
                    : System.IO.Path.GetFullPath(
                        System.IO.Path.Combine(projectDir, hintPathRaw));
            }

            result.Add(new AssemblyReferenceInfo(
                Name:          name,
                HintPath:      hintPath,
                Version:       version,
                IsFrameworkRef: hintPath == null));
        }

        return result;
    }

    /// <summary>
    /// Parses all <c>&lt;Analyzer Include="..."&gt;</c> elements, resolving each to
    /// an absolute path.
    /// </summary>
    private static IReadOnlyList<AnalyzerReferenceInfo> ParseAnalyzerReferences(
        XElement root, XNamespace ns, string projectDir)
    {
        return root.Descendants(ns + "Analyzer")
            .Select(e => e.Attribute("Include")?.Value)
            .Where(v => !string.IsNullOrEmpty(v))
            .Select(v =>
            {
                var abs = System.IO.Path.IsPathRooted(v!)
                    ? v!
                    : System.IO.Path.GetFullPath(System.IO.Path.Combine(projectDir, v!));
                return new AnalyzerReferenceInfo(abs);
            })
            .ToList();
    }

    // -----------------------------------------------------------------------
    // Helpers — property extraction
    // -----------------------------------------------------------------------

    private static string? GetProperty(IEnumerable<XElement> groups, string name)
        => groups.Elements(name).FirstOrDefault()?.Value.Trim();

    private static string? GetPropertyNs(IEnumerable<XElement> groups, XNamespace ns, string name)
        => groups.Elements(ns + name).FirstOrDefault()?.Value.Trim();

    // -----------------------------------------------------------------------
    // Helpers — file enumeration
    // -----------------------------------------------------------------------

    /// <summary>
    /// Project file extensions that are never content items — they represent the project itself
    /// and must not appear as children in the Solution Explorer file list.
    /// </summary>
    private static readonly HashSet<string> ProjectFileExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".csproj", ".vbproj", ".fsproj", ".esproj", ".njsproj", ".pyproj", ".sqlproj"
        };

    private static IEnumerable<string> EnumerateProjectFiles(string dir)
    {
        var skip = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "obj", "bin", ".git", ".vs" };

        foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
        {
            var rel = System.IO.Path.GetRelativePath(dir, file);

            // Skip if any segment matches a skip folder.
            var parts = rel.Split(System.IO.Path.DirectorySeparatorChar,
                                  System.IO.Path.AltDirectorySeparatorChar);
            if (parts.Any(p => skip.Contains(p))) continue;

            // Project files represent the project node itself — never show them as file items.
            if (ProjectFileExtensions.Contains(System.IO.Path.GetExtension(file))) continue;

            yield return file;
        }
    }

    // -----------------------------------------------------------------------
    // Helpers — virtual folder management
    // -----------------------------------------------------------------------

    private static string EnsureFolder(
        string relativeDirPath,
        Dictionary<string, VsVirtualFolder> folders)
    {
        if (folders.TryGetValue(relativeDirPath, out var existing))
            return existing.Id;

        var folder = new VsVirtualFolder
        {
            Name                 = System.IO.Path.GetFileName(relativeDirPath),
            PhysicalRelativePath = relativeDirPath,
        };
        folders[relativeDirPath] = folder;

        // Ensure parent exists.
        var parent = System.IO.Path.GetDirectoryName(relativeDirPath);
        if (!string.IsNullOrEmpty(parent) && parent != ".")
        {
            var parentId = EnsureFolder(parent, folders);
            folders[parent].AddChild(folder);
        }

        return folder.Id;
    }

    private static List<IVirtualFolder> BuildFolderTree(
        Dictionary<string, VsVirtualFolder> folders)
    {
        // Root folders are those whose parent is not tracked.
        var roots = new List<IVirtualFolder>();
        foreach (var (path, folder) in folders)
        {
            var parent = System.IO.Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(parent) || parent == "." || !folders.ContainsKey(parent))
                roots.Add(folder);
        }
        return roots;
    }

    private static void AttachItemsToFolders(
        IEnumerable<IProjectItem> items,
        Dictionary<string, VsVirtualFolder> folders)
    {
        foreach (var item in items.OfType<VsProjectItem>())
        {
            if (item.VirtualFolderId == null) continue;
            var folder = folders.Values.FirstOrDefault(f => f.Id == item.VirtualFolderId);
            folder?.AddItemId(item.Id);
        }
    }

    // -----------------------------------------------------------------------
    // Helpers — item type + language detection
    // -----------------------------------------------------------------------

    private static ProjectItemType MapItemType(string path)
    {
        var ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".json"                  => ProjectItemType.Json,
            ".tbl"                   => ProjectItemType.Tbl,
            ".txt" or ".log"         => ProjectItemType.Text,
            ".png" or ".bmp" or ".jpg" or ".gif" or ".ico" => ProjectItemType.Image,
            ".wav" or ".mp3"         => ProjectItemType.Audio,
            ".bin" or ".rom" or ".img" => ProjectItemType.Binary,
            ".patch"                 => ProjectItemType.Patch,
            _                        => ProjectItemType.Text,
        };
    }

    private static string DetectLanguage(string filePath)
    {
        var ext = System.IO.Path.GetExtension(filePath).ToLowerInvariant();
        return ext switch
        {
            ".vbproj" => "VB.NET",
            ".fsproj" => "F#",
            _         => "C#",
        };
    }
}
