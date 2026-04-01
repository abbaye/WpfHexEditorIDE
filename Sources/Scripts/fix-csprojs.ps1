# fix-csprojs.ps1 — Recompute all ProjectReference paths after folder reorganization
# Run from Sources/
param([string]$Root = (Split-Path $PSScriptRoot -Parent))

$Root = [System.IO.Path]::GetFullPath($Root)

# Map: project assembly name -> relative path from Sources/ (no trailing slash)
$projectLocations = @{
    # Core
    'WpfHexEditor.Core'                    = 'Core\WpfHexEditor.Core'
    'WpfHexEditor.Core.BinaryAnalysis'     = 'Core\WpfHexEditor.Core.BinaryAnalysis'
    'WpfHexEditor.Core.ProjectSystem'      = 'Core\WpfHexEditor.Core.ProjectSystem'
    'WpfHexEditor.Core.Decompiler'         = 'Core\WpfHexEditor.Core.Decompiler'
    'WpfHexEditor.Core.Definitions'        = 'Core\WpfHexEditor.Core.Definitions'
    'WpfHexEditor.Core.Options'            = 'Core\WpfHexEditor.Core.Options'
    'WpfHexEditor.Core.Events'             = 'Core\WpfHexEditor.Core.Events'
    'WpfHexEditor.Core.Terminal'           = 'Core\WpfHexEditor.Core.Terminal'
    'WpfHexEditor.Core.SourceAnalysis'     = 'Core\WpfHexEditor.Core.SourceAnalysis'
    'WpfHexEditor.Core.AssemblyAnalysis'   = 'Core\WpfHexEditor.Core.AssemblyAnalysis'
    'WpfHexEditor.Core.Diff'               = 'Core\WpfHexEditor.Core.Diff'
    'WpfHexEditor.Core.LSP'                = 'Core\WpfHexEditor.Core.LSP'
    'WpfHexEditor.Core.LSP.Client'         = 'Core\WpfHexEditor.Core.LSP.Client'
    'WpfHexEditor.Core.BuildSystem'        = 'Core\WpfHexEditor.Core.BuildSystem'
    'WpfHexEditor.Core.WorkspaceTemplates' = 'Core\WpfHexEditor.Core.WorkspaceTemplates'
    'WpfHexEditor.Core.Commands'           = 'Core\WpfHexEditor.Core.Commands'
    'WpfHexEditor.Core.Debugger'           = 'Core\WpfHexEditor.Core.Debugger'
    'WpfHexEditor.Core.Scripting'          = 'Core\WpfHexEditor.Core.Scripting'
    'WpfHexEditor.Core.Workspaces'         = 'Core\WpfHexEditor.Core.Workspaces'
    # Editors
    'WpfHexEditor.HexEditor'                  = 'Editors\WpfHexEditor.HexEditor'
    'WpfHexEditor.Editor.Core'                = 'Editors\WpfHexEditor.Editor.Core'
    'WpfHexEditor.Editor.CodeEditor'          = 'Editors\WpfHexEditor.Editor.CodeEditor'
    'WpfHexEditor.Editor.TextEditor'          = 'Editors\WpfHexEditor.Editor.TextEditor'
    'WpfHexEditor.Editor.ImageViewer'         = 'Editors\WpfHexEditor.Editor.ImageViewer'
    'WpfHexEditor.Editor.EntropyViewer'       = 'Editors\WpfHexEditor.Editor.EntropyViewer'
    'WpfHexEditor.Editor.DiffViewer'          = 'Editors\WpfHexEditor.Editor.DiffViewer'
    'WpfHexEditor.Editor.TblEditor'           = 'Editors\WpfHexEditor.Editor.TblEditor'
    'WpfHexEditor.Editor.ChangesetEditor'     = 'Editors\WpfHexEditor.Editor.ChangesetEditor'
    'WpfHexEditor.Editor.XamlDesigner'        = 'Editors\WpfHexEditor.Editor.XamlDesigner'
    'WpfHexEditor.Editor.MarkdownEditor'      = 'Editors\WpfHexEditor.Editor.MarkdownEditor'
    'WpfHexEditor.Editor.MarkdownEditor.Core' = 'Editors\WpfHexEditor.Editor.MarkdownEditor.Core'
    'WpfHexEditor.Editor.JsonEditor'          = 'Editors\WpfHexEditor.Editor.JsonEditor'
    'WpfHexEditor.Editor.ResxEditor'          = 'Editors\WpfHexEditor.Editor.ResxEditor'
    'WpfHexEditor.Editor.DisassemblyViewer'   = 'Editors\WpfHexEditor.Editor.DisassemblyViewer'
    'WpfHexEditor.Editor.StructureEditor'     = 'Editors\WpfHexEditor.Editor.StructureEditor'
    'WpfHexEditor.Editor.TileEditor'          = 'Editors\WpfHexEditor.Editor.TileEditor'
    'WpfHexEditor.Editor.AudioViewer'         = 'Editors\WpfHexEditor.Editor.AudioViewer'
    'WpfHexEditor.Editor.ScriptEditor'        = 'Editors\WpfHexEditor.Editor.ScriptEditor'
    'WpfHexEditor.Editor.ClassDiagram.Core'   = 'Editors\WpfHexEditor.Editor.ClassDiagram.Core'
    'WpfHexEditor.Editor.ClassDiagram'        = 'Editors\WpfHexEditor.Editor.ClassDiagram'
    'WpfHexEditor.Editor.DocumentEditor.Core' = 'Editors\WpfHexEditor.Editor.DocumentEditor.Core'
    'WpfHexEditor.Editor.DocumentEditor'      = 'Editors\WpfHexEditor.Editor.DocumentEditor'
    # Docking
    'WpfHexEditor.Docking.Core' = 'Docking\WpfHexEditor.Docking.Core'
    'WpfHexEditor.Docking.Wpf'  = 'Docking\WpfHexEditor.Docking.Wpf'
    # Controls
    'WpfHexEditor.ColorPicker' = 'Controls\WpfHexEditor.ColorPicker'
    'WpfHexEditor.HexBox'      = 'Controls\WpfHexEditor.HexBox'
    'WpfHexEditor.Terminal'    = 'Controls\WpfHexEditor.Terminal'
    # Shell
    'WpfHexEditor.Shell'        = 'Shell\WpfHexEditor.Shell'
    'WpfHexEditor.Shell.Panels' = 'Shell\WpfHexEditor.Shell.Panels'
    # Plugins (new arrivals)
    'WpfHexEditor.SDK'                           = 'Plugins\WpfHexEditor.SDK'
    'WpfHexEditor.PluginHost'                    = 'Plugins\WpfHexEditor.PluginHost'
    'WpfHexEditor.PluginSandbox'                 = 'Plugins\WpfHexEditor.PluginSandbox'
    'WpfHexEditor.PluginDev'                     = 'Plugins\WpfHexEditor.PluginDev'
    'WpfHexEditor.Plugins.ClassDiagram'          = 'Plugins\WpfHexEditor.Plugins.ClassDiagram'
    'WpfHexEditor.Plugins.SolutionLoader.VS'     = 'Plugins\WpfHexEditor.Plugins.SolutionLoader.VS'
    'WpfHexEditor.Plugins.SolutionLoader.WH'     = 'Plugins\WpfHexEditor.Plugins.SolutionLoader.WH'
    'WpfHexEditor.Plugins.SolutionLoader.Folder' = 'Plugins\WpfHexEditor.Plugins.SolutionLoader.Folder'
    'WpfHexEditor.Plugins.Build.MSBuild'         = 'Plugins\WpfHexEditor.Plugins.Build.MSBuild'
    # Plugins (already in Plugins/ — declare for correct path computation)
    'WpfHexEditor.Plugins.ArchiveExplorer'       = 'Plugins\WpfHexEditor.Plugins.ArchiveExplorer'
    'WpfHexEditor.Plugins.AssemblyExplorer'      = 'Plugins\WpfHexEditor.Plugins.AssemblyExplorer'
    'WpfHexEditor.Plugins.CustomParserTemplate'  = 'Plugins\WpfHexEditor.Plugins.CustomParserTemplate'
    'WpfHexEditor.Plugins.DataInspector'         = 'Plugins\WpfHexEditor.Plugins.DataInspector'
    'WpfHexEditor.Plugins.Debugger'              = 'Plugins\WpfHexEditor.Plugins.Debugger'
    'WpfHexEditor.Plugins.DiagnosticTools'       = 'Plugins\WpfHexEditor.Plugins.DiagnosticTools'
    'WpfHexEditor.Plugins.DocumentLoaders'       = 'Plugins\WpfHexEditor.Plugins.DocumentLoaders'
    'WpfHexEditor.Plugins.FileComparison'        = 'Plugins\WpfHexEditor.Plugins.FileComparison'
    'WpfHexEditor.Plugins.FileStatistics'        = 'Plugins\WpfHexEditor.Plugins.FileStatistics'
    'WpfHexEditor.Plugins.FormatInfo'            = 'Plugins\WpfHexEditor.Plugins.FormatInfo'
    'WpfHexEditor.Plugins.Git'                   = 'Plugins\WpfHexEditor.Plugins.Git'
    'WpfHexEditor.Plugins.LSPTools'              = 'Plugins\WpfHexEditor.Plugins.LSPTools'
    'WpfHexEditor.Plugins.ParsedFields'          = 'Plugins\WpfHexEditor.Plugins.ParsedFields'
    'WpfHexEditor.Plugins.PatternAnalysis'       = 'Plugins\WpfHexEditor.Plugins.PatternAnalysis'
    'WpfHexEditor.Plugins.ResxLocalization'      = 'Plugins\WpfHexEditor.Plugins.ResxLocalization'
    'WpfHexEditor.Plugins.ScriptRunner'          = 'Plugins\WpfHexEditor.Plugins.ScriptRunner'
    'WpfHexEditor.Plugins.StructureOverlay'      = 'Plugins\WpfHexEditor.Plugins.StructureOverlay'
    'WpfHexEditor.Plugins.SynalysisGrammar'      = 'Plugins\WpfHexEditor.Plugins.SynalysisGrammar'
    'WpfHexEditor.Plugins.UnitTesting'           = 'Plugins\WpfHexEditor.Plugins.UnitTesting'
    'WpfHexEditor.Plugins.XamlDesigner'          = 'Plugins\WpfHexEditor.Plugins.XamlDesigner'
    # Tests
    'WpfHexEditor.Tests'                          = 'Tests\WpfHexEditor.Tests'
    'WpfHexEditor.Docking.Tests'                  = 'Tests\WpfHexEditor.Docking.Tests'
    'WpfHexEditor.BinaryAnalysis.Tests'           = 'Tests\WpfHexEditor.BinaryAnalysis.Tests'
    'WpfHexEditor.Core.Diff.Tests'                = 'Tests\WpfHexEditor.Core.Diff.Tests'
    'WpfHexEditor.Core.Workspaces.Tests'          = 'Tests\WpfHexEditor.Core.Workspaces.Tests'
    'WpfHexEditor.Benchmarks'                     = 'Tests\WpfHexEditor.Benchmarks'
    'WpfHexEditor.PluginHost.Tests'               = 'Tests\WpfHexEditor.PluginHost.Tests'
    'WpfHexEditor.Plugins.AssemblyExplorer.Tests' = 'Tests\WpfHexEditor.Plugins.AssemblyExplorer.Tests'
    'WpfHexEditor.Editor.CodeEditor.Tests'        = 'Tests\WpfHexEditor.Editor.CodeEditor.Tests'
    # Root
    'WpfHexEditor.App'          = 'WpfHexEditor.App'
    # Samples
    'WpfHexEditor.Sample.HexEditor'  = 'Samples\WpfHexEditor.Sample.HexEditor'
    'WpfHexEditor.Sample.Docking'    = 'Samples\WpfHexEditor.Sample.Docking'
    'WpfHexEditor.Sample.Terminal'   = 'Samples\WpfHexEditor.Sample.Terminal'
    'WpfHexEditor.Sample.CodeEditor' = 'Samples\WpfHexEditor.Sample.CodeEditor'
    # Tools
    'WpfHexEditor.PackagingTool'   = 'Tools\WpfHexEditor.PackagingTool'
    'WpfHexEditor.PluginInstaller' = 'Tools\WpfHexEditor.PluginInstaller'
}

function Get-RelPath([string]$fromDir, [string]$toFile) {
    $from  = [System.IO.Path]::GetFullPath($fromDir) + [System.IO.Path]::DirectorySeparatorChar
    $to    = [System.IO.Path]::GetFullPath($toFile)
    $fromU = [Uri]::new($from)
    $toU   = [Uri]::new($to)
    return $fromU.MakeRelativeUri($toU).ToString() -replace '/', '\'
}

$allCsprojs = Get-ChildItem -Path $Root -Recurse -Filter '*.csproj' |
              Where-Object { $_.FullName -notmatch '\\obj\\' }

$updated = 0
foreach ($csproj in $allCsprojs) {
    $dir     = $csproj.DirectoryName
    $content = [System.IO.File]::ReadAllText($csproj.FullName)
    $original = $content

    # Rewrite ProjectReference Include="..."
    $content = [regex]::Replace($content,
        '(?i)(<ProjectReference\s+[^>]*Include\s*=\s*")([^"]+)(")',
        {
            param($m)
            $projName = [System.IO.Path]::GetFileNameWithoutExtension($m.Groups[2].Value)
            if ($script:projectLocations.ContainsKey($projName)) {
                $newDir  = Join-Path $script:Root $script:projectLocations[$projName]
                $newFile = Join-Path $newDir "$projName.csproj"
                $rel     = Get-RelPath $dir $newFile
                return "$($m.Groups[1].Value)$rel$($m.Groups[3].Value)"
            }
            return $m.Value
        }
    )

    if ($content -ne $original) {
        [System.IO.File]::WriteAllText($csproj.FullName, $content, [System.Text.Encoding]::UTF8)
        Write-Host "Updated: $($csproj.FullName -replace [regex]::Escape($Root), '')"
        $updated++
    }
}

Write-Host "Done: $updated .csproj files updated."
