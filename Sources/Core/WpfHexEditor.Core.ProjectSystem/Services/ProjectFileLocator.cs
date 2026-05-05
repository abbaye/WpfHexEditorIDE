// ==========================================================
// Project: WpfHexEditor.Core.ProjectSystem
// File: Services/ProjectFileLocator.cs
// Description:
//     Default implementation of IProjectFileLocator. Walks parent directories
//     upward from a given file to locate the nearest project file.
// Architecture: Stateless service — safe to register as singleton.
// ==========================================================

using System.IO;

namespace WpfHexEditor.Core.ProjectSystem.Services;

public sealed class ProjectFileLocator : IProjectFileLocator
{
    public string? FindNearest(string filePath, string searchPattern)
    {
        var dir = Path.GetDirectoryName(filePath);
        while (dir is not null)
        {
            var match = Directory.GetFiles(dir, searchPattern).FirstOrDefault();
            if (match is not null) return match;
            dir = Path.GetDirectoryName(dir);
        }
        return null;
    }

    public string? FindNearestVbproj(string filePath)  => FindNearest(filePath, "*.vbproj");
    public string? FindNearestCsproj(string filePath)  => FindNearest(filePath, "*.csproj");
}
