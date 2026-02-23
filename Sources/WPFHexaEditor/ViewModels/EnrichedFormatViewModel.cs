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
        private List<string> _specifications;
        private List<string> _webLinks;
        private string _version;
        private string _author;
        private List<string> _relatedFormats;
        private string _signatureHex;
        private string _offsetDisplay;
        private bool _isRequired;

        public EnrichedFormatViewModel()
        {
            _extensions = new List<string>();
            _software = new List<string>();
            _useCases = new List<string>();
            _mimeTypes = new List<string>();
            _specifications = new List<string>();
            _webLinks = new List<string>();
            _relatedFormats = new List<string>();
            _documentationLevel = "basic";
            _version = string.Empty;
            _author = string.Empty;
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
        /// Technical specifications for this format
        /// </summary>
        public List<string> Specifications
        {
            get => _specifications;
            set
            {
                _specifications = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SpecificationsDisplay));
                OnPropertyChanged(nameof(HasSpecifications));
            }
        }

        /// <summary>
        /// Display string for specifications
        /// </summary>
        public string SpecificationsDisplay => Specifications != null && Specifications.Any()
            ? string.Join("\n• ", new[] { "" }.Concat(Specifications))
            : "N/A";

        /// <summary>
        /// Whether this format has specifications
        /// </summary>
        public bool HasSpecifications => Specifications != null && Specifications.Any();

        /// <summary>
        /// Web links to documentation
        /// </summary>
        public List<string> WebLinks
        {
            get => _webLinks;
            set
            {
                _webLinks = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasWebLinks));
            }
        }

        /// <summary>
        /// Whether this format has web links
        /// </summary>
        public bool HasWebLinks => WebLinks != null && WebLinks.Any();

        /// <summary>
        /// Whether this format has any references (specs or links)
        /// </summary>
        public bool HasReferences => HasSpecifications || HasWebLinks;

        /// <summary>
        /// Format definition version
        /// </summary>
        public string Version
        {
            get => _version;
            set
            {
                _version = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Author of the format definition
        /// </summary>
        public string Author
        {
            get => _author;
            set
            {
                _author = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Related formats (variants, versions, compatible formats)
        /// </summary>
        public List<string> RelatedFormats
        {
            get => _relatedFormats;
            set
            {
                _relatedFormats = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(RelatedFormatsDisplay));
                OnPropertyChanged(nameof(HasRelatedFormats));
            }
        }

        /// <summary>
        /// Display string for related formats
        /// </summary>
        public string RelatedFormatsDisplay => RelatedFormats != null && RelatedFormats.Any()
            ? string.Join(", ", RelatedFormats)
            : "N/A";

        /// <summary>
        /// Whether this format has related formats
        /// </summary>
        public bool HasRelatedFormats => RelatedFormats != null && RelatedFormats.Any();

        /// <summary>
        /// Whether TechnicalSummary has content
        /// </summary>
        public bool HasTechnicalDetails => !string.IsNullOrEmpty(TechnicalSummary) && TechnicalSummary != "N/A";

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
        /// Signature hex string (formatted with spaces)
        /// </summary>
        public string SignatureHex
        {
            get => _signatureHex;
            set
            {
                _signatureHex = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Offset display string (hex and decimal)
        /// </summary>
        public string OffsetDisplay
        {
            get => _offsetDisplay;
            set
            {
                _offsetDisplay = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Whether the signature is required for detection
        /// </summary>
        public bool IsRequired
        {
            get => _isRequired;
            set
            {
                _isRequired = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(RequiredDisplay));
            }
        }

        /// <summary>
        /// Display string for required flag
        /// </summary>
        public string RequiredDisplay => IsRequired ? "✓ Yes" : "✗ No";

        /// <summary>
        /// Whether detection info is available
        /// </summary>
        public bool HasDetectionInfo => _currentFormat?.Detection != null &&
                                         !string.IsNullOrEmpty(_currentFormat.Detection.Signature);

        /// <summary>
        /// Format a hex signature string with spaces for readability
        /// </summary>
        private string FormatSignature(string hex)
        {
            if (string.IsNullOrEmpty(hex))
                return "N/A";

            // Remove 0x prefix if present
            hex = hex.Replace("0x", "").Replace("0X", "");

            // Insert space every 2 characters
            var formatted = string.Join(" ", System.Text.RegularExpressions.Regex.Split(hex, "(?<=\\G..)(?=.)"));
            return formatted.ToUpperInvariant();
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
            Version = _currentFormat.Version ?? string.Empty;
            Author = _currentFormat.Author ?? string.Empty;

            // Extract related formats
            if (_currentFormat.FormatRelationships != null)
            {
                RelatedFormats = _currentFormat.FormatRelationships.RelatedFormats ?? new List<string>();
            }
            else
            {
                RelatedFormats = new List<string>();
            }

            // Extract references
            if (_currentFormat.References != null)
            {
                Specifications = _currentFormat.References.Specifications ?? new List<string>();
                WebLinks = _currentFormat.References.WebLinks ?? new List<string>();
            }
            else
            {
                Specifications = new List<string>();
                WebLinks = new List<string>();
            }

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

            // Extract detection info
            if (_currentFormat.Detection != null)
            {
                SignatureHex = FormatSignature(_currentFormat.Detection.Signature);
                OffsetDisplay = $"0x{_currentFormat.Detection.Offset:X8} ({_currentFormat.Detection.Offset} bytes)";
                IsRequired = _currentFormat.Detection.Required;
            }
            else
            {
                SignatureHex = "N/A";
                OffsetDisplay = "N/A";
                IsRequired = false;
            }

            // Notify technical summary and detection changed
            OnPropertyChanged(nameof(TechnicalSummary));
            OnPropertyChanged(nameof(HasDetectionInfo));
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
            Specifications = new List<string>();
            WebLinks = new List<string>();
            RelatedFormats = new List<string>();
            Version = string.Empty;
            Author = string.Empty;
            CompletenessScore = 0;
            DocumentationLevel = "basic";
            IsPriorityFormat = false;
            SignatureHex = "N/A";
            OffsetDisplay = "N/A";
            IsRequired = false;
            OnPropertyChanged(nameof(TechnicalSummary));
            OnPropertyChanged(nameof(HasTechnicalDetails));
            OnPropertyChanged(nameof(HasDetectionInfo));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
