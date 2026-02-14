//////////////////////////////////////////////
// Apache 2.0 - 2018-2019
// Author : Derek Tremblay (derektremblay666@gmail.com)
//////////////////////////////////////////////

using System.IO;
using System.Windows;

namespace WpfHexaEditor.Dialog
{
    /// <summary>
    /// Logique d'interaction pour FindWindow.xaml
    /// </summary>
    public partial class FindWindow
    {
        private MemoryStream _findMs = new(1);
        private readonly HexEditor _parentV1;
        private readonly HexEditorV2 _parentV2;

        /// <summary>
        /// Constructor accepting V1 HexEditor
        /// </summary>
        public FindWindow(HexEditor parent, byte[] findData = null)
        {
            InitializeComponent();

            //Parent hexeditor for "binding" search
            _parentV1 = parent;

            InitializeMStream(findData);
        }

        /// <summary>
        /// Constructor accepting V2 HexEditorV2 (Phase 13 - 100% compatibility)
        /// </summary>
        public FindWindow(HexEditorV2 parent, byte[] findData = null)
        {
            InitializeComponent();

            //Parent hexeditor for "binding" search
            _parentV2 = parent;

            InitializeMStream(findData);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
        private void ClearButton_Click(object sender, RoutedEventArgs e) => InitializeMStream();

        private void FindHexEdit_BytesDeleted(object sender, System.EventArgs e) =>
            InitializeMStream(FindHexEdit.GetAllBytes());

        private void FindAllButton_Click(object sender, RoutedEventArgs e)
        {
            if (_parentV1 != null)
                _parentV1.FindAll(FindHexEdit.GetAllBytes(), true);
            else if (_parentV2 != null)
                _parentV2.FindAll(FindHexEdit.GetAllBytes(), true);
        }

        private void FindFirstButton_Click(object sender, RoutedEventArgs e)
        {
            if (_parentV1 != null)
                _parentV1.FindFirst(FindHexEdit.GetAllBytes());
            else if (_parentV2 != null)
                _parentV2.FindFirst(FindHexEdit.GetAllBytes());
        }

        private void FindNextButton_Click(object sender, RoutedEventArgs e)
        {
            if (_parentV1 != null)
                _parentV1.FindNext(FindHexEdit.GetAllBytes());
            else if (_parentV2 != null)
                _parentV2.FindNext(FindHexEdit.GetAllBytes(), false);
        }

        private void FindLastButton_Click(object sender, RoutedEventArgs e)
        {
            if (_parentV1 != null)
                _parentV1.FindLast(FindHexEdit.GetAllBytes());
            else if (_parentV2 != null)
                _parentV2.FindLast(FindHexEdit.GetAllBytes(), false);
        }

        /// <summary>
        /// Initialize stream and hexeditor
        /// </summary>
        private void InitializeMStream(byte[] findData = null)
        {
            FindHexEdit.CloseProvider();

            _findMs = new MemoryStream(1);

            if (findData is not null && findData.Length > 0)
                foreach (var b in findData)
                    _findMs.WriteByte(b);
            else
                _findMs.WriteByte(0);

            FindHexEdit.Stream = _findMs;
        }
    }
}
