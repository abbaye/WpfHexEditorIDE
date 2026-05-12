// ==========================================================
// Project: whfmt.Validate
// File: Engine/ValidationEngine.cs
// Description: Orchestrates format detection, magic-byte verification, checksum
//              execution, assertion evaluation, and forensic pattern matching
//              against a file using whfmt.FileFormatCatalog.
// ==========================================================

using System.Text.Json;
using WpfHexEditor.Core.Definitions;
using WpfHexEditor.Core.Definitions.Matching;
using WpfHexEditor.Core.Contracts;

namespace WhfmtValidate.Engine;

internal sealed class ValidationEngine
{
    private const int HeaderSize = 512;

    internal ValidationReport Validate(string filePath, string? forcedFormat)
    {
        var catalog = EmbeddedFormatCatalog.Instance;
        var fi      = new FileInfo(filePath);

        if (!fi.Exists)
            return ValidationReport.NotFound(filePath);

        byte[] data = File.ReadAllBytes(filePath);
        byte[] header = data.Length > HeaderSize ? data[..HeaderSize] : data;

        // ── Format detection ────────────────────────────────────────────────
        EmbeddedFormatEntry? entry = null;

        if (forcedFormat is not null)
        {
            entry = catalog.GetAll()
                .FirstOrDefault(e => e.Name.Equals(forcedFormat, StringComparison.OrdinalIgnoreCase)
                                  || e.Extensions.Any(x => x.Equals(forcedFormat, StringComparison.OrdinalIgnoreCase)));
        }

        FormatMatchResult? match = null;
        if (entry is null)
        {
            match = FormatFileAnalyzer.Analyze(catalog, filePath, HeaderSize);
            if (match is not null)
                entry = match.Entry;
        }

        var report = new ValidationReport
        {
            FilePath       = fi.FullName,
            FileName       = fi.Name,
            FileSize       = fi.Length,
            FormatName     = entry?.Name ?? "Unknown",
            FormatCategory = entry?.Category ?? "-",
            Confidence     = match?.Confidence ?? (entry is not null ? 1.0 : 0.0),
            MatchSource    = match?.Source.ToString() ?? (entry is not null ? "Forced" : "None"),
        };

        if (entry is null)
        {
            report.Issues.Add(new ValidationIssue
            {
                Severity = "warning",
                Category = "Detection",
                Name     = "UnknownFormat",
                Message  = "No matching format found in catalog. Structural validation skipped."
            });
            return report;
        }

        // ── Load full format definition ──────────────────────────────────────
        string? json = catalog.GetJson(entry.ResourceKey);
        if (json is null)
        {
            report.Issues.Add(new ValidationIssue { Severity = "info", Category = "Catalog", Name = "NoDefinition", Message = "Format matched but no full definition available." });
            return report;
        }

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // ── Signature verification ───────────────────────────────────────────
        VerifySignatures(root, header, report);

        // ── Checksums ────────────────────────────────────────────────────────
        RunChecksums(root, data, report);

        // ── Assertions ───────────────────────────────────────────────────────
        RunAssertions(root, data, report);

        // ── Forensic patterns ────────────────────────────────────────────────
        RunForensic(root, data, report);

        return report;
    }

    // ── Signature verification ───────────────────────────────────────────────

    private static void VerifySignatures(JsonElement root, byte[] header, ValidationReport report)
    {
        if (!root.TryGetProperty("detection", out var det)) return;

        var sigs = new List<(string hex, long offset, bool required)>();

        if (det.TryGetProperty("signatures", out var arr))
        {
            foreach (var s in arr.EnumerateArray())
            {
                string hex = s.TryGetProperty("signature", out var sv) ? sv.GetString() ?? "" : "";
                long   off = s.TryGetProperty("offset",    out var ov) ? ov.GetInt64() : 0;
                bool   req = s.TryGetProperty("required",  out var rv) && rv.GetBoolean();
                if (!string.IsNullOrWhiteSpace(hex))
                    sigs.Add((hex, off, req));
            }
        }
        else if (det.TryGetProperty("signature", out var single))
        {
            string hex = single.GetString() ?? "";
            long   off = det.TryGetProperty("offset", out var ov) ? ov.GetInt64() : 0;
            if (!string.IsNullOrWhiteSpace(hex))
                sigs.Add((hex, off, true));
        }

        foreach (var (hex, offset, required) in sigs)
        {
            byte[] expected = HexToBytes(hex);
            bool   found    = offset + expected.Length <= header.Length
                           && header.Skip((int)offset).Take(expected.Length).SequenceEqual(expected);

            if (!found && required)
                report.Issues.Add(new ValidationIssue
                {
                    Severity = "error",
                    Category = "Signature",
                    Name     = "SignatureMismatch",
                    Message  = $"Expected magic bytes {hex} at offset {offset} not found."
                });
            else if (found)
                report.Checks.Add(new ValidationCheck { Category = "Signature", Name = "MagicBytes", Passed = true, Detail = $"Signature {hex} @ offset {offset} ✓" });
        }
    }

    // ── Checksums ────────────────────────────────────────────────────────────

    private static void RunChecksums(JsonElement root, byte[] data, ValidationReport report)
    {
        if (!root.TryGetProperty("checksums", out var arr)) return;

        foreach (var cs in arr.EnumerateArray())
        {
            string name      = cs.TryGetProperty("name",      out var nv) ? nv.GetString() ?? "checksum" : "checksum";
            string algorithm = cs.TryGetProperty("algorithm", out var av) ? av.GetString() ?? ""         : "";
            string severity  = cs.TryGetProperty("severity",  out var sv) ? sv.GetString() ?? "warning"  : "warning";

            if (string.IsNullOrWhiteSpace(algorithm)) continue;

            // Resolve data range
            long dataOffset = 0, dataLength = data.Length;
            if (cs.TryGetProperty("dataRange", out var dr))
            {
                if (dr.TryGetProperty("fixedOffset", out var fo)) dataOffset = fo.GetInt64();
                if (dr.TryGetProperty("fixedLength", out var fl)) dataLength = fl.GetInt64();
                else dataLength = data.Length - dataOffset;
            }

            if (dataOffset < 0 || dataLength <= 0 || dataOffset + dataLength > data.Length)
            {
                report.Issues.Add(new ValidationIssue { Severity = severity, Category = "Checksum", Name = name, Message = $"Invalid data range for checksum '{name}'." });
                continue;
            }

            byte[] slice = data[(int)dataOffset..(int)(dataOffset + dataLength)];
            string? computed = ChecksumAlgorithms.Calculate(slice, algorithm);

            if (computed is null)
            {
                report.Issues.Add(new ValidationIssue { Severity = "info", Category = "Checksum", Name = name, Message = $"Unknown algorithm '{algorithm}' — skipped." });
                continue;
            }

            // Determine expected
            string? expected = null;
            if (cs.TryGetProperty("expectedValue", out var ev) && ev.GetString() is { } expStr)
            {
                expected = expStr;
            }
            else if (cs.TryGetProperty("storedAt", out var sat))
            {
                long storedOffset = sat.TryGetProperty("fixedOffset", out var sfo) ? sfo.GetInt64() : -1;
                int  storedLen    = sat.TryGetProperty("length",      out var sl)  ? sl.GetInt32()  : computed.Length / 2;
                bool bigEndian    = sat.TryGetProperty("endianness",  out var end) && (end.GetString() ?? "").Equals("big", StringComparison.OrdinalIgnoreCase);

                if (storedOffset >= 0 && storedOffset + storedLen <= data.Length)
                {
                    byte[] stored = data[(int)storedOffset..(int)(storedOffset + storedLen)];
                    if (bigEndian) Array.Reverse(stored);
                    expected = BitConverter.ToString(stored).Replace("-", "");
                }
            }

            if (expected is null)
            {
                report.Issues.Add(new ValidationIssue { Severity = "info", Category = "Checksum", Name = name, Message = "Cannot determine expected checksum value — skipped." });
                continue;
            }

            bool match = computed.Equals(expected, StringComparison.OrdinalIgnoreCase);
            if (match)
                report.Checks.Add(new ValidationCheck { Category = "Checksum", Name = name, Passed = true, Detail = $"{algorithm.ToUpper()}: {computed} ✓" });
            else
                report.Issues.Add(new ValidationIssue { Severity = severity, Category = "Checksum", Name = name, Message = $"{algorithm.ToUpper()} mismatch — computed {computed}, expected {expected}." });
        }
    }

    // ── Assertions ───────────────────────────────────────────────────────────

    private static void RunAssertions(JsonElement root, byte[] data, ValidationReport report)
    {
        if (!root.TryGetProperty("assertions", out var arr)) return;

        // Build a minimal variable set from magic bytes / file size
        var vars = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["file_size"] = (long)data.Length
        };

        // Populate variables declared in the format root
        if (root.TryGetProperty("variables", out var varBlock) && varBlock.ValueKind == JsonValueKind.Object)
            foreach (var v in varBlock.EnumerateObject())
                vars[v.Name] = v.Value.ValueKind switch
                {
                    JsonValueKind.Number => v.Value.TryGetInt64(out var i) ? (object)i : v.Value.GetDouble(),
                    JsonValueKind.String => v.Value.GetString() ?? "",
                    JsonValueKind.True   => true,
                    JsonValueKind.False  => false,
                    JsonValueKind.Null   => "",
                    _                    => v.Value.GetRawText()
                };

        foreach (var asr in arr.EnumerateArray())
        {
            string name     = asr.TryGetProperty("name",       out var nv) ? nv.GetString() ?? "assertion" : "assertion";
            string expr     = asr.TryGetProperty("expression", out var ev) ? ev.GetString() ?? ""          : "";
            string severity = asr.TryGetProperty("severity",   out var sv) ? sv.GetString() ?? "warning"   : "warning";
            string message  = asr.TryGetProperty("message",    out var mv) ? mv.GetString() ?? ""          : "";

            if (string.IsNullOrWhiteSpace(expr)) continue;

            bool? passed = TryEvaluate(expr, vars);

            if (passed is null)
            {
                report.Issues.Add(new ValidationIssue { Severity = "info", Category = "Assertion", Name = name, Message = $"Assertion '{name}' could not be evaluated (variables not resolved): {expr}" });
            }
            else if (passed == true)
            {
                report.Checks.Add(new ValidationCheck { Category = "Assertion", Name = name, Passed = true, Detail = expr });
            }
            else
            {
                report.Issues.Add(new ValidationIssue
                {
                    Severity = severity,
                    Category = "Assertion",
                    Name     = name,
                    Message  = string.IsNullOrWhiteSpace(message) ? $"Assertion failed: {expr}" : message
                });
            }
        }
    }

    // ── Forensic ─────────────────────────────────────────────────────────────

    private static void RunForensic(JsonElement root, byte[] data, ValidationReport report)
    {
        if (!root.TryGetProperty("forensic", out var forensic)) return;

        string riskLevel = forensic.TryGetProperty("riskLevel", out var rl) ? rl.GetString() ?? "low" : "low";
        report.ForensicRiskLevel = riskLevel;

        if (forensic.TryGetProperty("suspiciousPatterns", out var suspicious))
        {
            foreach (var pat in suspicious.EnumerateArray())
            {
                string pattern = pat.GetString() ?? "";
                if (string.IsNullOrWhiteSpace(pattern)) continue;

                if (MatchesBytePattern(data, pattern))
                    report.Issues.Add(new ValidationIssue
                    {
                        Severity = "warning",
                        Category = "Forensic",
                        Name     = "SuspiciousPattern",
                        Message  = $"Suspicious pattern detected: {pattern}"
                    });
            }
        }

        if (forensic.TryGetProperty("knownMaliciousPatterns", out var malicious))
        {
            foreach (var pat in malicious.EnumerateArray())
            {
                string pattern = pat.GetString() ?? "";
                if (string.IsNullOrWhiteSpace(pattern)) continue;

                if (MatchesBytePattern(data, pattern))
                    report.Issues.Add(new ValidationIssue
                    {
                        Severity = "error",
                        Category = "Forensic",
                        Name     = "MaliciousPattern",
                        Message  = $"Known malicious pattern detected: {pattern}"
                    });
            }
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static bool MatchesBytePattern(byte[] data, string pattern)
    {
        // Patterns can be hex sequences like "4D5A" or text strings
        if (pattern.StartsWith("0x", StringComparison.OrdinalIgnoreCase) || IsHex(pattern))
        {
            byte[] patBytes = HexToBytes(pattern.Replace("0x", "").Replace(" ", ""));
            for (int i = 0; i <= data.Length - patBytes.Length; i++)
                if (data.Skip(i).Take(patBytes.Length).SequenceEqual(patBytes))
                    return true;
        }
        else
        {
            // Text pattern search
            byte[] patBytes = System.Text.Encoding.ASCII.GetBytes(pattern);
            for (int i = 0; i <= data.Length - patBytes.Length; i++)
                if (data.Skip(i).Take(patBytes.Length).SequenceEqual(patBytes))
                    return true;
        }
        return false;
    }

    private static bool IsHex(string s) => s.Length > 0 && s.All(c => "0123456789abcdefABCDEF ".Contains(c));

    private static byte[] HexToBytes(string hex)
    {
        hex = hex.Replace(" ", "").Replace("-", "");
        if (hex.Length % 2 != 0) hex = "0" + hex;
        var bytes = new byte[hex.Length / 2];
        for (int i = 0; i < bytes.Length; i++)
            bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
        return bytes;
    }

    private static bool? TryEvaluate(string expr, Dictionary<string, object> vars)
    {
        string[] ops = ["==", "!=", ">=", "<=", ">", "<"];
        foreach (var op in ops)
        {
            int idx = expr.IndexOf(op, StringComparison.Ordinal);
            if (idx <= 0) continue;

            string left  = expr[..idx].Trim();
            string right = expr[(idx + op.Length)..].Trim();

            long? lv = ResolveValue(left,  vars);
            long? rv = ResolveValue(right, vars);
            if (lv is null || rv is null) return null;

            return op switch
            {
                "==" => lv == rv,
                "!=" => lv != rv,
                ">=" => lv >= rv,
                "<=" => lv <= rv,
                ">"  => lv >  rv,
                "<"  => lv <  rv,
                _    => null
            };
        }
        long? val = ResolveValue(expr.Trim(), vars);
        return val.HasValue ? val != 0 : null;
    }

    private static long? ResolveValue(string token, Dictionary<string, object> vars)
    {
        if (vars.TryGetValue(token, out var v))
        {
            try { return Convert.ToInt64(v); } catch { return null; }
        }
        if (long.TryParse(token, out var l)) return l;
        if (token.StartsWith("0x", StringComparison.OrdinalIgnoreCase) &&
            long.TryParse(token[2..], System.Globalization.NumberStyles.HexNumber, null, out var h))
            return h;
        return null;
    }
}
