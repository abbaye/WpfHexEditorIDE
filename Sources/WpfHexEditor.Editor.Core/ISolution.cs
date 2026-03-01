//////////////////////////////////////////////
// Apache 2.0  - 2026
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.Editor.Core;

/// <summary>
/// A WpfHexEditor solution (.whsln). A solution is the top-level container
/// that groups one or more projects and persists the docking layout so the
/// whole workspace is restored exactly as the user left it.
/// </summary>
public interface ISolution
{
    string Name { get; }

    /// <summary>Absolute path to the .whsln file.</summary>
    string FilePath { get; }

    IReadOnlyList<IProject> Projects { get; }

    /// <summary>The project that is considered "active" when the solution is opened.</summary>
    IProject? StartupProject { get; }

    /// <summary><see langword="true"/> when the solution or any of its projects has unsaved changes.</summary>
    bool IsModified { get; }
}
