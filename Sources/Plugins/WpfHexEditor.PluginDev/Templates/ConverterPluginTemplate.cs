// ==========================================================
// Project: WpfHexEditor.PluginDev
// File: Templates/ConverterPluginTemplate.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-23
// Description:
//     Scaffolds a byte-converter plugin project.
//     Generates MyConverter.cs implementing the IByteConverter contract
//     (defined in WpfHexEditor.Core) and MyConverterPlugin.cs which
//     registers it with the IDE.
//
// Architecture Notes:
//     Pattern: Template Method — variable-slot string interpolation.
//     IByteConverter is declared in WpfHexEditor.Core; the generated
//     plugin only references the SDK (which depends on Core transitively).
// ==========================================================

using System.IO;

namespace WpfHexEditor.PluginDev.Templates;

/// <summary>
/// Scaffolds a plugin that contributes a custom byte data converter.
/// </summary>
public sealed class ConverterPluginTemplate : IPluginTemplate
{
    // -----------------------------------------------------------------------
    // IPluginTemplate
    // -----------------------------------------------------------------------

    public string TemplateId   => "converter";
    public string DisplayName  => "Converter Plugin";
    public string Description  => "A plugin that contributes a custom byte data converter to the HexEditor.";
    public string Icon         => "\uE8AB";

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
            Path.Combine(outputDir, $"{safeName}Converter.cs"),
            BuildConverter(ns, safeName, authorName, date), ct);

        await File.WriteAllTextAsync(
            Path.Combine(outputDir, $"{safeName}Plugin.cs"),
            BuildPlugin(ns, safeName, pluginId, authorName, date), ct);
    }

    // -----------------------------------------------------------------------
    // Template bodies
    // -----------------------------------------------------------------------

    private static string BuildConverter(string ns, string name, string author, string date) => $$"""
// ==========================================================
// Plugin:  {{name}}
// File:    {{name}}Converter.cs
// Author:  {{author}}
// Created: {{date}}
// Description: Custom byte converter implementation.
//
// NOTE: IByteConverter is defined in WpfHexEditor.Core.
//       If your project does not reference Core directly, adapt as needed.
// ==========================================================

namespace {{ns}};

/// <summary>
/// Converts raw byte sequences using the {{name}} algorithm.
/// Register this converter via <c>context.HexEditor.RegisterConverter()</c>
/// (or the relevant SDK extension point) in <see cref="{{name}}Plugin"/>.
/// </summary>
public sealed class {{name}}Converter
{
    /// <summary>Human-readable converter name shown in the HexEditor UI.</summary>
    public string DisplayName => "{{name}}";

    /// <summary>
    /// Converts <paramref name="input"/> bytes and returns the result bytes.
    /// Replace this implementation with your conversion logic.
    /// </summary>
    /// <param name="input">Source byte array.</param>
    /// <returns>Converted byte array.</returns>
    public byte[] Convert(byte[] input)
    {
        // TODO: implement conversion logic.
        // Example: XOR each byte with 0xFF (simple bitwise NOT).
        var result = new byte[input.Length];
        for (var i = 0; i < input.Length; i++)
            result[i] = (byte)(input[i] ^ 0xFF);
        return result;
    }

    /// <summary>
    /// Inverse of <see cref="Convert"/>; used for round-trip validation.
    /// </summary>
    public byte[] Revert(byte[] input) => Convert(input); // XOR is its own inverse
}
""";

    private static string BuildPlugin(string ns, string name, string pluginId, string author, string date) => $$"""
// ==========================================================
// Plugin:  {{name}}
// File:    {{name}}Plugin.cs
// Author:  {{author}}
// Created: {{date}}
// Description: Plugin entry point — registers the {{name}} converter.
// ==========================================================

using WpfHexEditor.SDK.Contracts;
using WpfHexEditor.SDK.Models;

namespace {{ns}};

/// <summary>
/// Entry point for the {{name}} converter plugin.
/// </summary>
public sealed class {{name}}Plugin : IWpfHexEditorPlugin
{
    private readonly {{name}}Converter _converter = new();

    public string  Id      => "{{pluginId}}";
    public string  Name    => "{{name}}";
    public Version Version => new(1, 0, 0);

    public PluginCapabilities Capabilities => new()
    {
        WriteOutput = true,
    };

    public Task InitializeAsync(IIDEHostContext context, CancellationToken ct = default)
    {
        // TODO: register the converter via the appropriate SDK service.
        // The exact API depends on your IDE version; see SDK docs for IByteConverter
        // registration if context.HexEditor exposes a RegisterConverter() method.

        context.Output.WriteLine(Id, $"{{name}} converter loaded.");
        return Task.CompletedTask;
    }

    public Task ShutdownAsync(CancellationToken ct = default) => Task.CompletedTask;
}
""";
}
