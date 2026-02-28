//////////////////////////////////////////////
// Apache 2.0  - 2018-2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using System;
using System.Windows.Media;
using WpfHexEditor.Core.Bytes;

namespace WpfHexEditor.Core
{
    /// <summary>
    /// Used to create block of custom colors background
    /// Supports equality comparison, brush caching, and validation
    /// </summary>
    public class CustomBackgroundBlock : ICloneable, IEquatable<CustomBackgroundBlock>
    {
        #region Private Fields

        private SolidColorBrush _color = Brushes.Transparent;
        private SolidColorBrush _cachedTransparentBrush;
        private bool _brushCacheValid = false;
        private double _opacity = 0.3;

        #endregion

        #region Constructors

        /// <summary>
        /// Default constructor
        /// </summary>
        public CustomBackgroundBlock() { }

        /// <summary>
        /// Full constructor with all properties
        /// </summary>
        /// <param name="start">Start offset in bytes</param>
        /// <param name="length">Length in bytes</param>
        /// <param name="color">Background color brush</param>
        /// <param name="description">Optional description</param>
        /// <param name="opacity">Optional opacity (0.0-1.0, default 0.3)</param>
        public CustomBackgroundBlock(long start, long length, SolidColorBrush color, string description = "", double opacity = 0.3)
        {
            StartOffset = start;
            Length = length;
            Color = color;
            Description = description;
            Opacity = opacity;
        }

        /// <summary>
        /// Constructor with random brush
        /// </summary>
        /// <param name="start">Start offset in bytes</param>
        /// <param name="length">Length in bytes</param>
        /// <param name="setRandomBrush">If true, sets a random color</param>
        /// <param name="opacity">Optional opacity (0.0-1.0, default 0.3)</param>
        public CustomBackgroundBlock(long start, long length, bool setRandomBrush = true, double opacity = 0.3)
        {
            StartOffset = start;
            Length = length;
            Opacity = opacity;
            if (setRandomBrush) Color = RandomBrushes.PickBrush();
        }

        #endregion

        #region Properties

        /// <summary>
        /// Get or set the start offset
        /// </summary>
        public long StartOffset { get; set; }

        /// <summary>
        /// Get the stop offset (exclusive)
        /// </summary>
        public long StopOffset => StartOffset + Length;

        /// <summary>
        /// Get or set the length of background block
        /// </summary>
        public long Length { get; set; }

        /// <summary>
        /// Description of background block
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Get or set the color used in the visual
        /// Setting this property invalidates the cached brush
        /// </summary>
        public SolidColorBrush Color
        {
            get => _color;
            set
            {
                _color = value;
                InvalidateBrushCache();
            }
        }

        /// <summary>
        /// Get or set the opacity of the background block (0.0 to 1.0)
        /// Default: 0.3 (30% transparent)
        /// Setting this property invalidates the cached brush
        /// </summary>
        public double Opacity
        {
            get => _opacity;
            set
            {
                _opacity = Math.Max(0.0, Math.Min(1.0, value)); // Clamp to [0.0, 1.0]
                InvalidateBrushCache();
            }
        }

        /// <summary>
        /// Get or set the foreground color for text within this block
        /// Currently not used by rendering engine, reserved for future enhancement
        /// </summary>
        public SolidColorBrush ForegroundColor { get; set; } = null;

        #endregion

        #region Validation Properties

        /// <summary>
        /// Check if this block has valid parameters
        /// Returns false if StartOffset is negative, Length is zero or negative, or Color is null
        /// </summary>
        public bool IsValid => StartOffset >= 0 && Length > 0 && Color != null;

        #endregion

        #region Helper Methods

        /// <summary>
        /// Check if this block contains a specific position
        /// </summary>
        /// <param name="position">Position to check</param>
        /// <returns>True if position is within [StartOffset, StopOffset)</returns>
        public bool ContainsPosition(long position)
        {
            return position >= StartOffset && position < StopOffset;
        }

        /// <summary>
        /// Check if this block overlaps with another range
        /// </summary>
        /// <param name="start">Start position of range</param>
        /// <param name="length">Length of range</param>
        /// <returns>True if ranges overlap</returns>
        public bool Overlaps(long start, long length)
        {
            long end = start + length;
            return StartOffset < end && StopOffset > start;
        }

        /// <summary>
        /// Get the intersection of this block with a range
        /// </summary>
        /// <param name="rangeStart">Start position of range</param>
        /// <param name="rangeLength">Length of range</param>
        /// <returns>Tuple of (intersectionStart, intersectionLength), or null if no overlap</returns>
        public (long start, long length)? GetIntersection(long rangeStart, long rangeLength)
        {
            long rangeEnd = rangeStart + rangeLength;

            if (StartOffset >= rangeEnd || StopOffset <= rangeStart)
                return null; // No overlap

            long intersectStart = Math.Max(StartOffset, rangeStart);
            long intersectEnd = Math.Min(StopOffset, rangeEnd);

            return (intersectStart, intersectEnd - intersectStart);
        }

        #endregion

        #region Brush Caching

        /// <summary>
        /// Get a frozen brush with the configured opacity
        /// Performance optimization: Cached and reused until Color or Opacity changes
        /// </summary>
        public SolidColorBrush GetTransparentBrush()
        {
            // Return cached brush if valid
            if (_brushCacheValid && _cachedTransparentBrush != null)
                return _cachedTransparentBrush;

            // Create new brush with opacity
            if (Color != null)
            {
                _cachedTransparentBrush = Color.Clone();
                _cachedTransparentBrush.Opacity = Opacity;

                // Freeze for performance (WPF pattern from HexViewport)
                if (_cachedTransparentBrush.CanFreeze)
                    _cachedTransparentBrush.Freeze();

                _brushCacheValid = true;
            }
            else
            {
                _cachedTransparentBrush = Brushes.Transparent;
                _brushCacheValid = true;
            }

            return _cachedTransparentBrush;
        }

        /// <summary>
        /// Invalidate cached brush when Color or Opacity changes
        /// Must be called by property setters
        /// </summary>
        public void InvalidateBrushCache()
        {
            _brushCacheValid = false;
            _cachedTransparentBrush = null;
        }

        #endregion

        #region ICloneable Implementation

        /// <summary>
        /// Get clone of this CustomBackgroundBlock
        /// Note: Performs shallow copy (brush references are copied, not cloned)
        /// </summary>
        public object Clone() => MemberwiseClone();

        #endregion

        #region IEquatable Implementation

        /// <summary>
        /// Determines whether the specified CustomBackgroundBlock is equal to the current one
        /// </summary>
        public bool Equals(CustomBackgroundBlock other)
        {
            if (other == null) return false;
            if (ReferenceEquals(this, other)) return true;

            return StartOffset == other.StartOffset &&
                   Length == other.Length &&
                   Equals(Color, other.Color) &&
                   Description == other.Description &&
                   Math.Abs(Opacity - other.Opacity) < 0.001; // Float comparison with tolerance
        }

        /// <summary>
        /// Determines whether the specified object is equal to the current CustomBackgroundBlock
        /// </summary>
        public override bool Equals(object obj) => Equals(obj as CustomBackgroundBlock);

        /// <summary>
        /// Serves as the default hash function
        /// </summary>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + StartOffset.GetHashCode();
                hash = hash * 31 + Length.GetHashCode();
                hash = hash * 31 + (Color?.GetHashCode() ?? 0);
                hash = hash * 31 + (Description?.GetHashCode() ?? 0);
                hash = hash * 31 + Opacity.GetHashCode();
                return hash;
            }
        }

        /// <summary>
        /// Returns a string representation of this CustomBackgroundBlock for debugging
        /// </summary>
        public override string ToString()
        {
            var colorName = Color != null ?
                (Color == Brushes.Transparent ? "Transparent" :
                 Color.Color.ToString()) :
                "null";

            return $"CustomBackgroundBlock[0x{StartOffset:X8}-0x{StopOffset:X8}, Len={Length}, Color={colorName}, Opacity={Opacity:F2}, Desc=\"{Description ?? ""}\"]";
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// Set a random brush to this instance
        /// </summary>
        public void SetRandomColor() => Color = RandomBrushes.PickBrush();

        #endregion
    }
}
