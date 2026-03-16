// ==========================================================
// Project: WpfHexEditor.Plugins.Build.MSBuild
// File: MSBuildPlugin.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     IWpfHexEditorPlugin entry point for the MSBuild adapter plugin.
//     Registers MSBuildAdapter as an IBuildAdapter extension point so the
//     BuildSystem can compile .csproj / .vbproj / .fsproj projects.
//
// Architecture Notes:
//     Pattern: Adapter + Extension Point
//     Registration via IExtensionRegistry (no direct MainWindow coupling).
// ==========================================================

using WpfHexEditor.BuildSystem;
using WpfHexEditor.SDK.Contracts;

namespace WpfHexEditor.Plugins.Build.MSBuild;

/// <summary>
/// Plugin entry point for the MSBuild adapter.
/// Contributes <see cref="MSBuildAdapter"/> to the <see cref="IBuildAdapter"/>
/// extension point so the BuildSystem can invoke MSBuild for VS project files.
/// </summary>
public sealed class MSBuildPlugin : IWpfHexEditorPlugin
{
    public string  Id      => "WpfHexEditor.Plugins.Build.MSBuild";
    public string  Name    => "MSBuild Adapter";
    public Version Version => new(1, 0, 0);

    public PluginCapabilities Capabilities => new()
    {
        AccessHexEditor  = false,
        AccessFileSystem = true,
        RegisterMenus    = false,
        WriteOutput      = true,
    };

    public Task InitializeAsync(IIDEHostContext context, CancellationToken ct = default)
    {
        context.ExtensionRegistry.Register<IBuildAdapter>(Id, new MSBuildAdapter());
        return Task.CompletedTask;
    }

    public Task ShutdownAsync(CancellationToken ct = default) => Task.CompletedTask;
}
