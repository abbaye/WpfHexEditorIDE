// ==========================================================
// Project: WpfHexEditor.Editor.DocumentEditor
// File: Controls/DocumentStatisticsDialog.xaml.cs
// Description:
//     Modal dialog showing word, character, block, and line counts.
//     Counts are computed synchronously from the block collection at open time.
// ==========================================================

using System.Collections.ObjectModel;
using System.Windows;
using WpfHexEditor.Editor.DocumentEditor.Core.Model;

namespace WpfHexEditor.Editor.DocumentEditor.Controls;

public partial class DocumentStatisticsDialog : Window
{
    public DocumentStatisticsDialog(ObservableCollection<DocumentBlock> blocks)
    {
        InitializeComponent();
        PopulateStats(blocks);
    }

    private void PopulateStats(ObservableCollection<DocumentBlock> blocks)
    {
        int wordCount       = 0;
        int charCount       = 0;
        int charNoSpace     = 0;
        int lineCount       = 0;

        foreach (var block in blocks)
        {
            var text = block.Text;
            charCount   += text.Length;
            charNoSpace += CountNonWhitespace(text);
            wordCount   += CountWords(text);
            lineCount   += CountLines(text);
        }

        TxtWordCount.Text       = wordCount.ToString("N0");
        TxtCharCount.Text       = charCount.ToString("N0");
        TxtCharNoSpaceCount.Text = charNoSpace.ToString("N0");
        TxtBlockCount.Text      = blocks.Count.ToString("N0");
        TxtLineCount.Text       = lineCount.ToString("N0");
    }

    private static int CountWords(string text)
    {
        bool inWord = false;
        int count   = 0;
        foreach (char c in text)
        {
            if (char.IsWhiteSpace(c))
            {
                inWord = false;
            }
            else if (!inWord)
            {
                inWord = true;
                count++;
            }
        }
        return count;
    }

    private static int CountNonWhitespace(string text)
    {
        int count = 0;
        foreach (char c in text)
            if (!char.IsWhiteSpace(c)) count++;
        return count;
    }

    private static int CountLines(string text)
    {
        if (text.Length == 0) return 1;
        int count = 1;
        foreach (char c in text)
            if (c == '\n') count++;
        return count;
    }

    private void OnCloseClicked(object sender, RoutedEventArgs e) => Close();
}
