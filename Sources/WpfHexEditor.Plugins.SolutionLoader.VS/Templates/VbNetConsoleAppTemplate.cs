// ==========================================================
// Project: WpfHexEditor.Plugins.SolutionLoader.VS
// File: VbNetConsoleAppTemplate.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-25
// Description:
//     VB.NET .NET 8 Console Application project template.
//     Generates: {name}.sln, {name}/{name}.vbproj, {name}/Program.vb
// ==========================================================

namespace WpfHexEditor.Plugins.SolutionLoader.VS.Templates;

/// <summary>VB.NET .NET 8 console application template (OutputType=Exe).</summary>
internal sealed class VbNetConsoleAppTemplate : DotNetProjectTemplate
{
    public override string Id          => "vbnet-console";
    public override string DisplayName => "VB.NET Console Application (.NET 8)";
    public override string Description => "A VB.NET .NET 8 command-line application. Generates a ready-to-build .sln + .vbproj.";
    public override string Category    => "Development";

    protected override string SolutionProjectTypeGuid => VbNetProjectTypeGuid;
    protected override string ProjectFileExtension     => ".vbproj";

    protected override Task WriteCsprojAsync(string projectDir, string projectName, CancellationToken ct) =>
        WriteAsync(Path.Combine(projectDir, $"{projectName}.vbproj"), $$"""
            <Project Sdk="Microsoft.NET.Sdk">

              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net8.0</TargetFramework>
                <AssemblyName>{{projectName}}</AssemblyName>
                <RootNamespace>{{projectName}}</RootNamespace>
                <StartupObject>{{projectName}}.Program</StartupObject>
              </PropertyGroup>

            </Project>
            """, ct);

    protected override Task WriteSourceFilesAsync(string projectDir, string projectName, CancellationToken ct) =>
        WriteAsync(Path.Combine(projectDir, "Program.vb"), $$"""
            Namespace {{projectName}}

                Module Program

                    Sub Main(args As String())
                        Console.WriteLine("Hello, VB.NET!")
                    End Sub

                End Module

            End Namespace
            """, ct);
}
