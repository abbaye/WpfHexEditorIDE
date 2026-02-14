//////////////////////////////////////////////
// Apache 2.0  - 2021
// Author : Derek Tremblay (derektremblay666@gmail.com)
//
//
// NOT A TRUE PROJECT! IT'S JUST A SAMPLE FOR TESTING THE HEXEDITOR IN VARIOUS SITUATIONS... 
//////////////////////////////////////////////

using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using WpfHexaEditor;
using WpfHexaEditor.Core;
using WpfHexaEditor.Core.Bytes;
using WpfHexaEditor.Core.EventArguments;
using WpfHexaEditor.Core.MethodExtention;

namespace WpfHexEditor.Sample.BinaryFilesDifference
{
    public partial class MainWindow : Window
    {
        /// <summary>
        /// Used to catch internal change for cath potential infinite loop
        /// </summary>
        private bool _internalChange;
        List<ByteDifference> _differences;
        List<BlockListItem> _blockListItem = new();

        public MainWindow() => InitializeComponent();

        #region Various controls events
        private void FirstHexEditor_Click(object sender, RoutedEventArgs e) => OpenFile(FirstFile);

        private void SecondHexEditor_Click(object sender, RoutedEventArgs e) => OpenFile(SecondFile);

        private void FindDifferenceButton_Click(object sender, RoutedEventArgs e)
        {
            if (FirstFile.FileName == string.Empty || SecondFile.FileName == string.Empty)
            {
                MessageBox.Show("LOAD TWO FILE!!", "HexEditor sample", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return;
            }

            Application.Current.MainWindow.Cursor = Cursors.Wait;
            FindDifference();
            Application.Current.MainWindow.Cursor = null;
        }


        private void FileDiffBytesList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (_internalChange) return;
            if (FileDiffBytesList.SelectedItem is not ByteDifferenceListItem byteDifferenceItem) return;

            _internalChange = true;
            FirstFile.SetPosition(byteDifferenceItem.ByteDiff.BytePositionInStream, 1);
            SecondFile.SetPosition(byteDifferenceItem.ByteDiff.BytePositionInStream, 1);
            _internalChange = false;
        }

        private void PatchButton_Click(object sender, RoutedEventArgs e)
        {
            if (_differences is null) return;

            SecondFile.With(c =>
            {
                c.ReadOnlyMode = false;

                foreach (BlockListItem itm in FileDiffBlockList.Children)
                {
                    var diffList = _differences.Where(d => d.BytePositionInStream >= itm.CustomBlock.StartOffset &&
                                                           d.BytePositionInStream <= itm.CustomBlock.StopOffset);

                    foreach (var byteDiff in diffList)
                        c.ModifyByte(byteDiff.Destination, byteDiff.BytePositionInStream);

                    itm.PatchBlockButton.IsEnabled = false;
                }

                c.ReadOnlyMode = true;
            });
        }

        private void BlockItemProgress_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) =>
            UpdateListofBlockItem();

        private void SaveChangeButton_Click(object sender, RoutedEventArgs e)
        {
            SecondFile.With(c =>
            {
                c.ReadOnlyMode = false;
                c.SubmitChanges();
                c.ReadOnlyMode = true;
            });

            ClearUI();
        }

        #endregion

        #region Various methods

        private void OpenFile(HexEditorLegacy hexEditor)
        {
            ClearUI();

            #region Create file dialog
            var fileDialog = new OpenFileDialog
            {
                Multiselect = true,
                CheckFileExists = true
            };

            if (fileDialog.ShowDialog() == null || !File.Exists(fileDialog.FileName)) return;
            #endregion

            hexEditor.FileName = fileDialog.FileName;
        }

        /// <summary>
        /// Clear the difference in various control
        /// </summary>
        private void ClearUI()
        {
            FileDiffBytesList.Items.Clear();
            FirstFile.ClearCustomBackgroundBlock();
            SecondFile.ClearCustomBackgroundBlock();
            SecondFile.ClearAllChange();
            PatchButton.IsEnabled = false;
            _blockListItem.Clear();
            _differences = null;
        }

        /// <summary>
        /// Update the list of block item
        /// </summary>
        private void UpdateListofBlockItem()
        {
            FileDiffBlockList.Children.Clear();

            var nbViewItem = (int)BlockItemProgress.Value + (int)(FileDiffBlockList.ActualHeight / new BlockListItem().Height);

            for (var i = (int)BlockItemProgress.Value; i < nbViewItem; i++)
            {
                if (i < _blockListItem.Count)
                    FileDiffBlockList.Children.Add(_blockListItem[i]);
            }
        }

        /// <summary>
        /// Find the difference of the two loaded file and add to lists the results
        /// </summary>
        private void FindDifference()
        {
            ClearUI();

            if (FirstFile.FileName == string.Empty || SecondFile.FileName == string.Empty) return;

            FileDiffBlockList.Children.Clear();

            //load the difference
            _differences = FirstFile.Compare(SecondFile).ToList();

            //Load list of difference
            var cbb = new CustomBackgroundBlock();
            var j = 0;

            foreach (var byteDifference in _differences)
            {
                //create or update custom background block
                if (j == 0)
                    cbb = new CustomBackgroundBlock(byteDifference.BytePositionInStream, ++j, RandomBrushes.PickBrush());
                else
                    cbb.Length = ++j;

                if (!_differences.Any(c => c.BytePositionInStream == byteDifference.BytePositionInStream + 1))
                {
                    j = 0;

                    new BlockListItem(cbb).With(c =>
                    {
                        c.PatchButtonClick += BlockItem_PatchButtonClick;
                        c.Click += BlockItem_Click;
                        _blockListItem.Add(c);
                    });

                    //add to hexeditor
                    FirstFile.CustomBackgroundBlockItems.Add(cbb);
                    SecondFile.CustomBackgroundBlockItems.Add(cbb);
                }
            }

            //Update progressbar
            BlockItemProgress.Maximum = _blockListItem.Count();
            UpdateListofBlockItem();

            //refresh editor
            FirstFile.RefreshView();
            SecondFile.RefreshView();

            //Enable patch button
            PatchButton.IsEnabled = true;
        }

        /// <summary>
        /// Update view when item is clicked
        /// </summary>
        private void BlockItem_Click(object sender, EventArgs e)
        {
            if (_internalChange) return;
            if (sender is not BlockListItem blockitm) return;
            if (_differences is null) return;

            //Clear UI
            FileDiffBytesList.Items.Clear();

            _internalChange = true;
            FirstFile.SetPosition(blockitm.CustomBlock.StartOffset, 1);
            SecondFile.SetPosition(blockitm.CustomBlock.StartOffset, 1);
            _internalChange = false;

            //Load list of byte difference
            foreach (var byteDifference in _differences
                .Where(c => c.BytePositionInStream >= blockitm.CustomBlock.StartOffset &&
                            c.BytePositionInStream <= blockitm.CustomBlock.StopOffset))
            {
                byteDifference.Color = blockitm.CustomBlock.Color;
                FileDiffBytesList.Items.Add(new ByteDifferenceListItem(byteDifference));
            }
        }

        /// <summary>
        /// Patch the selected block in the second file
        /// </summary>
        private void BlockItem_PatchButtonClick(object sender, EventArgs e)
        {
            if (sender is not BlockListItem itm) return;
            if (_differences is null) return;

            SecondFile.With(c =>
            {
                c.ReadOnlyMode = false;

                var diffList = _differences.Where(d => d.BytePositionInStream >= itm.CustomBlock.StartOffset &&
                                                       d.BytePositionInStream <= itm.CustomBlock.StopOffset);

                foreach (var byteDiff in diffList)
                    c.ModifyByte(byteDiff.Origine, byteDiff.BytePositionInStream);

                c.ReadOnlyMode = true;
            });

            itm.PatchBlockButton.IsEnabled = false;
        }
        #endregion

        #region Synchronise the two hexeditor
        private void FirstFile_VerticalScrollBarChanged(object sender, ByteEventArgs e)
        {
            if (_internalChange) return;

            _internalChange = true;
            SecondFile.SetPosition(e.BytePositionInStream);
            _internalChange = false;
        }

        private void SecondFile_VerticalScrollBarChanged(object sender, ByteEventArgs e)
        {
            if (_internalChange) return;

            _internalChange = true;
            FirstFile.SetPosition(e.BytePositionInStream);
            _internalChange = false;
        }
        #endregion
      
    }
}
