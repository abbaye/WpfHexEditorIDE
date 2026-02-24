using System;
using System.Collections.Generic;
using System.Text;

namespace WpfHexaEditor.Core.FormatDetection
{
    /// <summary>
    /// Built-in functions for format detection scripts.
    /// These functions can be called from format definition JSON files.
    /// </summary>
    public class BuiltInFunctions
    {
        private readonly byte[] _data;
        private readonly Dictionary<string, object> _variables;

        public BuiltInFunctions(byte[] data, Dictionary<string, object> variables)
        {
            _data = data ?? throw new ArgumentNullException(nameof(data));
            _variables = variables ?? new Dictionary<string, object>();
        }

        /// <summary>
        /// Detects Byte Order Mark (BOM) at the beginning of the file.
        /// Sets variables: encoding, bomDetected, bomSize
        /// </summary>
        public void DetectBOM()
        {
            if (_data.Length >= 3 &&
                _data[0] == 0xEF && _data[1] == 0xBB && _data[2] == 0xBF)
            {
                _variables["encoding"] = "UTF-8";
                _variables["bomDetected"] = true;
                _variables["bomSize"] = 3;
                return;
            }

            if (_data.Length >= 2 &&
                _data[0] == 0xFF && _data[1] == 0xFE)
            {
                if (_data.Length >= 4 && _data[2] == 0x00 && _data[3] == 0x00)
                {
                    _variables["encoding"] = "UTF-32LE";
                    _variables["bomDetected"] = true;
                    _variables["bomSize"] = 4;
                }
                else
                {
                    _variables["encoding"] = "UTF-16LE";
                    _variables["bomDetected"] = true;
                    _variables["bomSize"] = 2;
                }
                return;
            }

            if (_data.Length >= 2 &&
                _data[0] == 0xFE && _data[1] == 0xFF)
            {
                _variables["encoding"] = "UTF-16BE";
                _variables["bomDetected"] = true;
                _variables["bomSize"] = 2;
                return;
            }

            if (_data.Length >= 4 &&
                _data[0] == 0x00 && _data[1] == 0x00 &&
                _data[2] == 0xFE && _data[3] == 0xFF)
            {
                _variables["encoding"] = "UTF-32BE";
                _variables["bomDetected"] = true;
                _variables["bomSize"] = 4;
                return;
            }

            // No BOM detected
            _variables["bomDetected"] = false;
            _variables["bomSize"] = 0;
        }

        /// <summary>
        /// Counts the number of lines in the file (LF count).
        /// Sets variable: lineCount
        /// </summary>
        public void CountLines()
        {
            int count = 0;
            for (int i = 0; i < _data.Length; i++)
            {
                if (_data[i] == 0x0A) // LF
                    count++;
            }
            _variables["lineCount"] = count;
        }

        /// <summary>
        /// Detects the predominant line ending style in the file.
        /// Analyzes first 8192 bytes (or entire file if smaller).
        /// Sets variables: lineEnding, lfCount, crlfCount, crCount
        /// </summary>
        public void DetectLineEnding()
        {
            int sampleSize = Math.Min(8192, _data.Length);
            int lf = 0;
            int crlf = 0;
            int cr = 0;

            for (int i = 0; i < sampleSize; i++)
            {
                if (_data[i] == 0x0D) // CR
                {
                    if (i + 1 < sampleSize && _data[i + 1] == 0x0A) // CRLF
                    {
                        crlf++;
                        i++; // Skip the LF
                    }
                    else
                    {
                        cr++;
                    }
                }
                else if (_data[i] == 0x0A) // LF (standalone)
                {
                    lf++;
                }
            }

            _variables["lfCount"] = lf;
            _variables["crlfCount"] = crlf;
            _variables["crCount"] = cr;

            // Determine predominant style
            if (crlf > lf && crlf > cr)
                _variables["lineEnding"] = "CRLF";
            else if (lf > cr)
                _variables["lineEnding"] = "LF";
            else if (cr > 0)
                _variables["lineEnding"] = "CR";
            else
                _variables["lineEnding"] = "None";
        }

        /// <summary>
        /// Returns the minimum of two numbers.
        /// </summary>
        public long Min(long a, long b)
        {
            return Math.Min(a, b);
        }

        /// <summary>
        /// Returns the maximum of two numbers.
        /// </summary>
        public long Max(long a, long b)
        {
            return Math.Max(a, b);
        }

        /// <summary>
        /// Extracts a substring from the data as UTF-8 text.
        /// </summary>
        public string Substring(long offset, long length)
        {
            if (offset < 0 || offset >= _data.Length)
                return string.Empty;

            long actualLength = Math.Min(length, _data.Length - offset);
            if (actualLength <= 0)
                return string.Empty;

            try
            {
                return Encoding.UTF8.GetString(_data, (int)offset, (int)actualLength);
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Executes a built-in function by name with optional arguments.
        /// Returns the result or null if function doesn't return a value.
        /// </summary>
        public object Execute(string functionName, params object[] args)
        {
            switch (functionName.ToLowerInvariant())
            {
                case "detectbom":
                    DetectBOM();
                    return null;

                case "countlines":
                    CountLines();
                    return null;

                case "detectlineending":
                    DetectLineEnding();
                    return null;

                case "min":
                    if (args.Length >= 2)
                    {
                        long a = Convert.ToInt64(args[0]);
                        long b = Convert.ToInt64(args[1]);
                        return Min(a, b);
                    }
                    return 0L;

                case "max":
                    if (args.Length >= 2)
                    {
                        long a = Convert.ToInt64(args[0]);
                        long b = Convert.ToInt64(args[1]);
                        return Max(a, b);
                    }
                    return 0L;

                case "substring":
                    if (args.Length >= 2)
                    {
                        long offset = Convert.ToInt64(args[0]);
                        long length = Convert.ToInt64(args[1]);
                        return Substring(offset, length);
                    }
                    return string.Empty;

                default:
                    return null;
            }
        }
    }
}
