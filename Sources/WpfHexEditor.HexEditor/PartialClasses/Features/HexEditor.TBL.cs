//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using System;
using WpfHexEditor.Core;
using WpfHexEditor.Core.CharacterTable;

namespace WpfHexEditor.HexEditor
{
    /// <summary>
    /// HexEditor partial class - TBL Support
    /// Contains methods for loading and managing TBL (Character Table) files
    /// </summary>
    public partial class HexEditor
    {
        #region TBL Support

        /// <summary>
        /// Load a TBL (Character Table) file
        /// </summary>
        /// <param name="path">Path to the TBL file</param>
        public void LoadTBLFile(string path)
        {
            try
            {
                _tblStream = new TblStream(path);
                _characterTableType = CharacterTableType.TblFile;

                // Sync TblStream to HexViewport for rendering and enable TBL display
                if (HexViewport != null)
                {
                    HexViewport.TblStream = _tblStream;
                    // Enable ALL TBL types when loading a TBL file (V1 compatibility)
                    HexViewport.ShowTblAscii = true;
                    HexViewport.ShowTblDte = true;
                    HexViewport.ShowTblMte = true;
                    HexViewport.ShowTblJaponais = true;
                    HexViewport.ShowTblEndBlock = true;
                    HexViewport.ShowTblEndLine = true;
                    // Force refresh to apply TBL changes
                    HexViewport.Refresh();
                }

                // Update status bar
                string fileName = System.IO.Path.GetFileName(path);
                StatusText.Text = $"TBL loaded: {fileName}";

                // Update TBL status icon with detailed statistics
                TblStatusIcon.Visibility = System.Windows.Visibility.Visible;

                // Build detailed tooltip with TBL content statistics
                var tooltipText = new System.Text.StringBuilder();
                tooltipText.AppendLine($"📋 TBL File: {fileName}");
                tooltipText.AppendLine($"━━━━━━━━━━━━━━━━━━━━━━━━━");
                tooltipText.AppendLine($"Total Entries: {_tblStream.Length}");
                tooltipText.AppendLine();

                // Show breakdown by type (only non-zero counts)
                if (_tblStream.TotalAscii > 0)
                    tooltipText.AppendLine($"  • ASCII: {_tblStream.TotalAscii}");
                if (_tblStream.TotalDte > 0)
                    tooltipText.AppendLine($"  • DTE (Dual): {_tblStream.TotalDte}");
                if (_tblStream.TotalMte > 0)
                    tooltipText.AppendLine($"  • MTE (Multi): {_tblStream.TotalMte}");
                if (_tblStream.Total3Byte > 0)
                    tooltipText.AppendLine($"  • 3-Byte: {_tblStream.Total3Byte}");
                if (_tblStream.Total4Byte > 0)
                    tooltipText.AppendLine($"  • 4-Byte: {_tblStream.Total4Byte}");
                if (_tblStream.Total5PlusByte > 0)
                    tooltipText.AppendLine($"  • 5+ Byte: {_tblStream.Total5PlusByte}");
                if (_tblStream.TotalJaponais > 0)
                    tooltipText.AppendLine($"  • Japanese: {_tblStream.TotalJaponais}");
                if (_tblStream.TotalEndBlock > 0)
                    tooltipText.AppendLine($"  • End Block: {_tblStream.TotalEndBlock}");
                if (_tblStream.TotalEndLine > 0)
                    tooltipText.AppendLine($"  • End Line: {_tblStream.TotalEndLine}");

                tooltipText.AppendLine();
                tooltipText.AppendLine("⚠️ Edit only in HEX panel when TBL is loaded");

                TblStatusTooltipText.Text = tooltipText.ToString();
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Failed to load TBL: {ex.Message}";
                _tblStream = null;
                _characterTableType = CharacterTableType.Ascii;

                // Clear TblStream in HexViewport and disable TBL display
                if (HexViewport != null)
                {
                    HexViewport.TblStream = null;
                    HexViewport.ShowTblMte = false;
                }

                // Hide TBL status icon
                TblStatusIcon.Visibility = System.Windows.Visibility.Collapsed;
            }
        }

        /// <summary>
        /// Close the current TBL file and revert to ASCII
        /// </summary>
        public void CloseTBL()
        {
            if (_tblStream != null)
            {
                _tblStream.Close();
                _tblStream.Dispose();
                _tblStream = null;
            }
            _characterTableType = CharacterTableType.Ascii;

            // Clear TblStream in HexViewport and disable TBL display
            if (HexViewport != null)
            {
                HexViewport.TblStream = null;
                HexViewport.ShowTblMte = false;
            }

            // Update status bar
            StatusText.Text = "TBL closed, using ASCII";

            // Hide TBL status icon
            TblStatusIcon.Visibility = System.Windows.Visibility.Collapsed;
        }

        /// <summary>
        /// Get or set the type of character table to use
        /// </summary>
        public CharacterTableType TypeOfCharacterTable
        {
            get => _characterTableType;
            set
            {
                _characterTableType = value;
                // If switching to TBL but no TBL loaded, create default ASCII
                if (value == CharacterTableType.TblFile && _tblStream == null)
                {
                    _tblStream = TblStream.CreateDefaultTbl(DefaultCharacterTableType.Ascii);

                    // Sync TblStream to HexViewport
                    if (HexViewport != null)
                        HexViewport.TblStream = _tblStream;
                }
                // If switching away from TBL, close it
                else if (value != CharacterTableType.TblFile && _tblStream != null)
                {
                    CloseTBL();
                }
            }
        }

        /// <summary>
        /// Get the current TBL stream
        /// </summary>
        public TblStream TBL => _tblStream;

        /// <summary>
        /// Fired when the user requests to open the TBL editor.
        /// The host application (Sample.Main, docking host, etc.) handles this event
        /// by creating a TblEditorControl and passing <see cref="TBL"/> as its Source.
        /// </summary>
        public event EventHandler? TblEditorRequested;

        /// <summary>
        /// Request the host to open a TBL Editor for the current TBL stream.
        /// If no TBL is loaded, creates an empty one first so the editor starts fresh.
        /// </summary>
        public void OpenTblEditor()
        {
            if (_tblStream == null)
                _tblStream = new TblStream();

            TblEditorRequested?.Invoke(this, EventArgs.Empty);
        }

        #endregion
    }
}
