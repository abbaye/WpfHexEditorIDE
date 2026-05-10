// ==========================================================
// Project: WpfHexEditor.Core.ProjectSystem
// File: Providers/CsprojItemGroupPropertyProvider.cs
// Description:
//     IPropertyProvider extension that adds C#-specific item group
//     properties (BuildAction, SubType, CopyToOutputDirectory,
//     GenerateDocumentationFile) to the F4 Properties panel for .cs files.
//     Properties are read-only display by default; write-back via
//     SaveProperty is wired through ValueChanged.
// Architecture: Decorator — called by ProjectItemPropertyProvider when
//               the item extension is ".cs".
// ==========================================================

using System.Xml.Linq;
using WpfHexEditor.Core.ProjectSystem.Services;
using WpfHexEditor.Core.ProjectSystem.Utilities;
using WpfHexEditor.Editor.Core;

namespace WpfHexEditor.Core.ProjectSystem.Providers;

/// <summary>
/// Reads C# item-group metadata from the nearest <c>.csproj</c> and
/// returns editable <see cref="PropertyGroup"/> entries for the selected file.
/// </summary>
public sealed class CsprojItemGroupPropertyProvider
{
    private readonly string _absoluteFilePath;
    private readonly string? _csprojPath;

    private string _buildAction           = "Compile";
    private string _subType               = "";
    private string _copyToOutput          = "Do not copy";
    private bool   _generateDocumentation = false;

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

    public CsprojItemGroupPropertyProvider(string absoluteFilePath, IProjectFileLocator? locator = null)
    {
        _absoluteFilePath = absoluteFilePath;
        _csprojPath       = (locator ?? new ProjectFileLocator()).FindNearestCsproj(absoluteFilePath);
        if (_csprojPath is not null)
            LoadFromProject(_csprojPath);
    }

    /// <summary>Returns the C# item group as a <see cref="PropertyGroup"/>.</summary>
    public PropertyGroup BuildGroup() => new()
    {
        Name = "C# Item",
        Entries =
        [
            new PropertyEntry
            {
                Name          = Props.BuildAction,
                Value         = _buildAction,
                Type          = PropertyEntryType.Enum,
                AllowedValues = BuildActionValues,
                IsReadOnly    = _csprojPath is null,
                Description   = "Determines how the file is treated during build.",
                OnValueChanged = v => SaveProperty(Props.BuildAction, v),
            },
            new PropertyEntry
            {
                Name           = Props.SubType,
                Value          = _subType,
                Type           = PropertyEntryType.Text,
                IsReadOnly     = _csprojPath is null,
                Description    = "Optional sub-type (e.g. Form, UserControl, Component).",
                OnValueChanged = v => SaveProperty(Props.SubType, v),
            },
            new PropertyEntry
            {
                Name           = Props.CopyToOutput,
                Value          = _copyToOutput,
                Type           = PropertyEntryType.Enum,
                AllowedValues  = CopyToOutputValues,
                IsReadOnly     = _csprojPath is null,
                Description    = "Copy behavior on build output.",
                OnValueChanged = v => SaveProperty(Props.CopyToOutput, v),
            },
            new PropertyEntry
            {
                Name           = Props.XmlDocumentation,
                Value          = _generateDocumentation,
                Type           = PropertyEntryType.Boolean,
                IsReadOnly     = _csprojPath is null,
                Description    = "Generate XML documentation comments for this file.",
                OnValueChanged = v => SaveProperty(Props.XmlDocumentation, v),
            },
        ],
    };

    // ── Project file I/O ──────────────────────────────────────────────────────

    private void LoadFromProject(string csprojPath)
    {
        try
        {
            var doc      = XDocument.Load(csprojPath);
            var ns       = doc.Root?.Name.Namespace ?? XNamespace.None;
            var relative = ProjectFileXmlUtils.GetRelativePath(csprojPath, _absoluteFilePath);
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
    /// Persists a changed item-group property back to the .csproj.
    /// Called when the user edits a value in the Properties panel.
    /// </summary>
    public void SaveProperty(string propertyName, object? newValue)
    {
        if (_csprojPath is null) return;
        try
        {
            var doc      = XDocument.Load(_csprojPath);
            var ns       = doc.Root?.Name.Namespace ?? XNamespace.None;
            var relative = ProjectFileXmlUtils.GetRelativePath(_csprojPath, _absoluteFilePath);
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

            doc.Save(_csprojPath);
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
