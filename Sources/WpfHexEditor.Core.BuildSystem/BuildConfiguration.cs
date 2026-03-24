// ==========================================================
// Project: WpfHexEditor.BuildSystem
// File: BuildConfiguration.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Concrete IBuildConfiguration implementation. Mutable record used by
//     ConfigurationManager and ProjectPropertiesDialog.
// ==========================================================

using WpfHexEditor.Editor.Core;

namespace WpfHexEditor.Core.BuildSystem;

/// <summary>Mutable implementation of <see cref="IBuildConfiguration"/>.</summary>
public sealed class BuildConfiguration : IBuildConfiguration
{
    public string Name           { get; set; } = "Debug";
    public string Platform       { get; set; } = "AnyCPU";
    public string OutputPath     { get; set; } = @"bin\Debug\net8.0\";
    public bool   Optimize       { get; set; }
    public string DefineConstants { get; set; } = "DEBUG;TRACE";

    public static BuildConfiguration Debug   => new() { Name = "Debug",   Optimize = false, DefineConstants = "DEBUG;TRACE" };
    public static BuildConfiguration Release => new() { Name = "Release", Optimize = true,  DefineConstants = "TRACE",       OutputPath = @"bin\Release\net8.0\" };
}
