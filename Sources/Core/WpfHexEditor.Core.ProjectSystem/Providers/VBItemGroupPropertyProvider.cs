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

using System.Xml.Linq;
using WpfHexEditor.Core.ProjectSystem.Services;
using WpfHexEditor.Core.ProjectSystem.Utilities;
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

    private static class Props
    {
        public const string BuildAction      = "Build Action";
        public const string SubType          = "Sub Type";
        public const string CopyToOutput     = "Copy to Output";
        public const string XmlDocumentation = "XML Documentation";
    }

    private static readonly string[] BuildActionValues =
        ["Compile", "Content", "EmbeddedResource", "None", "ApplicationDefinition", "Page", "Resource"];

    private static readonly string[] CopyToOutputValues =
        ["Do not copy", "Copy always", "Copy if newer"];

    public VBItemGroupPropertyProvider(string absoluteFilePath, IProjectFileLocator? locator = null)
    {
        _absoluteFilePath = absoluteFilePath;
        _vbprojPath       = (locator ?? new ProjectFileLocator()).FindNearestVbproj(absoluteFilePath);
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
                Name          = Props.BuildAction,
                Value         = _buildAction,
                Type          = PropertyEntryType.Enum,
                AllowedValues = BuildActionValues,
                IsReadOnly    = _vbprojPath is null,
                Description   = "Determines how the file is treated during build.",
            },
            new PropertyEntry
            {
                Name          = Props.SubType,
                Value         = _subType,
                Type          = PropertyEntryType.Text,
                IsReadOnly    = _vbprojPath is null,
                Description   = "Optional sub-type (e.g. Form, UserControl, Component).",
            },
            new PropertyEntry
            {
                Name          = Props.CopyToOutput,
                Value         = _copyToOutput,
                Type          = PropertyEntryType.Enum,
                AllowedValues = CopyToOutputValues,
                IsReadOnly    = _vbprojPath is null,
                Description   = "Copy behavior on build output.",
            },
            new PropertyEntry
            {
                Name        = Props.XmlDocumentation,
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
            var relative = ProjectFileXmlUtils.GetRelativePath(vbprojPath, _absoluteFilePath);
            var item     = ProjectFileXmlUtils.FindItemByInclude(doc, ns, relative);
            if (item is null) return;

            _buildAction           = item.Name.LocalName;
            _subType               = item.Element(ns + "SubType")?.Value ?? "";
            _copyToOutput          = MapCopyToOutput(item.Element(ns + "CopyToOutputDirectory")?.Value);
            _generateDocumentation = string.Equals(
                item.Element(ns + "GenerateDocumentationFile")?.Value, "true",
                StringComparison.OrdinalIgnoreCase);
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
            var relative = ProjectFileXmlUtils.GetRelativePath(_vbprojPath, _absoluteFilePath);
            var target   = ProjectFileXmlUtils.FindItemByInclude(doc, ns, relative);
            if (target is null) return;

            switch (propertyName)
            {
                case Props.BuildAction:
                    target.Name = ns + (newValue?.ToString() ?? "Compile");
                    break;
                case Props.SubType:
                    ProjectFileXmlUtils.SetOrRemove(target, ns + "SubType", newValue?.ToString());
                    break;
                case Props.CopyToOutput:
                    ProjectFileXmlUtils.SetOrRemove(target, ns + "CopyToOutputDirectory",
                        MapCopyToOutputXml(newValue?.ToString()));
                    break;
                case Props.XmlDocumentation:
                    ProjectFileXmlUtils.SetOrRemove(target, ns + "GenerateDocumentationFile",
                        newValue is true ? "true" : null);
                    break;
            }

            doc.Save(_vbprojPath);
        }
        catch { /* write errors are non-fatal */ }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string MapCopyToOutput(string? xmlValue) => xmlValue switch
    {
        "Always"         => "Copy always",
        "PreserveNewest" => "Copy if newer",
        _                => "Do not copy",
    };

    private static string? MapCopyToOutputXml(string? displayValue) => displayValue switch
    {
        "Copy always"   => "Always",
        "Copy if newer" => "PreserveNewest",
        _               => null,
    };
}
