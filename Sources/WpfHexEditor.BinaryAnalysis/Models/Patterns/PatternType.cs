//////////////////////////////////////////////
// Apache 2.0  - 2026
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

namespace WpfHexEditor.BinaryAnalysis.Models.Patterns
{
    /// <summary>
    /// Types of patterns that can be detected in binary data
    /// </summary>
    public enum PatternType
    {
        /// <summary>
        /// Repeated byte sequence (same bytes occurring multiple times)
        /// </summary>
        RepeatedSequence,

        /// <summary>
        /// Embedded file detected via magic bytes signature
        /// </summary>
        EmbeddedFile,

        /// <summary>
        /// Null byte padding (00 00 00...)
        /// </summary>
        NullPadding,

        /// <summary>
        /// xFF padding (FF FF FF...)
        /// </summary>
        FFPadding,

        /// <summary>
        /// ASCII text string
        /// </summary>
        AsciiString,

        /// <summary>
        /// Unicode (UTF-16) text string
        /// </summary>
        UnicodeString,

        /// <summary>
        /// Repeating pattern (ABAB... or 01020102...)
        /// </summary>
        RepeatingPattern,

        /// <summary>
        /// Data corruption or anomaly detected
        /// </summary>
        Corruption,

        /// <summary>
        /// Entropy spike (sudden change in randomness)
        /// </summary>
        EntropyAnomaly,

        /// <summary>
        /// Checksum or hash value
        /// </summary>
        ChecksumHash,

        /// <summary>
        /// Compressed data (high entropy, specific patterns)
        /// </summary>
        CompressedData,

        /// <summary>
        /// Encrypted data (high uniform entropy)
        /// </summary>
        EncryptedData,

        /// <summary>
        /// Alignment boundary (data aligned to 16/32/64 byte boundaries)
        /// </summary>
        AlignmentBoundary,

        /// <summary>
        /// Bitmap or image data
        /// </summary>
        ImageData,

        /// <summary>
        /// Executable code section
        /// </summary>
        ExecutableCode,

        /// <summary>
        /// Custom user-defined pattern
        /// </summary>
        Custom
    }
}
