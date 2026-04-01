// ==========================================================
// Project: WpfHexEditor.Plugins.SolutionLoader.VS
// File: ClassLibraryTemplate.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     .NET 8 Class Library project template.
//     Generates: {name}.sln, {name}/{name}.csproj, {name}/Class1.cs
// ==========================================================

namespace WpfHexEditor.Plugins.SolutionLoader.VS.Templates;

/// <summary>.NET 8 class library template (OutputType defaults to Library).</summary>
internal sealed class ClassLibraryTemplate : DotNetProjectTemplate
{
    public override string Id          => "dotnet-classlib";
    public override string DisplayName => "Class Library (.NET 8)";
    public override string Description => "A .NET 8 class library. Generates a ready-to-build .sln + .csproj.";

    protected override Task WriteCsprojAsync(string projectDir, string projectName, CancellationToken ct) =>
        WriteAsync(Path.Combine(projectDir, $"{projectName}.csproj"), $$"""
            <Project Sdk="Microsoft.NET.Sdk">

              <PropertyGroup>
                <TargetFramework>net8.0</TargetFramework>
                <AssemblyName>{{projectName}}</AssemblyName>
                <RootNamespace>{{projectName}}</RootNamespace>
                <Nullable>enable</Nullable>
                <ImplicitUsings>enable</ImplicitUsings>
              </PropertyGroup>

            </Project>
            """, ct);

    protected override Task WriteSourceFilesAsync(string projectDir, string projectName, CancellationToken ct) =>
        WriteAsync(Path.Combine(projectDir, "Class1.cs"), $$"""
            namespace {{projectName}};

            /// <summary>
            ///
            /// </summary>
            public class Class1
            {
            }
            """, ct);
}
