# ==========================================================
# Add-UtTokens.ps1
# Inserts 8 UT_* SolidColorBrush tokens into every Colors.xaml
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
# Keys: Pass, Fail, Skip, FailRowBg, Toolbar, StatusBar, Hover, Selection
# ---------------------------------------------------------------------------
$themes = @{
    'VS2022Dark'      = @{ Pass='#4EC9B0'; Fail='#F14C4C'; Skip='#9CDCFE'; FailRowBg='#2A1515'; Toolbar='#2D2D30'; StatusBar='#1E1E1E'; Hover='#2A2D2E'; Selection='#094771' }
    'VisualStudio'    = @{ Pass='#1E8A6E'; Fail='#C0392B'; Skip='#2980B9'; FailRowBg='#FFEEEE'; Toolbar='#F0F0F0'; StatusBar='#E8E8E8'; Hover='#E8F0FE'; Selection='#CCE5FF' }
    'CatppuccinMocha' = @{ Pass='#94E2D5'; Fail='#F38BA8'; Skip='#89B4FA'; FailRowBg='#2A1520'; Toolbar='#313244'; StatusBar='#1E1E2E'; Hover='#313244'; Selection='#45475A' }
    'CatppuccinLatte' = @{ Pass='#179299'; Fail='#D20F39'; Skip='#1E66F5'; FailRowBg='#FFE0E6'; Toolbar='#CCD0DA'; StatusBar='#BCC0CC'; Hover='#DCE0E8'; Selection='#1E66F5' }
    'Cyberpunk'       = @{ Pass='#00FF9F'; Fail='#FF003C'; Skip='#00FFFF'; FailRowBg='#1A0008'; Toolbar='#1A1A33'; StatusBar='#0D0D1A'; Hover='#1A1A40'; Selection='#003333' }
    'DarkGlass'       = @{ Pass='#5ABFA0'; Fail='#E05555'; Skip='#7AB8E0'; FailRowBg='#261515'; Toolbar='#28283A'; StatusBar='#1C1C24'; Hover='#26263A'; Selection='#2A4A7A' }
    'Dracula'         = @{ Pass='#50FA7B'; Fail='#FF5555'; Skip='#8BE9FD'; FailRowBg='#2A1515'; Toolbar='#383A4A'; StatusBar='#282A36'; Hover='#383A4A'; Selection='#44475A' }
    'Forest'          = @{ Pass='#6AAA5A'; Fail='#D05050'; Skip='#6AB0C8'; FailRowBg='#2A1515'; Toolbar='#243020'; StatusBar='#1A2216'; Hover='#1E2C1A'; Selection='#2E5A22' }
    'GruvboxDark'     = @{ Pass='#8EC07C'; Fail='#FB4934'; Skip='#83A598'; FailRowBg='#2A1208'; Toolbar='#3C3836'; StatusBar='#282828'; Hover='#3C3836'; Selection='#458588' }
    'HighContrast'    = @{ Pass='#00FF00'; Fail='#FF0000'; Skip='#1AEBFF'; FailRowBg='#330000'; Toolbar='#000000'; StatusBar='#000000'; Hover='#001A00'; Selection='#1AEBFF' }
    'Matrix'          = @{ Pass='#00FF41'; Fail='#FF2200'; Skip='#00CC99'; FailRowBg='#1A0000'; Toolbar='#001400'; StatusBar='#010A01'; Hover='#001400'; Selection='#003300' }
    'Minimal'         = @{ Pass='#1E8A6E'; Fail='#C0392B'; Skip='#2980B9'; FailRowBg='#FFF0F0'; Toolbar='#EBEBEB'; StatusBar='#E0E0E0'; Hover='#E8E8E8'; Selection='#D0E4F7' }
    'Nord'            = @{ Pass='#A3BE8C'; Fail='#BF616A'; Skip='#81A1C1'; FailRowBg='#261A1A'; Toolbar='#3B4252'; StatusBar='#2E3440'; Hover='#3B4252'; Selection='#4C566A' }
    'Office'          = @{ Pass='#107C41'; Fail='#C0392B'; Skip='#0078D4'; FailRowBg='#FFF0F0'; Toolbar='#F0F0F0'; StatusBar='#E8E8E8'; Hover='#E8F0FE'; Selection='#0078D4' }
    'Synthwave84'     = @{ Pass='#72F1B8'; Fail='#FF2A6D'; Skip='#36F9F6'; FailRowBg='#2A0A18'; Toolbar='#34294F'; StatusBar='#2B213A'; Hover='#34294F'; Selection='#FF2A6D' }
    'TokyoNight'      = @{ Pass='#9ECE6A'; Fail='#F7768E'; Skip='#7AA2F7'; FailRowBg='#261520'; Toolbar='#24283B'; StatusBar='#1A1B26'; Hover='#24283B'; Selection='#3D59A1' }
    # Docking.Wpf dark/light
    'DockDark'        = @{ Pass='#4EC9B0'; Fail='#F14C4C'; Skip='#9CDCFE'; FailRowBg='#2A1515'; Toolbar='#2D2D30'; StatusBar='#1E1E1E'; Hover='#2A2D2E'; Selection='#094771' }
    'DockLight'       = @{ Pass='#1E8A6E'; Fail='#C0392B'; Skip='#2980B9'; FailRowBg='#FFEEEE'; Toolbar='#F0F0F0'; StatusBar='#E8E8E8'; Hover='#E8F0FE'; Selection='#CCE5FF' }
}

function Get-UtTokenBlock {
    param([hashtable]$t)

    return @"

    <!-- Unit Testing tokens (UT_*) — inserted by Add-UtTokens.ps1 -->
    <SolidColorBrush x:Key="UT_PassForegroundBrush"       Color="$($t.Pass)" />
    <SolidColorBrush x:Key="UT_FailForegroundBrush"       Color="$($t.Fail)" />
    <SolidColorBrush x:Key="UT_SkipForegroundBrush"       Color="$($t.Skip)" />
    <SolidColorBrush x:Key="UT_FailRowBackgroundBrush"    Color="$($t.FailRowBg)" />
    <SolidColorBrush x:Key="UT_ToolbarBackgroundBrush"    Color="$($t.Toolbar)" />
    <SolidColorBrush x:Key="UT_StatusBarBackgroundBrush"  Color="$($t.StatusBar)" />
    <SolidColorBrush x:Key="UT_HoverBrush"                Color="$($t.Hover)" />
    <SolidColorBrush x:Key="UT_SelectionBrush"            Color="$($t.Selection)" />
"@
}

function Inject-Tokens {
    param([string]$filePath, [hashtable]$themeColors)

    $content = Get-Content $filePath -Raw -Encoding UTF8

    if ($content -match 'UT_PassForegroundBrush') {
        Write-Host "  SKIP (already has UT_ tokens): $filePath"
        return
    }

    $block      = Get-UtTokenBlock -t $themeColors
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

Write-Host "`nInjecting UT_* tokens into Shell themes..."
foreach ($name in $shellMap.Keys) {
    Inject-Tokens -filePath $shellMap[$name] -themeColors $themes[$name]
}

# ---------------------------------------------------------------------------
# Docking.Wpf themes
# ---------------------------------------------------------------------------
Write-Host "`nInjecting UT_* tokens into Docking.Wpf themes..."
Inject-Tokens -filePath (Join-Path $dock 'Dark\Colors.xaml')  -themeColors $themes['DockDark']
Inject-Tokens -filePath (Join-Path $dock 'Light\Colors.xaml') -themeColors $themes['DockLight']

Write-Host "`nDone - 18 files processed."
