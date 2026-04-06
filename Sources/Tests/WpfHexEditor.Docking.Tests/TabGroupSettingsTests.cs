// ==========================================================
// Project: WpfHexEditor.Docking.Tests
// File: TabGroupSettingsTests.cs
// Contributors: Claude Sonnet 4.6
// Description:
//     Unit tests for TabGroupSettings — default values and JSON round-trip.
// ==========================================================

using System.Text.Json;
using WpfHexEditor.Core.Options;

namespace WpfHexEditor.Docking.Tests;

public class TabGroupSettingsTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var settings = new TabGroupSettings();

        Assert.Equal(200,  settings.MinGroupWidthPx);
        Assert.Equal(150,  settings.MinGroupHeightPx);
        Assert.True(settings.EnforceEqualSize);
        Assert.True(settings.PersistTabGroupLayout);
        Assert.True(settings.ShowGroupNumberBadge);
    }

    [Fact]
    public void JsonRoundTrip_PreservesAllValues()
    {
        var original = new TabGroupSettings
        {
            MinGroupWidthPx    = 350,
            MinGroupHeightPx   = 250,
            EnforceEqualSize   = false,
            PersistTabGroupLayout = false,
            ShowGroupNumberBadge  = false
        };

        var json = JsonSerializer.Serialize(original);
        var restored = JsonSerializer.Deserialize<TabGroupSettings>(json)!;

        Assert.Equal(original.MinGroupWidthPx,       restored.MinGroupWidthPx);
        Assert.Equal(original.MinGroupHeightPx,      restored.MinGroupHeightPx);
        Assert.Equal(original.EnforceEqualSize,      restored.EnforceEqualSize);
        Assert.Equal(original.PersistTabGroupLayout, restored.PersistTabGroupLayout);
        Assert.Equal(original.ShowGroupNumberBadge,  restored.ShowGroupNumberBadge);
    }

    [Fact]
    public void AppSettings_TabGroups_IsNotNull()
    {
        var settings = new AppSettings();
        Assert.NotNull(settings.TabGroups);
    }
}
