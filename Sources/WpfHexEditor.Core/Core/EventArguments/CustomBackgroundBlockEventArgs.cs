//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

//////////////////////////////////////////////
// Apache 2.0  - 2021
// Modified by : Derek Tremblay (derektremblay666@gmail.com)
//////////////////////////////////////////////

using System;

namespace WpfHexEditor.Core.EventArguments
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
