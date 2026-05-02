// ==========================================================
// Project: WpfHexEditor.Plugins.UnitTesting
// File: Properties/UnitTestingResources.Designer.cs
// Description: Auto-generated strongly-typed resource class for UnitTesting plugin.
// Architecture: Standard ResX pattern — do not edit manually.
// ==========================================================

using System.Globalization;
using System.Resources;

namespace WpfHexEditor.Plugins.UnitTesting.Properties;

internal static class UnitTestingResources
{
    private static ResourceManager? _resourceManager;
    private static CultureInfo? _resourceCulture;

    internal static ResourceManager ResourceManager
    {
        get
        {
            _resourceManager ??= new ResourceManager(
                "WpfHexEditor.Plugins.UnitTesting.Properties.UnitTestingResources",
                typeof(UnitTestingResources).Assembly);
            return _resourceManager;
        }
    }

    internal static CultureInfo? Culture
    {
        get => _resourceCulture;
        set => _resourceCulture = value;
    }

    /// <summary>Localized: "No test projects found in the current solution"</summary>
    internal static string UnitTest_NoTestProjects
        => ResourceManager.GetString("UnitTest_NoTestProjects", _resourceCulture)!;

    /// <summary>Localized: "Run cancelled"</summary>
    internal static string UnitTest_RunCancelled
        => ResourceManager.GetString("UnitTest_RunCancelled", _resourceCulture)!;

    /// <summary>Localized: "Run failed — see Output panel"</summary>
    internal static string UnitTest_RunFailed
        => ResourceManager.GetString("UnitTest_RunFailed", _resourceCulture)!;

    /// <summary>Localized: "Ready"</summary>
    internal static string UnitTest_Ready
        => ResourceManager.GetString("UnitTest_Ready", _resourceCulture)!;

    /// <summary>Localized: "Open in Code Editor"</summary>
    internal static string UnitTest_OpenInCodeEditor
        => ResourceManager.GetString("UnitTest_OpenInCodeEditor", _resourceCulture)!;

    /// <summary>Localized: "Open in Hex Editor"</summary>
    internal static string UnitTest_OpenInHexEditor
        => ResourceManager.GetString("UnitTest_OpenInHexEditor", _resourceCulture)!;
}
