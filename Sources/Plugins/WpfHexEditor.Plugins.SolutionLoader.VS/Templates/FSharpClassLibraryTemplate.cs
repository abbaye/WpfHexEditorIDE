// ==========================================================
// Project: WpfHexEditor.Plugins.SolutionLoader.VS
// File: FSharpClassLibraryTemplate.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-25
// Description:
//     F# .NET 8 Class Library project template.
//     Generates: {name}.sln, {name}/{name}.fsproj, {name}/Library.fs
// ==========================================================

namespace WpfHexEditor.Plugins.SolutionLoader.VS.Templates;

/// <summary>F# .NET 8 class library template.</summary>
internal sealed class FSharpClassLibraryTemplate : DotNetProjectTemplate
{
    public override string Id          => "fsharp-classlib";
    public override string DisplayName => "F# Class Library (.NET 8)";
    public override string Description => "An F# .NET 8 class library. Generates a ready-to-build .sln + .fsproj.";
    public override string Category    => "Development";

    protected override string SolutionProjectTypeGuid => FSharpProjectTypeGuid;
    protected override string ProjectFileExtension     => ".fsproj";

    protected override Task WriteCsprojAsync(string projectDir, string projectName, CancellationToken ct) =>
        WriteAsync(Path.Combine(projectDir, $"{projectName}.fsproj"), $$"""
            <Project Sdk="Microsoft.NET.Sdk">

              <PropertyGroup>
                <TargetFramework>net8.0</TargetFramework>
                <AssemblyName>{{projectName}}</AssemblyName>
                <RootNamespace>{{projectName}}</RootNamespace>
              </PropertyGroup>

              <ItemGroup>
                <Compile Include="Library.fs" />
              </ItemGroup>

            </Project>
            """, ct);

    protected override Task WriteSourceFilesAsync(string projectDir, string projectName, CancellationToken ct) =>
        WriteAsync(Path.Combine(projectDir, "Library.fs"), $$"""
            module {{projectName}}.Library

            let hello name =
                printfn $"Hello, {name}!"
            """, ct);
}
