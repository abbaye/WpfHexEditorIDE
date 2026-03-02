//////////////////////////////////////////////
// Apache 2.0  - 2026
// File Comparison - Difference Model
// Author : Claude Sonnet 4.5
// Contributors: Derek Tremblay (derektremblay666@gmail.com), Claude Sonnet 4.6
//////////////////////////////////////////////

using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WpfHexEditor.Core.Models.Comparison
{
    /// <summary>
    /// Type of difference between two files
    /// </summary>
    public enum DifferenceType
    {
        /// <summary>
        /// Bytes are identical
        /// </summary>
        Identical,
        /// <summary>
        /// Bytes are different
        /// </summary>
        Modified,
        /// <summary>
        /// Byte exists only in first file
        /// </summary>
        DeletedInSecond,
        /// <summary>
        /// Byte exists only in second file
        /// </summary>
        AddedInSecond
    }

    /// <summary>
    /// Represents a difference between two files
    /// </summary>
    public class FileDifference : INotifyPropertyChanged
    {
        private long _offset;
        private int _length;
        private DifferenceType _type;
        private byte[] _bytesFile1;
        private byte[] _bytesFile2;
        private string _description;

        /// <summary>
        /// Offset where difference starts
        /// </summary>
        public long Offset
        {
            get => _offset;
            set { _offset = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Length of the difference in bytes
        /// </summary>
        public int Length
        {
            get => _length;
            set { _length = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Type of difference
        /// </summary>
        public DifferenceType Type
        {
            get => _type;
            set { _type = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Bytes from first file
        /// </summary>
        public byte[] BytesFile1
        {
            get => _bytesFile1;
            set { _bytesFile1 = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Bytes from second file
        /// </summary>
        public byte[] BytesFile2
        {
            get => _bytesFile2;
            set { _bytesFile2 = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Human-readable description of the difference
        /// </summary>
        public string Description
        {
            get => _description;
            set { _description = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
