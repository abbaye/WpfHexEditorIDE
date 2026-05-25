// Project     : WpfHexEditor.App
// File        : StringPatternDetector.cs
// Description : Classifies a StringRun value into a StringKind using compiled regexes.
//               First-match wins; order encodes priority (more specific patterns first).
// Architecture: Stateless static service; called in post-pass after StringExtractor.Extract.

using System.Text.RegularExpressions;

namespace WpfHexEditor.App.BinaryAnalysis.Services;

public enum StringKind
{
    None,
    Email,
    Url,
    PathWin,
    PathUnix,
    Guid,
    RegistryKey,
    Version,
    IpV6,
    IpV4,
    HexHash,
}

internal static partial class StringPatternDetector
{
    [GeneratedRegex(@"^[a-zA-Z0-9._%+\-]+@[a-zA-Z0-9.\-]+\.[a-zA-Z]{2,}$", RegexOptions.None)]
    private static partial Regex EmailRx();

    [GeneratedRegex(@"^(https?|ftp|file)://[^\s]+$", RegexOptions.IgnoreCase)]
    private static partial Regex UrlRx();

    [GeneratedRegex(@"^[a-zA-Z]:\\[^\x00-\x1F""<>|?*]+", RegexOptions.None)]
    private static partial Regex PathWinRx();

    [GeneratedRegex(@"^(/[^\x00-\x1F""<>|?* ]+){2,}$", RegexOptions.None)]
    private static partial Regex PathUnixRx();

    [GeneratedRegex(@"^\{?[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\}?$", RegexOptions.None)]
    private static partial Regex GuidRx();

    [GeneratedRegex(@"^HKEY_(LOCAL_MACHINE|CURRENT_USER|CLASSES_ROOT|USERS|CURRENT_CONFIG)(\\[^\x00-\x1F]+)*$", RegexOptions.None)]
    private static partial Regex RegistryKeyRx();

    [GeneratedRegex(@"^\d{1,5}(\.\d{1,5}){1,3}$", RegexOptions.None)]
    private static partial Regex VersionRx();

    // IPv6: simplified — at least two colon groups
    [GeneratedRegex(@"^[0-9a-fA-F]{1,4}(:[0-9a-fA-F]{0,4}){2,7}$", RegexOptions.None)]
    private static partial Regex IpV6Rx();

    [GeneratedRegex(@"^((25[0-5]|2[0-4]\d|[01]?\d\d?)\.){3}(25[0-5]|2[0-4]\d|[01]?\d\d?)$", RegexOptions.None)]
    private static partial Regex IpV4Rx();

    // Hex hash: 32, 40, 56, 64, 96, or 128 hex chars (MD5/SHA-1/SHA-224/SHA-256/SHA-384/SHA-512)
    [GeneratedRegex(@"^[0-9a-fA-F]{32}$|^[0-9a-fA-F]{40}$|^[0-9a-fA-F]{56}$|^[0-9a-fA-F]{64}$|^[0-9a-fA-F]{96}$|^[0-9a-fA-F]{128}$", RegexOptions.None)]
    private static partial Regex HexHashRx();

    public static StringKind Detect(string value)
    {
        if (string.IsNullOrEmpty(value)) return StringKind.None;

        int len = value.Length;

        if ((len == 36 || len == 38) && value.Contains('-'))
            if (GuidRx().IsMatch(value)) return StringKind.Guid;

        if (value.Contains('@') && value.Contains('.'))
            if (EmailRx().IsMatch(value)) return StringKind.Email;

        if (len > 7 && (value[0] == 'h' || value[0] == 'f') && value.Contains("://"))
            if (UrlRx().IsMatch(value)) return StringKind.Url;

        if (len > 5 && value[0] == 'H' && value.StartsWith("HKEY_", StringComparison.Ordinal))
            if (RegistryKeyRx().IsMatch(value)) return StringKind.RegistryKey;

        if (len > 3 && value[1] == ':' && value[2] == '\\')
            if (PathWinRx().IsMatch(value)) return StringKind.PathWin;

        if (len > 2 && value[0] == '/' && value[1] != ' ')
            if (PathUnixRx().IsMatch(value)) return StringKind.PathUnix;

        bool hasDot   = value.Contains('.');
        bool hasColon = value.Contains(':');
        if (hasDot)
        {
            if (IpV4Rx().IsMatch(value))    return StringKind.IpV4;
            if (VersionRx().IsMatch(value)) return StringKind.Version;
        }
        if (hasColon && !hasDot)
            if (IpV6Rx().IsMatch(value)) return StringKind.IpV6;

        if (len == 32 || len == 40 || len == 56 || len == 64 || len == 96 || len == 128)
            if (HexHashRx().IsMatch(value)) return StringKind.HexHash;

        return StringKind.None;
    }
}
