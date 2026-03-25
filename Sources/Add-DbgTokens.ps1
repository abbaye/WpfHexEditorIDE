# ==========================================================
# Add-DbgTokens.ps1
# Inserts 14 DB_* SolidColorBrush tokens into every Colors.xaml
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
# Keys: BpActive, BpDisabled, BpConditional, ExecLine, ExecLineBg,
#       ExceptionLine, CallStackActive, WatchError, LocalChanged,
#       ConsoleStdout, ConsoleStderr, ConsoleDebug, Toolbar, StatusActive
# ---------------------------------------------------------------------------
$themes = @{
    'VS2022Dark'      = @{ BpActive='#E51400'; BpDisabled='#888888'; BpConditional='#FF8C00'; ExecLine='#FFDD00'; ExecLineBg='#30FFDD00'; ExceptionLine='#FF5555'; CallStackActive='#4EC9B0'; WatchError='#F14C4C'; LocalChanged='#CE9178'; ConsoleStdout='#CCCCCC'; ConsoleStderr='#F14C4C'; ConsoleDebug='#888888'; Toolbar='#2D2D30'; StatusActive='#C80000' }
    'VisualStudio'    = @{ BpActive='#C80000'; BpDisabled='#888888'; BpConditional='#E07700'; ExecLine='#FFE000'; ExecLineBg='#30FFE000'; ExceptionLine='#C0392B'; CallStackActive='#1E8A6E'; WatchError='#C0392B'; LocalChanged='#0070C1'; ConsoleStdout='#1E1E1E'; ConsoleStderr='#C0392B'; ConsoleDebug='#666666'; Toolbar='#F0F0F0'; StatusActive='#C80000' }
    'CatppuccinMocha' = @{ BpActive='#F38BA8'; BpDisabled='#6C7086'; BpConditional='#FAB387'; ExecLine='#F9E2AF'; ExecLineBg='#30F9E2AF'; ExceptionLine='#F38BA8'; CallStackActive='#94E2D5'; WatchError='#F38BA8'; LocalChanged='#CBA6F7'; ConsoleStdout='#CDD6F4'; ConsoleStderr='#F38BA8'; ConsoleDebug='#6C7086'; Toolbar='#313244'; StatusActive='#F38BA8' }
    'CatppuccinLatte' = @{ BpActive='#D20F39'; BpDisabled='#9CA0B0'; BpConditional='#FE640B'; ExecLine='#DF8E1D'; ExecLineBg='#30DF8E1D'; ExceptionLine='#D20F39'; CallStackActive='#179299'; WatchError='#D20F39'; LocalChanged='#8839EF'; ConsoleStdout='#4C4F69'; ConsoleStderr='#D20F39'; ConsoleDebug='#9CA0B0'; Toolbar='#CCD0DA'; StatusActive='#D20F39' }
    'Cyberpunk'       = @{ BpActive='#FF003C'; BpDisabled='#555577'; BpConditional='#FF8800'; ExecLine='#FFFF00'; ExecLineBg='#30FFFF00'; ExceptionLine='#FF003C'; CallStackActive='#00FF9F'; WatchError='#FF003C'; LocalChanged='#00FFFF'; ConsoleStdout='#E0E0FF'; ConsoleStderr='#FF003C'; ConsoleDebug='#555577'; Toolbar='#1A1A33'; StatusActive='#FF003C' }
    'DarkGlass'       = @{ BpActive='#E05555'; BpDisabled='#667788'; BpConditional='#E08840'; ExecLine='#FFE060'; ExecLineBg='#30FFE060'; ExceptionLine='#E05555'; CallStackActive='#5ABFA0'; WatchError='#E05555'; LocalChanged='#9988CC'; ConsoleStdout='#CCDDEE'; ConsoleStderr='#E05555'; ConsoleDebug='#667788'; Toolbar='#28283A'; StatusActive='#E05555' }
    'Dracula'         = @{ BpActive='#FF5555'; BpDisabled='#6272A4'; BpConditional='#FFB86C'; ExecLine='#F1FA8C'; ExecLineBg='#30F1FA8C'; ExceptionLine='#FF5555'; CallStackActive='#50FA7B'; WatchError='#FF5555'; LocalChanged='#BD93F9'; ConsoleStdout='#F8F8F2'; ConsoleStderr='#FF5555'; ConsoleDebug='#6272A4'; Toolbar='#383A4A'; StatusActive='#FF5555' }
    'Forest'          = @{ BpActive='#D05050'; BpDisabled='#607060'; BpConditional='#C87020'; ExecLine='#E8D840'; ExecLineBg='#30E8D840'; ExceptionLine='#D05050'; CallStackActive='#6AAA5A'; WatchError='#D05050'; LocalChanged='#8888CC'; ConsoleStdout='#D0E8D0'; ConsoleStderr='#D05050'; ConsoleDebug='#607060'; Toolbar='#243020'; StatusActive='#D05050' }
    'GruvboxDark'     = @{ BpActive='#FB4934'; BpDisabled='#665C54'; BpConditional='#FE8019'; ExecLine='#FABD2F'; ExecLineBg='#30FABD2F'; ExceptionLine='#FB4934'; CallStackActive='#8EC07C'; WatchError='#FB4934'; LocalChanged='#D3869B'; ConsoleStdout='#EBDBB2'; ConsoleStderr='#FB4934'; ConsoleDebug='#665C54'; Toolbar='#3C3836'; StatusActive='#CC241D' }
    'HighContrast'    = @{ BpActive='#FF0000'; BpDisabled='#888888'; BpConditional='#FF8800'; ExecLine='#FFFF00'; ExecLineBg='#30FFFF00'; ExceptionLine='#FF0000'; CallStackActive='#00FF00'; WatchError='#FF0000'; LocalChanged='#1AEBFF'; ConsoleStdout='#FFFFFF'; ConsoleStderr='#FF0000'; ConsoleDebug='#888888'; Toolbar='#000000'; StatusActive='#FF0000' }
    'Matrix'          = @{ BpActive='#FF2200'; BpDisabled='#336633'; BpConditional='#FF8800'; ExecLine='#00FF41'; ExecLineBg='#3000FF41'; ExceptionLine='#FF2200'; CallStackActive='#00FF41'; WatchError='#FF2200'; LocalChanged='#00CC99'; ConsoleStdout='#00FF41'; ConsoleStderr='#FF2200'; ConsoleDebug='#336633'; Toolbar='#001400'; StatusActive='#FF2200' }
    'Minimal'         = @{ BpActive='#CC0000'; BpDisabled='#999999'; BpConditional='#CC6600'; ExecLine='#DDCC00'; ExecLineBg='#30DDCC00'; ExceptionLine='#CC0000'; CallStackActive='#1E8A6E'; WatchError='#CC0000'; LocalChanged='#0066CC'; ConsoleStdout='#333333'; ConsoleStderr='#CC0000'; ConsoleDebug='#999999'; Toolbar='#EBEBEB'; StatusActive='#CC0000' }
    'Nord'            = @{ BpActive='#BF616A'; BpDisabled='#4C566A'; BpConditional='#D08770'; ExecLine='#EBCB8B'; ExecLineBg='#30EBCB8B'; ExceptionLine='#BF616A'; CallStackActive='#A3BE8C'; WatchError='#BF616A'; LocalChanged='#B48EAD'; ConsoleStdout='#ECEFF4'; ConsoleStderr='#BF616A'; ConsoleDebug='#4C566A'; Toolbar='#3B4252'; StatusActive='#BF616A' }
    'Office'          = @{ BpActive='#C0392B'; BpDisabled='#999999'; BpConditional='#D05000'; ExecLine='#DDB800'; ExecLineBg='#30DDB800'; ExceptionLine='#C0392B'; CallStackActive='#107C41'; WatchError='#C0392B'; LocalChanged='#0078D4'; ConsoleStdout='#333333'; ConsoleStderr='#C0392B'; ConsoleDebug='#999999'; Toolbar='#F0F0F0'; StatusActive='#C0392B' }
    'Synthwave84'     = @{ BpActive='#FF2A6D'; BpDisabled='#6644AA'; BpConditional='#FF7700'; ExecLine='#FFD700'; ExecLineBg='#30FFD700'; ExceptionLine='#FF2A6D'; CallStackActive='#72F1B8'; WatchError='#FF2A6D'; LocalChanged='#FE4450'; ConsoleStdout='#F8F8F2'; ConsoleStderr='#FF2A6D'; ConsoleDebug='#6644AA'; Toolbar='#34294F'; StatusActive='#FF2A6D' }
    'TokyoNight'      = @{ BpActive='#F7768E'; BpDisabled='#414868'; BpConditional='#FF9E64'; ExecLine='#E0AF68'; ExecLineBg='#30E0AF68'; ExceptionLine='#F7768E'; CallStackActive='#9ECE6A'; WatchError='#F7768E'; LocalChanged='#BB9AF7'; ConsoleStdout='#C0CAF5'; ConsoleStderr='#F7768E'; ConsoleDebug='#414868'; Toolbar='#24283B'; StatusActive='#F7768E' }
    'DockDark'        = @{ BpActive='#E51400'; BpDisabled='#888888'; BpConditional='#FF8C00'; ExecLine='#FFDD00'; ExecLineBg='#30FFDD00'; ExceptionLine='#FF5555'; CallStackActive='#4EC9B0'; WatchError='#F14C4C'; LocalChanged='#CE9178'; ConsoleStdout='#CCCCCC'; ConsoleStderr='#F14C4C'; ConsoleDebug='#888888'; Toolbar='#2D2D30'; StatusActive='#C80000' }
    'DockLight'       = @{ BpActive='#C80000'; BpDisabled='#888888'; BpConditional='#E07700'; ExecLine='#FFE000'; ExecLineBg='#30FFE000'; ExceptionLine='#C0392B'; CallStackActive='#1E8A6E'; WatchError='#C0392B'; LocalChanged='#0070C1'; ConsoleStdout='#1E1E1E'; ConsoleStderr='#C0392B'; ConsoleDebug='#666666'; Toolbar='#F0F0F0'; StatusActive='#C80000' }
}

function Get-DbgTokenBlock {
    param([hashtable]$t)
    return @"

    <!-- Debugger tokens (DB_*) — inserted by Add-DbgTokens.ps1 -->
    <SolidColorBrush x:Key="DB_BreakpointActiveBrush"        Color="$($t.BpActive)" />
    <SolidColorBrush x:Key="DB_BreakpointDisabledBrush"      Color="$($t.BpDisabled)" />
    <SolidColorBrush x:Key="DB_BreakpointConditionalBrush"   Color="$($t.BpConditional)" />
    <SolidColorBrush x:Key="DB_ExecutionLineBrush"           Color="$($t.ExecLine)" />
    <SolidColorBrush x:Key="DB_ExecutionLineBackgroundBrush" Color="$($t.ExecLineBg)" />
    <SolidColorBrush x:Key="DB_ExceptionLineBrush"           Color="$($t.ExceptionLine)" />
    <SolidColorBrush x:Key="DB_CallStackActiveBrush"         Color="$($t.CallStackActive)" />
    <SolidColorBrush x:Key="DB_WatchErrorBrush"              Color="$($t.WatchError)" />
    <SolidColorBrush x:Key="DB_LocalChangedBrush"            Color="$($t.LocalChanged)" />
    <SolidColorBrush x:Key="DB_ConsoleStdoutBrush"           Color="$($t.ConsoleStdout)" />
    <SolidColorBrush x:Key="DB_ConsoleStderrBrush"           Color="$($t.ConsoleStderr)" />
    <SolidColorBrush x:Key="DB_ConsoleDebugBrush"            Color="$($t.ConsoleDebug)" />
    <SolidColorBrush x:Key="DB_ToolbarBackgroundBrush"       Color="$($t.Toolbar)" />
    <SolidColorBrush x:Key="DB_StatusActiveBrush"            Color="$($t.StatusActive)" />
"@
}

function Inject-Tokens {
    param([string]$filePath, [hashtable]$themeColors)

    $content = Get-Content $filePath -Raw -Encoding UTF8

    if ($content -match 'DB_BreakpointActiveBrush') {
        Write-Host "  SKIP (already up-to-date): $filePath"
        return
    }

    $block      = Get-DbgTokenBlock -t $themeColors
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

Write-Host "`nInjecting DB_* tokens into Shell themes..."
foreach ($name in $shellMap.Keys) {
    Inject-Tokens -filePath $shellMap[$name] -themeColors $themes[$name]
}

# ---------------------------------------------------------------------------
# Docking.Wpf themes
# ---------------------------------------------------------------------------
Write-Host "`nInjecting DB_* tokens into Docking.Wpf themes..."
Inject-Tokens -filePath (Join-Path $dock 'Dark\Colors.xaml')  -themeColors $themes['DockDark']
Inject-Tokens -filePath (Join-Path $dock 'Light\Colors.xaml') -themeColors $themes['DockLight']

Write-Host "`nDone - 18 files processed."
