// ==========================================================
// Project: WpfHexEditor.Core
// File: ChecksumEngine.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude (Anthropic)
// Created: 2026-03-25
// Description:
//     Executes the `checksums` array from a .whfmt v2.0 format definition.
//     Computes CRC32, CRC16, Adler32, MD5, SHA-1, SHA-256, Sum8/16/32
//     against user-specified byte ranges and compares them to stored or
//     expected values.
//
// Architecture Notes:
//     Stateless — instantiate per interpretation run.
//     ChecksumValidator (existing) is reused for algorithm dispatch.
//     Returns a list of ChecksumResult records; caller decides severity handling.
//     No WPF dependencies.
// ==========================================================

using System;
using System.Collections.Generic;

namespace WpfHexEditor.Core.FormatDetection
{
    /// <summary>
    /// Result of a single checksum verification.
    /// </summary>
    public sealed class ChecksumResult
    {
        /// <summary>Checksum definition name (from whfmt).</summary>
        public string Name        { get; init; }

        /// <summary>Algorithm used (crc32, md5, sha1, ...).</summary>
        public string Algorithm   { get; init; }

        /// <summary>Whether the computed value matched the stored/expected value.</summary>
        public bool   IsValid     { get; init; }

        /// <summary>Computed hex string.</summary>
        public string Computed    { get; init; }

        /// <summary>Expected/stored hex string (from file or definition).</summary>
        public string Expected    { get; init; }

        /// <summary>Human-readable failure reason, or null on success.</summary>
        public string ErrorMessage { get; init; }

        /// <summary>Severity from definition ("error" | "warning" | "info").</summary>
        public string Severity    { get; init; }
    }

    /// <summary>
    /// Executes checksums defined in a .whfmt format definition against the file data.
    /// </summary>
    public sealed class ChecksumEngine
    {
        private readonly ChecksumValidator _validator = new();

        /// <summary>
        /// Execute all checksum definitions against <paramref name="data"/>
        /// using <paramref name="variables"/> to resolve offset/length variable references.
        /// </summary>
        public IReadOnlyList<ChecksumResult> Execute(
            IReadOnlyList<ChecksumDefinition> definitions,
            byte[] data,
            IReadOnlyDictionary<string, object> variables)
        {
            if (definitions == null || definitions.Count == 0 || data == null)
                return Array.Empty<ChecksumResult>();

            var results = new List<ChecksumResult>(definitions.Count);

            foreach (var def in definitions)
            {
                try
                {
                    results.Add(ExecuteOne(def, data, variables));
                }
                catch (Exception ex)
                {
                    results.Add(new ChecksumResult
                    {
                        Name      = def.Name ?? def.Algorithm,
                        Algorithm = def.Algorithm,
                        IsValid   = false,
                        Severity  = def.Severity ?? "error",
                        ErrorMessage = $"Checksum execution error: {ex.Message}"
                    });
                }
            }

            return results;
        }

        // ── Private ─────────────────────────────────────────────────────────────

        private ChecksumResult ExecuteOne(
            ChecksumDefinition def,
            byte[] data,
            IReadOnlyDictionary<string, object> variables)
        {
            string name      = def.Name ?? def.Algorithm ?? "unnamed";
            string severity  = def.Severity ?? "error";

            if (string.IsNullOrWhiteSpace(def.Algorithm))
                return Fail(name, def.Algorithm, severity, "Algorithm not specified.");

            // Resolve data range
            long dataOffset = ResolveVar(def.DataRange?.OffsetVar, variables, def.DataRange?.FixedOffset ?? 0);
            long dataLength = ResolveVar(def.DataRange?.LengthVar, variables, def.DataRange?.FixedLength ?? 0);

            if (dataOffset < 0 || dataLength <= 0 || dataOffset + dataLength > data.Length)
                return Fail(name, def.Algorithm, severity,
                    $"Invalid data range: offset={dataOffset} length={dataLength}");

            byte[] slice = new byte[dataLength];
            Array.Copy(data, dataOffset, slice, 0, dataLength);

            string computed = _validator.Calculate(slice, def.Algorithm);
            if (computed == null)
                return Fail(name, def.Algorithm, severity, $"Unknown algorithm: {def.Algorithm}");

            // Determine expected value
            string expected;
            if (!string.IsNullOrWhiteSpace(def.ExpectedValue))
            {
                expected = def.ExpectedValue;
            }
            else if (def.StoredAt != null)
            {
                long storedOffset = ResolveVar(def.StoredAt.OffsetVar, variables, def.StoredAt.FixedOffset ?? 0);
                int  storedLen    = def.StoredAt.Length > 0 ? def.StoredAt.Length : computed.Length / 2;

                if (storedOffset < 0 || storedOffset + storedLen > data.Length)
                    return Fail(name, def.Algorithm, severity,
                        $"Stored checksum offset out of range: {storedOffset}");

                byte[] storedBytes = new byte[storedLen];
                Array.Copy(data, storedOffset, storedBytes, 0, storedLen);

                // Handle endianness for stored int values
                bool bigEndian = string.Equals(def.StoredAt.Endianness, "big",
                    StringComparison.OrdinalIgnoreCase);
                if (bigEndian && storedLen <= 4)
                    Array.Reverse(storedBytes);

                expected = BitConverter.ToString(storedBytes).Replace("-", "");
            }
            else
            {
                return Fail(name, def.Algorithm, severity,
                    "No expected value or storedAt location provided.");
            }

            bool match = string.Equals(computed, expected, StringComparison.OrdinalIgnoreCase);

            return new ChecksumResult
            {
                Name      = name,
                Algorithm = def.Algorithm,
                IsValid   = match,
                Computed  = computed,
                Expected  = expected,
                Severity  = severity,
                ErrorMessage = match
                    ? null
                    : $"Checksum mismatch: computed {computed}, expected {expected}"
            };
        }

        private static long ResolveVar(
            string varName,
            IReadOnlyDictionary<string, object> variables,
            long fallback)
        {
            if (string.IsNullOrWhiteSpace(varName))
                return fallback;
            if (variables != null && variables.TryGetValue(varName, out var v))
            {
                try { return Convert.ToInt64(v); }
                catch { /* fall through */ }
            }
            return fallback;
        }

        private static ChecksumResult Fail(string name, string algo, string severity, string msg)
            => new ChecksumResult
            {
                Name      = name,
                Algorithm = algo,
                IsValid   = false,
                Severity  = severity,
                ErrorMessage = msg
            };
    }
}
