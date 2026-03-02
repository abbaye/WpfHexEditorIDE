//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

//////////////////////////////////////////////
// Apache 2.0  - 2026
// Interface IDataInspectorPanel
// Decouples HexEditor Core from the concrete WindowPanels implementation
//////////////////////////////////////////////

namespace WpfHexEditor.Core.Interfaces
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
