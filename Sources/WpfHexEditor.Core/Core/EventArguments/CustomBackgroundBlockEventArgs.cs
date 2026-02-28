//////////////////////////////////////////////
// Apache 2.0  - 2021
// Modified by : Derek Tremblay (derektremblay666@gmail.com)
//////////////////////////////////////////////

using System;

namespace WpfHexaEditor.Core.EventArguments
{
    /// <summary>
    /// Custom event arguments used for pass somes informations to delegate
    /// </summary>
    public class CustomBackgroundBlockEventArgs : EventArgs
    {
        public CustomBackgroundBlockEventArgs() { }

        public CustomBackgroundBlockEventArgs(CustomBackgroundBlock customBlock) => CustomBlock = customBlock;

        /// <summary>
        /// CustomBackgroundBlock to pass in arguments
        /// /// </summary>
        public CustomBackgroundBlock CustomBlock;
    }
}
