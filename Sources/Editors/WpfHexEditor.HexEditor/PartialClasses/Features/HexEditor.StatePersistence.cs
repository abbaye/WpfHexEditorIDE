// ==========================================================
// Project: WpfHexEditor.HexEditor
// File: HexEditor.StatePersistence.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude (Anthropic)
// Created: 2026-03-06
// Description:
//     Partial class managing editor state persistence for the HexEditor.
//     Saves and loads editor state (bookmarks, scroll position, selection, highlights)
//     to/from XML files associated with the opened binary file.
//
// Architecture Notes:
//     Uses System.Xml.Linq for XML serialization. State file stored alongside the
//     target file with a .hexstate extension. Called on file open/close lifecycle.
//
// ==========================================================

using System;
using System.Linq;
using System.Xml.Linq;

namespace WpfHexEditor.HexEditor
{
    /// <summary>
    /// HexEditor partial class - State Persistence
    /// Contains methods for saving and loading editor state to/from XML files
    /// </summary>
    public partial class HexEditor
    {
        #region State Persistence

        /// <summary>
        /// Save current editor state to XML file
        /// Saves: position, selection, bookmarks, font size, filename
        /// </summary>
        public void SaveCurrentState(string stateFilename)
        {
            try
            {
                var doc = new XDocument(
                    new XElement("HexEditorState",
                        new XElement("FileName", FileName ?? string.Empty),
                        new XElement("Position", Position),
                        new XElement("SelectionStart", SelectionStart),
                        new XElement("SelectionStop", SelectionStop),
                        new XElement("FontSize", FontSize),
                        new XElement("BytePerLine", BytePerLine),
                        new XElement("Bookmarks",
                            _bookmarks.Select(b => new XElement("Bookmark", b))
                        )
                    )
                );

                doc.Save(stateFilename);
                StatusText.Text = $"State saved to {System.IO.Path.GetFileName(stateFilename)}";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Failed to save state: {ex.Message}";
            }
        }

        /// <summary>
        /// Load editor state from XML file
        /// Restores: position, selection, bookmarks, font size
        /// Note: Does NOT reload the file, only restores state
        /// </summary>
        public void LoadCurrentState(string stateFilename)
        {
            try
            {
                var doc = XDocument.Load(stateFilename);
                var root = doc.Root;

                if (root?.Name != "HexEditorState") return;

                // Restore basic properties
                var fontSize = root.Element("FontSize")?.Value;
                if (fontSize != null && double.TryParse(fontSize, out double fs))
                    FontSize = fs;

                var bytesPerLine = root.Element("BytePerLine")?.Value;
                if (bytesPerLine != null && int.TryParse(bytesPerLine, out int bpl))
                    BytePerLine = bpl;

                // Restore position and selection
                var position = root.Element("Position")?.Value;
                if (position != null && long.TryParse(position, out long pos))
                    SetPosition(pos);

                var selStart = root.Element("SelectionStart")?.Value;
                var selStop = root.Element("SelectionStop")?.Value;
                if (selStart != null && long.TryParse(selStart, out long start) &&
                    selStop != null && long.TryParse(selStop, out long stop))
                {
                    SelectionStart = start;
                    SelectionStop = stop;
                }

                // Restore bookmarks
                var bookmarks = root.Element("Bookmarks")?.Elements("Bookmark");
                if (bookmarks != null)
                {
                    ClearAllBookmarks();
                    foreach (var bookmark in bookmarks)
                    {
                        if (long.TryParse(bookmark.Value, out long bookmarkPos))
                            SetBookmark(bookmarkPos);
                    }
                }

                StatusText.Text = $"State loaded from {System.IO.Path.GetFileName(stateFilename)}";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Failed to load state: {ex.Message}";
            }
        }

        #endregion
    }
}
