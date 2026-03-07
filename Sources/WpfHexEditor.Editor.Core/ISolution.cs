//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
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

    /// <summary>
    /// Absolute path to the .whsln file.
    /// </summary>
    string FilePath { get; }

    IReadOnlyList<IProject> Projects { get; }

    /// <summary>
    /// Top-level VS-like Solution Folders that group projects logically.
    /// Projects not in any folder are accessible directly via <see cref="Projects"/>.
    /// </summary>
    IReadOnlyList<ISolutionFolder> RootFolders { get; }

    /// <summary>
    /// The project that is considered "active" when the solution is opened.
    /// </summary>
    IProject? StartupProject { get; }

    /// <summary>
    /// <see langword="true"/> when the solution or any of its projects has unsaved changes.
    /// </summary>
    bool IsModified { get; }

    // -- Format versioning -------------------------------------------------

    /// <summary>
    /// The format version that was read from disk.
    /// <c>0</c> until a file is actually loaded.
    /// </summary>
    int SourceFormatVersion { get; }

    /// <summary>
    /// <see langword="true"/> when the files on disk use an older format than the
    /// application currently writes. The solution is loaded and usable, but saves are
    /// blocked until the user explicitly upgrades (or accepts read-only mode).
    /// </summary>
    bool FormatUpgradeRequired { get; }

    /// <summary>
    /// <see langword="true"/> when the user chose to continue in read-only mode after
    /// a format-upgrade prompt. Saves are disabled.
    /// </summary>
    bool IsReadOnlyFormat { get; }
}
