// ==========================================================
// Project: WpfHexEditor.Plugins.SolutionLoader.Folder
// File: FolderSolutionLoaderPlugin.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-17
// Description:
//     IWpfHexEditorPlugin entry point for the Folder Mode loader.
//     Registers FolderSolutionLoader as an ISolutionLoader extension so the
//     IDE can open any directory on disk as a VS Code–style "Open Folder"
//     session via the .whfolder marker file format.
//
// Architecture Notes:
//     Pattern: Adapter + Extension Point (identical to VsSolutionLoaderPlugin)
//     Registration via IExtensionRegistry — no direct MainWindow coupling.
//     The loader is IDisposable; ShutdownAsync stops the FileSystemWatcher.
// ==========================================================

using WpfHexEditor.Editor.Core;
using WpfHexEditor.SDK.Contracts;
using WpfHexEditor.SDK.Models;

namespace WpfHexEditor.Plugins.SolutionLoader.Folder;

/// <summary>
/// Plugin entry point for the Folder Mode solution loader.
/// Contributes <see cref="FolderSolutionLoader"/> to the <see cref="ISolutionLoader"/>
/// extension point so the IDE can open any folder as a flat solution.
/// </summary>
public sealed class FolderSolutionLoaderPlugin : IWpfHexEditorPlugin
{
    public string  Id      => "WpfHexEditor.Plugins.SolutionLoader.Folder";
    public string  Name    => "Folder Mode Loader";
    public Version Version => new(1, 0, 0);

    public PluginCapabilities Capabilities => new()
    {
        AccessHexEditor  = false,
        AccessFileSystem = true,
        RegisterMenus    = false,
        WriteOutput      = true,
    };

    private FolderSolutionLoader? _loader;

    public Task InitializeAsync(IIDEHostContext context, CancellationToken ct = default)
    {
        _loader = new FolderSolutionLoader(context);
        context.ExtensionRegistry.Register<ISolutionLoader>(Id, _loader);
        return Task.CompletedTask;
    }

    public Task ShutdownAsync(CancellationToken ct = default)
    {
        _loader?.Dispose();
        _loader = null;
        return Task.CompletedTask;
    }
}
