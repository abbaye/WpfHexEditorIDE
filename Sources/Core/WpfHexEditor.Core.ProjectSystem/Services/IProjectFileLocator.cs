// ==========================================================
// Project: WpfHexEditor.Core.ProjectSystem
// File: Services/IProjectFileLocator.cs
// Description:
//     Contract for locating project files (.csproj, .vbproj) by walking
//     the directory tree upward from a given file path.
// Architecture: Service interface — implementations injected via DI.
// ==========================================================

namespace WpfHexEditor.Core.ProjectSystem.Services;

public interface IProjectFileLocator
{
    /// <summary>
    /// Walks parent directories from <paramref name="filePath"/> upward until a file
    /// matching <paramref name="searchPattern"/> is found.
    /// </summary>
    /// <returns>Absolute path of the first match, or <c>null</c> if none found.</returns>
    string? FindNearest(string filePath, string searchPattern);

    /// <summary>Finds the nearest <c>.vbproj</c> file above <paramref name="filePath"/>.</summary>
    string? FindNearestVbproj(string filePath);

    /// <summary>Finds the nearest <c>.csproj</c> file above <paramref name="filePath"/>.</summary>
    string? FindNearestCsproj(string filePath);
}
