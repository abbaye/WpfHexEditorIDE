// ==========================================================
// Project: WpfHexEditor.Core
// File: ByteDifference.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude (Anthropic)
// Created: 2026-03-06
// Description:
//     Represents a byte-level difference between two binary streams,
//     storing the original and destination byte values alongside the
//     stream offset. Used by the file comparison and diff services.
//
// Architecture Notes:
//     Implements ICloneable for safe copy operations. Carries a WPF
//     SolidColorBrush for UI highlighting; keep this in mind if
//     future decoupling of the domain model from WPF is desired.
//
// ==========================================================

using System;
using System.Windows.Media;

namespace WpfHexEditor.Core.Bytes
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
