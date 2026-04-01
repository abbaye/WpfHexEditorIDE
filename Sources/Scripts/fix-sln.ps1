# fix-sln.ps1 — Rewrite project paths in WpfHexEditorControl.sln after folder reorganization
# Run from Sources/
param([string]$SlnPath = "$PSScriptRoot\..\WpfHexEditorControl.sln")

$content = [System.IO.File]::ReadAllText([System.IO.Path]::GetFullPath($SlnPath))

# Each entry: old-path-fragment => new-path-fragment (single backslashes in PS single-quoted strings)
# Order: longer/more-specific keys FIRST to avoid partial matches
$moves = [ordered]@{
    'WpfHexEditor.Core.LSP.Client\'         = 'Core\WpfHexEditor.Core.LSP.Client\'
    'WpfHexEditor.Core.LSP\'                = 'Core\WpfHexEditor.Core.LSP\'
    'WpfHexEditor.Core.BinaryAnalysis\'     = 'Core\WpfHexEditor.Core.BinaryAnalysis\'
    'WpfHexEditor.Core.ProjectSystem\'      = 'Core\WpfHexEditor.Core.ProjectSystem\'
    'WpfHexEditor.Core.Decompiler\'         = 'Core\WpfHexEditor.Core.Decompiler\'
    'WpfHexEditor.Core.Definitions\'        = 'Core\WpfHexEditor.Core.Definitions\'
    'WpfHexEditor.Core.Options\'            = 'Core\WpfHexEditor.Core.Options\'
    'WpfHexEditor.Core.Events\'             = 'Core\WpfHexEditor.Core.Events\'
    'WpfHexEditor.Core.Terminal\'           = 'Core\WpfHexEditor.Core.Terminal\'
    'WpfHexEditor.Core.SourceAnalysis\'     = 'Core\WpfHexEditor.Core.SourceAnalysis\'
    'WpfHexEditor.Core.AssemblyAnalysis\'   = 'Core\WpfHexEditor.Core.AssemblyAnalysis\'
    'WpfHexEditor.Core.Diff\'               = 'Core\WpfHexEditor.Core.Diff\'
    'WpfHexEditor.Core.BuildSystem\'        = 'Core\WpfHexEditor.Core.BuildSystem\'
    'WpfHexEditor.Core.WorkspaceTemplates\' = 'Core\WpfHexEditor.Core.WorkspaceTemplates\'
    'WpfHexEditor.Core.Commands\'           = 'Core\WpfHexEditor.Core.Commands\'
    'WpfHexEditor.Core.Debugger\'           = 'Core\WpfHexEditor.Core.Debugger\'
    'WpfHexEditor.Core.Scripting\'          = 'Core\WpfHexEditor.Core.Scripting\'
    'WpfHexEditor.Core.Workspaces\'         = 'Core\WpfHexEditor.Core.Workspaces\'
    'WpfHexEditor.Core\'                    = 'Core\WpfHexEditor.Core\'
    'WpfHexEditor.Editor.MarkdownEditor.Core\'  = 'Editors\WpfHexEditor.Editor.MarkdownEditor.Core\'
    'WpfHexEditor.Editor.MarkdownEditor\'       = 'Editors\WpfHexEditor.Editor.MarkdownEditor\'
    'WpfHexEditor.Editor.DocumentEditor.Core\'  = 'Editors\WpfHexEditor.Editor.DocumentEditor.Core\'
    'WpfHexEditor.Editor.DocumentEditor\'       = 'Editors\WpfHexEditor.Editor.DocumentEditor\'
    'WpfHexEditor.Editor.ClassDiagram.Core\'    = 'Editors\WpfHexEditor.Editor.ClassDiagram.Core\'
    'WpfHexEditor.Editor.ClassDiagram\'         = 'Editors\WpfHexEditor.Editor.ClassDiagram\'
    'WpfHexEditor.Editor.Core\'                 = 'Editors\WpfHexEditor.Editor.Core\'
    'WpfHexEditor.Editor.CodeEditor\'           = 'Editors\WpfHexEditor.Editor.CodeEditor\'
    'WpfHexEditor.Editor.TextEditor\'           = 'Editors\WpfHexEditor.Editor.TextEditor\'
    'WpfHexEditor.Editor.ImageViewer\'          = 'Editors\WpfHexEditor.Editor.ImageViewer\'
    'WpfHexEditor.Editor.EntropyViewer\'        = 'Editors\WpfHexEditor.Editor.EntropyViewer\'
    'WpfHexEditor.Editor.DiffViewer\'           = 'Editors\WpfHexEditor.Editor.DiffViewer\'
    'WpfHexEditor.Editor.TblEditor\'            = 'Editors\WpfHexEditor.Editor.TblEditor\'
    'WpfHexEditor.Editor.ChangesetEditor\'      = 'Editors\WpfHexEditor.Editor.ChangesetEditor\'
    'WpfHexEditor.Editor.XamlDesigner\'         = 'Editors\WpfHexEditor.Editor.XamlDesigner\'
    'WpfHexEditor.Editor.JsonEditor\'           = 'Editors\WpfHexEditor.Editor.JsonEditor\'
    'WpfHexEditor.Editor.ResxEditor\'           = 'Editors\WpfHexEditor.Editor.ResxEditor\'
    'WpfHexEditor.Editor.DisassemblyViewer\'    = 'Editors\WpfHexEditor.Editor.DisassemblyViewer\'
    'WpfHexEditor.Editor.StructureEditor\'      = 'Editors\WpfHexEditor.Editor.StructureEditor\'
    'WpfHexEditor.Editor.TileEditor\'           = 'Editors\WpfHexEditor.Editor.TileEditor\'
    'WpfHexEditor.Editor.AudioViewer\'          = 'Editors\WpfHexEditor.Editor.AudioViewer\'
    'WpfHexEditor.Editor.ScriptEditor\'         = 'Editors\WpfHexEditor.Editor.ScriptEditor\'
    'WpfHexEditor.HexEditor\'                   = 'Editors\WpfHexEditor.HexEditor\'
    'WpfHexEditor.Docking.Core\'  = 'Docking\WpfHexEditor.Docking.Core\'
    'WpfHexEditor.Docking.Wpf\'   = 'Docking\WpfHexEditor.Docking.Wpf\'
    'WpfHexEditor.ColorPicker\'   = 'Controls\WpfHexEditor.ColorPicker\'
    'WpfHexEditor.HexBox\'        = 'Controls\WpfHexEditor.HexBox\'
    'WpfHexEditor.Terminal\'      = 'Controls\WpfHexEditor.Terminal\'
    'WpfHexEditor.Shell.Panels\'  = 'Shell\WpfHexEditor.Shell.Panels\'
    'WpfHexEditor.Shell\'         = 'Shell\WpfHexEditor.Shell\'
    'WpfHexEditor.SDK\'                            = 'Plugins\WpfHexEditor.SDK\'
    'WpfHexEditor.PluginHost\'                     = 'Plugins\WpfHexEditor.PluginHost\'
    'WpfHexEditor.PluginSandbox\'                  = 'Plugins\WpfHexEditor.PluginSandbox\'
    'WpfHexEditor.PluginDev\'                      = 'Plugins\WpfHexEditor.PluginDev\'
    'WpfHexEditor.Plugins.ClassDiagram\'           = 'Plugins\WpfHexEditor.Plugins.ClassDiagram\'
    'WpfHexEditor.Plugins.SolutionLoader.VS\'      = 'Plugins\WpfHexEditor.Plugins.SolutionLoader.VS\'
    'WpfHexEditor.Plugins.SolutionLoader.WH\'      = 'Plugins\WpfHexEditor.Plugins.SolutionLoader.WH\'
    'WpfHexEditor.Plugins.SolutionLoader.Folder\'  = 'Plugins\WpfHexEditor.Plugins.SolutionLoader.Folder\'
    'WpfHexEditor.Plugins.Build.MSBuild\'          = 'Plugins\WpfHexEditor.Plugins.Build.MSBuild\'
    'WpfHexEditor.Plugins.AssemblyExplorer.Tests\' = 'Tests\WpfHexEditor.Plugins.AssemblyExplorer.Tests\'
    'WpfHexEditor.Editor.CodeEditor.Tests\'        = 'Tests\WpfHexEditor.Editor.CodeEditor.Tests\'
    'WpfHexEditor.Core.Diff.Tests\'                = 'Tests\WpfHexEditor.Core.Diff.Tests\'
    'WpfHexEditor.Core.Workspaces.Tests\'          = 'Tests\WpfHexEditor.Core.Workspaces.Tests\'
    'WpfHexEditor.BinaryAnalysis.Tests\'           = 'Tests\WpfHexEditor.BinaryAnalysis.Tests\'
    'WpfHexEditor.Docking.Tests\'                  = 'Tests\WpfHexEditor.Docking.Tests\'
    'WpfHexEditor.PluginHost.Tests\'               = 'Tests\WpfHexEditor.PluginHost.Tests\'
    'WpfHexEditor.Benchmarks\'                     = 'Tests\WpfHexEditor.Benchmarks\'
    'WpfHexEditor.Tests\'                          = 'Tests\WpfHexEditor.Tests\'
}

foreach ($old in $moves.Keys) {
    $new = $moves[$old]
    $content = $content.Replace($old, $new)
}

[System.IO.File]::WriteAllText([System.IO.Path]::GetFullPath($SlnPath), $content, (New-Object System.Text.UTF8Encoding $false))
Write-Host "Done."
