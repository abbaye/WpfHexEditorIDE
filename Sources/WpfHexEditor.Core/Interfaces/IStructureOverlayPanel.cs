//////////////////////////////////////////////
// Apache 2.0  - 2026
// Interface IStructureOverlayPanel
// Decouples HexEditor Core from the concrete WindowPanels implementation
//////////////////////////////////////////////

using System;
using Newtonsoft.Json.Linq;
using WpfHexEditor.Core.Models.StructureOverlay;

namespace WpfHexEditor.Core.Interfaces
{
    /// <summary>
    /// Contract for a panel that overlays structure information on the hex view.
    /// </summary>
    public interface IStructureOverlayPanel
    {
        event EventHandler<OverlayStructure> OnOverlayAdded;
        event EventHandler OnAllOverlaysCleared;
        event EventHandler<OverlayField> OnFieldSelectedForHighlight;
        event EventHandler<OverlayStructure> OnStructureSelectedForHighlight;

        void UpdateFileBytes(byte[] bytes);
        void AddOverlayFromFormat(JObject format);
    }
}
