//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Format Selection Dialog for Ambiguous Detection
// Author: Derek Tremblay
// Contributors: Claude Sonnet 4.5, Claude Sonnet 4.6
//////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using WpfHexEditor.Core.FormatDetection;

namespace WpfHexEditor.Panels.FileOps
{
    /// <summary>
    /// Dialog for manual format selection when auto-detection is ambiguous
    /// </summary>
    public partial class FormatSelectionDialog : Window
    {
        /// <summary>
        /// The format candidate selected by the user
        /// </summary>
        public FormatMatchCandidate SelectedCandidate { get; private set; }

        /// <summary>
        /// List of candidates to display
        /// </summary>
        public List<FormatMatchCandidate> Candidates
        {
            get => CandidatesListView.ItemsSource as List<FormatMatchCandidate>;
            set
            {
                CandidatesListView.ItemsSource = value;
                if (value != null && value.Count > 0)
                {
                    // Auto-select the first (highest confidence) candidate
                    CandidatesListView.SelectedIndex = 0;
                }
            }
        }

        /// <summary>
        /// Custom message to display
        /// </summary>
        public string Message
        {
            get => MessageText.Text;
            set => MessageText.Text = value;
        }

        public FormatSelectionDialog()
        {
            InitializeComponent();
        }

        private void SelectButton_Click(object sender, RoutedEventArgs e)
        {
            SelectedCandidate = CandidatesListView.SelectedItem as FormatMatchCandidate;
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            SelectedCandidate = null;
            DialogResult = false;
            Close();
        }
    }

    /// <summary>
    /// Converter to display confidence score as percentage (0.0-1.0 → 0-100)
    /// </summary>
    public class ConfidenceToPercentageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double confidence)
                return confidence * 100.0;
            return 0.0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// Converter to check if object is not null
    /// </summary>
    public class NullToBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value != null;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
