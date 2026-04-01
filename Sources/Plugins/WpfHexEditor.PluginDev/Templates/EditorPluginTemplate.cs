// ==========================================================
// Project: WpfHexEditor.PluginDev
// File: Templates/EditorPluginTemplate.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-23
// Description:
//     Scaffolds an Editor-style plugin project:
//       - MyEditor UserControl (code-behind, implements IMyEditorContent)
//       - IMyEditorContent interface
//       - MyEditorPlugin (entry point, registers the editor document type)
//
// Architecture Notes:
//     Pattern: Template Method — fills variable slots in embedded strings.
//     Editor plugins register a DocumentDescriptor so the IDE can open
//     file types in the custom editor.
// ==========================================================

using System.IO;

namespace WpfHexEditor.PluginDev.Templates;

/// <summary>
/// Scaffolds a plugin that contributes a custom document editor.
/// </summary>
public sealed class EditorPluginTemplate : IPluginTemplate
{
    // -----------------------------------------------------------------------
    // IPluginTemplate
    // -----------------------------------------------------------------------

    public string TemplateId   => "editor";
    public string DisplayName  => "Editor Plugin";
    public string Description  => "A custom document editor registered for specific file extensions.";
    public string Icon         => "\uE8A4";

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
            Path.Combine(outputDir, $"I{safeName}EditorContent.cs"),
            BuildInterface(ns, safeName, authorName, date), ct);

        await File.WriteAllTextAsync(
            Path.Combine(outputDir, $"{safeName}Editor.cs"),
            BuildEditor(ns, safeName, authorName, date), ct);

        await File.WriteAllTextAsync(
            Path.Combine(outputDir, $"{safeName}Plugin.cs"),
            BuildPlugin(ns, safeName, pluginId, authorName, date), ct);
    }

    // -----------------------------------------------------------------------
    // Template bodies
    // -----------------------------------------------------------------------

    private static string BuildInterface(string ns, string name, string author, string date) => $$"""
// ==========================================================
// Plugin:  {{name}}
// File:    I{{name}}EditorContent.cs
// Author:  {{author}}
// Created: {{date}}
// Description: Contract for the {{name}} editor content.
// ==========================================================

namespace {{ns}};

/// <summary>
/// Contract implemented by <see cref="{{name}}Editor"/>.
/// Keeps editor logic decoupled from its WPF representation.
/// </summary>
public interface I{{name}}EditorContent
{
    /// <summary>Absolute file path currently loaded into the editor.</summary>
    string? FilePath { get; }

    /// <summary>True when the editor contains unsaved changes.</summary>
    bool IsDirty { get; }

    /// <summary>Opens <paramref name="filePath"/> in the editor.</summary>
    Task OpenAsync(string filePath, CancellationToken ct = default);

    /// <summary>Saves the current content back to disk.</summary>
    Task SaveAsync(CancellationToken ct = default);
}
""";

    private static string BuildEditor(string ns, string name, string author, string date) => $$"""
// ==========================================================
// Plugin:  {{name}}
// File:    {{name}}Editor.cs
// Author:  {{author}}
// Created: {{date}}
// Description: Custom document editor (code-behind UserControl).
// ==========================================================

using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace {{ns}};

/// <summary>
/// Custom editor for the {{name}} plugin. Implements <see cref="I{{name}}EditorContent"/>.
/// </summary>
public sealed class {{name}}Editor : UserControl, I{{name}}EditorContent
{
    private string? _filePath;
    private bool    _isDirty;

    private readonly TextBox _textBox;

    public string? FilePath => _filePath;
    public bool    IsDirty  => _isDirty;

    public {{name}}Editor()
    {
        _textBox = new TextBox
        {
            AcceptsReturn  = true,
            AcceptsTab     = true,
            VerticalScrollBarVisibility   = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            FontFamily = new System.Windows.Media.FontFamily("Consolas"),
            FontSize   = 13,
        };
        _textBox.TextChanged += (_, _) => _isDirty = true;

        Content = _textBox;
    }

    public async Task OpenAsync(string filePath, CancellationToken ct = default)
    {
        _filePath     = filePath;
        _textBox.Text = await File.ReadAllTextAsync(filePath, ct);
        _isDirty      = false;
    }

    public async Task SaveAsync(CancellationToken ct = default)
    {
        if (_filePath is null) return;
        await File.WriteAllTextAsync(_filePath, _textBox.Text, ct);
        _isDirty = false;
    }
}
""";

    private static string BuildPlugin(string ns, string name, string pluginId, string author, string date) => $$"""
// ==========================================================
// Plugin:  {{name}}
// File:    {{name}}Plugin.cs
// Author:  {{author}}
// Created: {{date}}
// Description: Plugin entry point — registers the {{name}} editor.
// ==========================================================

using WpfHexEditor.SDK.Contracts;
using WpfHexEditor.SDK.Descriptors;
using WpfHexEditor.SDK.Models;

namespace {{ns}};

/// <summary>
/// Entry point for the {{name}} editor plugin.
/// </summary>
public sealed class {{name}}Plugin : IWpfHexEditorPlugin
{
    public string  Id      => "{{pluginId}}";
    public string  Name    => "{{name}}";
    public Version Version => new(1, 0, 0);

    public PluginCapabilities Capabilities => new()
    {
        AccessFileSystem = true,
        WriteOutput      = true,
    };

    public Task InitializeAsync(IIDEHostContext context, CancellationToken ct = default)
    {
        context.UIRegistry.RegisterDocumentType(new DocumentDescriptor
        {
            // TODO: add the file extensions your editor handles, e.g. ".myext"
            Extensions      = [".myext"],
            EditorTitle     = "{{name}} Editor",
            ContentFactory  = filePath =>
            {
                var editor = new {{name}}Editor();
                // Fire-and-forget open; errors are surfaced in Output.
                _ = editor.OpenAsync(filePath);
                return editor;
            },
        });

        context.Output.WriteLine(Id, $"{{name}} editor registered.");
        return Task.CompletedTask;
    }

    public Task ShutdownAsync(CancellationToken ct = default) => Task.CompletedTask;
}
""";
}
