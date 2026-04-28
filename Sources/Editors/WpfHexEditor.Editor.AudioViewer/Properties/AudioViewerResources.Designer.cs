///////////////////////////////////////////////////////////////
// GNU Affero General Public License v3.0  2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Project     : WpfHexEditor.Editor.AudioViewer
// File        : AudioViewerResources.Designer.cs
// Description : Strongly-typed accessor for AudioViewer-specific UI strings.
///////////////////////////////////////////////////////////////

using System.Globalization;
using System.Resources;

namespace WpfHexEditor.Editor.AudioViewer.Properties;

internal static class AudioViewerResources
{
    private static ResourceManager? _resourceManager;
    private static CultureInfo? _resourceCulture;

    internal static ResourceManager ResourceManager =>
        _resourceManager ??= new ResourceManager(
            "WpfHexEditor.Editor.AudioViewer.Properties.AudioViewerResources",
            typeof(AudioViewerResources).Assembly);

    internal static CultureInfo? Culture
    {
        get => _resourceCulture;
        set => _resourceCulture = value;
    }

    private static string GetString(string name) =>
        ResourceManager.GetString(name, _resourceCulture) ?? name;

    internal static string AudioViewer_ToolbarOverflowToolTip => GetString(nameof(AudioViewer_ToolbarOverflowToolTip));
    internal static string AudioViewer_FormatLabel            => GetString(nameof(AudioViewer_FormatLabel));
    internal static string AudioViewer_PlayPauseToolTip       => GetString(nameof(AudioViewer_PlayPauseToolTip));
    internal static string AudioViewer_StopToolTip            => GetString(nameof(AudioViewer_StopToolTip));
    internal static string AudioViewer_PositionDefault        => GetString(nameof(AudioViewer_PositionDefault));
    internal static string AudioViewer_StatusBarOpenFile      => GetString(nameof(AudioViewer_StatusBarOpenFile));
    internal static string AudioViewer_OverlayAnalyzing       => GetString(nameof(AudioViewer_OverlayAnalyzing));
}
