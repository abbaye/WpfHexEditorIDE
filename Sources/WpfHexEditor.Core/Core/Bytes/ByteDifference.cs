//////////////////////////////////////////////
// Apache 2.0 - 2021
// Author : Derek Tremblay (derektremblay666@gmail.com)
//////////////////////////////////////////////

using System;
using System.Windows.Media;

namespace WpfHexaEditor.Core.Bytes
{
    /// <summary>
    /// Used to track a byte difference in a stream vs another stream
    /// </summary>
    public class ByteDifference : ICloneable
    {
        public byte Origine { get; set; }
        public byte Destination { get; set; }
        public long BytePositionInStream { get; set; } = -1;

        public SolidColorBrush Color { get; set; } = Brushes.Transparent;

        public ByteDifference() { }

        public ByteDifference(byte origine, byte destination, long bytePositionInStream)
        {
            Origine = origine;
            Destination = destination;
            BytePositionInStream = bytePositionInStream;
        }

        public ByteDifference(byte origine, byte destination, long bytePositionInStream, SolidColorBrush color) :
            this(origine, destination, bytePositionInStream) => Color = color;

        #region Substitution
        public override bool Equals(object obj) =>
            obj is ByteDifference difference &&
                   Origine == difference.Origine &&
                   Destination == difference.Destination &&
                   BytePositionInStream == difference.BytePositionInStream;

        public override int GetHashCode() => base.GetHashCode();

        public override string ToString() => base.ToString();
        #endregion

        #region Methods
        /// <summary>
        /// Get clone of this CustomBackgroundBlock
        /// </summary>
        public object Clone() => MemberwiseClone();

        /// <summary>
        /// Set a random brush to this instance
        /// </summary>
        public void SetRandomColor() => Color = RandomBrushes.PickBrush();
        #endregion
    }
}
