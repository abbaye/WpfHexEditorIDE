// ==========================================================
// Project: WpfHexEditor.PluginDev
// File: Templates/EventListenerPluginTemplate.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-23
// Description:
//     Scaffolds an event-listener plugin project.
//     Generates MyListenerPlugin.cs which subscribes to IIDEEventBus
//     events and logs them to the IDE Output panel.
//
// Architecture Notes:
//     Pattern: Template Method — variable-slot string interpolation.
//     The generated plugin subscribes to common IDE events as examples;
//     developers add/remove subscriptions in InitializeAsync.
// ==========================================================

using System.IO;

namespace WpfHexEditor.PluginDev.Templates;

/// <summary>
/// Scaffolds a plugin that listens to typed IDE events via <c>IIDEEventBus</c>.
/// </summary>
public sealed class EventListenerPluginTemplate : IPluginTemplate
{
    // -----------------------------------------------------------------------
    // IPluginTemplate
    // -----------------------------------------------------------------------

    public string TemplateId   => "event-listener";
    public string DisplayName  => "Event Listener Plugin";
    public string Description  => "A plugin that subscribes to IDE events and reacts to IDE lifecycle changes.";
    public string Icon         => "\uE7C3";

    public async Task ScaffoldAsync(
        string            outputDir,
        string            pluginName,
        string            authorName,
        CancellationToken ct = default)
    {
        var safeName = PluginTemplateHelpers.MakeSafeName(pluginName);
        var ns       = $"WpfHexEditor.Plugins.{safeName}";
        var date     = DateTime.Now.ToString("yyyy-MM-dd");
        var pluginId = $"WpfHexEditor.Plugins.{safeName}";

        Directory.CreateDirectory(outputDir);

        await File.WriteAllTextAsync(
            Path.Combine(outputDir, $"{safeName}Plugin.cs"),
            BuildPlugin(ns, safeName, pluginId, authorName, date), ct);
    }

    // -----------------------------------------------------------------------
    // Template body
    // -----------------------------------------------------------------------

    private static string BuildPlugin(string ns, string name, string pluginId, string author, string date) => $$"""
// ==========================================================
// Plugin:  {{name}}
// File:    {{name}}Plugin.cs
// Author:  {{author}}
// Created: {{date}}
// Description: Event-listener plugin — subscribes to IIDEEventBus events.
// ==========================================================

using WpfHexEditor.Events.IDEEvents;
using WpfHexEditor.SDK.Contracts;
using WpfHexEditor.SDK.Models;

namespace {{ns}};

/// <summary>
/// Plugin that subscribes to IDE events and logs them to the Output panel.
/// </summary>
public sealed class {{name}}Plugin : IWpfHexEditorPlugin
{
    // Subscriptions are held so they can be disposed on shutdown.
    private readonly List<IDisposable> _subscriptions = [];
    private IIDEHostContext? _context;

    public string  Id      => "{{pluginId}}";
    public string  Name    => "{{name}}";
    public Version Version => new(1, 0, 0);

    public PluginCapabilities Capabilities => new()
    {
        WriteOutput = true,
    };

    public Task InitializeAsync(IIDEHostContext context, CancellationToken ct = default)
    {
        _context = context;

        // Subscribe to IDE events — add or remove as needed.
        _subscriptions.Add(
            context.IDEEvents.Subscribe<FileOpenedEvent>(OnFileOpened));

        _subscriptions.Add(
            context.IDEEvents.Subscribe<FileClosedEvent>(OnFileClosed));

        _subscriptions.Add(
            context.IDEEvents.Subscribe<BuildSucceededEvent>(OnBuildSucceeded));

        _subscriptions.Add(
            context.IDEEvents.Subscribe<BuildFailedEvent>(OnBuildFailed));

        context.Output.WriteLine(Id, $"{{name}} listening for IDE events.");
        return Task.CompletedTask;
    }

    public Task ShutdownAsync(CancellationToken ct = default)
    {
        foreach (var sub in _subscriptions)
            sub.Dispose();
        _subscriptions.Clear();

        return Task.CompletedTask;
    }

    // ── Event handlers ─────────────────────────────────────────────────────

    private void OnFileOpened(FileOpenedEvent e)
        => _context?.Output.WriteLine(Id, $"File opened: {e.FilePath}");

    private void OnFileClosed(FileClosedEvent e)
        => _context?.Output.WriteLine(Id, $"File closed: {e.FilePath}");

    private void OnBuildSucceeded(BuildSucceededEvent e)
        => _context?.Output.WriteLine(Id, $"Build succeeded in {e.Duration.TotalSeconds:F1}s.");

    private void OnBuildFailed(BuildFailedEvent e)
        => _context?.Output.WriteLine(Id, $"Build FAILED — {e.ErrorCount} error(s).");
}
""";
}
