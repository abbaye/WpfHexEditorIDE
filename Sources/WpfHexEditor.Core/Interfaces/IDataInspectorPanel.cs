//////////////////////////////////////////////
// Apache 2.0  - 2026
// Interface IDataInspectorPanel
// Decouples HexEditor Core from the concrete WindowPanels implementation
//////////////////////////////////////////////

namespace WpfHexaEditor.Interfaces
{
    /// <summary>
    /// Contract for a panel that interprets bytes in multiple formats.
    /// </summary>
    public interface IDataInspectorPanel
    {
        void UpdateBytes(byte[] bytes);
        void Clear();
    }
}
