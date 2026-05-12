// ==========================================================
// Project: WpfHexEditor.App
// File: Services/StructureHexEventBridge.cs
// Description:
//     Bridges StructureHexSyncService.FieldEdited events to the global
//     IIDEEventBus by publishing a StructureFieldEditedEvent. Any
//     HexEditor instance subscribed to that event can update its
//     ByteProvider in response.
//
// Architecture: lightweight pure-code service — instantiate once per
//                 StructureEditor session and call Attach(sync, binaryPath).
// ==========================================================

using WpfHexEditor.Core.Events;
using WpfHexEditor.Core.Events.IDEEvents;
using WpfHexEditor.Editor.StructureEditor.Services;

namespace WpfHexEditor.App.Services;

/// <summary>Forwards structure-field edits from a sync service to the global event bus.</summary>
public sealed class StructureHexEventBridge
{
    private readonly IIDEEventBus _bus;

    public StructureHexEventBridge(IIDEEventBus bus) => _bus = bus;

    /// <summary>
    /// Subscribes to <paramref name="sync"/>'s FieldEdited events and republishes
    /// them on the bus tagged with the active <paramref name="binaryFilePath"/>.
    /// </summary>
    public void Attach(IStructureHexSyncService sync, string binaryFilePath)
    {
        sync.FieldEdited += (_, e) =>
        {
            _bus.Publish(new StructureFieldEditedEvent
            {
                BinaryFilePath = binaryFilePath,
                Offset         = e.Offset,
                NewBytes       = e.NewBytes,
                FieldName      = e.FieldName,
            });
        };
    }
}
