namespace WpfHexEditor.Core.RomHacking
{
    /// <summary>
    /// Supported binary patch formats.
    /// </summary>
    public enum PatchFormat
    {
        /// <summary>IPS — International Patching System (offset + data records, 16 MB limit)</summary>
        IPS,

        /// <summary>BPS — Beat Patch System by Near/byuu (VLQ actions, CRC32 verification)</summary>
        BPS,

        /// <summary>xdelta — VCDIFF RFC 3284 delta encoding (no size limit, better compression)</summary>
        XDelta
    }
}
