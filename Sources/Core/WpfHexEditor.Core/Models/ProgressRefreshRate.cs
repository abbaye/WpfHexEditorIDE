//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5, Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.Core.Models
{
    /// <summary>
    /// Progress bar refresh rate for long-running operations
    /// </summary>
    public enum ProgressRefreshRate
    {
        /// <summary>
        /// Slow refresh (100ms interval = 10 updates/second)
        /// Best for older hardware or to minimize CPU usage
        /// </summary>
        Slow = 100,

        /// <summary>
        /// Medium refresh (50ms interval = 20 updates/second)
        /// Balanced between smoothness and performance
        /// </summary>
        Medium = 50,

        /// <summary>
        /// Fast refresh (33ms interval = ~30 updates/second)
        /// Smooth animation, recommended default
        /// </summary>
        Fast = 33,

        /// <summary>
        /// Very fast refresh (16ms interval = ~60 updates/second)
        /// Very smooth animation, higher CPU usage
        /// </summary>
        VeryFast = 16
    }
}
