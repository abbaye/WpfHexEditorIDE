//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5, Claude Sonnet 4.6
//////////////////////////////////////////////

using WpfHexEditor.Docking.Core;

namespace WpfHexEditor.Docking.Core.Serialization;

/// <summary>
/// Base DTO for dock nodes.
/// </summary>
public abstract class DockNodeDto
{
    public required string Type { get; set; }
    public Guid Id { get; set; }
    public DockLockMode LockMode { get; set; }
    public double? DockMinWidth { get; set; }
    public double? DockMinHeight { get; set; }
    public double? DockMaxWidth { get; set; }
    public double? DockMaxHeight { get; set; }
}

/// <summary>
/// DTO for <see cref="Nodes.DockSplitNode"/>.
/// </summary>
public class DockSplitNodeDto : DockNodeDto
{
    public SplitOrientation Orientation { get; set; }
    public List<DockNodeDto> Children { get; set; } = [];
    public List<double> Ratios { get; set; } = [];

    /// <summary>
    /// Absolute pixel sizes for non-document panels. Null entries = Star (document host).
    /// When present, these override the proportional Ratios for layout restore, ensuring
    /// side panels keep their exact pixel width/height regardless of window size.
    /// </summary>
    public List<double?>? PixelSizes { get; set; }
}

/// <summary>
/// DTO for <see cref="Nodes.DockGroupNode"/>.
/// </summary>
public class DockGroupNodeDto : DockNodeDto
{
    public List<DockItemDto> Items { get; set; } = [];
    public string? ActiveItemContentId { get; set; }
}

/// <summary>
/// DTO for <see cref="Nodes.DocumentHostNode"/>.
/// </summary>
public class DocumentHostNodeDto : DockGroupNodeDto
{
    public bool IsMain { get; set; }
}

/// <summary>
/// DTO for <see cref="Nodes.DockItem"/>.
/// </summary>
public class DockItemDto
{
    public required string Title { get; set; }
    public required string ContentId { get; set; }
    public bool CanClose { get; set; } = true;
    public bool CanFloat { get; set; } = true;
    public bool IsPinned { get; set; }
    public bool IsDocument { get; set; }
    public DockItemState State { get; set; }
    public DockSide LastDockSide { get; set; } = DockSide.Bottom;
    public double? FloatLeft { get; set; }
    public double? FloatTop { get; set; }
    public double? FloatWidth { get; set; }
    public double? FloatHeight { get; set; }
    public Dictionary<string, string>? Metadata { get; set; }
}

/// <summary>
/// DTO for a single regex colorization rule.
/// </summary>
public class RegexColorRuleDto
{
    public required string Pattern { get; set; }
    /// <summary>Color stored as #AARRGGBB hex string.</summary>
    public required string ColorHex { get; set; }
}

/// <summary>
/// DTO for <see cref="DocumentTabBarSettings"/>.
/// </summary>
public class DocumentTabBarSettingsDto
{
    public DocumentTabPlacement TabPlacement { get; set; } = DocumentTabPlacement.Top;
    public DocumentTabColorMode ColorMode { get; set; } = DocumentTabColorMode.None;
    public bool MultiRowTabs { get; set; }
    public bool MultiRowWithMouseWheel { get; set; } = true;
    public List<RegexColorRuleDto> RegexRules { get; set; } = [];
}

/// <summary>
/// Root DTO for the entire dock layout.
/// </summary>
public class DockLayoutRootDto
{
    public int Version { get; set; } = 2;
    public required DockNodeDto RootNode { get; set; }
    public List<DockItemDto> FloatingItems { get; set; } = [];
    public List<DockItemDto> AutoHideItems { get; set; } = [];
    public List<DockItemDto> HiddenItems { get; set; } = [];

    /// <summary>
    /// Document tab bar settings. Null in old layouts → default settings at runtime.
    /// </summary>
    public DocumentTabBarSettingsDto? TabBarSettings { get; set; }

    /// <summary>
    /// Main window state: 0 = Normal, 1 = Minimized, 2 = Maximized.
    /// </summary>
    public int? WindowState { get; set; }

    /// <summary>
    /// Main window restore bounds (position and size when in Normal state).
    /// These represent the normal-state bounds even when the window is maximized.
    /// </summary>
    public double? WindowLeft { get; set; }
    public double? WindowTop { get; set; }
    public double? WindowWidth { get; set; }
    public double? WindowHeight { get; set; }
}
