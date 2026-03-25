# ==========================================================
# Add-DiffTokens.ps1
# Inserts 14 DF_* SolidColorBrush tokens into every Colors.xaml
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
# Keys: Modified, Inserted, Deleted, Identical, CharModified,
#       FoldFg, FoldBg, MmModified, MmInserted, MmDeleted, MmIdentical,
#       Patched, PickerBg, PickerHL
# ---------------------------------------------------------------------------
$themes = @{
    'VS2022Dark'      = @{ Modified='#3A3A00'; Inserted='#14300F'; Deleted='#3A0F0F'; Identical='Transparent'; CharModified='#888800'; FoldFg='#858585'; FoldBg='#2D2D2D'; MmModified='#B8A000'; MmInserted='#4EC94E'; MmDeleted='#F14C4C'; MmIdentical='#404040'; Patched='#003A2A'; PickerBg='#252526'; PickerHL='#04395E' }
    'VisualStudio'    = @{ Modified='#FFF8D6'; Inserted='#E6FFED'; Deleted='#FFECEC'; Identical='Transparent'; CharModified='#FFDC00'; FoldFg='#6A6A6A'; FoldBg='#F0F0F0'; MmModified='#B8A000'; MmInserted='#24A349'; MmDeleted='#C0392B'; MmIdentical='#D0D0D0'; Patched='#E6FFF8'; PickerBg='#F8F8F8'; PickerHL='#CCE5FF' }
    'CatppuccinMocha' = @{ Modified='#36300A'; Inserted='#0E2A14'; Deleted='#2A1020'; Identical='Transparent'; CharModified='#F9E2AF'; FoldFg='#6C7086'; FoldBg='#313244'; MmModified='#F9E2AF'; MmInserted='#A6E3A1'; MmDeleted='#F38BA8'; MmIdentical='#45475A'; Patched='#162A20'; PickerBg='#313244'; PickerHL='#45475A' }
    'CatppuccinLatte' = @{ Modified='#FFF5D0'; Inserted='#E8F8EE'; Deleted='#FFECF0'; Identical='Transparent'; CharModified='#DF8E1D'; FoldFg='#7C7F93'; FoldBg='#CCD0DA'; MmModified='#DF8E1D'; MmInserted='#40A02B'; MmDeleted='#D20F39'; MmIdentical='#DCE0E8'; Patched='#E8F8F0'; PickerBg='#EFF1F5'; PickerHL='#1E66F5' }
    'Cyberpunk'       = @{ Modified='#1A1A00'; Inserted='#001A0A'; Deleted='#1A0010'; Identical='Transparent'; CharModified='#FFFF00'; FoldFg='#00FF9F'; FoldBg='#0D0D1A'; MmModified='#FFE600'; MmInserted='#00FF9F'; MmDeleted='#FF003C'; MmIdentical='#1A1A33'; Patched='#003320'; PickerBg='#1A1A33'; PickerHL='#00FFFF' }
    'DarkGlass'       = @{ Modified='#333300'; Inserted='#103018'; Deleted='#301010'; Identical='Transparent'; CharModified='#999900'; FoldFg='#808080'; FoldBg='#28283A'; MmModified='#CCCC00'; MmInserted='#5ABF80'; MmDeleted='#E05555'; MmIdentical='#404050'; Patched='#103025'; PickerBg='#28283A'; PickerHL='#2A4A7A' }
    'Dracula'         = @{ Modified='#383100'; Inserted='#0E2A14'; Deleted='#2A1020'; Identical='Transparent'; CharModified='#F1FA8C'; FoldFg='#6272A4'; FoldBg='#383A4A'; MmModified='#F1FA8C'; MmInserted='#50FA7B'; MmDeleted='#FF5555'; MmIdentical='#44475A'; Patched='#0E2A28'; PickerBg='#383A4A'; PickerHL='#44475A' }
    'Forest'          = @{ Modified='#2A2800'; Inserted='#0A2010'; Deleted='#280A0A'; Identical='Transparent'; CharModified='#9A9A00'; FoldFg='#6A8060'; FoldBg='#243020'; MmModified='#AAAA00'; MmInserted='#6AAA5A'; MmDeleted='#D05050'; MmIdentical='#2A3820'; Patched='#0A2818'; PickerBg='#243020'; PickerHL='#2E5A22' }
    'GruvboxDark'     = @{ Modified='#352D00'; Inserted='#122010'; Deleted='#2A1008'; Identical='Transparent'; CharModified='#FABD2F'; FoldFg='#928374'; FoldBg='#3C3836'; MmModified='#FABD2F'; MmInserted='#8EC07C'; MmDeleted='#FB4934'; MmIdentical='#504945'; Patched='#12281A'; PickerBg='#3C3836'; PickerHL='#458588' }
    'HighContrast'    = @{ Modified='#333300'; Inserted='#003300'; Deleted='#330000'; Identical='Transparent'; CharModified='#FFFF00'; FoldFg='#FFFFFF'; FoldBg='#000000'; MmModified='#FFFF00'; MmInserted='#00FF00'; MmDeleted='#FF0000'; MmIdentical='#333333'; Patched='#003333'; PickerBg='#000000'; PickerHL='#1AEBFF' }
    'Matrix'          = @{ Modified='#1A1A00'; Inserted='#001A08'; Deleted='#1A0000'; Identical='Transparent'; CharModified='#88FF00'; FoldFg='#00AA41'; FoldBg='#001400'; MmModified='#88FF00'; MmInserted='#00FF41'; MmDeleted='#FF2200'; MmIdentical='#002800'; Patched='#002210'; PickerBg='#001400'; PickerHL='#00CC99' }
    'Minimal'         = @{ Modified='#FFFBE6'; Inserted='#F0FFF4'; Deleted='#FFF0F0'; Identical='Transparent'; CharModified='#FFD700'; FoldFg='#808080'; FoldBg='#EBEBEB'; MmModified='#C8A000'; MmInserted='#28A745'; MmDeleted='#C0392B'; MmIdentical='#D8D8D8'; Patched='#F0FFF8'; PickerBg='#F5F5F5'; PickerHL='#D0E4F7' }
    'Nord'            = @{ Modified='#3A3300'; Inserted='#0E2A16'; Deleted='#2A1818'; Identical='Transparent'; CharModified='#EBCB8B'; FoldFg='#4C566A'; FoldBg='#3B4252'; MmModified='#EBCB8B'; MmInserted='#A3BE8C'; MmDeleted='#BF616A'; MmIdentical='#4C566A'; Patched='#0E2828'; PickerBg='#3B4252'; PickerHL='#4C566A' }
    'Office'          = @{ Modified='#FFF8D6'; Inserted='#E6FFED'; Deleted='#FFECEC'; Identical='Transparent'; CharModified='#FFD700'; FoldFg='#6A6A6A'; FoldBg='#E8E8E8'; MmModified='#C8A000'; MmInserted='#107C41'; MmDeleted='#C0392B'; MmIdentical='#D0D0D0'; Patched='#E6FFF8'; PickerBg='#F8F8F8'; PickerHL='#0078D4' }
    'Synthwave84'     = @{ Modified='#2A2800'; Inserted='#0A1A10'; Deleted='#2A0010'; Identical='Transparent'; CharModified='#FF8B39'; FoldFg='#848CA8'; FoldBg='#34294F'; MmModified='#FF8B39'; MmInserted='#72F1B8'; MmDeleted='#FF2A6D'; MmIdentical='#45475A'; Patched='#0A2A20'; PickerBg='#34294F'; PickerHL='#FF2A6D' }
    'TokyoNight'      = @{ Modified='#2A2800'; Inserted='#0E2514'; Deleted='#2A1020'; Identical='Transparent'; CharModified='#E0AF68'; FoldFg='#565F89'; FoldBg='#24283B'; MmModified='#E0AF68'; MmInserted='#9ECE6A'; MmDeleted='#F7768E'; MmIdentical='#3D59A1'; Patched='#0E2828'; PickerBg='#24283B'; PickerHL='#3D59A1' }
    # Docking.Wpf dark/light (same as VS2022Dark / VisualStudio)
    'DockDark'        = @{ Modified='#3A3A00'; Inserted='#14300F'; Deleted='#3A0F0F'; Identical='Transparent'; CharModified='#888800'; FoldFg='#858585'; FoldBg='#2D2D2D'; MmModified='#B8A000'; MmInserted='#4EC94E'; MmDeleted='#F14C4C'; MmIdentical='#404040'; Patched='#003A2A'; PickerBg='#252526'; PickerHL='#04395E' }
    'DockLight'       = @{ Modified='#FFF8D6'; Inserted='#E6FFED'; Deleted='#FFECEC'; Identical='Transparent'; CharModified='#FFDC00'; FoldFg='#6A6A6A'; FoldBg='#F0F0F0'; MmModified='#B8A000'; MmInserted='#24A349'; MmDeleted='#C0392B'; MmIdentical='#D0D0D0'; Patched='#E6FFF8'; PickerBg='#F8F8F8'; PickerHL='#CCE5FF' }
}

function Get-DfTokenBlock {
    param([hashtable]$t)

    return @"

    <!-- Diff tokens (DF_*) - inserted by Add-DiffTokens.ps1 -->
    <SolidColorBrush x:Key="DF_ModifiedLineBrush"     Color="$($t.Modified)" />
    <SolidColorBrush x:Key="DF_InsertedLineBrush"     Color="$($t.Inserted)" />
    <SolidColorBrush x:Key="DF_DeletedLineBrush"      Color="$($t.Deleted)" />
    <SolidColorBrush x:Key="DF_IdenticalLineBrush"    Color="$($t.Identical)" />
    <SolidColorBrush x:Key="DF_CharModifiedBrush"     Color="$($t.CharModified)" />
    <SolidColorBrush x:Key="DF_FoldMarkerForeground"  Color="$($t.FoldFg)" />
    <SolidColorBrush x:Key="DF_FoldMarkerBackground"  Color="$($t.FoldBg)" />
    <SolidColorBrush x:Key="DF_MinimapModified"       Color="$($t.MmModified)" />
    <SolidColorBrush x:Key="DF_MinimapInserted"       Color="$($t.MmInserted)" />
    <SolidColorBrush x:Key="DF_MinimapDeleted"        Color="$($t.MmDeleted)" />
    <SolidColorBrush x:Key="DF_MinimapIdentical"      Color="$($t.MmIdentical)" />
    <SolidColorBrush x:Key="DF_PatchedBrush"          Color="$($t.Patched)" />
    <SolidColorBrush x:Key="DF_PickerBackground"      Color="$($t.PickerBg)" />
    <SolidColorBrush x:Key="DF_PickerHighlightBrush"  Color="$($t.PickerHL)" />
"@
}

function Inject-Tokens {
    param([string]$filePath, [hashtable]$t)

    $content = Get-Content $filePath -Raw -Encoding UTF8

    # Skip if already injected
    if ($content -match 'DF_ModifiedLineBrush') {
        Write-Host "  SKIP (already present): $filePath"
        return
    }

    $block = Get-DfTokenBlock -t $t
    $updated = $content -replace '</ResourceDictionary>', "$block`n</ResourceDictionary>"
    Set-Content $filePath $updated -Encoding UTF8 -NoNewline
    Write-Host "  OK: $filePath"
}

Write-Host "`n=== Injecting DF_* tokens into Shell themes ==="
$shellThemes = @{
    'VS2022Dark'      = 'VS2022Dark'
    'VisualStudio'    = 'VisualStudio'
    'CatppuccinMocha' = 'CatppuccinMocha'
    'CatppuccinLatte' = 'CatppuccinLatte'
    'Cyberpunk'       = 'Cyberpunk'
    'DarkGlass'       = 'DarkGlass'
    'Dracula'         = 'Dracula'
    'Forest'          = 'Forest'
    'GruvboxDark'     = 'GruvboxDark'
    'HighContrast'    = 'HighContrast'
    'Matrix'          = 'Matrix'
    'Minimal'         = 'Minimal'
    'Nord'            = 'Nord'
    'Office'          = 'Office'
    'Synthwave84'     = 'Synthwave84'
    'TokyoNight'      = 'TokyoNight'
}

foreach ($themeName in $shellThemes.Keys) {
    $path = Join-Path $shell "$themeName\Colors.xaml"
    if (Test-Path $path) {
        Inject-Tokens -filePath $path -t $themes[$themeName]
    } else {
        Write-Warning "NOT FOUND: $path"
    }
}

Write-Host "`n=== Injecting DF_* tokens into Docking.Wpf themes ==="
Inject-Tokens -filePath (Join-Path $dock 'Dark\Colors.xaml')  -t $themes['DockDark']
Inject-Tokens -filePath (Join-Path $dock 'Light\Colors.xaml') -t $themes['DockLight']

Write-Host "`nDone. 14 DF_* tokens injected into all 18 Colors.xaml files."
