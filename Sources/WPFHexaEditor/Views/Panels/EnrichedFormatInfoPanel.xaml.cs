//////////////////////////////////////////////
// Apache 2.0  - 2026
// Enriched Format Info Panel
// Author : Claude Sonnet 4.5
//////////////////////////////////////////////

using System.Windows;
using System.Windows.Controls;
using WpfHexaEditor.Core.FormatDetection;
using WpfHexaEditor.ViewModels;

namespace WpfHexaEditor.Views.Panels
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

            // Update quality score bar
            UpdateQualityScoreBar();
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

                // Show all cards
                if (HeaderCard != null) HeaderCard.Visibility = Visibility.Visible;
                if (ExtensionsCard != null) ExtensionsCard.Visibility = Visibility.Visible;
                if (MimeTypesCard != null) MimeTypesCard.Visibility = Visibility.Visible;
                if (SoftwareCard != null) SoftwareCard.Visibility = Visibility.Visible;
                if (UseCasesCard != null) UseCasesCard.Visibility = Visibility.Visible;
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
