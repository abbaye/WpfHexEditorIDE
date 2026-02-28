using System;
using System.Collections.Generic;

namespace WpfHexEditor.Core.RomHacking
{
    /// <summary>
    /// Result of an IPS patching operation
    /// </summary>
    public class IPSPatchResult
    {
        /// <summary>
        /// True if the patch was applied successfully
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Error message if the operation failed
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Number of records successfully applied
        /// </summary>
        public int RecordsApplied { get; set; }

        /// <summary>
        /// Total number of records in the patch file
        /// </summary>
        public int TotalRecords { get; set; }

        /// <summary>
        /// Original file size
        /// </summary>
        public long OriginalFileSize { get; set; }

        /// <summary>
        /// Patched file size (may differ if patch includes truncation extension)
        /// </summary>
        public long PatchedFileSize { get; set; }

        /// <summary>
        /// List of all records that were applied
        /// </summary>
        public List<IPSRecord> AppliedRecords { get; set; }

        /// <summary>
        /// Time taken to apply the patch
        /// </summary>
        public TimeSpan Duration { get; set; }

        /// <summary>
        /// Creates a new IPSPatchResult
        /// </summary>
        public IPSPatchResult()
        {
            AppliedRecords = new List<IPSRecord>();
        }

        /// <summary>
        /// Creates a successful result
        /// </summary>
        public static IPSPatchResult CreateSuccess(int recordsApplied, int totalRecords, long originalSize, long patchedSize, TimeSpan duration)
        {
            return new IPSPatchResult
            {
                Success = true,
                RecordsApplied = recordsApplied,
                TotalRecords = totalRecords,
                OriginalFileSize = originalSize,
                PatchedFileSize = patchedSize,
                Duration = duration
            };
        }

        /// <summary>
        /// Creates a failure result
        /// </summary>
        public static IPSPatchResult CreateFailure(string errorMessage)
        {
            return new IPSPatchResult
            {
                Success = false,
                ErrorMessage = errorMessage
            };
        }

        /// <summary>
        /// Returns a string representation of the result
        /// </summary>
        public override string ToString()
        {
            if (Success)
            {
                return $"IPS Patch Applied: {RecordsApplied}/{TotalRecords} records, {OriginalFileSize} → {PatchedFileSize} bytes ({Duration.TotalMilliseconds:F2}ms)";
            }
            return $"IPS Patch Failed: {ErrorMessage}";
        }
    }
}
