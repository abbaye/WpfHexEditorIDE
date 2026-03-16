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
//     - SDK-style: enumerates physical files on disk matching glob patterns,
//       then applies <Remove> and <Update> directives
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

        var packageReferences = root.Descendants("PackageReference")
            .Select(e => e.Attribute("Include")?.Value ?? string.Empty)
            .Where(v => v.Length > 0)
            .ToList();

        return new VsProject
        {
            Name             = assemblyName,
            ProjectFilePath  = filePath,
            Items            = items,
            RootFolders      = folders,
            TargetFramework  = targetFramework,
            Language         = language,
            OutputType       = outputType,
            AssemblyName     = assemblyName,
            RootNamespace    = rootNs,
            ProjectGuid      = projectGuid,
            ProjectReferences = projectReferences,
            PackageReferences = packageReferences,
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

                // Skip project references and package references in this pass.
                var localName = element.Name.LocalName;
                if (localName is "ProjectReference" or "Reference" or "PackageReference") continue;

                var absPath     = System.IO.Path.GetFullPath(System.IO.Path.Combine(dir, include));
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

        var packageReferences = root.Descendants(ns + "PackageReference")
            .Select(e => e.Attribute("Include")?.Value ?? string.Empty)
            .Where(v => v.Length > 0)
            .ToList();

        var rootFolders = BuildFolderTree(folders);
        AttachItemsToFolders(items, folders);

        return new VsProject
        {
            Name              = assemblyName,
            ProjectFilePath   = filePath,
            Items             = items,
            RootFolders       = rootFolders,
            TargetFramework   = targetFramework,
            Language          = language,
            OutputType        = outputType,
            AssemblyName      = assemblyName,
            RootNamespace     = rootNs,
            ProjectGuid       = projectGuid,
            ProjectReferences = projectReferences,
            PackageReferences = packageReferences,
        };
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
