//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.IO;
using System.Security.Cryptography;
using WpfHexEditor.SDK.Models;

namespace WpfHexEditor.PluginHost;

/// <summary>Result of manifest validation.</summary>
internal sealed class ValidationResult
{
    public bool IsValid => Errors.Count == 0;
    public List<string> Errors { get; } = [];
    public List<string> Warnings { get; } = [];
}

/// <summary>
/// Validates a <see cref="PluginManifest"/> for required fields, version compatibility,
/// and optionally assembly hash integrity.
/// </summary>
internal sealed class PluginManifestValidator
{
    private readonly Version _ideVersion;
    private readonly Version _sdkVersion;

    public PluginManifestValidator(Version ideVersion, Version sdkVersion)
    {
        _ideVersion = ideVersion;
        _sdkVersion = sdkVersion;
    }

    /// <summary>
    /// Validates the manifest. Returns a <see cref="ValidationResult"/> with all errors found.
    /// </summary>
    /// <param name="manifest">Manifest to validate.</param>
    /// <param name="pluginDirectory">Directory containing the plugin DLL (for hash verification).</param>
    public ValidationResult Validate(PluginManifest manifest, string pluginDirectory)
    {
        var result = new ValidationResult();

        ValidateRequiredFields(manifest, result);
        ValidateVersionConstraints(manifest, result);
        ValidateAssemblyHash(manifest, pluginDirectory, result);
        ValidateSignature(manifest, pluginDirectory, result);

        return result;
    }

    private static void ValidateRequiredFields(PluginManifest manifest, ValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(manifest.Id))
            result.Errors.Add("Manifest is missing required field: 'id'.");

        if (string.IsNullOrWhiteSpace(manifest.Name))
            result.Errors.Add("Manifest is missing required field: 'name'.");

        if (string.IsNullOrWhiteSpace(manifest.Version))
            result.Errors.Add("Manifest is missing required field: 'version'.");

        if (string.IsNullOrWhiteSpace(manifest.EntryPoint))
            result.Errors.Add("Manifest is missing required field: 'entryPoint'.");

        if (string.IsNullOrWhiteSpace(manifest.SdkVersion))
            result.Errors.Add("Manifest is missing required field: 'sdkVersion'.");

        // Validate SemVer parsability
        if (!string.IsNullOrWhiteSpace(manifest.Version) && !TryParseVersion(manifest.Version, out _))
            result.Errors.Add($"Manifest 'version' is not a valid SemVer string: '{manifest.Version}'.");

        if (!string.IsNullOrWhiteSpace(manifest.SdkVersion) && !TryParseVersion(manifest.SdkVersion, out _))
            result.Errors.Add($"Manifest 'sdkVersion' is not a valid SemVer string: '{manifest.SdkVersion}'.");
    }

    private void ValidateVersionConstraints(PluginManifest manifest, ValidationResult result)
    {
        // minIDEVersion
        if (!string.IsNullOrWhiteSpace(manifest.MinIDEVersion))
        {
            if (TryParseVersion(manifest.MinIDEVersion, out var minIde) && _ideVersion < minIde)
                result.Errors.Add($"Plugin requires IDE version >= {manifest.MinIDEVersion} (current: {_ideVersion}).");
        }

        // minSDKVersion / maxSDKVersion
        if (!string.IsNullOrWhiteSpace(manifest.MinSDKVersion))
        {
            if (TryParseVersion(manifest.MinSDKVersion, out var minSdk) && _sdkVersion < minSdk)
                result.Errors.Add($"Plugin requires SDK version >= {manifest.MinSDKVersion} (current: {_sdkVersion}).");
        }

        if (!string.IsNullOrWhiteSpace(manifest.MaxSDKVersion))
        {
            if (TryParseVersion(manifest.MaxSDKVersion, out var maxSdk) && _sdkVersion > maxSdk)
                result.Errors.Add($"Plugin requires SDK version <= {manifest.MaxSDKVersion} (current: {_sdkVersion}).");
        }
    }

    private static void ValidateAssemblyHash(PluginManifest manifest, string pluginDirectory, ValidationResult result)
    {
        // Only Distribution Manifests have assembly.sha256 —" skip for Build Manifests.
        if (manifest.Assembly?.Sha256 is not { Length: > 0 } expectedHash)
            return;

        string? dllFileName = manifest.Assembly?.File;
        if (string.IsNullOrWhiteSpace(dllFileName))
        {
            result.Warnings.Add("Distribution manifest has 'sha256' but missing 'assembly.file' - hash not verified.");
            return;
        }

        string dllPath = Path.Combine(pluginDirectory, dllFileName);
        if (!File.Exists(dllPath))
        {
            result.Errors.Add($"Assembly file not found: '{dllPath}'.");
            return;
        }

        string actualHash = ComputeSha256(dllPath);
        if (!string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase))
        {
            string prefix = manifest.Signature?.IsSigned == true ? "[SECURITY] " : string.Empty;
            result.Errors.Add($"{prefix}Assembly hash mismatch for '{dllFileName}'. Expected: {expectedHash}. Actual: {actualHash}.");
        }
    }

    /// <summary>
    /// Validates the structural presence of the signature file when a plugin declares itself as signed.
    /// Full cryptographic RSA verification is deferred pending key-distribution strategy (ADR-SB-02).
    /// </summary>
    private static void ValidateSignature(PluginManifest manifest, string pluginDirectory, ValidationResult result)
    {
        var sig = manifest.Signature;
        if (sig is null || !sig.IsSigned) return;

        if (string.IsNullOrWhiteSpace(sig.SignatureFile))
        {
            result.Warnings.Add("Plugin declares IsSigned=true but 'signatureFile' is empty.");
            return;
        }

        var sigPath = Path.Combine(pluginDirectory, sig.SignatureFile);
        if (!File.Exists(sigPath))
        {
            result.Errors.Add($"Signature file not found: '{sigPath}'.");
            return;
        }

        var bytes = File.ReadAllBytes(sigPath);
        if (bytes.Length < 8)
            result.Errors.Add($"Signature file '{sig.SignatureFile}' is malformed (too small).");
    }

    private static string ComputeSha256(string filePath)
    {
        using var sha = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        byte[] hash = sha.ComputeHash(stream);
        return Convert.ToHexString(hash);
    }

    private static bool TryParseVersion(string versionString, out Version version)
    {
        // Strip pre-release suffix for Version.Parse compatibility
        int dashIndex = versionString.IndexOf('-');
        string clean = dashIndex >= 0 ? versionString[..dashIndex] : versionString;
        return Version.TryParse(clean, out version!);
    }
}
