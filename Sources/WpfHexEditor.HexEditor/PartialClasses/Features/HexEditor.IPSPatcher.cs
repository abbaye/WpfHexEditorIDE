//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using WpfHexEditor.Core.RomHacking;

namespace WpfHexEditor.HexEditor
{
    public partial class HexEditor
    {
        /// <summary>
        /// Applies an IPS patch to the currently loaded file
        /// </summary>
        public void ApplyIPSPatch()
        {
            if (!IsFileOrStreamLoaded)
            {
                MessageBox.Show(
                    "Please open a ROM file first before applying a patch.",
                    "No File Open",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            // Select IPS patch file
            var openDialog = new OpenFileDialog
            {
                Title = "Select IPS Patch File",
                Filter = "IPS Patch Files (*.ips)|*.ips|All Files (*.*)|*.*",
                CheckFileExists = true
            };

            if (openDialog.ShowDialog() != true)
                return;

            var ipsFilePath = openDialog.FileName;

            // Validate IPS file
            if (!IPSPatcher.IsValidIPSFile(ipsFilePath))
            {
                MessageBox.Show(
                    $"The selected file is not a valid IPS patch.\n\nFile: {Path.GetFileName(ipsFilePath)}",
                    "Invalid IPS File",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }

            // Confirm patch application
            var result = MessageBox.Show(
                $"Apply IPS patch to current file?\n\n" +
                $"ROM: {Path.GetFileName(FileName)}\n" +
                $"Patch: {Path.GetFileName(ipsFilePath)}\n\n" +
                $"This operation cannot be undone. Make sure you have a backup!",
                "Apply IPS Patch",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
                return;

            try
            {
                // Get current data
                var romData = GetAllBytes();

                // Apply patch
                var patchResult = IPSPatcher.ApplyPatchToData(ref romData, ipsFilePath);

                if (patchResult.Success)
                {
                    // Update the editor with patched data
                    OpenMemory(romData);

                    // Show success message
                    MessageBox.Show(
                        $"IPS patch applied successfully!\n\n" +
                        $"Records Applied: {patchResult.RecordsApplied}/{patchResult.TotalRecords}\n" +
                        $"Original Size: {patchResult.OriginalFileSize:N0} bytes\n" +
                        $"Patched Size: {patchResult.PatchedFileSize:N0} bytes\n" +
                        $"Duration: {patchResult.Duration.TotalMilliseconds:F2} ms\n\n" +
                        $"Don't forget to save the file!",
                        "Patch Applied",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    // Mark as modified
                    //UnSavedChanges = true;
                }
                else
                {
                    MessageBox.Show(
                        $"Failed to apply IPS patch:\n\n{patchResult.ErrorMessage}",
                        "Patch Failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error applying IPS patch:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Creates an IPS patch from the current unsaved modifications and writes it to
        /// <paramref name="outputIpsPath"/>. No dialog is shown.
        /// </summary>
        /// <param name="outputIpsPath">Destination path for the .ips file.</param>
        /// <returns>
        /// <see langword="true"/> on success; <see langword="false"/> when there is nothing
        /// to patch (no file loaded, no unsaved changes) or when an I/O error occurs.
        /// </returns>
        public bool CreateIPSPatchFromUnsavedChanges(string outputIpsPath)
        {
            if (!IsFileOrStreamLoaded || !IsModified)
                return false;

            if (string.IsNullOrWhiteSpace(outputIpsPath))
                throw new ArgumentException("Output path must not be empty.", nameof(outputIpsPath));

            // Read the on-disk file as the authoritative original — equivalent to
            // "save the modified file then compare with the original on disk".
            // Stream-only buffers (no FileName) fall back to GetAllBytes(copyChange:false),
            // but NOTE: in V2 that parameter is currently ignored (returns modified bytes),
            // so stream-only edits won't produce a meaningful patch.
            byte[] original = !string.IsNullOrEmpty(FileName) && File.Exists(FileName)
                ? File.ReadAllBytes(FileName)
                : GetAllBytes(copyChange: false);

            var modified   = GetAllBytes(copyChange: true);
            var patchBytes = IPSPatcher.CreatePatch(original, modified);

            File.WriteAllBytes(outputIpsPath, patchBytes);
            return true;
        }

        /// <summary>
        /// Exports IPS patch from comparing current file with another file
        /// </summary>
        public void CreateIPSPatch()
        {
            if (!IsFileOrStreamLoaded)
            {
                MessageBox.Show(
                    "Please open a modified ROM file first.",
                    "No File Open",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            // Select original (unmodified) ROM file
            var openDialog = new OpenFileDialog
            {
                Title = "Select Original (Unmodified) ROM File",
                Filter = "ROM Files|*.nes;*.smc;*.sfc;*.gb;*.gbc;*.gba;*.n64;*.z64;*.md;*.bin;*.gen|All Files (*.*)|*.*",
                CheckFileExists = true
            };

            if (openDialog.ShowDialog() != true)
                return;

            var originalFilePath = openDialog.FileName;

            // Select output IPS file location
            var saveDialog = new SaveFileDialog
            {
                Title = "Save IPS Patch As",
                Filter = "IPS Patch Files (*.ips)|*.ips|All Files (*.*)|*.*",
                DefaultExt = ".ips",
                FileName = Path.GetFileNameWithoutExtension(FileName) + ".ips"
            };

            if (saveDialog.ShowDialog() != true)
                return;

            var outputIpsPath = saveDialog.FileName;

            try
            {
                var originalData = File.ReadAllBytes(originalFilePath);
                var modifiedData = GetAllBytes();

                var patchBytes = IPSPatcher.CreatePatch(originalData, modifiedData);
                File.WriteAllBytes(outputIpsPath, patchBytes);

                MessageBox.Show(
                    $"IPS patch created successfully!\n\n" +
                    $"Saved to: {Path.GetFileName(outputIpsPath)}\n" +
                    $"Original size: {originalData.Length:N0} bytes\n" +
                    $"Modified size: {modifiedData.Length:N0} bytes\n" +
                    $"Patch size:    {patchBytes.Length:N0} bytes",
                    "Patch Created",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error creating IPS patch:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }
}
