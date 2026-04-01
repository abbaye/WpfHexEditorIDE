// ==========================================================
// Project: WpfHexEditor.Plugins.SolutionLoader.VS
// File: VsModels/VsSolution.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     ISolution implementation representing a parsed Visual Studio .sln file.
// ==========================================================

using WpfHexEditor.Editor.Core;

namespace WpfHexEditor.Plugins.SolutionLoader.VS.VsModels;

internal sealed class VsSolution : ISolution, IMutableStartupProject
{
    public string                        Name                 { get; init; } = string.Empty;
    public string                        FilePath             { get; init; } = string.Empty;
    public IReadOnlyList<IProject>       Projects             { get; init; } = [];
    public IReadOnlyList<ISolutionFolder> RootFolders         { get; init; } = [];
    public IProject?                     StartupProject       { get; private set; }
    public bool                          IsModified           => false;
    public int                           SourceFormatVersion  => 0;
    public bool                          FormatUpgradeRequired => false;
    public bool                          IsReadOnlyFormat     => true;

    // Not in ISolution interface — used by the build system.
    public string? DefaultConfigurationName { get; init; }
    public string? DefaultPlatform          { get; init; }

    // IMutableStartupProject — allows SolutionManager to update the in-memory
    // startup project without depending on this concrete type.
    void IMutableStartupProject.ChangeStartupProject(IProject? project)
        => StartupProject = project;

    // Package-internal convenience used by VsSolutionLoader during initial load.
    internal void InitStartupProject(IProject? project) => StartupProject = project;
}
