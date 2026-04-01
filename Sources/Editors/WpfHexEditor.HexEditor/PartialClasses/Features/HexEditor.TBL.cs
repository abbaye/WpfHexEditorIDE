// ==========================================================
// Project: WpfHexEditor.HexEditor
// File: HexEditor.TBL.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude (Anthropic)
// Created: 2026-03-06
// Description:
//     Partial class providing TBL (Character Table) file support for the HexEditor.
//     Loads .tbl files mapping byte values to custom characters (used in ROM hacking)
//     and switches the ASCII column display to use the loaded character table.
//
// Architecture Notes:
//     TBL parsing delegated to WpfHexEditor.Core.CharacterTable utilities.
//     Character table rendering applied to HexViewport ASCII column.
//
// ==========================================================

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
        /// Load a TBL (Character Table) file. Supports .tbl (Thingy/Atlas) only.
        /// For .tblx, populate a TblStream externally and call <see cref="LoadTBL"/>.
        /// </summary>
        /// <param name="path">Path to the TBL file</param>
        public void LoadTBLFile(string path)
        {
            try { LoadTBL(new TblStream(path), path); }
            catch (Exception ex)
            {
                StatusText.Text = $"Failed to load TBL: {ex.Message}";
                _tblStream = null;
                _characterTableType = CharacterTableType.Ascii;
                if (HexViewport != null) { HexViewport.TblStream = null; HexViewport.ShowTblMte = false; }
                TblStatusIcon.Visibility = System.Windows.Visibility.Collapsed;
            }
        }

        /// <summary>
        /// Apply a pre-populated <see cref="TblStream"/> as the active character table.
        /// Use this when the stream was built externally (e.g. from a .tblx file).
        /// </summary>
        /// <param name="tbl">Already-populated TblStream.</param>
        /// <param name="displayPath">Path shown in the status bar / tooltip (filename only).</param>
        public void LoadTBL(TblStream tbl, string displayPath)
        {
            _tblStream = tbl;
            _characterTableType = CharacterTableType.TblFile;

            if (HexViewport != null)
            {
                HexViewport.TblStream = _tblStream;
                HexViewport.ShowTblAscii    = true;
                HexViewport.ShowTblDte      = true;
                HexViewport.ShowTblMte      = true;
                HexViewport.ShowTblJaponais = true;
                HexViewport.ShowTblEndBlock = true;
                HexViewport.ShowTblEndLine  = true;
                HexViewport.Refresh();
            }

            string fileName = System.IO.Path.GetFileName(displayPath);
            StatusText.Text = $"TBL loaded: {fileName}";
            TblStatusIcon.Visibility = System.Windows.Visibility.Visible;

            var tt = new System.Text.StringBuilder();
            tt.AppendLine($"📋 TBL File: {fileName}");
            tt.AppendLine($"━━━━━━━━━━━━━━━━━━━━━━━━━");
            tt.AppendLine($"Total Entries: {_tblStream.Length}");
            tt.AppendLine();
            if (_tblStream.TotalAscii    > 0) tt.AppendLine($"  • ASCII: {_tblStream.TotalAscii}");
            if (_tblStream.TotalDte      > 0) tt.AppendLine($"  • DTE (Dual): {_tblStream.TotalDte}");
            if (_tblStream.TotalMte      > 0) tt.AppendLine($"  • MTE (Multi): {_tblStream.TotalMte}");
            if (_tblStream.Total3Byte    > 0) tt.AppendLine($"  • 3-Byte: {_tblStream.Total3Byte}");
            if (_tblStream.Total4Byte    > 0) tt.AppendLine($"  • 4-Byte: {_tblStream.Total4Byte}");
            if (_tblStream.Total5PlusByte> 0) tt.AppendLine($"  • 5+ Byte: {_tblStream.Total5PlusByte}");
            if (_tblStream.TotalJaponais > 0) tt.AppendLine($"  • Japanese: {_tblStream.TotalJaponais}");
            if (_tblStream.TotalEndBlock > 0) tt.AppendLine($"  • End Block: {_tblStream.TotalEndBlock}");
            if (_tblStream.TotalEndLine  > 0) tt.AppendLine($"  • End Line: {_tblStream.TotalEndLine}");
            tt.AppendLine();
            tt.AppendLine("⚠️ Edit only in HEX panel when TBL is loaded");
            TblStatusTooltipText.Text = tt.ToString();
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

                // Sync CharacterTableType to HexViewport for encoding-aware rendering
                if (HexViewport != null)
                    HexViewport.CharacterTableType = value;

                // If switching to TBL but no TBL loaded, create default ASCII
                if (value == CharacterTableType.TblFile && _tblStream == null)
                {
                    _tblStream = TblStream.CreateDefaultTbl(DefaultCharacterTableType.Ascii);

                    // Sync TblStream to HexViewport
                    if (HexViewport != null)
                        HexViewport.TblStream = _tblStream;
                }
                // If switching away from TBL, close it
                // (CloseTBL sets _characterTableType = Ascii directly, not via this setter — no recursion)
                else if (value != CharacterTableType.TblFile && _tblStream != null)
                {
                    CloseTBL();
                }
                // Refresh the viewport so the new encoding/table type is rendered immediately
                HexViewport?.Refresh();
            }
        }

        /// <summary>
        /// Get the current TBL stream
        /// </summary>
        public TblStream TBL => _tblStream;

        /// <summary>
        /// Fired when the user requests to open the TBL editor.
        /// The host application (Sample.Main, docking host, etc.) handles this event
        /// by creating a TblEditor and passing <see cref="TBL"/> as its Source.
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
