// ==========================================================
// Project: WpfHexEditor.Plugins.FormatInfo
// File: EnrichedFormatInfoPanel.xaml.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-06
// Description:
//     Enriched format metadata panel migrated from Panels.BinaryAnalysis.
//     Displays format name, category, quality score, extensions, MIME, software,
//     use cases, references, technical details, and detection info.
// ==========================================================

using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using WpfHexEditor.Core.FormatDetection;
using WpfHexEditor.HexEditor.ViewModels;

namespace WpfHexEditor.Plugins.FormatInfo.Views;

/// <summary>
/// Panel for displaying enriched format metadata.
/// </summary>
public partial class EnrichedFormatInfoPanel : UserControl
{
    private readonly EnrichedFormatViewModel _viewModel;

    public EnrichedFormatInfoPanel()
    {
        InitializeComponent();
        _viewModel = new EnrichedFormatViewModel();
        ShowNoFormatMessage();
    }

    // -- Public API -----------------------------------------------------------

    /// <summary>Sets the format to display.</summary>
    public void SetFormat(FormatDefinition? format)
    {
        if (format is null)
        {
            _viewModel.ClearData();
            ShowNoFormatMessage();
            return;
        }

        _viewModel.CurrentFormat = format;
        UpdateUI();
        ShowFormatInformation();
    }

    /// <summary>Clears the current format.</summary>
    public void ClearFormat()
    {
        _viewModel.ClearData();
        ShowNoFormatMessage();
    }

    // -- Private helpers ------------------------------------------------------

    private void UpdateUI()
    {
        FormatNameTextBlock.Text       = _viewModel.FormatName;
        CategoryTextBlock.Text         = _viewModel.FormatCategory;
        DescriptionTextBlock.Text      = _viewModel.FormatDescription;
        ExtensionsTextBlock.Text       = _viewModel.ExtensionsDisplay;
        MimeTypesTextBlock.Text        = _viewModel.MimeTypesDisplay;
        SoftwareTextBlock.Text         = _viewModel.SoftwareDisplay;
        UseCasesTextBlock.Text         = _viewModel.UseCasesDisplay;
        QualityScoreText.Text          = _viewModel.CompletenessScoreDisplay;
        DocumentationLevelTextBlock.Text = CapitalizeFirst(_viewModel.DocumentationLevel);
        TechnicalDetailsTextBlock.Text = _viewModel.TechnicalSummary;
        RelatedFormatsTextBlock.Text   = _viewModel.RelatedFormatsDisplay;
        VersionTextBlock.Text          = string.IsNullOrEmpty(_viewModel.Version) ? "N/A" : _viewModel.Version;
        AuthorTextBlock.Text           = string.IsNullOrEmpty(_viewModel.Author)  ? "N/A" : _viewModel.Author;
        SignatureHexTextBlock.Text     = _viewModel.SignatureHex;
        OffsetDisplayTextBlock.Text    = _viewModel.OffsetDisplay;
        RequiredDisplayTextBlock.Text  = _viewModel.RequiredDisplay;

        UpdateQualityScoreBar();
        UpdatePriorityBadge();
        UpdateReferencesCard();
        UpdateCardVisibility();
    }

    private static string CapitalizeFirst(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        return char.ToUpper(text[0]) + text[1..].ToLower();
    }

    private void UpdatePriorityBadge()
    {
        PriorityBadge.Visibility = _viewModel.IsPriorityFormat ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateReferencesCard()
    {
        bool hasRefs = _viewModel.HasReferences;

        SpecificationsSection.Visibility = _viewModel.HasSpecifications ? Visibility.Visible : Visibility.Collapsed;
        WebLinksSection.Visibility       = _viewModel.HasWebLinks        ? Visibility.Visible : Visibility.Collapsed;

        if (_viewModel.HasSpecifications)
            SpecificationsTextBlock.Text = _viewModel.SpecificationsDisplay;

        WebLinksStackPanel.Children.Clear();
        if (_viewModel.HasWebLinks && _viewModel.WebLinks != null)
        {
            foreach (var link in _viewModel.WebLinks)
            {
                var block = new System.Windows.Controls.TextBlock { Margin = new Thickness(0, 0, 0, 4), FontSize = 10 };
                var hyperlink = new System.Windows.Documents.Hyperlink
                {
                    NavigateUri = new Uri(link),
                    ToolTip     = link,
                    Foreground  = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x21, 0x96, 0xF3)),
                    Cursor      = System.Windows.Input.Cursors.Hand
                };
                hyperlink.Inlines.Add("\U0001F517 " + GetLinkDisplayName(link));
                hyperlink.RequestNavigate += (_, e) =>
                {
                    try   { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = e.Uri.ToString(), UseShellExecute = true }); }
                    catch { /* silently ignore */ }
                    e.Handled = true;
                };
                block.Inlines.Add(hyperlink);
                WebLinksStackPanel.Children.Add(block);
            }
        }

        NoReferencesTextBlock.Visibility = hasRefs ? Visibility.Collapsed : Visibility.Visible;
        SpecificationsSection.Visibility = hasRefs && _viewModel.HasSpecifications ? Visibility.Visible : Visibility.Collapsed;
        WebLinksSection.Visibility       = hasRefs && _viewModel.HasWebLinks        ? Visibility.Visible : Visibility.Collapsed;
    }

    private static string GetLinkDisplayName(string url)
    {
        try
        {
            var uri     = new Uri(url);
            var domain  = uri.Host.Replace("www.", "");
            var path    = uri.AbsolutePath.TrimEnd('/');
            if (!string.IsNullOrEmpty(path) && path != "/")
            {
                var seg = path.Split('/').Last();
                if (!string.IsNullOrEmpty(seg))
                {
                    var noExt = System.IO.Path.GetFileNameWithoutExtension(seg);
                    if (!string.IsNullOrEmpty(noExt)) return $"{domain} - {noExt}";
                }
            }
            return domain;
        }
        catch
        {
            return url.Length > 50 ? url[..47] + "..." : url;
        }
    }

    private void UpdateQualityScoreBar()
    {
        if (ActualWidth > 0)
            QualityScoreBar.Width = (ActualWidth - 80) * (_viewModel.CompletenessScore / 100.0) * 0.5;
    }

    private void UpdateCardVisibility()
    {
        TechnicalDetailsCard.Visibility = _viewModel.HasTechnicalDetails ? Visibility.Visible : Visibility.Collapsed;
        RelatedFormatsCard.Visibility   = _viewModel.HasRelatedFormats    ? Visibility.Visible : Visibility.Collapsed;

        bool hasInfo = !string.IsNullOrEmpty(_viewModel.Version) || !string.IsNullOrEmpty(_viewModel.Author);
        AuthorVersionCard.Visibility    = hasInfo                          ? Visibility.Visible : Visibility.Collapsed;
        DetectionInfoCard.Visibility    = _viewModel.HasDetectionInfo      ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ShowNoFormatMessage()
    {
        NoFormatMessage.Visibility   = Visibility.Visible;
        HeaderCard.Visibility        = Visibility.Collapsed;
        ExtensionsCard.Visibility    = Visibility.Collapsed;
        MimeTypesCard.Visibility     = Visibility.Collapsed;
        SoftwareCard.Visibility      = Visibility.Collapsed;
        UseCasesCard.Visibility      = Visibility.Collapsed;
        ReferencesCard.Visibility    = Visibility.Collapsed;
        TechnicalDetailsCard.Visibility = Visibility.Collapsed;
        RelatedFormatsCard.Visibility   = Visibility.Collapsed;
        AuthorVersionCard.Visibility    = Visibility.Collapsed;
        DetectionInfoCard.Visibility    = Visibility.Collapsed;
    }

    private void ShowFormatInformation()
    {
        NoFormatMessage.Visibility = Visibility.Collapsed;
        HeaderCard.Visibility      = Visibility.Visible;
        ExtensionsCard.Visibility  = Visibility.Visible;
        MimeTypesCard.Visibility   = Visibility.Visible;
        SoftwareCard.Visibility    = Visibility.Visible;
        UseCasesCard.Visibility    = Visibility.Visible;
        ReferencesCard.Visibility  = Visibility.Visible;
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        if (sizeInfo.WidthChanged) UpdateQualityScoreBar();
    }
}
