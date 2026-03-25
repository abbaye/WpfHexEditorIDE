# ==========================================================
# Add-EndBlockTokens.ps1
# Inserts 8 ET_* SolidColorBrush tokens into every Colors.xaml
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
# Keys: PopupBg, PopupBorder, HeaderBg, HeaderFg,
#       MetaFg, LineNumPill, LineCountPill, Accent
# ---------------------------------------------------------------------------
$themes = @{
    'VS2022Dark'      = @{ PopupBg='#1E1E1E'; PopupBorder='#3C3C3C'; HeaderBg='#252526'; HeaderFg='#D4D4D4'; MetaFg='#888888'; LineNumPill='#2D2D30'; LineCountPill='#3C3C3C'; Accent='#4EC9B0' }
    'VisualStudio'    = @{ PopupBg='#F5F5F5'; PopupBorder='#CCCCCC'; HeaderBg='#EBEBEB'; HeaderFg='#1E1E1E'; MetaFg='#555555'; LineNumPill='#E0E0E0'; LineCountPill='#D4D4D4'; Accent='#007ACC' }
    'CatppuccinMocha' = @{ PopupBg='#1E1E2E'; PopupBorder='#45475A'; HeaderBg='#181825'; HeaderFg='#CDD6F4'; MetaFg='#6C7086'; LineNumPill='#313244'; LineCountPill='#45475A'; Accent='#94E2D5' }
    'CatppuccinLatte' = @{ PopupBg='#EFF1F5'; PopupBorder='#BCC0CC'; HeaderBg='#E6E9EF'; HeaderFg='#4C4F69'; MetaFg='#8C8FA1'; LineNumPill='#DCE0E8'; LineCountPill='#CCD0DA'; Accent='#04A5E5' }
    'Cyberpunk'       = @{ PopupBg='#0D0D1A'; PopupBorder='#2A2A55'; HeaderBg='#0A0A14'; HeaderFg='#E0E0FF'; MetaFg='#5555AA'; LineNumPill='#1A1A33'; LineCountPill='#2A2A55'; Accent='#00FFFF' }
    'DarkGlass'       = @{ PopupBg='#1E2030'; PopupBorder='#3A3D55'; HeaderBg='#161826'; HeaderFg='#CCDDEE'; MetaFg='#667788'; LineNumPill='#28283A'; LineCountPill='#3A3D55'; Accent='#5ABFA0' }
    'Dracula'         = @{ PopupBg='#282A36'; PopupBorder='#44475A'; HeaderBg='#21222C'; HeaderFg='#F8F8F2'; MetaFg='#6272A4'; LineNumPill='#383A4A'; LineCountPill='#44475A'; Accent='#50FA7B' }
    'Forest'          = @{ PopupBg='#1A2018'; PopupBorder='#344030'; HeaderBg='#141A12'; HeaderFg='#D0E8D0'; MetaFg='#607060'; LineNumPill='#243020'; LineCountPill='#344030'; Accent='#6AAA5A' }
    'GruvboxDark'     = @{ PopupBg='#282828'; PopupBorder='#504945'; HeaderBg='#1D2021'; HeaderFg='#EBDBB2'; MetaFg='#665C54'; LineNumPill='#3C3836'; LineCountPill='#504945'; Accent='#8EC07C' }
    'HighContrast'    = @{ PopupBg='#000000'; PopupBorder='#FFFFFF'; HeaderBg='#000000'; HeaderFg='#FFFFFF'; MetaFg='#AAAAAA'; LineNumPill='#222222'; LineCountPill='#333333'; Accent='#00FF00' }
    'Matrix'          = @{ PopupBg='#001200'; PopupBorder='#005000'; HeaderBg='#000E00'; HeaderFg='#00FF41'; MetaFg='#336633'; LineNumPill='#001400'; LineCountPill='#003300'; Accent='#00FF41' }
    'Minimal'         = @{ PopupBg='#F8F8F8'; PopupBorder='#DDDDDD'; HeaderBg='#F0F0F0'; HeaderFg='#333333'; MetaFg='#777777'; LineNumPill='#E8E8E8'; LineCountPill='#DDDDDD'; Accent='#0066CC' }
    'Nord'            = @{ PopupBg='#2E3440'; PopupBorder='#4C566A'; HeaderBg='#252A33'; HeaderFg='#ECEFF4'; MetaFg='#4C566A'; LineNumPill='#3B4252'; LineCountPill='#434C5E'; Accent='#88C0D0' }
    'Office'          = @{ PopupBg='#F5F5F5'; PopupBorder='#D0D0D0'; HeaderBg='#EBEBEB'; HeaderFg='#1E1E1E'; MetaFg='#666666'; LineNumPill='#E2E2E2'; LineCountPill='#D8D8D8'; Accent='#0078D4' }
    'Synthwave84'     = @{ PopupBg='#1A1135'; PopupBorder='#4A3080'; HeaderBg='#130D28'; HeaderFg='#F8F8F2'; MetaFg='#6644AA'; LineNumPill='#241848'; LineCountPill='#3A2860'; Accent='#72F1B8' }
    'TokyoNight'      = @{ PopupBg='#1A1B26'; PopupBorder='#3D4166'; HeaderBg='#13141F'; HeaderFg='#C0CAF5'; MetaFg='#414868'; LineNumPill='#24283B'; LineCountPill='#3D4166'; Accent='#7AA2F7' }
    'DockDark'        = @{ PopupBg='#1E1E1E'; PopupBorder='#3C3C3C'; HeaderBg='#252526'; HeaderFg='#D4D4D4'; MetaFg='#888888'; LineNumPill='#2D2D30'; LineCountPill='#3C3C3C'; Accent='#4EC9B0' }
    'DockLight'       = @{ PopupBg='#F5F5F5'; PopupBorder='#CCCCCC'; HeaderBg='#EBEBEB'; HeaderFg='#1E1E1E'; MetaFg='#555555'; LineNumPill='#E0E0E0'; LineCountPill='#D4D4D4'; Accent='#007ACC' }
}

function Get-EtTokenBlock {
    param([hashtable]$t)
    return @"

    <!-- End-of-Block Hint tokens (ET_*) — inserted by Add-EndBlockTokens.ps1 -->
    <SolidColorBrush x:Key="ET_PopupBackground"    Color="$($t.PopupBg)" />
    <SolidColorBrush x:Key="ET_PopupBorderBrush"   Color="$($t.PopupBorder)" />
    <SolidColorBrush x:Key="ET_HeaderBackground"   Color="$($t.HeaderBg)" />
    <SolidColorBrush x:Key="ET_HeaderForeground"   Color="$($t.HeaderFg)" />
    <SolidColorBrush x:Key="ET_MetaForeground"     Color="$($t.MetaFg)" />
    <SolidColorBrush x:Key="ET_LineNumberPillBg"   Color="$($t.LineNumPill)" />
    <SolidColorBrush x:Key="ET_LineCountPillBg"    Color="$($t.LineCountPill)" />
    <SolidColorBrush x:Key="ET_AccentBrush"        Color="$($t.Accent)" />
"@
}

function Inject-Tokens {
    param([string]$filePath, [hashtable]$themeColors)

    $content = Get-Content $filePath -Raw -Encoding UTF8

    if ($content -match 'ET_PopupBackground') {
        Write-Host "  SKIP (already up-to-date): $filePath"
        return
    }

    $block      = Get-EtTokenBlock -t $themeColors
    $marker     = '</ResourceDictionary>'
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

Write-Host "`nInjecting ET_* tokens into Shell themes..."
foreach ($name in $shellMap.Keys) {
    Inject-Tokens -filePath $shellMap[$name] -themeColors $themes[$name]
}

# ---------------------------------------------------------------------------
# Docking.Wpf themes
# ---------------------------------------------------------------------------
Write-Host "`nInjecting ET_* tokens into Docking.Wpf themes..."
Inject-Tokens -filePath (Join-Path $dock 'Dark\Colors.xaml')  -themeColors $themes['DockDark']
Inject-Tokens -filePath (Join-Path $dock 'Light\Colors.xaml') -themeColors $themes['DockLight']

Write-Host "`nDone - 18 files processed."
