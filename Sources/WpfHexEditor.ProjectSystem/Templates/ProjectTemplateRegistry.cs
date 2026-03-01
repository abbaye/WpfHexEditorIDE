//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.ProjectSystem.Templates;

/// <summary>
/// Global registry of available <see cref="IProjectTemplate"/> instances.
/// The host application (or plugins) registers templates at startup via <see cref="Register"/>.
/// </summary>
public static class ProjectTemplateRegistry
{
    private static readonly List<IProjectTemplate> _templates = [];

    /// <summary>
    /// All registered templates, in registration order.
    /// </summary>
    public static IReadOnlyList<IProjectTemplate> Templates => _templates;

    /// <summary>
    /// Register a new template. Duplicate <see cref="IProjectTemplate.Id"/> replaces the existing entry.
    /// </summary>
    public static void Register(IProjectTemplate template)
    {
        ArgumentNullException.ThrowIfNull(template);
        var idx = _templates.FindIndex(t => t.Id == template.Id);
        if (idx >= 0) _templates[idx] = template;
        else          _templates.Add(template);
    }

    /// <summary>
    /// Returns the template with the given id, or <see langword="null"/>.
    /// </summary>
    public static IProjectTemplate? FindById(string id)
        => _templates.Find(t => t.Id == id);

    /// <summary>
    /// Returns all templates belonging to the given category (case-insensitive).
    /// </summary>
    public static IReadOnlyList<IProjectTemplate> GetByCategory(string category)
        => _templates.FindAll(t => string.Equals(t.Category, category, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Register the 16 built-in templates shipped with WpfHexEditor.
    /// </summary>
    public static void RegisterDefaults()
    {
        // ── General ─────────────────────────────────────────────────────
        Register(new EmptyProjectTemplate());
        Register(new ScratchProjectTemplate());

        // ── Analysis ────────────────────────────────────────────────────
        Register(new BinaryAnalysisTemplate());
        Register(new ForensicsTemplate());
        Register(new FirmwareAnalysisTemplate());
        Register(new NetworkCaptureTemplate());
        Register(new ScientificDataTemplate());
        Register(new MediaInspectionTemplate());

        // ── ReverseEngineering ───────────────────────────────────────────
        Register(new ReverseEngineeringTemplate());
        Register(new DecompilationTemplate());
        Register(new CryptoAnalysisTemplate());

        // ── Development ─────────────────────────────────────────────────
        Register(new FormatDefinitionTemplate());
        Register(new TextScriptTemplate());

        // ── RomHacking ──────────────────────────────────────────────────
        Register(new RomHackingTemplate());
        Register(new PatchDevelopmentTemplate());
        Register(new TranslationTemplate());
    }
}
