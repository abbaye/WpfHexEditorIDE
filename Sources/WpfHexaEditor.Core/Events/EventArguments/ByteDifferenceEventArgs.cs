//////////////////////////////////////////////
// Apache 2.0  - 2021
// Modified by : Derek Tremblay (derektremblay666@gmail.com)
//////////////////////////////////////////////

using System;
using WpfHexaEditor.Core.Bytes;

namespace WpfHexaEditor.Core.EventArguments
{
    /// <summary>
    /// Custom event arguments used for pass somes informations to delegate
    /// </summary>
    public class ByteDifferenceEventArgs : EventArgs
    {
        public ByteDifferenceEventArgs() { }

        public ByteDifferenceEventArgs(ByteDifference byteDifference) => ByteDiff = byteDifference;

        /// <summary>
        /// ByteDifference to pass in arguments
        /// /// </summary>
        public ByteDifference ByteDiff;
    }
}
