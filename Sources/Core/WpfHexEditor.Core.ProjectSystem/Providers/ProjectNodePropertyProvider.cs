// ==========================================================
// Project: WpfHexEditor.Core.ProjectSystem
// File: Providers/ProjectNodePropertyProvider.cs
// Description:
//     IPropertyProvider for a project file (.csproj / .vbproj) selected in
//     Solution Explorer. Exposes editable project-level MSBuild properties
//     (TargetFramework, AssemblyName, RootNamespace, OutputType, LangVersion,
//     Nullable, AllowUnsafeBlocks, Optimize, TreatWarningsAsErrors,
//     WarningLevel, Version, FileVersion) in the F4 Properties panel.
//     Edits persist immediately via CsprojPropertyWriter.
// Architecture: Decorator over a project path; reads on construction,
//               writes via OnValueChanged on each PropertyEntry.
// ==========================================================

using System.IO;
using WpfHexEditor.Core.ProjectSystem.Services;
using WpfHexEditor.Editor.Core;

namespace WpfHexEditor.Core.ProjectSystem.Providers;

/// <summary>
/// <see cref="IPropertyProvider"/> for a .csproj / .vbproj project node.
/// Shown in the Properties panel (F4) when a project node is selected
/// in the Solution Explorer.
/// </summary>
public sealed class ProjectNodePropertyProvider : IPropertyProvider
{
    private readonly string _projectPath;
    private readonly string _projectName;

    private static class Props
    {
        public const string TargetFramework        = "Target Framework";
        public const string OutputType             = "Output Type";
        public const string LangVersion            = "Language Version";
        public const string Nullable               = "Nullable";
        public const string AllowUnsafeBlocks      = "Allow Unsafe Blocks";
        public const string Optimize               = "Optimize";
        public const string TreatWarningsAsErrors  = "Treat Warnings As Errors";
        public const string WarningLevel           = "Warning Level";
        public const string AssemblyName           = "Assembly Name";
        public const string RootNamespace          = "Root Namespace";
        public const string Version                = "Version";
        public const string FileVersion            = "File Version";
    }

    private static readonly string[] TargetFrameworkValues =
        ["net8.0", "net8.0-windows", "net9.0", "net9.0-windows", "netstandard2.0", "netstandard2.1"];

    private static readonly string[] OutputTypeValues =
        ["Library", "Exe", "WinExe"];

    private static readonly string[] LangVersionValues =
        ["latest", "preview", "13", "12", "11", "10", "9.0"];

    private static readonly string[] NullableValues =
        ["enable", "disable", "warnings", "annotations"];

    public ProjectNodePropertyProvider(string projectPath)
    {
        _projectPath = projectPath;
        _projectName = Path.GetFileNameWithoutExtension(projectPath);
    }

    public string ContextLabel => $"{_projectName} — Project";

    public event EventHandler? PropertiesChanged { add { } remove { } }

    public IReadOnlyList<PropertyGroup> GetProperties() =>
    [
        BuildBuildGroup(),
        BuildAssemblyGroup(),
        BuildAdvancedGroup(),
    ];

    // ── Groups ────────────────────────────────────────────────────────────────

    private PropertyGroup BuildBuildGroup() => new()
    {
        Name = "Build",
        Entries =
        [
            EnumEntry(Props.TargetFramework, "TargetFramework", TargetFrameworkValues, "net8.0",
                "Target framework moniker (TFM) used for build."),
            EnumEntry(Props.OutputType, "OutputType", OutputTypeValues, "Library",
                "Type of output assembly."),
            EnumEntry(Props.LangVersion, "LangVersion", LangVersionValues, "latest",
                "C# / VB language version."),
            EnumEntry(Props.Nullable, "Nullable", NullableValues, "disable",
                "Nullable reference types behavior."),
        ],
    };

    private PropertyGroup BuildAssemblyGroup() => new()
    {
        Name = "Assembly",
        Entries =
        [
            TextEntry(Props.AssemblyName, "AssemblyName", _projectName,
                "Output assembly name (without extension)."),
            TextEntry(Props.RootNamespace, "RootNamespace", _projectName,
                "Default namespace for new types."),
            TextEntry(Props.Version, "Version", "1.0.0",
                "Assembly informational version."),
            TextEntry(Props.FileVersion, "FileVersion", "1.0.0.0",
                "File version (Win32 resource)."),
        ],
    };

    private PropertyGroup BuildAdvancedGroup() => new()
    {
        Name = "Advanced",
        Entries =
        [
            BoolEntry(Props.AllowUnsafeBlocks, "AllowUnsafeBlocks", false,
                "Allow unsafe code in this project."),
            BoolEntry(Props.Optimize, "Optimize", false,
                "Enable compiler optimizations."),
            BoolEntry(Props.TreatWarningsAsErrors, "TreatWarningsAsErrors", false,
                "Promote warnings to errors during build."),
            IntEntry(Props.WarningLevel, "WarningLevel", 4,
                "Compiler warning level (0–9999)."),
        ],
    };

    // ── Entry factories ───────────────────────────────────────────────────────

    private PropertyEntry EnumEntry(string display, string xmlName, string[] allowed, string fallback, string desc)
    {
        var current = CsprojPropertyWriter.GetProjectProperty(_projectPath, xmlName) ?? fallback;
        return new PropertyEntry
        {
            Name           = display,
            Value          = current,
            Type           = PropertyEntryType.Enum,
            AllowedValues  = allowed,
            Description    = desc,
            OnValueChanged = v => CsprojPropertyWriter.SetProjectProperty(_projectPath, xmlName, v?.ToString()),
        };
    }

    private PropertyEntry TextEntry(string display, string xmlName, string fallback, string desc)
    {
        var current = CsprojPropertyWriter.GetProjectProperty(_projectPath, xmlName) ?? fallback;
        return new PropertyEntry
        {
            Name           = display,
            Value          = current,
            Type           = PropertyEntryType.Text,
            Description    = desc,
            OnValueChanged = v => CsprojPropertyWriter.SetProjectProperty(_projectPath, xmlName, v?.ToString()),
        };
    }

    private PropertyEntry BoolEntry(string display, string xmlName, bool fallback, string desc)
    {
        var raw     = CsprojPropertyWriter.GetProjectProperty(_projectPath, xmlName);
        var current = raw is null
            ? fallback
            : string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
        return new PropertyEntry
        {
            Name           = display,
            Value          = current,
            Type           = PropertyEntryType.Boolean,
            Description    = desc,
            OnValueChanged = v => CsprojPropertyWriter.SetProjectProperty(_projectPath, xmlName,
                v is true ? "true" : null),
        };
    }

    private PropertyEntry IntEntry(string display, string xmlName, int fallback, string desc)
    {
        var raw     = CsprojPropertyWriter.GetProjectProperty(_projectPath, xmlName);
        var current = int.TryParse(raw, out var n) ? n : fallback;
        return new PropertyEntry
        {
            Name           = display,
            Value          = current,
            Type           = PropertyEntryType.Integer,
            Description    = desc,
            OnValueChanged = v => CsprojPropertyWriter.SetProjectProperty(_projectPath, xmlName, v?.ToString()),
        };
    }
}
