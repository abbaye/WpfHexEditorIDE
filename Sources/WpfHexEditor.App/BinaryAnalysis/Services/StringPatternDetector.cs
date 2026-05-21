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

        if (GuidRx().IsMatch(value))        return StringKind.Guid;
        if (EmailRx().IsMatch(value))        return StringKind.Email;
        if (UrlRx().IsMatch(value))          return StringKind.Url;
        if (RegistryKeyRx().IsMatch(value))  return StringKind.RegistryKey;
        if (PathWinRx().IsMatch(value))      return StringKind.PathWin;
        if (PathUnixRx().IsMatch(value))     return StringKind.PathUnix;
        if (IpV4Rx().IsMatch(value))         return StringKind.IpV4;
        if (IpV6Rx().IsMatch(value))         return StringKind.IpV6;
        if (VersionRx().IsMatch(value))      return StringKind.Version;
        if (HexHashRx().IsMatch(value))      return StringKind.HexHash;

        return StringKind.None;
    }
}
