//////////////////////////////////////////////
// Apache 2.0 - 2018-2019
// Author : Derek Tremblay (derektremblay666@gmail.com)
//////////////////////////////////////////////

using System.IO;
using System.Windows;

namespace WpfHexaEditor.Dialog
{
    /// <summary>
    /// Logique d'interaction pour FindReplaceWindow.xaml
    /// </summary>
    public partial class FindReplaceWindow
    {
        private readonly HexEditor _parentV1;
        private readonly HexEditorV2 _parentV2;

        /// <summary>
        /// Constructor accepting V1 HexEditor
        /// </summary>
        public FindReplaceWindow(HexEditor parent, byte[] findData = null)
        {
            InitializeComponent();

            //Parent hexeditor for "binding" search
            _parentV1 = parent;

            InitializeMStream(FindHexEdit, findData);
            InitializeMStream(ReplaceHexEdit);
        }

        /// <summary>
        /// Constructor accepting V2 HexEditorV2 (Phase 13 - 100% compatibility)
        /// </summary>
        public FindReplaceWindow(HexEditorV2 parent, byte[] findData = null)
        {
            InitializeComponent();

            //Parent hexeditor for "binding" search
            _parentV2 = parent;

            InitializeMStream(FindHexEdit, findData);
            InitializeMStream(ReplaceHexEdit);
        }

        #region Events
        private void ClearButton_Click(object sender, RoutedEventArgs e) => InitializeMStream(FindHexEdit);
        private void ClearReplaceButton_Click(object sender, RoutedEventArgs e) => InitializeMStream(ReplaceHexEdit);
        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

        private void FindAllButton_Click(object sender, RoutedEventArgs e)
        {
            if (_parentV1 != null)
                _parentV1.FindAll(FindHexEdit.GetAllBytes(), HighlightMenuItem.IsChecked);
            else if (_parentV2 != null)
                _parentV2.FindAll(FindHexEdit.GetAllBytes(), HighlightMenuItem.IsChecked);
        }

        private void FindFirstButton_Click(object sender, RoutedEventArgs e)
        {
            if (_parentV1 != null)
                _parentV1.FindFirst(FindHexEdit.GetAllBytes(), 0, HighlightMenuItem.IsChecked);
            else if (_parentV2 != null)
                _parentV2.FindFirst(FindHexEdit.GetAllBytes(), 0, HighlightMenuItem.IsChecked);
        }

        private void FindNextButton_Click(object sender, RoutedEventArgs e)
        {
            if (_parentV1 != null)
                _parentV1.FindNext(FindHexEdit.GetAllBytes(), HighlightMenuItem.IsChecked);
            else if (_parentV2 != null)
                _parentV2.FindNext(FindHexEdit.GetAllBytes(), HighlightMenuItem.IsChecked);
        }

        private void FindLastButton_Click(object sender, RoutedEventArgs e)
        {
            if (_parentV1 != null)
                _parentV1.FindLast(FindHexEdit.GetAllBytes(), HighlightMenuItem.IsChecked);
            else if (_parentV2 != null)
                _parentV2.FindLast(FindHexEdit.GetAllBytes(), HighlightMenuItem.IsChecked);
        }

        private void ReplaceButton_Click(object sender, RoutedEventArgs e)
        {
            if (_parentV1 != null)
                _parentV1.ReplaceFirst(FindHexEdit.GetAllBytes(), ReplaceHexEdit.GetAllBytes(),
                    TrimMenuItem.IsChecked, HighlightMenuItem.IsChecked);
            else if (_parentV2 != null)
                _parentV2.ReplaceFirst(FindHexEdit.GetAllBytes(), ReplaceHexEdit.GetAllBytes(),
                    TrimMenuItem.IsChecked, HighlightMenuItem.IsChecked);
        }

        private void ReplaceNextButton_Click(object sender, RoutedEventArgs e)
        {
            if (_parentV1 != null)
                _parentV1.ReplaceNext(FindHexEdit.GetAllBytes(), ReplaceHexEdit.GetAllBytes(),
                   TrimMenuItem.IsChecked, HighlightMenuItem.IsChecked);
            else if (_parentV2 != null)
                _parentV2.ReplaceNext(FindHexEdit.GetAllBytes(), ReplaceHexEdit.GetAllBytes(),
                   TrimMenuItem.IsChecked, HighlightMenuItem.IsChecked);
        }

        private void ReplaceAllButton_Click(object sender, RoutedEventArgs e)
        {
            if (_parentV1 != null)
                _parentV1.ReplaceAll(FindHexEdit.GetAllBytes(), ReplaceHexEdit.GetAllBytes(),
                    TrimMenuItem.IsChecked, HighlightMenuItem.IsChecked);
            else if (_parentV2 != null)
                _parentV2.ReplaceAll(FindHexEdit.GetAllBytes(), ReplaceHexEdit.GetAllBytes(),
                    TrimMenuItem.IsChecked, HighlightMenuItem.IsChecked);
        }

        private void ReplaceHexEdit_BytesDeleted(object sender, System.EventArgs e) =>
            InitializeMStream(ReplaceHexEdit, ReplaceHexEdit.GetAllBytes());

        private void FindHexEdit_BytesDeleted(object sender, System.EventArgs e) =>
            InitializeMStream(FindHexEdit, FindHexEdit.GetAllBytes());

        private void SettingButton_Click(object sender, RoutedEventArgs e) => SettingPopup.IsOpen = true;

        private void SettingMenuItem_Click(object sender, RoutedEventArgs e) => SettingPopup.IsOpen = false;
        #endregion

        #region Methods
        /// <summary>
        /// Initialize stream and hexeditor
        /// </summary>
        private void InitializeMStream(HexEditor hexeditor, byte[] findData = null)
        {
            hexeditor.CloseProvider();

            var ms = new MemoryStream(1);

            if (findData is not null && findData.Length > 0)
                foreach (var b in findData)
                    ms.WriteByte(b);
            else
                ms.WriteByte(0);

            hexeditor.Stream = ms;
        }
        #endregion
    }
}
