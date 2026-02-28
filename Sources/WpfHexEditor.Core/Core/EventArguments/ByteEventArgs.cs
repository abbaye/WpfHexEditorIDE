//////////////////////////////////////////////
// Apache 2.0  - 2020-2021
// Author      : ehsan69h
// Modified by : Derek Tremblay (derektremblay666@gmail.com)
//////////////////////////////////////////////

using System;

namespace WpfHexaEditor.Core.EventArguments
{
    /// <summary>
    /// Custom event arguments used for pass somes informations to delegate
    /// </summary>
    public class ByteEventArgs : EventArgs
    {
        #region Constructors
        public ByteEventArgs() { }

        public ByteEventArgs(long position) => BytePositionInStream = position;

        public ByteEventArgs(long position, int index)
        {
            BytePositionInStream = position;
            Index = index;
        }
        #endregion

        #region Properties
        /// <summary>
        /// Pass the position of byte 
        /// </summary>
        public long BytePositionInStream { get; set; }

        /// <summary>
        /// Pass index if byte using with BytePositionInStream in somes situations 
        /// </summary>
        public int Index { get; set; }
        #endregion
    }
}
