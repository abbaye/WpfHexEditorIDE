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
        int wordCount   = 0;
        int charCount   = 0;
        int charNoSpace = 0;
        int lineCount   = 0;

        foreach (var block in blocks)
        {
            var (w, cns, lc) = CountTextStats(block.Text);
            charCount   += block.Text.Length;
            charNoSpace += cns;
            wordCount   += w;
            lineCount   += lc;
        }

        TxtWordCount.Text        = wordCount.ToString("N0");
        TxtCharCount.Text        = charCount.ToString("N0");
        TxtCharNoSpaceCount.Text = charNoSpace.ToString("N0");
        TxtBlockCount.Text       = blocks.Count.ToString("N0");
        TxtLineCount.Text        = lineCount.ToString("N0");
    }

    private static (int words, int charsNoSpace, int lines) CountTextStats(string text)
    {
        int words = 0, charsNoSpace = 0, lines = text.Length > 0 ? 1 : 0;
        bool inWord = false;
        foreach (char c in text)
        {
            if (c == '\n') { lines++; inWord = false; }
            if (char.IsWhiteSpace(c)) { inWord = false; }
            else
            {
                charsNoSpace++;
                if (!inWord) { inWord = true; words++; }
            }
        }
        return (words, charsNoSpace, lines);
    }

    private void OnCloseClicked(object sender, RoutedEventArgs e) => Close();
}
