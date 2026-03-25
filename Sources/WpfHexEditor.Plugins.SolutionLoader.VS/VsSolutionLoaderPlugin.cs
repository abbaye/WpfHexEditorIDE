// ==========================================================
// Project: WpfHexEditor.Plugins.SolutionLoader.VS
// File: VsSolutionLoaderPlugin.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     IWpfHexEditorPlugin entry point for the Visual Studio solution loader.
//     Registers VsSolutionLoader as an ISolutionLoader extension point
//     so the IDE can open .sln / .csproj / .vbproj files.
//
// Architecture Notes:
//     Pattern: Adapter + Extension Point
//     Registration via IExtensionRegistry (no direct MainWindow coupling).
// ==========================================================

using WpfHexEditor.Editor.Core;
using WpfHexEditor.Plugins.SolutionLoader.VS.Templates;
using WpfHexEditor.Core.ProjectSystem.Templates;
using WpfHexEditor.SDK.Contracts;
using WpfHexEditor.SDK.Models;

namespace WpfHexEditor.Plugins.SolutionLoader.VS;

/// <summary>
/// Plugin entry point for the Visual Studio solution loader.
/// Contributes <see cref="VsSolutionLoader"/> to the <see cref="ISolutionLoader"/>
/// extension point so the IDE can open .sln / .csproj / .vbproj files.
/// </summary>
public sealed class VsSolutionLoaderPlugin : IWpfHexEditorPlugin
{
    public string  Id      => "WpfHexEditor.Plugins.SolutionLoader.VS";
    public string  Name    => "VS Solution Loader";
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
        context.ExtensionRegistry.Register<ISolutionLoader>(Id, new VsSolutionLoader());

        // Register .NET/C# project templates — only available when this plugin is loaded.
        ProjectTemplateRegistry.Register(new ConsoleAppTemplate());
        ProjectTemplateRegistry.Register(new ClassLibraryTemplate());
        ProjectTemplateRegistry.Register(new WpfAppTemplate());
        ProjectTemplateRegistry.Register(new AspNetApiTemplate());

        return Task.CompletedTask;
    }

    public Task ShutdownAsync(CancellationToken ct = default) => Task.CompletedTask;
}
