//////////////////////////////////////////////
// Apache 2.0  - 2026
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using WpfHexaEditor.Models.Bookmarks;
using WpfHexaEditor.Services;

namespace WpfHexEditor.WindowPanels.Panels
{
    /// <summary>
    /// Panel for managing bookmark groups/categories
    /// Allows creating, editing, and deleting bookmark groups with colors
    /// </summary>
    public partial class BookmarkGroupsPanel : UserControl
    {
        #region Private Fields

        private readonly ObservableCollection<BookmarkGroup> _groups = new();
        private BookmarkService _bookmarkService;

        #endregion

        #region Constructor

        public BookmarkGroupsPanel()
        {
            InitializeComponent();

            GroupsListBox.ItemsSource = _groups;

            // Initialize with default groups
            InitializeDefaultGroups();

            UpdateStats();
        }

        #endregion

        #region Initialization

        /// <summary>
        /// Initialize default bookmark groups
        /// </summary>
        private void InitializeDefaultGroups()
        {
            _groups.Add(new BookmarkGroup("Default", System.Windows.Media.Colors.Blue)
            {
                Description = "Default bookmark category",
                SortOrder = 0
            });

            _groups.Add(new BookmarkGroup("Important", System.Windows.Media.Colors.Red)
            {
                Description = "Important locations",
                SortOrder = 1
            });

            _groups.Add(new BookmarkGroup("Note", System.Windows.Media.Colors.Yellow)
            {
                Description = "Notes and comments",
                SortOrder = 2
            });

            _groups.Add(new BookmarkGroup("Data", System.Windows.Media.Colors.Green)
            {
                Description = "Data sections",
                SortOrder = 3
            });
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Set the bookmark service to sync with
        /// </summary>
        /// <param name="bookmarkService">Bookmark service</param>
        public void SetBookmarkService(BookmarkService bookmarkService)
        {
            _bookmarkService = bookmarkService;

            // Register existing groups with service
            foreach (var group in _groups)
            {
                _bookmarkService?.RegisterGroup(group);
            }
        }

        /// <summary>
        /// Get all groups
        /// </summary>
        /// <returns>Observable collection of groups</returns>
        public ObservableCollection<BookmarkGroup> GetGroups()
        {
            return _groups;
        }

        /// <summary>
        /// Add a new group
        /// </summary>
        /// <param name="group">Group to add</param>
        /// <returns>True if group was added</returns>
        public bool AddGroup(BookmarkGroup group)
        {
            if (group == null || string.IsNullOrWhiteSpace(group.Name))
                return false;

            // Check for duplicate names
            foreach (var existingGroup in _groups)
            {
                if (existingGroup.Name == group.Name)
                    return false;
            }

            _groups.Add(group);
            _bookmarkService?.RegisterGroup(group);
            UpdateStats();

            return true;
        }

        /// <summary>
        /// Remove a group
        /// </summary>
        /// <param name="group">Group to remove</param>
        /// <returns>True if group was removed</returns>
        public bool RemoveGroup(BookmarkGroup group)
        {
            if (group == null)
                return false;

            // Don't allow removing Default group
            if (group.Name == "Default")
            {
                MessageBox.Show("Cannot remove the Default group.", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            var result = _groups.Remove(group);
            if (result)
            {
                _bookmarkService?.UnregisterGroup(group.Name);
                UpdateStats();
            }

            return result;
        }

        #endregion

        #region Event Handlers

        private void AddGroupButton_Click(object sender, RoutedEventArgs e)
        {
            // Create dialog for new group (simplified for now)
            var dialog = new GroupEditDialog();
            if (dialog.ShowDialog() == true)
            {
                var newGroup = new BookmarkGroup(dialog.GroupName, dialog.GroupColor)
                {
                    Description = dialog.GroupDescription,
                    SortOrder = _groups.Count
                };

                if (!AddGroup(newGroup))
                {
                    MessageBox.Show($"A group named '{newGroup.Name}' already exists.", "Duplicate Group",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private void EditGroupButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedGroup = GroupsListBox.SelectedItem as BookmarkGroup;
            if (selectedGroup == null)
                return;

            // Create dialog for editing (simplified for now)
            var dialog = new GroupEditDialog
            {
                GroupName = selectedGroup.Name,
                GroupDescription = selectedGroup.Description,
                GroupColor = selectedGroup.Color
            };

            if (dialog.ShowDialog() == true)
            {
                selectedGroup.Name = dialog.GroupName;
                selectedGroup.Description = dialog.GroupDescription;
                selectedGroup.Color = dialog.GroupColor;

                // Update in service
                _bookmarkService?.UpdateGroupColor(selectedGroup.Name, selectedGroup.Color);

                // Refresh display
                GroupsListBox.Items.Refresh();
            }
        }

        private void DeleteGroupButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedGroup = GroupsListBox.SelectedItem as BookmarkGroup;
            if (selectedGroup == null)
                return;

            var result = MessageBox.Show(
                $"Delete group '{selectedGroup.Name}'?\n\nBookmarks in this group will not be deleted, but will lose their category assignment.",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                RemoveGroup(selectedGroup);
            }
        }

        private void GroupsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var hasSelection = GroupsListBox.SelectedItem != null;
            EditGroupButton.IsEnabled = hasSelection;
            DeleteGroupButton.IsEnabled = hasSelection;
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Update statistics display
        /// </summary>
        private void UpdateStats()
        {
            var count = _groups.Count;
            StatsTextBlock.Text = count == 1 ? "1 group" : $"{count} groups";
        }

        #endregion
    }

    #region Group Edit Dialog (Temporary Simple Implementation)

    /// <summary>
    /// Simple dialog for editing bookmark group properties
    /// TODO: Create proper XAML-based dialog in Views/Dialogs/
    /// </summary>
    internal class GroupEditDialog : Window
    {
        private TextBox _nameTextBox;
        private TextBox _descriptionTextBox;
        private ComboBox _colorComboBox;

        public string GroupName { get; set; }
        public string GroupDescription { get; set; }
        public System.Windows.Media.Color GroupColor { get; set; } = System.Windows.Media.Colors.Blue;

        public GroupEditDialog()
        {
            Title = "Edit Bookmark Group";
            Width = 400;
            Height = 250;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;

            var grid = new Grid { Margin = new Thickness(10) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Name
            var nameLabel = new TextBlock { Text = "Name:", Margin = new Thickness(0, 0, 0, 5) };
            Grid.SetRow(nameLabel, 0);
            grid.Children.Add(nameLabel);

            _nameTextBox = new TextBox { Margin = new Thickness(0, 0, 0, 10), Text = GroupName };
            Grid.SetRow(_nameTextBox, 1);
            grid.Children.Add(_nameTextBox);

            // Description
            var descLabel = new TextBlock { Text = "Description:", Margin = new Thickness(0, 0, 0, 5) };
            Grid.SetRow(descLabel, 2);
            grid.Children.Add(descLabel);

            _descriptionTextBox = new TextBox { Margin = new Thickness(0, 0, 0, 10), Text = GroupDescription };
            Grid.SetRow(_descriptionTextBox, 3);
            grid.Children.Add(_descriptionTextBox);

            // Buttons
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 10, 0, 0)
            };
            Grid.SetRow(buttonPanel, 4);

            var okButton = new Button
            {
                Content = "OK",
                Width = 75,
                Margin = new Thickness(0, 0, 10, 0),
                IsDefault = true
            };
            okButton.Click += (s, e) =>
            {
                GroupName = _nameTextBox.Text.Trim();
                GroupDescription = _descriptionTextBox.Text.Trim();

                if (string.IsNullOrWhiteSpace(GroupName))
                {
                    MessageBox.Show("Group name cannot be empty.", "Validation Error",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                DialogResult = true;
                Close();
            };

            var cancelButton = new Button
            {
                Content = "Cancel",
                Width = 75,
                IsCancel = true
            };

            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(cancelButton);
            grid.Children.Add(buttonPanel);

            Content = grid;
        }
    }

    #endregion
}
