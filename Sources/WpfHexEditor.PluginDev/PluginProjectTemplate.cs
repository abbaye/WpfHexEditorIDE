// ==========================================================
// Project: WpfHexEditor.PluginDev
// File: PluginProjectTemplate.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Scaffolds a new SDK plugin project from a built-in template:
//     .whproj (net8.0-windows), SDK reference, MyPlugin.cs starter,
//     plugin.manifest.json pre-filled, README.md.
//     Registered in TemplateManager as the "SDK Plugin" template.
//
// Architecture Notes:
//     Pattern: Template Method (fills variable slots in embedded template strings)
//     Does NOT depend on TemplateManager — TemplateManager references this class.
// ==========================================================

using System.Text;

namespace WpfHexEditor.PluginDev;

/// <summary>
/// Scaffolds a new WpfHexEditor SDK plugin project in the given directory.
/// </summary>
public sealed class PluginProjectTemplate
{
    // -----------------------------------------------------------------------
    // Public API
    // -----------------------------------------------------------------------

    /// <summary>
    /// Creates a fully scaffolded plugin project under <paramref name="parentDirectory"/>.
    /// The project lives in a subdirectory named <paramref name="pluginName"/>.
    /// </summary>
    public async Task<string> ScaffoldAsync(
        string             parentDirectory,
        string             pluginName,
        string             authorName,
        CancellationToken  ct = default)
    {
        if (string.IsNullOrWhiteSpace(pluginName))
            throw new ArgumentException("Plugin name required.", nameof(pluginName));

        var safeName   = MakeSafeName(pluginName);
        var pluginDir  = Path.Combine(parentDirectory, safeName);
        Directory.CreateDirectory(pluginDir);

        await WriteAllAsync(pluginDir, safeName, authorName, ct);

        return pluginDir;
    }

    // -----------------------------------------------------------------------
    // Private
    // -----------------------------------------------------------------------

    private static async Task WriteAllAsync(
        string dir, string name, string author, CancellationToken ct)
    {
        var pluginId  = $"WpfHexEditor.Plugins.{name}";
        var ns        = pluginId;
        var className = $"{name}Plugin";
        var date      = DateTime.Now.ToString("yyyy-MM-dd");

        // .whproj
        await File.WriteAllTextAsync(
            Path.Combine(dir, $"{name}.whproj"),
            BuildCsproj(pluginId, name, author), ct);

        // Entry point
        await File.WriteAllTextAsync(
            Path.Combine(dir, $"{className}.cs"),
            BuildEntryPoint(ns, className, pluginId, name, author, date), ct);

        // manifest
        await File.WriteAllTextAsync(
            Path.Combine(dir, "plugin.manifest.json"),
            BuildManifest(pluginId, name, author), ct);

        // README
        await File.WriteAllTextAsync(
            Path.Combine(dir, "README.md"),
            BuildReadme(name, pluginId, author, date), ct);
    }

    private static string BuildCsproj(string pluginId, string name, string author) => $"""
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <PluginId>{pluginId}</PluginId>
    <PluginName>{name}</PluginName>
    <PluginVersion>0.1.0</PluginVersion>
    <PluginEntryPoint>{pluginId}.{name}Plugin</PluginEntryPoint>
    <PluginAuthor>{author}</PluginAuthor>
    <PluginPublisher>WpfHexEditor</PluginPublisher>
    <PluginIsolationMode>Auto</PluginIsolationMode>
    <PluginLoadPriority>50</PluginLoadPriority>
    <PluginSdkVersion>1.2.0</PluginSdkVersion>
    <PluginPermAccessFileSystem>true</PluginPermAccessFileSystem>
    <PluginPermWriteOutput>true</PluginPermWriteOutput>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\WpfHexEditor.SDK\WpfHexEditor.SDK.csproj" />
  </ItemGroup>
  <Import Project="..\..\WpfHexEditor.SDK\Build\WpfHexEditor.SDK.targets" />
</Project>
""";

    private static string BuildEntryPoint(
        string ns, string className, string pluginId,
        string name, string author, string date) => $$"""
// ==========================================================
// Plugin: {{name}}
// Author: {{author}}
// Created: {{date}}
// Description: TODO — describe your plugin here.
// ==========================================================

using WpfHexEditor.SDK.Contracts;

namespace {{ns}};

public sealed class {{className}} : IWpfHexEditorPlugin
{
    public string  Id      => "{{pluginId}}";
    public string  Name    => "{{name}}";
    public Version Version => new(0, 1, 0);

    public PluginCapabilities Capabilities => new()
    {
        AccessFileSystem = true,
        WriteOutput      = true,
    };

    public Task InitializeAsync(IIDEHostContext context, CancellationToken ct = default)
    {
        // TODO: Register panels, menus, subscribe to events.
        context.Output.WriteLine(Id, Name + " initialized.");
        return Task.CompletedTask;
    }

    public Task ShutdownAsync(CancellationToken ct = default) => Task.CompletedTask;
}
""";

    private static string BuildManifest(string pluginId, string name, string author) => $$"""
{
  "id": "{{pluginId}}",
  "name": "{{name}}",
  "version": "0.1.0",
  "author": "{{author}}",
  "sdkVersion": "1.2.0",
  "isolationMode": "Auto",
  "loadPriority": 50,
  "permissions": {
    "accessFileSystem": true,
    "writeOutput": true
  }
}
""";

    private static string BuildReadme(string name, string pluginId, string author, string date) => $"""
# {name}

> WpfHexEditor SDK Plugin

- **ID**: `{pluginId}`
- **Author**: {author}
- **Created**: {date}

## Getting Started

1. Edit `{name}Plugin.cs` to implement your plugin logic.
2. Register UI panels via `context.UIRegistry`.
3. Subscribe to events via `context.IDEEvents` or `context.HexEditor`.
4. Build — the manifest and DLL are copied to the IDE's `Plugins/` directory automatically.

## References

- [WpfHexEditor SDK Documentation](https://github.com/...)
""";

    private static string MakeSafeName(string input)
    {
        var sb = new StringBuilder();
        foreach (var c in input)
            sb.Append(char.IsLetterOrDigit(c) || c == '_' ? c : '_');
        return sb.ToString().Trim('_');
    }
}
