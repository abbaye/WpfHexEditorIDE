//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Collections.Generic;

namespace WpfHexEditor.Core.ProjectSystem.Templates;

/// <summary>
/// Central registry of available <see cref="IFileTemplate"/> instances.
/// Third-party code may call <see cref="Register"/> to add custom templates.
/// </summary>
public static class FileTemplateRegistry
{
    private static readonly List<IFileTemplate> _templates =
    [
        // General (3)
        new BinaryFileTemplate(),
        new TextFileTemplate(),
        new WhfmtFileTemplate(),

        // C# / .NET (7)
        new CSharpClassTemplate(),
        new CSharpInterfaceTemplate(),
        new CSharpEnumTemplate(),
        new CSharpRecordTemplate(),
        new CSharpStructTemplate(),
        new VbNetClassTemplate(),
        new CsxScriptTemplate(),

        // Script (4)
        new AsmFileTemplate(),
        new PowerShellFileTemplate(),
        new PythonFileTemplate(),
        new LuaFileTemplate(),

        // Data (5)
        new JsonFileTemplate(),
        new TblFileTemplate(),
        new XmlFileTemplate(),
        new YamlFileTemplate(),
        new CsvFileTemplate(),

        // Web (4)
        new HtmlFileTemplate(),
        new CssFileTemplate(),
        new JavaScriptFileTemplate(),
        new TypeScriptFileTemplate(),
    ];

    /// <summary>
    /// All currently registered templates (in display order).
    /// </summary>
    public static IReadOnlyList<IFileTemplate> Templates => _templates;

    /// <summary>
    /// Adds a custom template to the registry.
    /// </summary>
    public static void Register(IFileTemplate template)
    {
        if (template is null) throw new ArgumentNullException(nameof(template));
        _templates.Add(template);
    }

    /// <summary>
    /// Returns the template whose <see cref="IFileTemplate.DefaultExtension"/>
    /// matches <paramref name="extension"/> (case-insensitive), or <c>null</c>.
    /// </summary>
    public static IFileTemplate? FindByExtension(string extension)
        => _templates.Find(t =>
            string.Equals(t.DefaultExtension, extension, StringComparison.OrdinalIgnoreCase));
}
