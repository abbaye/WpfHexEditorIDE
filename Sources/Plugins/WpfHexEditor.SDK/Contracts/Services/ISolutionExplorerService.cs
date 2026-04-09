//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.SDK.Contracts.Services;

/// <summary>
/// Describes a single project within the active solution,
/// including its source files eligible for class diagram analysis.
/// </summary>
/// <param name="Name">Display name of the project.</param>
/// <param name="ProjectPath">Absolute path to the project file (.whproj / .csproj).</param>
/// <param name="SourceFiles">All .cs/.vb source file paths belonging to this project.</param>
public sealed record SolutionProjectInfo(
    string Name,
    string ProjectPath,
    IReadOnlyList<string> SourceFiles);

/// <summary>
/// Provides read access to the active IDE solution structure.
/// </summary>
public interface ISolutionExplorerService
{
    /// <summary>Gets whether a solution is currently open.</summary>
    bool HasActiveSolution { get; }

    /// <summary>Gets the path to the active solution file (.whsln), or null if none.</summary>
    string? ActiveSolutionPath { get; }

    /// <summary>Gets the name of the active solution, or null if none.</summary>
    string? ActiveSolutionName { get; }

    /// <summary>
    /// Gets all file paths currently open in the IDE as document tabs.
    /// </summary>
    IReadOnlyList<string> GetOpenFilePaths();

    /// <summary>
    /// Gets all file paths belonging to the active solution (across all projects).
    /// Returns empty list if no solution is open.
    /// </summary>
    IReadOnlyList<string> GetSolutionFilePaths();

    /// <summary>
    /// Gets all projects in the active solution, each with their source file paths.
    /// Returns empty list if no solution is open.
    /// </summary>
    IReadOnlyList<SolutionProjectInfo> GetSolutionProjects();

    /// <summary>
    /// Opens a file in the IDE (creates a new document tab or activates an existing one).
    /// </summary>
    /// <param name="filePath">Absolute path to the file to open.</param>
    /// <param name="ct">Cancellation token.</param>
    Task OpenFileAsync(string filePath, CancellationToken ct = default);

    /// <summary>Closes an open file tab. If <paramref name="fileName"/> is null, closes the active document.</summary>
    Task CloseFileAsync(string? fileName = null, CancellationToken ct = default);

    /// <summary>Saves a file. If <paramref name="fileName"/> is null, saves the active document.</summary>
    Task SaveFileAsync(string? fileName = null, CancellationToken ct = default);

    /// <summary>Opens a folder in Solution Explorer (navigates to it).</summary>
    Task OpenFolderAsync(string path, CancellationToken ct = default);

    /// <summary>Activates a project node in Solution Explorer by name.</summary>
    Task OpenProjectAsync(string name, CancellationToken ct = default);

    /// <summary>Closes a project by name (removes from solution).</summary>
    Task CloseProjectAsync(string name, CancellationToken ct = default);

    /// <summary>Opens a solution file in the IDE.</summary>
    Task OpenSolutionAsync(string path, CancellationToken ct = default);

    /// <summary>Closes the active solution.</summary>
    Task CloseSolutionAsync(CancellationToken ct = default);

    /// <summary>Reloads the active solution from disk.</summary>
    Task ReloadSolutionAsync(CancellationToken ct = default);

    /// <summary>Returns file paths inside <paramref name="path"/> (non-recursive). Returns empty list on error.</summary>
    IReadOnlyList<string> GetFilesInDirectory(string path);

    /// <summary>
    /// Raised when the active solution changes (opened, closed, reloaded).
    /// Raised on the UI thread.
    /// </summary>
    event EventHandler SolutionChanged;
}
