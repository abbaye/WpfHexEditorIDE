# ==========================================================
# Add-DiffV2Tokens.ps1
# Inserts 8 new DF_*V2 SolidColorBrush tokens into every Colors.xaml
# theme file (16 Shell themes + 2 Docking.Wpf themes).
# New tokens: GutterBackground, LineNumberForeground, WordModified,
#             HeaderBackground, OverviewRulerBackground,
#             OverviewModified, OverviewAdded, OverviewRemoved
# Run from: Sources/
# ==========================================================
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root  = $PSScriptRoot
$shell = Join-Path $root 'WpfHexEditor.Shell\Themes'
$dock  = Join-Path $root 'WpfHexEditor.Docking.Wpf\Themes'

# ---------------------------------------------------------------------------
# Per-theme color map (dark defaults / light defaults)
# ---------------------------------------------------------------------------
$dark = @{
    GutterBg       = '#1E1E1E'
    LineNumFg      = '#858585'
    WordModified   = '#6B4C00'
    HeaderBg       = '#2D2D30'
    RulerBg        = '#252526'
    RulerModified  = '#D4AC4E'
    RulerAdded     = '#4EC94E'
    RulerRemoved   = '#C94E4E'
}
$light = @{
    GutterBg       = '#F3F3F3'
    LineNumFg      = '#AAAAAA'
    WordModified   = '#FFFACD'
    HeaderBg       = '#EAEAEA'
    RulerBg        = '#F0F0F0'
    RulerModified  = '#D4AC4E'
    RulerAdded     = '#2E7D32'
    RulerRemoved   = '#C62828'
}

$themes = @{
    'VS2022Dark'      = $dark
    'VisualStudio'    = $light
    'CatppuccinMocha' = @{ GutterBg='#1E1E2E'; LineNumFg='#6C7086'; WordModified='#36300A'; HeaderBg='#313244'; RulerBg='#181825'; RulerModified='#F9E2AF'; RulerAdded='#A6E3A1'; RulerRemoved='#F38BA8' }
    'CatppuccinLatte' = @{ GutterBg='#EFF1F5'; LineNumFg='#7C7F93'; WordModified='#FFF5D0'; HeaderBg='#CCD0DA'; RulerBg='#E6E9EF'; RulerModified='#DF8E1D'; RulerAdded='#40A02B'; RulerRemoved='#D20F39' }
    'Cyberpunk'       = @{ GutterBg='#0D0D1A'; LineNumFg='#00FF9F'; WordModified='#1A1A00'; HeaderBg='#1A1A33'; RulerBg='#0A0A14'; RulerModified='#FFE600'; RulerAdded='#00FF9F'; RulerRemoved='#FF003C' }
    'DarkGlass'       = @{ GutterBg='#121212'; LineNumFg='#808080'; WordModified='#2A2200'; HeaderBg='#1E1E2A'; RulerBg='#181820'; RulerModified='#CCCC00'; RulerAdded='#5ABF80'; RulerRemoved='#E05555' }
    'Dracula'         = @{ GutterBg='#21222C'; LineNumFg='#6272A4'; WordModified='#38310E'; HeaderBg='#2E303E'; RulerBg='#191A21'; RulerModified='#F1FA8C'; RulerAdded='#50FA7B'; RulerRemoved='#FF5555' }
    'Forest'          = @{ GutterBg='#16201A'; LineNumFg='#6A8060'; WordModified='#252200'; HeaderBg='#1E2A1E'; RulerBg='#121A14'; RulerModified='#AAAA00'; RulerAdded='#6AAA5A'; RulerRemoved='#D05050' }
    'GruvboxDark'     = @{ GutterBg='#1D2021'; LineNumFg='#928374'; WordModified='#352D00'; HeaderBg='#282828'; RulerBg='#1D2021'; RulerModified='#FABD2F'; RulerAdded='#8EC07C'; RulerRemoved='#FB4934' }
    'HighContrast'    = @{ GutterBg='#000000'; LineNumFg='#FFFFFF'; WordModified='#333300'; HeaderBg='#000000'; RulerBg='#000000'; RulerModified='#FFFF00'; RulerAdded='#00FF00'; RulerRemoved='#FF0000' }
    'Matrix'          = @{ GutterBg='#000A00'; LineNumFg='#00AA41'; WordModified='#0A1400'; HeaderBg='#001400'; RulerBg='#000800'; RulerModified='#88FF00'; RulerAdded='#00FF41'; RulerRemoved='#FF2200' }
    'Minimal'         = $light
    'Nord'            = @{ GutterBg='#2E3440'; LineNumFg='#4C566A'; WordModified='#35300A'; HeaderBg='#3B4252'; RulerBg='#2E3440'; RulerModified='#EBCB8B'; RulerAdded='#A3BE8C'; RulerRemoved='#BF616A' }
    'Office'          = $light
    'Synthwave84'     = @{ GutterBg='#1A1A2E'; LineNumFg='#848CA8'; WordModified='#2A2000'; HeaderBg='#2A2044'; RulerBg='#16162A'; RulerModified='#FF8B39'; RulerAdded='#72F1B8'; RulerRemoved='#FF2A6D' }
    'TokyoNight'      = @{ GutterBg='#16161E'; LineNumFg='#565F89'; WordModified='#25240A'; HeaderBg='#1F2335'; RulerBg='#13131D'; RulerModified='#E0AF68'; RulerAdded='#9ECE6A'; RulerRemoved='#F7768E' }
    'DockDark'        = $dark
    'DockLight'       = $light
}

function Get-Dfv2TokenBlock {
    param([hashtable]$t)

    return @"

    <!-- DiffV2 tokens (DF_*) — inserted by Add-DiffV2Tokens.ps1 -->
    <SolidColorBrush x:Key="DF_GutterBackgroundBrush"        Color="$($t.GutterBg)" />
    <SolidColorBrush x:Key="DF_LineNumberForegroundBrush"    Color="$($t.LineNumFg)" />
    <SolidColorBrush x:Key="DF_WordModifiedBrush"            Color="$($t.WordModified)" />
    <SolidColorBrush x:Key="DF_HeaderBackgroundBrush"        Color="$($t.HeaderBg)" />
    <SolidColorBrush x:Key="DF_OverviewRulerBackgroundBrush" Color="$($t.RulerBg)" />
    <SolidColorBrush x:Key="DF_OverviewModifiedBrush"        Color="$($t.RulerModified)" />
    <SolidColorBrush x:Key="DF_OverviewAddedBrush"           Color="$($t.RulerAdded)" />
    <SolidColorBrush x:Key="DF_OverviewRemovedBrush"         Color="$($t.RulerRemoved)" />
"@
}

function Inject-Tokens {
    param([string]$filePath, [hashtable]$t)

    $content = Get-Content $filePath -Raw -Encoding UTF8

    if ($content -match 'DF_GutterBackgroundBrush') {
        Write-Host "  SKIP (already present): $filePath"
        return
    }

    $block   = Get-Dfv2TokenBlock -t $t
    $updated = $content -replace '</ResourceDictionary>', "$block`n</ResourceDictionary>"
    Set-Content $filePath $updated -Encoding UTF8 -NoNewline
    Write-Host "  OK: $filePath"
}

Write-Host "`n=== Injecting DiffV2 DF_* tokens into Shell themes ==="
$shellThemes = @{
    'VS2022Dark'='VS2022Dark'; 'VisualStudio'='VisualStudio'; 'CatppuccinMocha'='CatppuccinMocha'
    'CatppuccinLatte'='CatppuccinLatte'; 'Cyberpunk'='Cyberpunk'; 'DarkGlass'='DarkGlass'
    'Dracula'='Dracula'; 'Forest'='Forest'; 'GruvboxDark'='GruvboxDark'
    'HighContrast'='HighContrast'; 'Matrix'='Matrix'; 'Minimal'='Minimal'
    'Nord'='Nord'; 'Office'='Office'; 'Synthwave84'='Synthwave84'; 'TokyoNight'='TokyoNight'
}

foreach ($themeName in $shellThemes.Keys) {
    $path = Join-Path $shell "$themeName\Colors.xaml"
    if (Test-Path $path) {
        Inject-Tokens -filePath $path -t $themes[$themeName]
    } else {
        Write-Warning "NOT FOUND: $path"
    }
}

Write-Host "`n=== Injecting DiffV2 DF_* tokens into Docking.Wpf themes ==="
Inject-Tokens -filePath (Join-Path $dock 'Dark\Colors.xaml')  -t $themes['DockDark']
Inject-Tokens -filePath (Join-Path $dock 'Light\Colors.xaml') -t $themes['DockLight']

Write-Host "`nDone. 8 new DF_* tokens injected into all 18 Colors.xaml files."
