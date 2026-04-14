//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////
// Project: WpfHexEditor.Editor.StructureEditor
// File: ViewModels/MetadataViewModel.cs
// Description: VM for the Metadata tab — FormatDefinition top-level fields.
//////////////////////////////////////////////

using System.Collections.ObjectModel;
using WpfHexEditor.Core.FormatDetection;
using WpfHexEditor.Core.ViewModels;

namespace WpfHexEditor.Editor.StructureEditor.ViewModels;

internal sealed class MetadataViewModel : ViewModelBase
{
    internal event EventHandler? Changed;

    private string _formatName  = "";
    private string _version     = "";
    private string _description = "";
    private string _author      = "";
    private string _category    = "";
    private string _diffMode    = "";
    private string _preferredEditor = "";
    private bool   _isTextFormat;

    public string FormatName      { get => _formatName;  set { if (SetField(ref _formatName, value))  RaiseChanged(); } }
    public string Version         { get => _version;     set { if (SetField(ref _version, value))     RaiseChanged(); } }
    public string Description     { get => _description; set { if (SetField(ref _description, value)) RaiseChanged(); } }
    public string Author          { get => _author;      set { if (SetField(ref _author, value))      RaiseChanged(); } }
    public string Category        { get => _category;    set { if (SetField(ref _category, value))    RaiseChanged(); } }
    public string DiffMode        { get => _diffMode;    set { if (SetField(ref _diffMode, value))    RaiseChanged(); } }
    public string PreferredEditor { get => _preferredEditor; set { if (SetField(ref _preferredEditor, value)) RaiseChanged(); } }
    public bool   IsTextFormat    { get => _isTextFormat; set { if (SetField(ref _isTextFormat, value)) RaiseChanged(); } }

    public ObservableCollection<StringItemViewModel> Extensions { get; } = [];
    public ObservableCollection<StringItemViewModel> MimeTypes  { get; } = [];
    public ObservableCollection<StringItemViewModel> Software   { get; } = [];
    public ObservableCollection<StringItemViewModel> UseCases   { get; } = [];

    public static IReadOnlyList<string> CategoryOptions { get; } =
    [
        "Archives", "Audio", "Databases", "Documents", "Executables",
        "Fonts", "Images", "Science", "Scripts", "Spreadsheets",
        "Video", "3D", "Other",
    ];

    public static IReadOnlyList<string> DiffModeOptions { get; } = ["", "text", "semantic", "binary"];

    public static IReadOnlyList<string> PreferredEditorOptions { get; } =
    [
        "", "hex-editor", "code-editor", "structure-editor",
        "text-editor", "tbl-editor", "auto",
    ];

    internal void LoadFrom(FormatDefinition def)
    {
        FormatName      = def.FormatName      ?? "";
        Version         = def.Version         ?? "";
        Description     = def.Description     ?? "";
        Author          = def.Author          ?? "";
        Category        = def.Category        ?? "";
        DiffMode        = def.DiffMode        ?? "";
        PreferredEditor = def.PreferredEditor ?? "";
        IsTextFormat    = def.Detection?.IsTextFormat ?? false;

        LoadList(Extensions, def.Extensions);
        LoadList(MimeTypes,  def.MimeTypes);
        LoadList(Software,   def.Software);
        LoadList(UseCases,   def.UseCases);
    }

    internal void SaveTo(FormatDefinition def)
    {
        def.FormatName      = FormatName;
        def.Version         = Version;
        def.Description     = Description;
        def.Author          = Author;
        def.Category        = Category;
        def.DiffMode        = string.IsNullOrEmpty(DiffMode) ? null : DiffMode;
        def.PreferredEditor = string.IsNullOrEmpty(PreferredEditor) ? null : PreferredEditor;
        def.Extensions      = [..Extensions.Select(x => x.Value)];
        def.MimeTypes       = [..MimeTypes.Select(x => x.Value)];
        def.Software        = [..Software.Select(x => x.Value)];
        def.UseCases        = [..UseCases.Select(x => x.Value)];

        if (def.Detection is not null)
            def.Detection.IsTextFormat = IsTextFormat;
    }

    internal void AddExtension()  => AddItem(Extensions);
    internal void AddMimeType()   => AddItem(MimeTypes);
    internal void AddSoftware()   => AddItem(Software);
    internal void AddUseCase()    => AddItem(UseCases);

    private void AddItem(ObservableCollection<StringItemViewModel> col)
    {
        var item = new StringItemViewModel("");
        item.RemoveRequested += (s, _) => { col.Remove((StringItemViewModel)s!); RaiseChanged(); };
        item.ValueChanged    += (_, _) => RaiseChanged();
        col.Add(item);
        RaiseChanged();
    }

    private void LoadList(ObservableCollection<StringItemViewModel> col, IEnumerable<string>? src)
    {
        col.Clear();
        foreach (var v in src ?? [])
        {
            var item = new StringItemViewModel(v);
            item.RemoveRequested += (s, _) => { col.Remove((StringItemViewModel)s!); RaiseChanged(); };
            item.ValueChanged    += (_, _) => RaiseChanged();
            col.Add(item);
        }
    }

    private void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
