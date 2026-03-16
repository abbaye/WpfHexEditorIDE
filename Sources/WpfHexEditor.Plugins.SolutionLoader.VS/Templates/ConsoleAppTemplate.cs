// ==========================================================
// Project: WpfHexEditor.Plugins.SolutionLoader.VS
// File: ConsoleAppTemplate.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     .NET 8 Console Application project template.
//     Generates: {name}.sln, {name}/{name}.csproj, {name}/Program.cs
// ==========================================================

namespace WpfHexEditor.Plugins.SolutionLoader.VS.Templates;

/// <summary>.NET 8 console application template (OutputType=Exe).</summary>
internal sealed class ConsoleAppTemplate : DotNetProjectTemplate
{
    public override string Id          => "dotnet-console";
    public override string DisplayName => "Console Application (.NET 8)";
    public override string Description => "A .NET 8 command-line application. Generates a ready-to-build .sln + .csproj.";

    protected override Task WriteCsprojAsync(string projectDir, string projectName, CancellationToken ct) =>
        WriteAsync(Path.Combine(projectDir, $"{projectName}.csproj"), $$"""
            <Project Sdk="Microsoft.NET.Sdk">

              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net8.0</TargetFramework>
                <AssemblyName>{{projectName}}</AssemblyName>
                <RootNamespace>{{projectName}}</RootNamespace>
                <Nullable>enable</Nullable>
                <ImplicitUsings>enable</ImplicitUsings>
              </PropertyGroup>

            </Project>
            """, ct);

    protected override Task WriteSourceFilesAsync(string projectDir, string projectName, CancellationToken ct) =>
        WriteAsync(Path.Combine(projectDir, "Program.cs"), """
            // See https://aka.ms/new-console-template for more information
            Console.WriteLine("Hello, World!");
            """, ct);
}
