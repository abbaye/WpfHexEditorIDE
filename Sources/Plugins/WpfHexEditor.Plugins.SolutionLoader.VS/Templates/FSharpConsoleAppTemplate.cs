// ==========================================================
// Project: WpfHexEditor.Plugins.SolutionLoader.VS
// File: FSharpConsoleAppTemplate.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-25
// Description:
//     F# .NET 8 Console Application project template.
//     Generates: {name}.sln, {name}/{name}.fsproj, {name}/Program.fs
// ==========================================================

namespace WpfHexEditor.Plugins.SolutionLoader.VS.Templates;

/// <summary>F# .NET 8 console application template (OutputType=Exe).</summary>
internal sealed class FSharpConsoleAppTemplate : DotNetProjectTemplate
{
    public override string Id          => "fsharp-console";
    public override string DisplayName => "F# Console Application (.NET 8)";
    public override string Description => "An F# .NET 8 command-line application. Generates a ready-to-build .sln + .fsproj.";
    public override string Category    => "Development";

    protected override string SolutionProjectTypeGuid => FSharpProjectTypeGuid;
    protected override string ProjectFileExtension     => ".fsproj";

    protected override Task WriteCsprojAsync(string projectDir, string projectName, CancellationToken ct) =>
        WriteAsync(Path.Combine(projectDir, $"{projectName}.fsproj"), $$"""
            <Project Sdk="Microsoft.NET.Sdk">

              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net8.0</TargetFramework>
                <AssemblyName>{{projectName}}</AssemblyName>
                <RootNamespace>{{projectName}}</RootNamespace>
              </PropertyGroup>

              <ItemGroup>
                <Compile Include="Program.fs" />
              </ItemGroup>

            </Project>
            """, ct);

    protected override Task WriteSourceFilesAsync(string projectDir, string projectName, CancellationToken ct) =>
        WriteAsync(Path.Combine(projectDir, "Program.fs"), """
            [<EntryPoint>]
            let main _ =
                printfn "Hello, F#!"
                0
            """, ct);
}
