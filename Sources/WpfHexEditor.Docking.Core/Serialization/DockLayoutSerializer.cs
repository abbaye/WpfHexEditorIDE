//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5, Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Text.Json;
using System.Text.Json.Serialization;
using WpfHexEditor.Docking.Core.Nodes;

namespace WpfHexEditor.Docking.Core.Serialization;

/// <summary>
/// Serializes and deserializes <see cref="DockLayoutRoot"/> to/from JSON.
/// </summary>
public static class DockLayoutSerializer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new DockNodeDtoConverter() }
    };

    /// <summary>
    /// Serializes a layout to a JSON string.
    /// </summary>
    public static string Serialize(DockLayoutRoot layout)
    {
        var dto = ToDto(layout);
        return JsonSerializer.Serialize(dto, JsonOptions);
    }

    /// <summary>
    /// Deserializes a layout from a JSON string.
    /// </summary>
    public static DockLayoutRoot Deserialize(string json)
    {
        var dto = JsonSerializer.Deserialize<DockLayoutRootDto>(json, JsonOptions)
                  ?? throw new InvalidOperationException("Failed to deserialize dock layout.");
        return FromDto(dto);
    }

    private static DockLayoutRootDto ToDto(DockLayoutRoot layout)
    {
        return new DockLayoutRootDto
        {
            Version = CurrentVersion,
            RootNode = NodeToDto(layout.RootNode),
            FloatingItems = layout.FloatingItems.Select(ItemToDto).ToList(),
            AutoHideItems = layout.AutoHideItems.Select(ItemToDto).ToList(),
            HiddenItems = layout.HiddenItems.Select(ItemToDto).ToList(),
            WindowState = layout.WindowState,
            WindowLeft = layout.WindowLeft,
            WindowTop = layout.WindowTop,
            WindowWidth = layout.WindowWidth,
            WindowHeight = layout.WindowHeight
        };
    }

    private static DockNodeDto NodeToDto(DockNode node)
    {
        var dto = node switch
        {
            DocumentHostNode docHost => (DockNodeDto)new DocumentHostNodeDto
            {
                Type = "DocumentHost",
                Id = docHost.Id,
                LockMode = docHost.LockMode,
                IsMain = docHost.IsMain,
                Items = docHost.Items.Select(ItemToDto).ToList(),
                ActiveItemContentId = docHost.ActiveItem?.ContentId
            },
            DockGroupNode group => new DockGroupNodeDto
            {
                Type = "Group",
                Id = group.Id,
                LockMode = group.LockMode,
                Items = group.Items.Select(ItemToDto).ToList(),
                ActiveItemContentId = group.ActiveItem?.ContentId
            },
            DockSplitNode split => new DockSplitNodeDto
            {
                Type = "Split",
                Id = split.Id,
                LockMode = split.LockMode,
                Orientation = split.Orientation,
                Children = split.Children.Select(NodeToDto).ToList(),
                Ratios = split.Ratios.ToList(),
                PixelSizes = split.PixelSizes.Any(s => s.HasValue) ? split.PixelSizes.ToList() : null
            },
            _ => throw new NotSupportedException($"Unknown node type: {node.GetType()}")
        };

        // Serialize min/max size constraints (only when set, i.e. non-NaN)
        if (!double.IsNaN(node.DockMinWidth))  dto.DockMinWidth  = node.DockMinWidth;
        if (!double.IsNaN(node.DockMinHeight)) dto.DockMinHeight = node.DockMinHeight;
        if (!double.IsNaN(node.DockMaxWidth))  dto.DockMaxWidth  = node.DockMaxWidth;
        if (!double.IsNaN(node.DockMaxHeight)) dto.DockMaxHeight = node.DockMaxHeight;

        return dto;
    }

    private static DockItemDto ItemToDto(DockItem item)
    {
        return new DockItemDto
        {
            Title = item.Title,
            ContentId = item.ContentId,
            CanClose = item.CanClose,
            CanFloat = item.CanFloat,
            IsPinned = item.IsPinned,
            IsDocument = item.IsDocument,
            State = item.State,
            LastDockSide = item.LastDockSide,
            FloatLeft = item.FloatLeft,
            FloatTop = item.FloatTop,
            FloatWidth = item.FloatWidth,
            FloatHeight = item.FloatHeight,
            Metadata = item.Metadata.Count > 0 ? new(item.Metadata) : null
        };
    }

    /// <summary>
    /// Current serialization format version. Increment when adding breaking changes.
    /// </summary>
    private const int CurrentVersion = 2;

    private static DockLayoutRoot FromDto(DockLayoutRootDto dto)
    {
        if (dto.Version > CurrentVersion)
            throw new NotSupportedException(
                $"Layout version {dto.Version} is not supported. Maximum supported version is {CurrentVersion}. " +
                "Please update the application to load this layout.");

        // v1 → v2: HiddenItems added (defaults to empty list, no migration needed)

        var layout = new DockLayoutRoot();

        // Rebuild the tree from DTO
        var rootNode = NodeFromDto(dto.RootNode, layout.MainDocumentHost);
        layout.RootNode = rootNode;

        foreach (var itemDto in dto.FloatingItems)
        {
            var item = ItemFromDto(itemDto);
            item.State = DockItemState.Float;
            layout.FloatingItems.Add(item);
        }

        foreach (var itemDto in dto.AutoHideItems)
        {
            var item = ItemFromDto(itemDto);
            item.State = DockItemState.AutoHide;
            layout.AutoHideItems.Add(item);
        }

        foreach (var itemDto in dto.HiddenItems)
        {
            var item = ItemFromDto(itemDto);
            item.State = DockItemState.Hidden;
            layout.HiddenItems.Add(item);
        }

        // Restore window state
        layout.WindowState = dto.WindowState;
        layout.WindowLeft = dto.WindowLeft;
        layout.WindowTop = dto.WindowTop;
        layout.WindowWidth = dto.WindowWidth;
        layout.WindowHeight = dto.WindowHeight;

        return layout;
    }

    private static DockNode NodeFromDto(DockNodeDto dto, DocumentHostNode mainHost)
    {
        DockNode result;
        switch (dto)
        {
            case DocumentHostNodeDto docHostDto:
                // Reuse the main host if this is the main document host
                var host = docHostDto.IsMain ? mainHost : new DocumentHostNode { IsMain = false };
                host.LockMode = docHostDto.LockMode;
                foreach (var itemDto in docHostDto.Items)
                    host.AddItem(ItemFromDto(itemDto));
                if (docHostDto.ActiveItemContentId is not null)
                    host.ActiveItem = host.Items.FirstOrDefault(i => i.ContentId == docHostDto.ActiveItemContentId);
                result = host;
                break;

            case DockGroupNodeDto groupDto:
                var group = new DockGroupNode { LockMode = groupDto.LockMode };
                foreach (var itemDto in groupDto.Items)
                    group.AddItem(ItemFromDto(itemDto));
                if (groupDto.ActiveItemContentId is not null)
                    group.ActiveItem = group.Items.FirstOrDefault(i => i.ContentId == groupDto.ActiveItemContentId);
                result = group;
                break;

            case DockSplitNodeDto splitDto:
                var split = new DockSplitNode
                {
                    Orientation = splitDto.Orientation,
                    LockMode = splitDto.LockMode
                };
                for (var i = 0; i < splitDto.Children.Count; i++)
                {
                    var childNode = NodeFromDto(splitDto.Children[i], mainHost);
                    var ratio = i < splitDto.Ratios.Count ? splitDto.Ratios[i] : 0.5;
                    split.AddChild(childNode, ratio);
                }
                // Restore pixel sizes for non-document panels
                if (splitDto.PixelSizes is { Count: > 0 } && splitDto.PixelSizes.Count == split.Children.Count)
                    split.SetPixelSizes(splitDto.PixelSizes.ToArray());
                result = split;
                break;

            default:
                throw new NotSupportedException($"Unknown DTO type: {dto.GetType()}");
        }

        // Restore min/max size constraints
        if (dto.DockMinWidth.HasValue)  result.DockMinWidth  = dto.DockMinWidth.Value;
        if (dto.DockMinHeight.HasValue) result.DockMinHeight = dto.DockMinHeight.Value;
        if (dto.DockMaxWidth.HasValue)  result.DockMaxWidth  = dto.DockMaxWidth.Value;
        if (dto.DockMaxHeight.HasValue) result.DockMaxHeight = dto.DockMaxHeight.Value;

        return result;
    }

    private static DockItem ItemFromDto(DockItemDto dto)
    {
        return new DockItem
        {
            Title = dto.Title,
            ContentId = dto.ContentId,
            CanClose = dto.CanClose,
            CanFloat = dto.CanFloat,
            IsPinned = dto.IsPinned,
            IsDocument = dto.IsDocument,
            State = dto.State,
            LastDockSide = dto.LastDockSide,
            FloatLeft = dto.FloatLeft,
            FloatTop = dto.FloatTop,
            FloatWidth = dto.FloatWidth,
            FloatHeight = dto.FloatHeight,
            Metadata = dto.Metadata is not null ? new(dto.Metadata) : []
        };
    }
}

/// <summary>
/// Custom JSON converter for polymorphic DockNodeDto.
/// </summary>
internal class DockNodeDtoConverter : JsonConverter<DockNodeDto>
{
    public override DockNodeDto? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;

        var type = root.GetProperty("type").GetString();

        var json = root.GetRawText();

        // Inner options must include this converter for recursive Children in DockSplitNodeDto
        var innerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { this }
        };

        return type switch
        {
            "Split" => JsonSerializer.Deserialize<DockSplitNodeDto>(json, innerOptions),
            "DocumentHost" => JsonSerializer.Deserialize<DocumentHostNodeDto>(json, innerOptions),
            "Group" => JsonSerializer.Deserialize<DockGroupNodeDto>(json, innerOptions),
            _ => throw new NotSupportedException($"Unknown node type: {type}")
        };
    }

    public override void Write(Utf8JsonWriter writer, DockNodeDto value, JsonSerializerOptions options)
    {
        // Inner options must include this converter for recursive Children in DockSplitNodeDto
        var innerOptions = new JsonSerializerOptions
        {
            WriteIndented = options.WriteIndented,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { this }
        };

        switch (value)
        {
            case DockSplitNodeDto splitDto:
                JsonSerializer.Serialize(writer, splitDto, innerOptions);
                break;
            case DocumentHostNodeDto docHostDto:
                JsonSerializer.Serialize(writer, docHostDto, innerOptions);
                break;
            case DockGroupNodeDto groupDto:
                JsonSerializer.Serialize(writer, groupDto, innerOptions);
                break;
            default:
                throw new NotSupportedException($"Unknown DTO type: {value.GetType()}");
        }
    }
}
