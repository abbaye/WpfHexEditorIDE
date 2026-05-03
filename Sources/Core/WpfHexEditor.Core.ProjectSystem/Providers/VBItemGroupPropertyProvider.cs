// ==========================================================
// Project: WpfHexEditor.Core.ProjectSystem
// File: Providers/VBItemGroupPropertyProvider.cs
// Description:
//     IPropertyProvider extension that adds VB.NET-specific item group
//     properties (BuildAction, SubType, CopyToOutputDirectory,
//     GenerateDocumentationFile) to the F4 Properties panel for .vb files.
//     Properties are read-only display by default; write-back via
//     VbProjItemGroupWriter is wired through ValueChanged.
// Architecture: Decorator — called by ProjectItemPropertyProvider when
//               the item extension is ".vb".
// ==========================================================

using System.IO;
using System.Xml.Linq;
using WpfHexEditor.Editor.Core;

namespace WpfHexEditor.Core.ProjectSystem.Providers;

/// <summary>
/// Reads VB.NET item-group metadata from the nearest <c>.vbproj</c> and
/// returns editable <see cref="PropertyGroup"/> entries for the selected file.
/// </summary>
public sealed class VBItemGroupPropertyProvider
{
    private readonly string _absoluteFilePath;
    private readonly string? _vbprojPath;

    // Cached values read from the .vbproj
    private string _buildAction            = "Compile";
    private string _subType                = "";
    private string _copyToOutput           = "Do not copy";
    private bool   _generateDocumentation  = false;

    private static readonly string[] BuildActionValues =
        ["Compile", "Content", "EmbeddedResource", "None", "ApplicationDefinition", "Page", "Resource"];

    private static readonly string[] CopyToOutputValues =
        ["Do not copy", "Copy always", "Copy if newer"];

    public VBItemGroupPropertyProvider(string absoluteFilePath)
    {
        _absoluteFilePath = absoluteFilePath;
        _vbprojPath       = FindVbproj(absoluteFilePath);
        if (_vbprojPath is not null)
            LoadFromProject(_vbprojPath);
    }

    /// <summary>Returns the VB.NET item group as a <see cref="PropertyGroup"/>.</summary>
    public PropertyGroup BuildGroup() => new()
    {
        Name = "VB.NET Item",
        Entries =
        [
            new PropertyEntry
            {
                Name          = "Build Action",
                Value         = _buildAction,
                Type          = PropertyEntryType.Enum,
                AllowedValues = BuildActionValues,
                IsReadOnly    = _vbprojPath is null,
                Description   = "Determines how the file is treated during build.",
            },
            new PropertyEntry
            {
                Name          = "Sub Type",
                Value         = _subType,
                Type          = PropertyEntryType.Text,
                IsReadOnly    = _vbprojPath is null,
                Description   = "Optional sub-type (e.g. Form, UserControl, Component).",
            },
            new PropertyEntry
            {
                Name          = "Copy to Output",
                Value         = _copyToOutput,
                Type          = PropertyEntryType.Enum,
                AllowedValues = CopyToOutputValues,
                IsReadOnly    = _vbprojPath is null,
                Description   = "Copy behavior on build output.",
            },
            new PropertyEntry
            {
                Name        = "XML Documentation",
                Value       = _generateDocumentation,
                Type        = PropertyEntryType.Boolean,
                IsReadOnly  = _vbprojPath is null,
                Description = "Generate XML documentation comments for this file.",
            },
        ],
    };

    // ── Project file I/O ──────────────────────────────────────────────────────

    private void LoadFromProject(string vbprojPath)
    {
        try
        {
            var doc      = XDocument.Load(vbprojPath);
            var ns       = doc.Root?.Name.Namespace ?? XNamespace.None;
            var relative = GetRelativePath(vbprojPath, _absoluteFilePath);

            // Find the <Compile> / <Content> / <None> etc. element for this file
            foreach (var itemGroup in doc.Descendants(ns + "ItemGroup"))
            {
                foreach (var item in itemGroup.Elements())
                {
                    var include = item.Attribute("Include")?.Value;
                    if (!string.Equals(NormPath(include), NormPath(relative),
                                       StringComparison.OrdinalIgnoreCase))
                        continue;

                    _buildAction           = item.Name.LocalName;
                    _subType               = item.Element(ns + "SubType")?.Value ?? "";
                    _copyToOutput          = MapCopyToOutput(item.Element(ns + "CopyToOutputDirectory")?.Value);
                    _generateDocumentation = string.Equals(
                        item.Element(ns + "GenerateDocumentationFile")?.Value, "true",
                        StringComparison.OrdinalIgnoreCase);
                    return;
                }
            }
        }
        catch { /* read errors are non-fatal */ }
    }

    /// <summary>
    /// Persists a changed item-group property back to the .vbproj.
    /// Called externally when the user edits a value in the Properties panel.
    /// </summary>
    public void SaveProperty(string propertyName, object? newValue)
    {
        if (_vbprojPath is null) return;
        try
        {
            var doc      = XDocument.Load(_vbprojPath);
            var ns       = doc.Root?.Name.Namespace ?? XNamespace.None;
            var relative = GetRelativePath(_vbprojPath, _absoluteFilePath);

            XElement? target = null;
            foreach (var itemGroup in doc.Descendants(ns + "ItemGroup"))
                foreach (var item in itemGroup.Elements())
                    if (string.Equals(NormPath(item.Attribute("Include")?.Value),
                                      NormPath(relative),
                                      StringComparison.OrdinalIgnoreCase))
                    { target = item; break; }

            if (target is null) return;

            switch (propertyName)
            {
                case "Build Action":
                    target.Name = ns + (newValue?.ToString() ?? "Compile");
                    break;
                case "Sub Type":
                    SetOrRemove(target, ns + "SubType", newValue?.ToString());
                    break;
                case "Copy to Output":
                    SetOrRemove(target, ns + "CopyToOutputDirectory",
                        MapCopyToOutputXml(newValue?.ToString()));
                    break;
                case "XML Documentation":
                    SetOrRemove(target, ns + "GenerateDocumentationFile",
                        newValue is true ? "true" : null);
                    break;
            }

            doc.Save(_vbprojPath);
        }
        catch { /* write errors are non-fatal */ }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string? FindVbproj(string filePath)
    {
        var dir = Path.GetDirectoryName(filePath);
        while (dir is not null)
        {
            var vbproj = Directory.GetFiles(dir, "*.vbproj").FirstOrDefault();
            if (vbproj is not null) return vbproj;
            dir = Path.GetDirectoryName(dir);
        }
        return null;
    }

    private static string GetRelativePath(string vbprojPath, string absoluteFilePath)
    {
        var projDir = Path.GetDirectoryName(vbprojPath) ?? "";
        return Path.GetRelativePath(projDir, absoluteFilePath);
    }

    private static string NormPath(string? p) =>
        p?.Replace('/', '\\').TrimStart('\\') ?? "";

    private static string MapCopyToOutput(string? xmlValue) => xmlValue switch
    {
        "Always"    => "Copy always",
        "PreserveNewest" => "Copy if newer",
        _ => "Do not copy",
    };

    private static string? MapCopyToOutputXml(string? displayValue) => displayValue switch
    {
        "Copy always"    => "Always",
        "Copy if newer"  => "PreserveNewest",
        _ => null,
    };

    private static void SetOrRemove(XElement parent, XName name, string? value)
    {
        if (string.IsNullOrEmpty(value))
            parent.Element(name)?.Remove();
        else
        {
            var el = parent.Element(name);
            if (el is null) parent.Add(new XElement(name, value));
            else el.Value = value;
        }
    }
}
