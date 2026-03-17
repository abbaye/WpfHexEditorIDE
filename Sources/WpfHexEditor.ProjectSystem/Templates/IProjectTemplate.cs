//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using WpfHexEditor.Editor.Core;

namespace WpfHexEditor.ProjectSystem.Templates;

/// <summary>
/// A VS-style project template. When the user creates a new project from the
/// <c>NewProjectDialog</c>, the chosen template's <see cref="ScaffoldAsync"/> method
/// creates the initial file/folder structure on disk.
/// </summary>
public interface IProjectTemplate
{
    /// <summary>
    /// Unique stable identifier (e.g. "rom-hacking", "binary-analysis").
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Human-readable name shown in the dialog.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Short description shown below the name card.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Category for the filter strip (e.g. "General", "Analysis", "ReverseEngineering", "Development", "RomHacking").
    /// </summary>
    string Category { get; }

    /// <summary>
    /// Scaffold the project directory: create folders + initial files.
    /// <paramref name="projectDirectory"/> is the directory that will contain the .whproj file.
    /// </summary>
    Task<ProjectScaffold> ScaffoldAsync(string projectDirectory, string projectName, CancellationToken ct = default);
}

/// <summary>
/// A project template that is fully self-contained: it creates its own .sln + .csproj
/// structure on disk and returns the path to the generated solution file.
/// MainWindow bypasses the .whproj creation flow and opens the result via OpenSolutionAsync.
/// </summary>
public interface ISelfContainedProjectTemplate : IProjectTemplate
{
    /// <summary>
    /// Scaffolds a brand-new .sln + .csproj under <paramref name="parentDirectory"/>
    /// and returns the absolute path to the generated .sln file.
    /// </summary>
    Task<string> CreateAsync(string parentDirectory, string projectName,
                              CancellationToken ct = default);

    /// <summary>
    /// Scaffolds only the .csproj + source files under <paramref name="parentDirectory"/>
    /// and appends the project entry into an existing <paramref name="existingSlnPath"/>.
    /// Returns the path to the (modified) .sln file.
    /// </summary>
    Task<string> AddToSolutionAsync(string existingSlnPath, string parentDirectory,
                                     string projectName, CancellationToken ct = default);
}

/// <summary>
/// Description of the files and folders to create when scaffolding a project.
/// </summary>
public sealed class ProjectScaffold
{
    /// <summary>
    /// Initial files to write to disk.
    /// </summary>
    public IReadOnlyList<ScaffoldFile> Files { get; init; } = [];

    /// <summary>
    /// Virtual folder names to pre-create in the .whproj.
    /// </summary>
    public IReadOnlyList<string> VirtualFolders { get; init; } = [];

    /// <summary>
    /// Project type id to store in the .whproj (mirrors <see cref="IProjectTemplate.Id"/>).
    /// </summary>
    public string? ProjectType { get; init; }
}

/// <summary>
/// A single file to create during project scaffolding.
/// </summary>
public sealed class ScaffoldFile
{
    /// <summary>
    /// Path relative to the project directory (forward slashes, e.g. "Data/readme.txt").
    /// </summary>
    public required string RelativePath { get; init; }

    /// <summary>
    /// Raw content bytes. Empty array creates an empty file.
    /// </summary>
    public byte[] Content { get; init; } = [];

    /// <summary>
    /// If true, the host should open this file in a tab immediately after creation.
    /// </summary>
    public bool OpenOnCreate { get; init; }

    /// <summary>
    /// Item type hint for the project system.
    /// </summary>
    public ProjectItemType ItemType { get; init; } = ProjectItemType.Binary;
}
