//////////////////////////////////////////////
// Apache 2.0  - 2026
// Enriched Format ViewModel
// Author : Claude Sonnet 4.5
//////////////////////////////////////////////

using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using WpfHexaEditor.Core.FormatDetection;

namespace WpfHexaEditor.ViewModels
{
    /// <summary>
    /// ViewModel for displaying enriched format metadata
    /// Presents format information, software, use cases, and technical details
    /// </summary>
    public class EnrichedFormatViewModel : INotifyPropertyChanged
    {
        private FormatDefinition _currentFormat;
        private string _formatName;
        private string _formatDescription;
        private string _formatCategory;
        private List<string> _extensions;
        private List<string> _software;
        private List<string> _useCases;
        private List<string> _mimeTypes;
        private int _completenessScore;
        private string _documentationLevel;
        private bool _isPriorityFormat;

        public EnrichedFormatViewModel()
        {
            _extensions = new List<string>();
            _software = new List<string>();
            _useCases = new List<string>();
            _mimeTypes = new List<string>();
            _documentationLevel = "basic";
        }

        /// <summary>
        /// Current format definition
        /// </summary>
        public FormatDefinition CurrentFormat
        {
            get => _currentFormat;
            set
            {
                _currentFormat = value;
                OnPropertyChanged();
                UpdateFromFormat();
            }
        }

        /// <summary>
        /// Format name (e.g., "ZIP Archive", "PNG Image")
        /// </summary>
        public string FormatName
        {
            get => _formatName;
            set
            {
                _formatName = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Format description
        /// </summary>
        public string FormatDescription
        {
            get => _formatDescription;
            set
            {
                _formatDescription = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Format category (Archives, Images, Documents, etc.)
        /// </summary>
        public string FormatCategory
        {
            get => _formatCategory;
            set
            {
                _formatCategory = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// File extensions (.zip, .png, etc.)
        /// </summary>
        public List<string> Extensions
        {
            get => _extensions;
            set
            {
                _extensions = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ExtensionsDisplay));
            }
        }

        /// <summary>
        /// Display string for extensions
        /// </summary>
        public string ExtensionsDisplay => Extensions != null && Extensions.Any()
            ? string.Join(", ", Extensions)
            : "N/A";

        /// <summary>
        /// Software that can open this format
        /// </summary>
        public List<string> Software
        {
            get => _software;
            set
            {
                _software = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SoftwareDisplay));
            }
        }

        /// <summary>
        /// Display string for software
        /// </summary>
        public string SoftwareDisplay => Software != null && Software.Any()
            ? string.Join(", ", Software)
            : "N/A";

        /// <summary>
        /// Common use cases
        /// </summary>
        public List<string> UseCases
        {
            get => _useCases;
            set
            {
                _useCases = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(UseCasesDisplay));
            }
        }

        /// <summary>
        /// Display string for use cases
        /// </summary>
        public string UseCasesDisplay => UseCases != null && UseCases.Any()
            ? string.Join(", ", UseCases)
            : "N/A";

        /// <summary>
        /// MIME types
        /// </summary>
        public List<string> MimeTypes
        {
            get => _mimeTypes;
            set
            {
                _mimeTypes = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(MimeTypesDisplay));
            }
        }

        /// <summary>
        /// Display string for MIME types
        /// </summary>
        public string MimeTypesDisplay => MimeTypes != null && MimeTypes.Any()
            ? string.Join(", ", MimeTypes)
            : "N/A";

        /// <summary>
        /// Completeness score (0-100)
        /// </summary>
        public int CompletenessScore
        {
            get => _completenessScore;
            set
            {
                _completenessScore = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CompletenessScoreDisplay));
            }
        }

        /// <summary>
        /// Display string for completeness score with percentage
        /// </summary>
        public string CompletenessScoreDisplay => $"{CompletenessScore}%";

        /// <summary>
        /// Documentation level (basic, standard, detailed, comprehensive)
        /// </summary>
        public string DocumentationLevel
        {
            get => _documentationLevel;
            set
            {
                _documentationLevel = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Whether this is a priority format
        /// </summary>
        public bool IsPriorityFormat
        {
            get => _isPriorityFormat;
            set
            {
                _isPriorityFormat = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Technical details summary (varies by format type)
        /// </summary>
        public string TechnicalSummary
        {
            get
            {
                if (_currentFormat?.TechnicalDetails == null)
                    return "N/A";

                var details = _currentFormat.TechnicalDetails;
                var parts = new List<string>();

                if (!string.IsNullOrWhiteSpace(details.CompressionMethod))
                    parts.Add($"Compression: {details.CompressionMethod}");

                if (!string.IsNullOrWhiteSpace(details.Platform))
                    parts.Add($"Platform: {details.Platform}");

                if (!string.IsNullOrWhiteSpace(details.Container))
                    parts.Add($"Container: {details.Container}");

                if (details.BitDepth.HasValue)
                    parts.Add($"Bit Depth: {details.BitDepth}");

                if (!string.IsNullOrWhiteSpace(details.ColorSpace))
                    parts.Add($"Color Space: {details.ColorSpace}");

                if (details.SampleRate.HasValue)
                    parts.Add($"Sample Rate: {details.SampleRate} Hz");

                if (details.SupportsEncryption)
                    parts.Add("Encryption: Supported");

                return parts.Any() ? string.Join(", ", parts) : "N/A";
            }
        }

        /// <summary>
        /// Update all properties from the current format definition
        /// </summary>
        private void UpdateFromFormat()
        {
            if (_currentFormat == null)
            {
                ClearData();
                return;
            }

            FormatName = _currentFormat.FormatName ?? "Unknown";
            FormatDescription = _currentFormat.Description ?? "No description available";
            FormatCategory = _currentFormat.Category ?? "Other";
            Extensions = _currentFormat.Extensions ?? new List<string>();
            Software = _currentFormat.Software ?? new List<string>();
            UseCases = _currentFormat.UseCases ?? new List<string>();
            MimeTypes = _currentFormat.MimeTypes ?? new List<string>();

            if (_currentFormat.QualityMetrics != null)
            {
                CompletenessScore = _currentFormat.QualityMetrics.CompletenessScore;
                DocumentationLevel = _currentFormat.QualityMetrics.DocumentationLevel ?? "basic";
                IsPriorityFormat = _currentFormat.QualityMetrics.PriorityFormat;
            }
            else
            {
                CompletenessScore = 0;
                DocumentationLevel = "basic";
                IsPriorityFormat = false;
            }

            // Notify technical summary changed
            OnPropertyChanged(nameof(TechnicalSummary));
        }

        /// <summary>
        /// Clear all data
        /// </summary>
        public void ClearData()
        {
            FormatName = string.Empty;
            FormatDescription = string.Empty;
            FormatCategory = string.Empty;
            Extensions = new List<string>();
            Software = new List<string>();
            UseCases = new List<string>();
            MimeTypes = new List<string>();
            CompletenessScore = 0;
            DocumentationLevel = "basic";
            IsPriorityFormat = false;
            OnPropertyChanged(nameof(TechnicalSummary));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
