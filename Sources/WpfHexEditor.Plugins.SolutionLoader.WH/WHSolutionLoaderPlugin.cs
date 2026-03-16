// ==========================================================
// Project: WpfHexEditor.Plugins.SolutionLoader.WH
// File: WHSolutionLoaderPlugin.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     IWpfHexEditorPlugin entry point for the native WH solution loader.
//     Registers WHSolutionLoader as an ISolutionLoader extension point
//     so the IDE can open .whsln / .whproj files natively.
//
// Architecture Notes:
//     Pattern: Adapter + Extension Point
//     Registration via IExtensionRegistry (no direct MainWindow coupling).
// ==========================================================

using WpfHexEditor.Editor.Core;
using WpfHexEditor.SDK.Contracts;

namespace WpfHexEditor.Plugins.SolutionLoader.WH;

/// <summary>
/// Plugin entry point for the native WpfHexEditor solution loader.
/// Contributes <see cref="WHSolutionLoader"/> to the <see cref="ISolutionLoader"/>
/// extension point so the IDE can open .whsln / .whproj files.
/// </summary>
public sealed class WHSolutionLoaderPlugin : IWpfHexEditorPlugin
{
    public string  Id      => "WpfHexEditor.Plugins.SolutionLoader.WH";
    public string  Name    => "WH Solution Loader";
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
        context.ExtensionRegistry.Register<ISolutionLoader>(Id, new WHSolutionLoader());
        return Task.CompletedTask;
    }

    public Task ShutdownAsync(CancellationToken ct = default) => Task.CompletedTask;
}
