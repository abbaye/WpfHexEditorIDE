// ==========================================================
// Project: WpfHexEditor.Core
// File: EnrichedFormatViewModel.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6, Claude (Anthropic)
// Created: 2026-03-06
// Moved: 2026-03-26 (from WpfHexEditor.HexEditor to WpfHexEditor.Core)
// Description:
//     ViewModel providing enriched format detection information.
//     Wraps the FormatDetection result and exposes detected format name, confidence,
//     MIME type, description, and related format entries for UI binding.
//     Moved to Core so that any editor/plugin can use it without depending on HexEditor.
//
// Architecture Notes:
//     MVVM pattern â€” implements INotifyPropertyChanged manually.
//     Consumes WpfHexEditor.Core.FormatDetection output; no I/O performed here.
//
// ==========================================================

using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using WpfHexEditor.Core.FormatDetection;
using WpfHexEditor.Core.ViewModels;

namespace WpfHexEditor.Core.ViewModels
{
    /// <summary>
    /// ViewModel for displaying enriched format metadata.
    /// Presents format information, software, use cases, and technical details.
    /// </summary>
    public class EnrichedFormatViewModel : ViewModelBase
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

        /// <summary>Current format definition.</summary>
        public FormatDefinition CurrentFormat
        {
            get => _currentFormat;
            set
            {
                _currentFormat = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsAvailable));
                UpdateFromFormat();
            }
        }

        /// <summary>True when a format has been detected and enriched data is available.</summary>
        public bool IsAvailable => _currentFormat != null;

        public string FormatName
        {
            get => _formatName;
            set { _formatName = value; OnPropertyChanged(); }
        }

        public string FormatDescription
        {
            get => _formatDescription;
            set { _formatDescription = value; OnPropertyChanged(); }
        }

        public string FormatCategory
        {
            get => _formatCategory;
            set { _formatCategory = value; OnPropertyChanged(); }
        }

        public List<string> Extensions
        {
            get => _extensions;
            set { _extensions = value; OnPropertyChanged(); OnPropertyChanged(nameof(ExtensionsDisplay)); }
        }

        public string ExtensionsDisplay => Extensions != null && Extensions.Any()
            ? string.Join(", ", Extensions) : "N/A";

        public List<string> Software
        {
            get => _software;
            set { _software = value; OnPropertyChanged(); OnPropertyChanged(nameof(SoftwareDisplay)); }
        }

        public string SoftwareDisplay => Software != null && Software.Any()
            ? string.Join(", ", Software) : "N/A";

        public List<string> UseCases
        {
            get => _useCases;
            set { _useCases = value; OnPropertyChanged(); OnPropertyChanged(nameof(UseCasesDisplay)); }
        }

        public string UseCasesDisplay => UseCases != null && UseCases.Any()
            ? string.Join(", ", UseCases) : "N/A";

        public List<string> MimeTypes
        {
            get => _mimeTypes;
            set { _mimeTypes = value; OnPropertyChanged(); OnPropertyChanged(nameof(MimeTypesDisplay)); }
        }

        public string MimeTypesDisplay => MimeTypes != null && MimeTypes.Any()
            ? string.Join(", ", MimeTypes) : "N/A";

        public int CompletenessScore
        {
            get => _completenessScore;
            set { _completenessScore = value; OnPropertyChanged(); OnPropertyChanged(nameof(CompletenessScoreDisplay)); }
        }

        public string CompletenessScoreDisplay => $"{CompletenessScore}%";

        public string DocumentationLevel
        {
            get => _documentationLevel;
            set { _documentationLevel = value; OnPropertyChanged(); }
        }

        public bool IsPriorityFormat
        {
            get => _isPriorityFormat;
            set { _isPriorityFormat = value; OnPropertyChanged(); }
        }

        public List<string> Specifications
        {
            get => _specifications;
            set { _specifications = value; OnPropertyChanged(); OnPropertyChanged(nameof(SpecificationsDisplay)); OnPropertyChanged(nameof(HasSpecifications)); }
        }

        public string SpecificationsDisplay => Specifications != null && Specifications.Any()
            ? string.Join("\nâ€¢ ", new[] { "" }.Concat(Specifications)) : "N/A";

        public bool HasSpecifications => Specifications != null && Specifications.Any();

        public List<string> WebLinks
        {
            get => _webLinks;
            set { _webLinks = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasWebLinks)); }
        }

        public bool HasWebLinks => WebLinks != null && WebLinks.Any();
        public bool HasReferences => HasSpecifications || HasWebLinks;

        public string Version
        {
            get => _version;
            set { _version = value; OnPropertyChanged(); }
        }

        public string Author
        {
            get => _author;
            set { _author = value; OnPropertyChanged(); }
        }

        public List<string> RelatedFormats
        {
            get => _relatedFormats;
            set { _relatedFormats = value; OnPropertyChanged(); OnPropertyChanged(nameof(RelatedFormatsDisplay)); OnPropertyChanged(nameof(HasRelatedFormats)); }
        }

        public string RelatedFormatsDisplay => RelatedFormats != null && RelatedFormats.Any()
            ? string.Join(", ", RelatedFormats) : "N/A";

        public bool HasRelatedFormats => RelatedFormats != null && RelatedFormats.Any();
        public bool HasTechnicalDetails => !string.IsNullOrEmpty(TechnicalSummary) && TechnicalSummary != "N/A";

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
                if (!string.IsNullOrWhiteSpace(details.SampleRate))
                    parts.Add($"Sample Rate: {details.SampleRate}");
                if (details.SupportsEncryption)
                    parts.Add("Encryption: Supported");

                return parts.Any() ? string.Join(", ", parts) : "N/A";
            }
        }

        public string SignatureHex
        {
            get => _signatureHex;
            set { _signatureHex = value; OnPropertyChanged(); }
        }

        public string OffsetDisplay
        {
            get => _offsetDisplay;
            set { _offsetDisplay = value; OnPropertyChanged(); }
        }

        public bool IsRequired
        {
            get => _isRequired;
            set { _isRequired = value; OnPropertyChanged(); OnPropertyChanged(nameof(RequiredDisplay)); }
        }

        public string RequiredDisplay => IsRequired ? "\u2713 Yes" : "\u2717 No";

        public bool HasDetectionInfo => _currentFormat?.Detection != null &&
                                         !string.IsNullOrEmpty(_currentFormat.Detection.Signature);

        private string FormatSignature(string hex)
        {
            if (string.IsNullOrEmpty(hex))
                return "N/A";

            hex = hex.Replace("0x", "").Replace("0X", "");
            var formatted = string.Join(" ", System.Text.RegularExpressions.Regex.Split(hex, "(?<=\\G..)(?=.)"));
            return formatted.ToUpperInvariant();
        }

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
            Software = _currentFormat.Software?.Select(s => s.ToString()).ToList() ?? new List<string>();
            UseCases = _currentFormat.UseCases ?? new List<string>();
            MimeTypes = _currentFormat.MimeTypes ?? new List<string>();
            Version = _currentFormat.Version ?? string.Empty;
            Author = _currentFormat.Author ?? string.Empty;

            if (_currentFormat.FormatRelationships != null)
                RelatedFormats = _currentFormat.FormatRelationships.RelatedFormats ?? new List<string>();
            else
                RelatedFormats = new List<string>();

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

            OnPropertyChanged(nameof(HasReferences));

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

            OnPropertyChanged(nameof(TechnicalSummary));
            OnPropertyChanged(nameof(HasDetectionInfo));
        }

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


    }
}
