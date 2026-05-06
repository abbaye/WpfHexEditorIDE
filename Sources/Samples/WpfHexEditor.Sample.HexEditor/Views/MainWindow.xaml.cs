// WpfHexEditor.Sample.HexEditor — MainWindow.xaml.cs

using System;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using WpfHexEditor.Core.Models;
using WpfHexEditor.Sample.HexEditor.ViewModels;

namespace WpfHexEditor.Sample.HexEditor.Views
{
    public partial class MainWindow : Window
    {
        private readonly MainWindowViewModel _vm;

        // Convenience accessor to the primary HexEditor inside the split host
        private WpfHexEditor.HexEditor.HexEditor HexEditor => HexEditorHost.PrimaryEditor;

        public MainWindow()
        {
            InitializeComponent();

            _vm = new MainWindowViewModel();
            DataContext = _vm;

            _vm.OpenFileRequested += (_, path) => OpenFile(path);
            _vm.SaveFileRequested += (_, _)    => HexEditor.SubmitChanges();

            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Wire HexEditor to ViewModel
            _vm.SetHexEditor(HexEditor);

            // Wire HexEditor to Settings panel
            HexEditorSettings.HexEditorControl = HexEditor;

            // Wire ParsedFieldsPanel once everything is loaded
            if (HexEditor.ParsedFieldsPanel != null)
                ParsedFieldsPanel.ViewModel.AttachPanel(HexEditor.ParsedFieldsPanel);

            HexEditor.FileOpened += (_, _) =>
            {
                if (HexEditor.ParsedFieldsPanel != null)
                    ParsedFieldsPanel.ViewModel.AttachPanel(HexEditor.ParsedFieldsPanel);
            };

            HexEditor.FileClosed += (_, _) =>
            {
                ParsedFieldsPanel.ViewModel.OnFileClosed();
            };
        }

        private void OpenFile(string path)
        {
            try
            {
                HexEditor.FileName = path;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error opening file", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BytesPerLine_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem item) return;
            if (!int.TryParse(item.Tag?.ToString(), out var bpl)) return;

            HexEditor.BytePerLine = bpl;

            Bpl8.IsChecked  = bpl == 8;
            Bpl16.IsChecked = bpl == 16;
            Bpl32.IsChecked = bpl == 32;
            Bpl64.IsChecked = bpl == 64;
        }

        private void InsertMode_Click(object sender, RoutedEventArgs e)
        {
            HexEditor.EditMode = InsertModeToggle.IsChecked == true
                ? EditMode.Insert
                : EditMode.Overwrite;
        }

        private void ReadOnly_Click(object sender, RoutedEventArgs e)
        {
            HexEditor.ReadOnlyMode = ReadOnlyToggle.IsChecked == true;
        }

        private void LoadTbl_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title  = "Select TBL File",
                Filter = "TBL Files (*.tbl)|*.tbl|All Files (*.*)|*.*"
            };
            if (dlg.ShowDialog() == true)
            {
                HexEditor.LoadTBLFile(dlg.FileName);
                CloseTblItem.IsEnabled = true;
            }
        }

        private void CloseTbl_Click(object sender, RoutedEventArgs e)
        {
            HexEditor.CloseTBL();
            CloseTblItem.IsEnabled = false;
        }

        protected override void OnClosed(EventArgs e)
        {
            HexEditor?.Close();
            base.OnClosed(e);
        }
    }
}
