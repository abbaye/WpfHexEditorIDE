//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using System;
using WpfHexaEditor.Core;
using WpfHexaEditor.Core.CharacterTable;

namespace WpfHexaEditor
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

                // Sync TblStream to HexViewport for color rendering
                if (HexViewport != null)
                    HexViewport.TblStream = _tblStream;

                StatusText.Text = $"TBL loaded: {System.IO.Path.GetFileName(path)}";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Failed to load TBL: {ex.Message}";
                _tblStream = null;
                _characterTableType = CharacterTableType.Ascii;

                // Clear TblStream in HexViewport
                if (HexViewport != null)
                    HexViewport.TblStream = null;
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

            // Clear TblStream in HexViewport
            if (HexViewport != null)
                HexViewport.TblStream = null;

            StatusText.Text = "TBL closed, using ASCII";
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

        #endregion
    }
}
