# ==========================================================
# Add-ResxTokens.ps1
# Inserts 20 RES_* SolidColorBrush tokens into every Colors.xaml
# theme file (16 Shell themes + 2 Docking.Wpf themes).
# Run from: Sources/
# ==========================================================
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root  = $PSScriptRoot
$shell = Join-Path $root 'WpfHexEditor.Shell\Themes'
$dock  = Join-Path $root 'WpfHexEditor.Docking.Wpf\Themes'

# ---------------------------------------------------------------------------
# Per-theme color map
# Keys: BG, FG, HeaderBG, HeaderFG, AltRow, SelBG, SelFG, Hover,
#       CellEditor, Comment, Sep, LocaleMatch, LocaleMiss, Toolbar
# Badge colors (String/Image/Binary/FileRef/Dupe/Missing) are fixed semantics
# ---------------------------------------------------------------------------
$themes = @{
    'VS2022Dark'      = @{ BG='#1E1E1E'; FG='#D4D4D4'; HeaderBG='#2D2D30'; HeaderFG='#9B9B9B'; AltRow='#1A1A1A'; SelBG='#094771'; SelFG='#FFFFFF'; Hover='#2A2D2E'; CellEditor='#252526'; Comment='#608B4E'; Sep='#3F3F46'; LocaleMatch='#1B3A1B'; LocaleMiss='#3A1B1B'; Toolbar='#2D2D30' }
    'VisualStudio'    = @{ BG='#FFFFFF'; FG='#1E1E1E'; HeaderBG='#F0F0F0'; HeaderFG='#717171'; AltRow='#F8F8F8'; SelBG='#CCE5FF'; SelFG='#000000'; Hover='#E8F0FE'; CellEditor='#FAFAFA'; Comment='#008000'; Sep='#CCCCCC'; LocaleMatch='#D4EDDA'; LocaleMiss='#FFCCCC'; Toolbar='#F0F0F0' }
    'CatppuccinMocha' = @{ BG='#1E1E2E'; FG='#CDD6F4'; HeaderBG='#313244'; HeaderFG='#A6ADC8'; AltRow='#181825'; SelBG='#45475A'; SelFG='#CDD6F4'; Hover='#313244'; CellEditor='#181825'; Comment='#94E2D5'; Sep='#45475A'; LocaleMatch='#1B3A2E'; LocaleMiss='#3A1B2E'; Toolbar='#313244' }
    'CatppuccinLatte' = @{ BG='#EFF1F5'; FG='#4C4F69'; HeaderBG='#CCD0DA'; HeaderFG='#7C7F93'; AltRow='#E6E9EF'; SelBG='#1E66F5'; SelFG='#FFFFFF'; Hover='#DCE0E8'; CellEditor='#E6E9EF'; Comment='#40A02B'; Sep='#ACB0BE'; LocaleMatch='#D4F0DC'; LocaleMiss='#F5D5D5'; Toolbar='#CCD0DA' }
    'Cyberpunk'       = @{ BG='#0D0D1A'; FG='#E0E0FF'; HeaderBG='#1A1A33'; HeaderFG='#8080C0'; AltRow='#0A0A14'; SelBG='#00FFFF'; SelFG='#000000'; Hover='#1A1A40'; CellEditor='#0D0D1A'; Comment='#00FF9F'; Sep='#1A1A40'; LocaleMatch='#003322'; LocaleMiss='#330022'; Toolbar='#1A1A33' }
    'DarkGlass'       = @{ BG='#1C1C24'; FG='#D0D4E0'; HeaderBG='#28283A'; HeaderFG='#8A8DAA'; AltRow='#181820'; SelBG='#2A4A7A'; SelFG='#FFFFFF'; Hover='#26263A'; CellEditor='#1C1C24'; Comment='#5A8A6A'; Sep='#2E2E42'; LocaleMatch='#1A3A1A'; LocaleMiss='#3A1A1A'; Toolbar='#28283A' }
    'Dracula'         = @{ BG='#282A36'; FG='#F8F8F2'; HeaderBG='#383A4A'; HeaderFG='#6272A4'; AltRow='#21222C'; SelBG='#44475A'; SelFG='#F8F8F2'; Hover='#383A4A'; CellEditor='#21222C'; Comment='#50FA7B'; Sep='#44475A'; LocaleMatch='#1A3A1A'; LocaleMiss='#3A1A1A'; Toolbar='#383A4A' }
    'Forest'          = @{ BG='#1A2216'; FG='#C8D8C0'; HeaderBG='#243020'; HeaderFG='#7A9A72'; AltRow='#162012'; SelBG='#2E5A22'; SelFG='#FFFFFF'; Hover='#1E2C1A'; CellEditor='#1A2216'; Comment='#6AAA5A'; Sep='#2E3E28'; LocaleMatch='#1A3A18'; LocaleMiss='#3A1A18'; Toolbar='#243020' }
    'GruvboxDark'     = @{ BG='#282828'; FG='#EBDBB2'; HeaderBG='#3C3836'; HeaderFG='#928374'; AltRow='#1D2021'; SelBG='#458588'; SelFG='#EBDBB2'; Hover='#3C3836'; CellEditor='#1D2021'; Comment='#8EC07C'; Sep='#504945'; LocaleMatch='#1A3A1A'; LocaleMiss='#3A1A1A'; Toolbar='#3C3836' }
    'HighContrast'    = @{ BG='#000000'; FG='#FFFFFF'; HeaderBG='#000000'; HeaderFG='#FFFF00'; AltRow='#0A0A0A'; SelBG='#1AEBFF'; SelFG='#000000'; Hover='#001A00'; CellEditor='#000000'; Comment='#00FF00'; Sep='#FFFFFF'; LocaleMatch='#003300'; LocaleMiss='#330000'; Toolbar='#000000' }
    'Matrix'          = @{ BG='#010A01'; FG='#00FF41'; HeaderBG='#001400'; HeaderFG='#00CC33'; AltRow='#000800'; SelBG='#003300'; SelFG='#00FF41'; Hover='#001400'; CellEditor='#010A01'; Comment='#00AA22'; Sep='#004400'; LocaleMatch='#003300'; LocaleMiss='#1A0000'; Toolbar='#001400' }
    'Minimal'         = @{ BG='#FAFAFA'; FG='#1A1A1A'; HeaderBG='#EBEBEB'; HeaderFG='#888888'; AltRow='#F3F3F3'; SelBG='#D0E4F7'; SelFG='#000000'; Hover='#E8E8E8'; CellEditor='#FFFFFF'; Comment='#5A8A5A'; Sep='#CCCCCC'; LocaleMatch='#D4F0D4'; LocaleMiss='#F7D4D4'; Toolbar='#EBEBEB' }
    'Nord'            = @{ BG='#2E3440'; FG='#ECEFF4'; HeaderBG='#3B4252'; HeaderFG='#7B8FAA'; AltRow='#282C38'; SelBG='#4C566A'; SelFG='#ECEFF4'; Hover='#3B4252'; CellEditor='#2E3440'; Comment='#A3BE8C'; Sep='#4C566A'; LocaleMatch='#1B3A1B'; LocaleMiss='#3A1B1B'; Toolbar='#3B4252' }
    'Office'          = @{ BG='#FFFFFF'; FG='#1E1E1E'; HeaderBG='#F0F0F0'; HeaderFG='#505050'; AltRow='#F8F8F8'; SelBG='#0078D4'; SelFG='#FFFFFF'; Hover='#E8F0FE'; CellEditor='#FFFFFF'; Comment='#1B7F1B'; Sep='#D0D0D0'; LocaleMatch='#D4F0DC'; LocaleMiss='#FFD4D4'; Toolbar='#F0F0F0' }
    'Synthwave84'     = @{ BG='#2B213A'; FG='#F9F9F9'; HeaderBG='#34294F'; HeaderFG='#988BC7'; AltRow='#231B31'; SelBG='#FF2A6D'; SelFG='#FFFFFF'; Hover='#34294F'; CellEditor='#231B31'; Comment='#36F9F6'; Sep='#4A3D66'; LocaleMatch='#1B3A1B'; LocaleMiss='#3A1B1B'; Toolbar='#34294F' }
    'TokyoNight'      = @{ BG='#1A1B26'; FG='#C0CAF5'; HeaderBG='#24283B'; HeaderFG='#565F89'; AltRow='#13141F'; SelBG='#3D59A1'; SelFG='#C0CAF5'; Hover='#24283B'; CellEditor='#1A1B26'; Comment='#9ECE6A'; Sep='#292E42'; LocaleMatch='#1B3A1B'; LocaleMiss='#3A1B1B'; Toolbar='#24283B' }
    # Docking.Wpf dark/light
    'DockDark'        = @{ BG='#1E1E1E'; FG='#D4D4D4'; HeaderBG='#2D2D30'; HeaderFG='#9B9B9B'; AltRow='#1A1A1A'; SelBG='#094771'; SelFG='#FFFFFF'; Hover='#2A2D2E'; CellEditor='#252526'; Comment='#608B4E'; Sep='#3F3F46'; LocaleMatch='#1B3A1B'; LocaleMiss='#3A1B1B'; Toolbar='#2D2D30' }
    'DockLight'       = @{ BG='#FFFFFF'; FG='#1E1E1E'; HeaderBG='#F0F0F0'; HeaderFG='#717171'; AltRow='#F8F8F8'; SelBG='#CCE5FF'; SelFG='#000000'; Hover='#E8F0FE'; CellEditor='#FAFAFA'; Comment='#008000'; Sep='#CCCCCC'; LocaleMatch='#D4EDDA'; LocaleMiss='#FFCCCC'; Toolbar='#F0F0F0' }
}

# ---------------------------------------------------------------------------
# Fixed badge/semantic colors (same across all themes)
# ---------------------------------------------------------------------------
$fixed = @{
    StringBadge  = '#2E7D32'
    ImageBadge   = '#1565C0'
    BinaryBadge  = '#E65100'
    FileRefBadge = '#6A1B9A'
    DupeKey      = '#C62828'
    Missing      = '#F57F17'
}

function Get-ResxTokenBlock {
    param([hashtable]$t)

    $b = $fixed

    return @"

    <!-- RESX Editor tokens (RES_*) — inserted by Add-ResxTokens.ps1 -->
    <SolidColorBrush x:Key="RES_BackgroundBrush"          Color="$($t.BG)" />
    <SolidColorBrush x:Key="RES_ForegroundBrush"          Color="$($t.FG)" />
    <SolidColorBrush x:Key="RES_HeaderBackgroundBrush"    Color="$($t.HeaderBG)" />
    <SolidColorBrush x:Key="RES_HeaderForegroundBrush"    Color="$($t.HeaderFG)" />
    <SolidColorBrush x:Key="RES_RowAlternateBrush"        Color="$($t.AltRow)" />
    <SolidColorBrush x:Key="RES_SelectedRowBrush"         Color="$($t.SelBG)" />
    <SolidColorBrush x:Key="RES_SelectedRowForegroundBrush" Color="$($t.SelFG)" />
    <SolidColorBrush x:Key="RES_HoverRowBrush"            Color="$($t.Hover)" />
    <SolidColorBrush x:Key="RES_StringTypeBadgeBrush"     Color="$($b.StringBadge)" />
    <SolidColorBrush x:Key="RES_ImageTypeBadgeBrush"      Color="$($b.ImageBadge)" />
    <SolidColorBrush x:Key="RES_BinaryTypeBadgeBrush"     Color="$($b.BinaryBadge)" />
    <SolidColorBrush x:Key="RES_FileRefTypeBadgeBrush"    Color="$($b.FileRefBadge)" />
    <SolidColorBrush x:Key="RES_DuplicateKeyBrush"        Color="$($b.DupeKey)" />
    <SolidColorBrush x:Key="RES_MissingTranslationBrush"  Color="$($b.Missing)" />
    <SolidColorBrush x:Key="RES_CellEditorBackgroundBrush" Color="$($t.CellEditor)" />
    <SolidColorBrush x:Key="RES_CommentForegroundBrush"   Color="$($t.Comment)" />
    <SolidColorBrush x:Key="RES_SeparatorBrush"           Color="$($t.Sep)" />
    <SolidColorBrush x:Key="RES_LocaleMatchBrush"         Color="$($t.LocaleMatch)" />
    <SolidColorBrush x:Key="RES_LocaleMissingBrush"       Color="$($t.LocaleMiss)" />
    <SolidColorBrush x:Key="RES_ToolbarBackgroundBrush"   Color="$($t.Toolbar)" />
"@
}

function Inject-Tokens {
    param([string]$filePath, [hashtable]$themeColors)

    $content = Get-Content $filePath -Raw -Encoding UTF8

    if ($content -match 'RES_BackgroundBrush') {
        Write-Host "  SKIP (already has RES_ tokens): $filePath"
        return
    }

    $block  = Get-ResxTokenBlock -t $themeColors
    $marker = '</ResourceDictionary>'
    $newContent = $content.TrimEnd() -replace [regex]::Escape($marker), "$block`n$marker"

    Set-Content $filePath $newContent -Encoding UTF8 -NoNewline
    Write-Host "  OK: $filePath"
}

# ---------------------------------------------------------------------------
# Shell themes
# ---------------------------------------------------------------------------
$shellMap = @{
    'VS2022Dark'      = (Join-Path $shell 'VS2022Dark\Colors.xaml')
    'VisualStudio'    = (Join-Path $shell 'VisualStudio\Colors.xaml')
    'CatppuccinMocha' = (Join-Path $shell 'CatppuccinMocha\Colors.xaml')
    'CatppuccinLatte' = (Join-Path $shell 'CatppuccinLatte\Colors.xaml')
    'Cyberpunk'       = (Join-Path $shell 'Cyberpunk\Colors.xaml')
    'DarkGlass'       = (Join-Path $shell 'DarkGlass\Colors.xaml')
    'Dracula'         = (Join-Path $shell 'Dracula\Colors.xaml')
    'Forest'          = (Join-Path $shell 'Forest\Colors.xaml')
    'GruvboxDark'     = (Join-Path $shell 'GruvboxDark\Colors.xaml')
    'HighContrast'    = (Join-Path $shell 'HighContrast\Colors.xaml')
    'Matrix'          = (Join-Path $shell 'Matrix\Colors.xaml')
    'Minimal'         = (Join-Path $shell 'Minimal\Colors.xaml')
    'Nord'            = (Join-Path $shell 'Nord\Colors.xaml')
    'Office'          = (Join-Path $shell 'Office\Colors.xaml')
    'Synthwave84'     = (Join-Path $shell 'Synthwave84\Colors.xaml')
    'TokyoNight'      = (Join-Path $shell 'TokyoNight\Colors.xaml')
}

Write-Host "`nInjecting RES_* tokens into Shell themes..."
foreach ($name in $shellMap.Keys) {
    Inject-Tokens -filePath $shellMap[$name] -themeColors $themes[$name]
}

# ---------------------------------------------------------------------------
# Docking.Wpf themes
# ---------------------------------------------------------------------------
Write-Host "`nInjecting RES_* tokens into Docking.Wpf themes..."
Inject-Tokens -filePath (Join-Path $dock 'Dark\Colors.xaml')  -themeColors $themes['DockDark']
Inject-Tokens -filePath (Join-Path $dock 'Light\Colors.xaml') -themeColors $themes['DockLight']

Write-Host "`nDone - 18 files processed."
