# ==========================================================
# Add-ScriptTokens.ps1
# Inserts 8 SR_* SolidColorBrush tokens into every Colors.xaml
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
# Keys: Toolbar, CodeBg, CodeFg, OutputBg, OutputFg, StatusBar, Running, Error
# ---------------------------------------------------------------------------
$themes = @{
    'VS2022Dark'      = @{ Toolbar='#2D2D30'; CodeBg='#1E1E1E'; CodeFg='#D4D4D4'; OutputBg='#252526'; OutputFg='#CCCCCC'; StatusBar='#1E1E1E'; Running='#9CDCFE'; Error='#F14C4C' }
    'VisualStudio'    = @{ Toolbar='#F0F0F0'; CodeBg='#FFFFFF'; CodeFg='#1E1E1E'; OutputBg='#F8F8F8'; OutputFg='#333333'; StatusBar='#E8E8E8'; Running='#2980B9'; Error='#C0392B' }
    'CatppuccinMocha' = @{ Toolbar='#313244'; CodeBg='#1E1E2E'; CodeFg='#CDD6F4'; OutputBg='#181825'; OutputFg='#BAC2DE'; StatusBar='#1E1E2E'; Running='#89B4FA'; Error='#F38BA8' }
    'CatppuccinLatte' = @{ Toolbar='#CCD0DA'; CodeBg='#EFF1F5'; CodeFg='#4C4F69'; OutputBg='#E6E9EF'; OutputFg='#5C5F77'; StatusBar='#BCC0CC'; Running='#1E66F5'; Error='#D20F39' }
    'Cyberpunk'       = @{ Toolbar='#1A1A33'; CodeBg='#0D0D1A'; CodeFg='#00FFFF'; OutputBg='#050510'; OutputFg='#00FF9F'; StatusBar='#0D0D1A'; Running='#FF003C'; Error='#FF003C' }
    'DarkGlass'       = @{ Toolbar='#28283A'; CodeBg='#1C1C24'; CodeFg='#CCCCDD'; OutputBg='#16161E'; OutputFg='#AAAACC'; StatusBar='#1C1C24'; Running='#7AB8E0'; Error='#E05555' }
    'Dracula'         = @{ Toolbar='#383A4A'; CodeBg='#282A36'; CodeFg='#F8F8F2'; OutputBg='#21222C'; OutputFg='#ABB2BF'; StatusBar='#282A36'; Running='#8BE9FD'; Error='#FF5555' }
    'Forest'          = @{ Toolbar='#243020'; CodeBg='#1A2216'; CodeFg='#D0E8C8'; OutputBg='#141C10'; OutputFg='#A8C898'; StatusBar='#1A2216'; Running='#6AB0C8'; Error='#D05050' }
    'GruvboxDark'     = @{ Toolbar='#3C3836'; CodeBg='#282828'; CodeFg='#EBDBB2'; OutputBg='#1D2021'; OutputFg='#BDAE93'; StatusBar='#282828'; Running='#83A598'; Error='#FB4934' }
    'HighContrast'    = @{ Toolbar='#000000'; CodeBg='#000000'; CodeFg='#FFFFFF'; OutputBg='#000000'; OutputFg='#FFFFFF'; StatusBar='#000000'; Running='#1AEBFF'; Error='#FF0000' }
    'Matrix'          = @{ Toolbar='#001400'; CodeBg='#010A01'; CodeFg='#00FF41'; OutputBg='#000800'; OutputFg='#00CC30'; StatusBar='#010A01'; Running='#00CC99'; Error='#FF2200' }
    'Minimal'         = @{ Toolbar='#EBEBEB'; CodeBg='#FFFFFF'; CodeFg='#1A1A1A'; OutputBg='#F5F5F5'; OutputFg='#444444'; StatusBar='#E0E0E0'; Running='#2980B9'; Error='#C0392B' }
    'Nord'            = @{ Toolbar='#3B4252'; CodeBg='#2E3440'; CodeFg='#D8DEE9'; OutputBg='#242932'; OutputFg='#BBC5D4'; StatusBar='#2E3440'; Running='#81A1C1'; Error='#BF616A' }
    'Office'          = @{ Toolbar='#F0F0F0'; CodeBg='#FFFFFF'; CodeFg='#1A1A1A'; OutputBg='#F8F8F8'; OutputFg='#333333'; StatusBar='#E8E8E8'; Running='#0078D4'; Error='#C0392B' }
    'Synthwave84'     = @{ Toolbar='#34294F'; CodeBg='#2B213A'; CodeFg='#F92AFF'; OutputBg='#231B31'; OutputFg='#72F1B8'; StatusBar='#2B213A'; Running='#36F9F6'; Error='#FF2A6D' }
    'TokyoNight'      = @{ Toolbar='#24283B'; CodeBg='#1A1B26'; CodeFg='#A9B1D6'; OutputBg='#16161E'; OutputFg='#787C99'; StatusBar='#1A1B26'; Running='#7AA2F7'; Error='#F7768E' }
    # Docking.Wpf dark/light
    'DockDark'        = @{ Toolbar='#2D2D30'; CodeBg='#1E1E1E'; CodeFg='#D4D4D4'; OutputBg='#252526'; OutputFg='#CCCCCC'; StatusBar='#1E1E1E'; Running='#9CDCFE'; Error='#F14C4C' }
    'DockLight'       = @{ Toolbar='#F0F0F0'; CodeBg='#FFFFFF'; CodeFg='#1E1E1E'; OutputBg='#F8F8F8'; OutputFg='#333333'; StatusBar='#E8E8E8'; Running='#2980B9'; Error='#C0392B' }
}

function Get-SrTokenBlock {
    param([hashtable]$t)

    return @"

    <!-- Script Runner tokens (SR_*) — inserted by Add-ScriptTokens.ps1 -->
    <SolidColorBrush x:Key="SR_ToolbarBackgroundBrush"   Color="$($t.Toolbar)" />
    <SolidColorBrush x:Key="SR_CodeBackgroundBrush"      Color="$($t.CodeBg)" />
    <SolidColorBrush x:Key="SR_CodeForegroundBrush"      Color="$($t.CodeFg)" />
    <SolidColorBrush x:Key="SR_OutputBackgroundBrush"    Color="$($t.OutputBg)" />
    <SolidColorBrush x:Key="SR_OutputForegroundBrush"    Color="$($t.OutputFg)" />
    <SolidColorBrush x:Key="SR_StatusBarBackgroundBrush" Color="$($t.StatusBar)" />
    <SolidColorBrush x:Key="SR_RunningForegroundBrush"   Color="$($t.Running)" />
    <SolidColorBrush x:Key="SR_ErrorForegroundBrush"     Color="$($t.Error)" />
"@
}

function Inject-Tokens {
    param([string]$filePath, [hashtable]$themeColors)

    $content = Get-Content $filePath -Raw -Encoding UTF8

    if ($content -match 'SR_ToolbarBackgroundBrush') {
        Write-Host "  SKIP (already present): $filePath"
        return
    }

    $block      = Get-SrTokenBlock -t $themeColors
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

Write-Host "`nInjecting SR_* tokens into Shell themes..."
foreach ($name in $shellMap.Keys) {
    Inject-Tokens -filePath $shellMap[$name] -themeColors $themes[$name]
}

# ---------------------------------------------------------------------------
# Docking.Wpf themes
# ---------------------------------------------------------------------------
Write-Host "`nInjecting SR_* tokens into Docking.Wpf themes..."
Inject-Tokens -filePath (Join-Path $dock 'Dark\Colors.xaml')  -themeColors $themes['DockDark']
Inject-Tokens -filePath (Join-Path $dock 'Light\Colors.xaml') -themeColors $themes['DockLight']

Write-Host "`nDone - 18 files processed."
