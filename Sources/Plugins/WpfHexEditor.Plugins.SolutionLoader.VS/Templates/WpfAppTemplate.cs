// ==========================================================
// Project: WpfHexEditor.Plugins.SolutionLoader.VS
// File: WpfAppTemplate.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     .NET 8 WPF Application project template.
//     Generates: {name}.sln, {name}/{name}.csproj,
//                {name}/App.xaml, App.xaml.cs,
//                {name}/MainWindow.xaml, MainWindow.xaml.cs
// ==========================================================

namespace WpfHexEditor.Plugins.SolutionLoader.VS.Templates;

/// <summary>.NET 8-windows WPF application template (UseWPF=true, OutputType=WinExe).</summary>
internal sealed class WpfAppTemplate : DotNetProjectTemplate
{
    public override string Id          => "dotnet-wpf";
    public override string DisplayName => "WPF Application (.NET 8)";
    public override string Description => "A .NET 8 WPF desktop application. Generates a ready-to-build .sln + .csproj with App.xaml and MainWindow.";

    protected override Task WriteCsprojAsync(string projectDir, string projectName, CancellationToken ct) =>
        WriteAsync(Path.Combine(projectDir, $"{projectName}.csproj"), $$"""
            <Project Sdk="Microsoft.NET.Sdk">

              <PropertyGroup>
                <OutputType>WinExe</OutputType>
                <TargetFramework>net8.0-windows</TargetFramework>
                <UseWPF>true</UseWPF>
                <AssemblyName>{{projectName}}</AssemblyName>
                <RootNamespace>{{projectName}}</RootNamespace>
                <Nullable>enable</Nullable>
                <ImplicitUsings>enable</ImplicitUsings>
              </PropertyGroup>

            </Project>
            """, ct);

    protected override async Task WriteSourceFilesAsync(string projectDir, string projectName, CancellationToken ct)
    {
        await WriteAsync(Path.Combine(projectDir, "App.xaml"), $$"""
            <Application x:Class="{{projectName}}.App"
                         xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                         StartupUri="MainWindow.xaml">
                <Application.Resources>
                </Application.Resources>
            </Application>
            """, ct);

        await WriteAsync(Path.Combine(projectDir, "App.xaml.cs"), $$"""
            using System.Windows;

            namespace {{projectName}};

            public partial class App : Application
            {
            }
            """, ct);

        await WriteAsync(Path.Combine(projectDir, "MainWindow.xaml"), $$"""
            <Window x:Class="{{projectName}}.MainWindow"
                    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                    Title="{{projectName}}" Height="450" Width="800">
                <Grid>
                </Grid>
            </Window>
            """, ct);

        await WriteAsync(Path.Combine(projectDir, "MainWindow.xaml.cs"), $$"""
            using System.Windows;

            namespace {{projectName}};

            public partial class MainWindow : Window
            {
                public MainWindow()
                {
                    InitializeComponent();
                }
            }
            """, ct);
    }
}
