// ==========================================================
// Project: WpfHexEditor.Plugins.SolutionLoader.Folder
// File: FolderModels/FolderSolution.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-17
// Description:
//     ISolution adapter for a folder session.
//     Contains a single FolderProject representing the opened directory.
//     IsReadOnlyFormat = true — the folder session is never serialized to disk;
//     the .whfolder marker file stores only settings, not project structure.
// ==========================================================

using WpfHexEditor.Editor.Core;

namespace WpfHexEditor.Plugins.SolutionLoader.Folder.FolderModels;

internal sealed class FolderSolution : ISolution
{
    public string                         Name                  { get; init; } = string.Empty;
    public string                         FilePath              { get; init; } = string.Empty;
    public IReadOnlyList<IProject>        Projects              { get; init; } = [];
    public IReadOnlyList<ISolutionFolder> RootFolders           { get; init; } = [];
    public IProject?                      StartupProject        => null;
    public bool                           IsModified            => false;
    public int                            SourceFormatVersion   => 1;
    public bool                           FormatUpgradeRequired => false;
    public bool                           IsReadOnlyFormat      => true;
}
