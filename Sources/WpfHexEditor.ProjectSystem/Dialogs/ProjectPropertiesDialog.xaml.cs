//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.IO;
using System.Windows;
using WpfHexEditor.Editor.Core;

namespace WpfHexEditor.ProjectSystem.Dialogs;

/// <summary>
/// Read-only properties dialog for a <see cref="IProject"/>.
/// </summary>
public partial class ProjectPropertiesDialog : Window
{
    public ProjectPropertiesDialog(IProject project)
    {
        InitializeComponent();
        Populate(project);
    }

    private void Populate(IProject project)
    {
        ProjectTitleText.Text = project.Name + " — Properties";
        NameText.Text         = project.Name;
        FilePathText.Text     = project.ProjectFilePath;
        FilePathTooltip.Text  = project.ProjectFilePath;
        DirectoryText.Text    = Path.GetDirectoryName(project.ProjectFilePath) ?? "";
        ItemCountText.Text    = project.Items.Count.ToString();
        IsModifiedText.Text   = project.IsModified ? "Yes" : "No";

        if (project.DefaultTblItemId is not null)
        {
            var tbl = project.FindItem(project.DefaultTblItemId);
            DefaultTblText.Text = tbl is not null ? tbl.Name : $"(id: {project.DefaultTblItemId})";
        }
        else
        {
            DefaultTblText.Text = "(none)";
        }
    }
}
