//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using System;
using System.IO;
using System.Xml;
using System.Xml.Serialization;
using WpfHexaEditor.Models;

namespace WpfHexaEditor.Services
{
    /// <summary>
    /// Service for saving and loading editor state to/from XML files.
    /// V1 compatible feature - Phase 7.5 (State Persistence).
    /// </summary>
    public class StateService
    {
        /// <summary>
        /// Save editor state to XML file.
        /// </summary>
        /// <param name="state">The editor state to save</param>
        /// <param name="filePath">Path to XML file</param>
        /// <returns>True if successful, false otherwise</returns>
        public bool SaveState(EditorState state, string filePath)
        {
            if (state == null || string.IsNullOrEmpty(filePath))
                return false;

            try
            {
                var serializer = new XmlSerializer(typeof(EditorState));
                var settings = new XmlWriterSettings
                {
                    Indent = true,
                    IndentChars = "  ",
                    NewLineChars = "\r\n",
                    NewLineHandling = NewLineHandling.Replace
                };

                using (var writer = XmlWriter.Create(filePath, settings))
                {
                    serializer.Serialize(writer, state);
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SaveState failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Load editor state from XML file.
        /// </summary>
        /// <param name="filePath">Path to XML file</param>
        /// <returns>Loaded state, or null if failed</returns>
        public EditorState LoadState(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return null;

            try
            {
                var serializer = new XmlSerializer(typeof(EditorState));

                using (var reader = XmlReader.Create(filePath))
                {
                    return serializer.Deserialize(reader) as EditorState;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadState failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Validate that a state file is valid XML and can be deserialized.
        /// </summary>
        /// <param name="filePath">Path to XML file</param>
        /// <returns>True if valid, false otherwise</returns>
        public bool ValidateStateFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return false;

            try
            {
                var state = LoadState(filePath);
                return state != null;
            }
            catch
            {
                return false;
            }
        }
    }
}
