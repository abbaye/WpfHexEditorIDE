//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

namespace WpfHexEditor.Docking.Core.Serialization;

/// <summary>
/// Base DTO for dock nodes.
/// </summary>
public abstract class DockNodeDto
{
    public required string Type { get; set; }
    public Guid Id { get; set; }
    public DockLockMode LockMode { get; set; }
}

/// <summary>
/// DTO for <see cref="Nodes.DockSplitNode"/>.
/// </summary>
public class DockSplitNodeDto : DockNodeDto
{
    public SplitOrientation Orientation { get; set; }
    public List<DockNodeDto> Children { get; set; } = [];
    public List<double> Ratios { get; set; } = [];
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
    public DockItemState State { get; set; }
    public double? FloatLeft { get; set; }
    public double? FloatTop { get; set; }
}

/// <summary>
/// Root DTO for the entire dock layout.
/// </summary>
public class DockLayoutRootDto
{
    public int Version { get; set; } = 1;
    public required DockNodeDto RootNode { get; set; }
    public List<DockItemDto> FloatingItems { get; set; } = [];
    public List<DockItemDto> AutoHideItems { get; set; } = [];
}
