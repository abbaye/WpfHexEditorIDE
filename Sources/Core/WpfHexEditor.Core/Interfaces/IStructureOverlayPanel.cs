//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Interface IStructureOverlayPanel
// Decouples HexEditor Core from the concrete WindowPanels implementation
//////////////////////////////////////////////

using System;
using System.Text.Json.Nodes;
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
        void AddOverlayFromFormat(JsonObject format);
    }
}
