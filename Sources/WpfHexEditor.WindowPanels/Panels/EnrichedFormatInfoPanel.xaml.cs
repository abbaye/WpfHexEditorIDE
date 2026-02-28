//////////////////////////////////////////////
// Apache 2.0  - 2026
// Enriched Format Info Panel
// Author : Claude Sonnet 4.5
//////////////////////////////////////////////

using System.Linq;
using System.Windows;
using System.Windows.Controls;
using WpfHexEditor.Core.FormatDetection;
using WpfHexEditor.HexEditor.ViewModels;

namespace WpfHexEditor.WindowPanels.Panels
{
    /// <summary>
    /// Panel for displaying enriched format metadata
    /// Shows format information, software, use cases, and technical details
    /// </summary>
    public partial class EnrichedFormatInfoPanel : UserControl
    {
        private readonly EnrichedFormatViewModel _viewModel;

        public EnrichedFormatInfoPanel()
        {
            InitializeComponent();

            _viewModel = new EnrichedFormatViewModel();

            // Initialize with no format
            ShowNoFormatMessage();
        }

        /// <summary>
        /// Set the format to display
        /// </summary>
        public void SetFormat(FormatDefinition format)
        {
            if (format == null)
            {
                _viewModel.ClearData();
                ShowNoFormatMessage();
                return;
            }

            _viewModel.CurrentFormat = format;
            UpdateUI();
            ShowFormatInformation();
        }

        /// <summary>
        /// Clear the current format
        /// </summary>
        public void ClearFormat()
        {
            _viewModel.ClearData();
            ShowNoFormatMessage();
        }

        /// <summary>
        /// Update UI elements from ViewModel
        /// </summary>
        private void UpdateUI()
        {
            // Update text blocks
            FormatNameTextBlock.Text = _viewModel.FormatName;
            CategoryTextBlock.Text = _viewModel.FormatCategory;
            DescriptionTextBlock.Text = _viewModel.FormatDescription;
            ExtensionsTextBlock.Text = _viewModel.ExtensionsDisplay;
            MimeTypesTextBlock.Text = _viewModel.MimeTypesDisplay;
            SoftwareTextBlock.Text = _viewModel.SoftwareDisplay;
            UseCasesTextBlock.Text = _viewModel.UseCasesDisplay;
            QualityScoreText.Text = _viewModel.CompletenessScoreDisplay;
            DocumentationLevelTextBlock.Text = CapitalizeFirst(_viewModel.DocumentationLevel);

            // Update new cards
            TechnicalDetailsTextBlock.Text = _viewModel.TechnicalSummary;
            RelatedFormatsTextBlock.Text = _viewModel.RelatedFormatsDisplay;
            VersionTextBlock.Text = string.IsNullOrEmpty(_viewModel.Version) ? "N/A" : _viewModel.Version;
            AuthorTextBlock.Text = string.IsNullOrEmpty(_viewModel.Author) ? "N/A" : _viewModel.Author;

            // Update detection info card
            SignatureHexTextBlock.Text = _viewModel.SignatureHex;
            OffsetDisplayTextBlock.Text = _viewModel.OffsetDisplay;
            RequiredDisplayTextBlock.Text = _viewModel.RequiredDisplay;

            // Update quality score bar
            UpdateQualityScoreBar();

            // Update priority badge visibility
            UpdatePriorityBadge();

            // Update references card
            UpdateReferencesCard();

            // Update card visibility
            UpdateCardVisibility();
        }

        /// <summary>
        /// Capitalize first letter of a string
        /// </summary>
        private string CapitalizeFirst(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            return char.ToUpper(text[0]) + text.Substring(1).ToLower();
        }

        /// <summary>
        /// Update priority badge visibility
        /// </summary>
        private void UpdatePriorityBadge()
        {
            if (PriorityBadge != null)
            {
                PriorityBadge.Visibility = _viewModel.IsPriorityFormat
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
        }

        /// <summary>
        /// Update the references card with specifications and web links
        /// </summary>
        private void UpdateReferencesCard()
        {
            if (SpecificationsTextBlock == null || WebLinksStackPanel == null || NoReferencesTextBlock == null)
                return;

            bool hasReferences = _viewModel.HasReferences;

            // Show/hide sections based on available data
            if (SpecificationsSection != null)
                SpecificationsSection.Visibility = _viewModel.HasSpecifications ? Visibility.Visible : Visibility.Collapsed;

            if (WebLinksSection != null)
                WebLinksSection.Visibility = _viewModel.HasWebLinks ? Visibility.Visible : Visibility.Collapsed;

            // Update specifications
            if (_viewModel.HasSpecifications)
            {
                SpecificationsTextBlock.Text = _viewModel.SpecificationsDisplay;
            }

            // Update web links
            WebLinksStackPanel.Children.Clear();
            if (_viewModel.HasWebLinks && _viewModel.WebLinks != null)
            {
                foreach (var link in _viewModel.WebLinks)
                {
                    var hyperlinkBlock = new System.Windows.Controls.TextBlock
                    {
                        Margin = new Thickness(0, 0, 0, 4),
                        FontSize = 10
                    };

                    var hyperlink = new System.Windows.Documents.Hyperlink
                    {
                        NavigateUri = new System.Uri(link),
                        ToolTip = link,
                        Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x21, 0x96, 0xF3)),
                        Cursor = System.Windows.Input.Cursors.Hand
                    };
                    hyperlink.Inlines.Add("🔗 " + GetLinkDisplayName(link));
                    hyperlink.RequestNavigate += (s, e) =>
                    {
                        try
                        {
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = e.Uri.ToString(),
                                UseShellExecute = true
                            });
                            e.Handled = true;
                        }
                        catch (System.Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error opening link: {ex.Message}");
                            System.Windows.MessageBox.Show(
                                $"Could not open link:\n{e.Uri}\n\nError: {ex.Message}",
                                "Error",
                                System.Windows.MessageBoxButton.OK,
                                System.Windows.MessageBoxImage.Warning);
                            e.Handled = true;
                        }
                    };

                    hyperlinkBlock.Inlines.Add(hyperlink);
                    WebLinksStackPanel.Children.Add(hyperlinkBlock);
                }
            }

            // Show "no references" message if no data
            NoReferencesTextBlock.Visibility = hasReferences ? Visibility.Collapsed : Visibility.Visible;
            SpecificationsSection.Visibility = hasReferences && _viewModel.HasSpecifications ? Visibility.Visible : Visibility.Collapsed;
            WebLinksSection.Visibility = hasReferences && _viewModel.HasWebLinks ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>
        /// Get a friendly display name from a URL
        /// </summary>
        private string GetLinkDisplayName(string url)
        {
            if (string.IsNullOrEmpty(url))
                return "Link";

            try
            {
                var uri = new System.Uri(url);
                var domain = uri.Host.Replace("www.", "");

                // Extract meaningful part from path
                var path = uri.AbsolutePath.TrimEnd('/');
                if (!string.IsNullOrEmpty(path) && path != "/")
                {
                    var lastSegment = path.Split('/').Last();
                    if (!string.IsNullOrEmpty(lastSegment))
                    {
                        // Remove file extension if present
                        var withoutExt = System.IO.Path.GetFileNameWithoutExtension(lastSegment);
                        if (!string.IsNullOrEmpty(withoutExt))
                            return $"{domain} - {withoutExt}";
                    }
                }

                return domain;
            }
            catch
            {
                return url.Length > 50 ? url.Substring(0, 47) + "..." : url;
            }
        }

        /// <summary>
        /// Update the quality score bar width
        /// </summary>
        private void UpdateQualityScoreBar()
        {
            if (QualityScoreBar != null && ActualWidth > 0)
            {
                var percentage = _viewModel.CompletenessScore / 100.0;
                QualityScoreBar.Width = (ActualWidth - 80) * percentage * 0.5; // Rough estimate
            }
        }

        /// <summary>
        /// Update card visibility based on available data
        /// </summary>
        private void UpdateCardVisibility()
        {
            // Hide cards if they have no data
            if (TechnicalDetailsCard != null)
                TechnicalDetailsCard.Visibility = _viewModel.HasTechnicalDetails ? Visibility.Visible : Visibility.Collapsed;

            if (RelatedFormatsCard != null)
                RelatedFormatsCard.Visibility = _viewModel.HasRelatedFormats ? Visibility.Visible : Visibility.Collapsed;

            if (AuthorVersionCard != null)
            {
                bool hasInfo = !string.IsNullOrEmpty(_viewModel.Version) || !string.IsNullOrEmpty(_viewModel.Author);
                AuthorVersionCard.Visibility = hasInfo ? Visibility.Visible : Visibility.Collapsed;
            }

            if (DetectionInfoCard != null)
                DetectionInfoCard.Visibility = _viewModel.HasDetectionInfo ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>
        /// Show the "no format selected" message
        /// </summary>
        private void ShowNoFormatMessage()
        {
            if (NoFormatMessage != null)
            {
                NoFormatMessage.Visibility = Visibility.Visible;

                // Hide all cards
                if (HeaderCard != null) HeaderCard.Visibility = Visibility.Collapsed;
                if (ExtensionsCard != null) ExtensionsCard.Visibility = Visibility.Collapsed;
                if (MimeTypesCard != null) MimeTypesCard.Visibility = Visibility.Collapsed;
                if (SoftwareCard != null) SoftwareCard.Visibility = Visibility.Collapsed;
                if (UseCasesCard != null) UseCasesCard.Visibility = Visibility.Collapsed;
                if (ReferencesCard != null) ReferencesCard.Visibility = Visibility.Collapsed;
                if (TechnicalDetailsCard != null) TechnicalDetailsCard.Visibility = Visibility.Collapsed;
                if (RelatedFormatsCard != null) RelatedFormatsCard.Visibility = Visibility.Collapsed;
                if (AuthorVersionCard != null) AuthorVersionCard.Visibility = Visibility.Collapsed;
                if (DetectionInfoCard != null) DetectionInfoCard.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// Show format information cards
        /// </summary>
        private void ShowFormatInformation()
        {
            if (NoFormatMessage != null)
            {
                NoFormatMessage.Visibility = Visibility.Collapsed;

                // Show all cards (except new cards which are conditionally shown)
                if (HeaderCard != null) HeaderCard.Visibility = Visibility.Visible;
                if (ExtensionsCard != null) ExtensionsCard.Visibility = Visibility.Visible;
                if (MimeTypesCard != null) MimeTypesCard.Visibility = Visibility.Visible;
                if (SoftwareCard != null) SoftwareCard.Visibility = Visibility.Visible;
                if (UseCasesCard != null) UseCasesCard.Visibility = Visibility.Visible;
                if (ReferencesCard != null) ReferencesCard.Visibility = Visibility.Visible;
                // TechnicalDetailsCard, RelatedFormatsCard, AuthorVersionCard, DetectionInfoCard visibility handled by UpdateCardVisibility()
            }
        }

        /// <summary>
        /// Handle size changes to update quality bar
        /// </summary>
        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);

            if (sizeInfo.WidthChanged)
            {
                UpdateQualityScoreBar();
            }
        }
    }
}
