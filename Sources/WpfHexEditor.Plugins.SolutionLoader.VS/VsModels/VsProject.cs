// ==========================================================
// Project: WpfHexEditor.Plugins.SolutionLoader.VS
// File: VsModels/VsProject.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     IProject implementation representing a parsed VS project (.csproj / .vbproj).
//     Populated by VSProjectParser; consumed by the VsSolution.
// ==========================================================

using WpfHexEditor.Editor.Core;

namespace WpfHexEditor.Plugins.SolutionLoader.VS.VsModels;

/// <summary>
/// Represents a Visual Studio project loaded into the WpfHexEditor project model.
/// </summary>
internal sealed class VsProject : IProject, IProjectWithReferences
{
    public string                      Id              { get; init; } = Guid.NewGuid().ToString();
    public string                      Name            { get; init; } = string.Empty;
    public string                      ProjectFilePath { get; init; } = string.Empty;
    public IReadOnlyList<IProjectItem> Items           { get; init; } = [];
    public IReadOnlyList<IVirtualFolder> RootFolders   { get; init; } = [];
    public bool                        IsModified       => false;
    public string?                     DefaultTblItemId => null;
    public string?                     ProjectType      { get; set; }

    // -- VS-specific metadata (not in IProject interface) --
    public string  TargetFramework { get; init; } = "net8.0";
    public string  Language        { get; init; } = "C#";
    public string  OutputType      { get; init; } = "Library";
    public string  AssemblyName    { get; init; } = string.Empty;
    public string  RootNamespace   { get; init; } = string.Empty;
    public string  ProjectGuid     { get; init; } = string.Empty;
    public IReadOnlyList<string>               ProjectReferences  { get; init; } = [];
    public IReadOnlyList<PackageReferenceInfo>  PackageReferences  { get; init; } = [];
    public IReadOnlyList<AssemblyReferenceInfo> AssemblyReferences { get; init; } = [];
    public IReadOnlyList<AnalyzerReferenceInfo> AnalyzerReferences { get; init; } = [];

    public IProjectItem? FindItem(string id)
        => Items.FirstOrDefault(i => i.Id == id);

    public IProjectItem? FindItemByPath(string absolutePath)
        => Items.FirstOrDefault(i =>
            string.Equals(i.AbsolutePath, absolutePath, StringComparison.OrdinalIgnoreCase));
}
