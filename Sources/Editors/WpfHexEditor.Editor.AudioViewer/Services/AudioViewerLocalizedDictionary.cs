///////////////////////////////////////////////////////////////
// GNU Affero General Public License v3.0  2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Project     : WpfHexEditor.Editor.AudioViewer
// File        : AudioViewerLocalizedDictionary.cs
// Description : Self-contained LocalizedResourceDictionary for AudioViewer.
///////////////////////////////////////////////////////////////

using WpfHexEditor.Core.Localization.Services;
using WpfHexEditor.Editor.AudioViewer.Properties;

namespace WpfHexEditor.Editor.AudioViewer.Services;

public sealed class AudioViewerLocalizedDictionary : LocalizedResourceDictionary
{
    public AudioViewerLocalizedDictionary()
    {
        RegisterResourceManager(AudioViewerResources.ResourceManager);
        LoadResources();
    }
}
