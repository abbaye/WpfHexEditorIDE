// ==========================================================
// Project: WpfHexEditor.Editor.Core
// File: ISolutionFolder.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026
// Description:
//     A VS-like Solution Folder — a logical container at the solution level
//     that groups Projects. Solution Folders can be nested.
//     They do NOT hold project items (files); only Projects.
//
// Architecture Notes:
//     Pattern: Composite (ISolutionFolder has Children of ISolutionFolder).
//     Immutable public interface; mutations via the internal SolutionFolder class.
// ==========================================================

namespace WpfHexEditor.Editor.Core;

/// <summary>
/// A logical folder at the solution level, similar to Visual Studio Solution Folders.
/// Can contain <see cref="Projects"/> (by ID) and nested <see cref="Children"/> folders.
/// Solution Folders do NOT contain project items (files); only <see cref="IProject"/>s.
/// </summary>
public interface ISolutionFolder
{
    /// <summary>Stable unique identifier (GUID string).</summary>
    string Id { get; }

    /// <summary>Display name shown in Solution Explorer.</summary>
    string Name { get; }

    /// <summary>
    /// IDs of <see cref="IProject"/> instances directly under this folder.
    /// </summary>
    IReadOnlyList<string> ProjectIds { get; }

    /// <summary>Nested solution sub-folders.</summary>
    IReadOnlyList<ISolutionFolder> Children { get; }
}
