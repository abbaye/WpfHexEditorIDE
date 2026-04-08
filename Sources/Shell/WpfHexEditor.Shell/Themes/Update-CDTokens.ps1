# Update-CDTokens.ps1
# Updates CD_* color token values in all 18 Colors.xaml theme files.
# Each theme gets visually appropriate values derived from its own palette.

$root = Split-Path $PSScriptRoot -Parent
$dockingRoot = Join-Path (Split-Path $root -Parent) "Docking\WpfHexEditor.Docking.Wpf\Themes"

# Map: theme file → CD_* overrides
# Keys not listed fall back to the "dark-default" set below.
$themeTokens = @{

    "Dark\Colors.xaml" = @{
        CD_CanvasBackground             = "#1A1B26"
        CD_CanvasBorderBrush            = "#2A2B3D"
        CD_CanvasGridLineBrush          = "#20213A"
        CD_ClassBoxBackground           = "#1E2030"
        CD_ClassBoxBorderBrush          = "#454B6B"
        CD_ClassBoxHeaderBackground     = "#252840"
        CD_ClassBoxSectionDivider       = "#2E3250"
        CD_ClassNameForeground          = "#64BAFF"
        CD_StereotypeForeground         = "#B0B0C8"
        CD_MemberTextForeground         = "#E8E8F0"
        CD_FieldForeground              = "#9CDCFE"
        CD_PropertyForeground           = "#4EC9B0"
        CD_MethodForeground             = "#DCDCAA"
        CD_EventForeground              = "#CE9178"
        CD_InheritanceArrowBrush        = "#569CD6"
        CD_AssociationArrowBrush        = "#9CDCFE"
        CD_DependencyArrowBrush         = "#858585"
        CD_AggregationArrowBrush        = "#4EC9B0"
        CD_CompositionArrowBrush        = "#CE9178"
        CD_RelationshipLabelForeground  = "#C8C8D8"
        CD_SearchMatchBrush             = "#3A3D1F"
        CD_SearchCurrentMatchBrush      = "#6B5B1A"
        CD_MetricsBadgeGood             = "#1A7F3C"
        CD_MetricsBadgeWarning          = "#7F5B00"
        CD_MetricsBadgeBad              = "#8B1A1A"
    }

    "Light\Colors.xaml" = @{
        CD_CanvasBackground             = "#F0F2F8"
        CD_CanvasBorderBrush            = "#C0C8D8"
        CD_CanvasGridLineBrush          = "#D8DCE8"
        CD_ClassBoxBackground           = "#FFFFFF"
        CD_ClassBoxBorderBrush          = "#A0AACC"
        CD_ClassBoxHeaderBackground     = "#E8EEF8"
        CD_ClassBoxSectionDivider       = "#C8D0E8"
        CD_ClassBoxSelectedBorderBrush  = "#0078D4"
        CD_ClassBoxHoverBorderBrush     = "#50A0A0"
        CD_ClassNameForeground          = "#1A3A6B"
        CD_StereotypeForeground         = "#4A5080"
        CD_MemberTextForeground         = "#1A1A2E"
        CD_FieldForeground              = "#0057A8"
        CD_PropertyForeground           = "#005C5C"
        CD_MethodForeground             = "#7030A0"
        CD_EventForeground              = "#8B3A00"
        CD_InheritanceArrowBrush        = "#2E75B6"
        CD_AssociationArrowBrush        = "#0057A8"
        CD_DependencyArrowBrush         = "#808080"
        CD_AggregationArrowBrush        = "#005C5C"
        CD_CompositionArrowBrush        = "#8B3A00"
        CD_RelationshipLabelForeground  = "#3A3A5A"
        CD_SearchMatchBrush             = "#FFFACD"
        CD_SearchCurrentMatchBrush      = "#FFD700"
        CD_MetricsBadgeGood             = "#107C10"
        CD_MetricsBadgeWarning          = "#C07800"
        CD_MetricsBadgeBad              = "#C42B1C"
    }
}

# Per-theme overrides for Shell themes (keyed by theme folder name)
$shellThemeTokens = @{

    "Nord" = @{
        CD_CanvasBackground             = "#2E3440"
        CD_CanvasBorderBrush            = "#3B4252"
        CD_CanvasGridLineBrush          = "#373E4C"
        CD_ClassBoxBackground           = "#3B4252"
        CD_ClassBoxBorderBrush          = "#5E81AC"
        CD_ClassBoxHeaderBackground     = "#313745"
        CD_ClassBoxSectionDivider       = "#434C5E"
        CD_ClassNameForeground          = "#88C0D0"
        CD_StereotypeForeground         = "#8FBCBB"
        CD_MemberTextForeground         = "#ECEFF4"
        CD_FieldForeground              = "#81A1C1"
        CD_PropertyForeground           = "#88C0D0"
        CD_MethodForeground             = "#EBCB8B"
        CD_EventForeground              = "#D08770"
        CD_InheritanceArrowBrush        = "#5E81AC"
        CD_AssociationArrowBrush        = "#81A1C1"
        CD_DependencyArrowBrush         = "#7A8899"
        CD_AggregationArrowBrush        = "#88C0D0"
        CD_CompositionArrowBrush        = "#D08770"
        CD_RelationshipLabelForeground  = "#D8DEE9"
    }

    "Dracula" = @{
        CD_CanvasBackground             = "#21222C"
        CD_CanvasBorderBrush            = "#3A3C4E"
        CD_CanvasGridLineBrush          = "#2B2D3A"
        CD_ClassBoxBackground           = "#282A36"
        CD_ClassBoxBorderBrush          = "#6272A4"
        CD_ClassBoxHeaderBackground     = "#21222C"
        CD_ClassBoxSectionDivider       = "#3A3C4E"
        CD_ClassNameForeground          = "#8BE9FD"
        CD_StereotypeForeground         = "#BD93F9"
        CD_MemberTextForeground         = "#F8F8F2"
        CD_FieldForeground              = "#66D9E8"
        CD_PropertyForeground           = "#50FA7B"
        CD_MethodForeground             = "#FFB86C"
        CD_EventForeground              = "#FF79C6"
        CD_InheritanceArrowBrush        = "#BD93F9"
        CD_AssociationArrowBrush        = "#8BE9FD"
        CD_DependencyArrowBrush         = "#6272A4"
        CD_AggregationArrowBrush        = "#50FA7B"
        CD_CompositionArrowBrush        = "#FF79C6"
        CD_RelationshipLabelForeground  = "#F8F8F2"
    }

    "TokyoNight" = @{
        CD_CanvasBackground             = "#1A1B26"
        CD_CanvasBorderBrush            = "#292E42"
        CD_CanvasGridLineBrush          = "#20213A"
        CD_ClassBoxBackground           = "#1F2335"
        CD_ClassBoxBorderBrush          = "#7AA2F7"
        CD_ClassBoxHeaderBackground     = "#1A1B26"
        CD_ClassBoxSectionDivider       = "#292E42"
        CD_ClassNameForeground          = "#7DCFFF"
        CD_StereotypeForeground         = "#BB9AF7"
        CD_MemberTextForeground         = "#C0CAF5"
        CD_FieldForeground              = "#7DCFFF"
        CD_PropertyForeground           = "#73DACA"
        CD_MethodForeground             = "#E0AF68"
        CD_EventForeground              = "#F7768E"
        CD_InheritanceArrowBrush        = "#7AA2F7"
        CD_AssociationArrowBrush        = "#7DCFFF"
        CD_DependencyArrowBrush         = "#565F89"
        CD_AggregationArrowBrush        = "#73DACA"
        CD_CompositionArrowBrush        = "#F7768E"
        CD_RelationshipLabelForeground  = "#A9B1D6"
    }

    "GruvboxDark" = @{
        CD_CanvasBackground             = "#282828"
        CD_CanvasBorderBrush            = "#3C3836"
        CD_CanvasGridLineBrush          = "#32302F"
        CD_ClassBoxBackground           = "#3C3836"
        CD_ClassBoxBorderBrush          = "#A89984"
        CD_ClassBoxHeaderBackground     = "#282828"
        CD_ClassBoxSectionDivider       = "#504945"
        CD_ClassNameForeground          = "#83A598"
        CD_StereotypeForeground         = "#D3869B"
        CD_MemberTextForeground         = "#EBDBB2"
        CD_FieldForeground              = "#83A598"
        CD_PropertyForeground           = "#8EC07C"
        CD_MethodForeground             = "#FABD2F"
        CD_EventForeground              = "#FE8019"
        CD_InheritanceArrowBrush        = "#458588"
        CD_AssociationArrowBrush        = "#83A598"
        CD_DependencyArrowBrush         = "#928374"
        CD_AggregationArrowBrush        = "#689D6A"
        CD_CompositionArrowBrush        = "#D65D0E"
        CD_RelationshipLabelForeground  = "#D5C4A1"
    }

    "Synthwave84" = @{
        CD_CanvasBackground             = "#262335"
        CD_CanvasBorderBrush            = "#3B3153"
        CD_CanvasGridLineBrush          = "#2D2542"
        CD_ClassBoxBackground           = "#2A2139"
        CD_ClassBoxBorderBrush          = "#FF7EDB"
        CD_ClassBoxHeaderBackground     = "#1E1731"
        CD_ClassBoxSectionDivider       = "#3B3153"
        CD_ClassNameForeground          = "#36F9F6"
        CD_StereotypeForeground         = "#FF7EDB"
        CD_MemberTextForeground         = "#FFFFFF"
        CD_FieldForeground              = "#36F9F6"
        CD_PropertyForeground           = "#72F1B8"
        CD_MethodForeground             = "#FFF68F"
        CD_EventForeground              = "#FE4450"
        CD_InheritanceArrowBrush        = "#FF7EDB"
        CD_AssociationArrowBrush        = "#36F9F6"
        CD_DependencyArrowBrush         = "#848BBD"
        CD_AggregationArrowBrush        = "#72F1B8"
        CD_CompositionArrowBrush        = "#FE4450"
        CD_RelationshipLabelForeground  = "#E3E3E3"
    }

    "Cyberpunk" = @{
        CD_CanvasBackground             = "#06080F"
        CD_CanvasBorderBrush            = "#0D1428"
        CD_CanvasGridLineBrush          = "#0A0E1E"
        CD_ClassBoxBackground           = "#0D1428"
        CD_ClassBoxBorderBrush          = "#00FFC8"
        CD_ClassBoxHeaderBackground     = "#060810"
        CD_ClassBoxSectionDivider       = "#0D1E38"
        CD_ClassNameForeground          = "#00FFC8"
        CD_StereotypeForeground         = "#F057A1"
        CD_MemberTextForeground         = "#D0E8FF"
        CD_FieldForeground              = "#00CFFF"
        CD_PropertyForeground           = "#00FFC8"
        CD_MethodForeground             = "#FFE44D"
        CD_EventForeground              = "#F057A1"
        CD_InheritanceArrowBrush        = "#00FFC8"
        CD_AssociationArrowBrush        = "#00CFFF"
        CD_DependencyArrowBrush         = "#3A4A6A"
        CD_AggregationArrowBrush        = "#00FF80"
        CD_CompositionArrowBrush        = "#F057A1"
        CD_RelationshipLabelForeground  = "#90B8D8"
    }

    "Matrix" = @{
        CD_CanvasBackground             = "#000000"
        CD_CanvasBorderBrush            = "#003300"
        CD_CanvasGridLineBrush          = "#001800"
        CD_ClassBoxBackground           = "#001200"
        CD_ClassBoxBorderBrush          = "#00AA44"
        CD_ClassBoxHeaderBackground     = "#000A00"
        CD_ClassBoxSectionDivider       = "#003300"
        CD_ClassNameForeground          = "#00FF41"
        CD_StereotypeForeground         = "#00CC33"
        CD_MemberTextForeground         = "#00EE33"
        CD_FieldForeground              = "#00CC33"
        CD_PropertyForeground           = "#00FF80"
        CD_MethodForeground             = "#00FF41"
        CD_EventForeground              = "#66FF44"
        CD_InheritanceArrowBrush        = "#00AA44"
        CD_AssociationArrowBrush        = "#00CC33"
        CD_DependencyArrowBrush         = "#005500"
        CD_AggregationArrowBrush        = "#00EE33"
        CD_CompositionArrowBrush        = "#88FF44"
        CD_RelationshipLabelForeground  = "#00CC33"
    }

    "CatppuccinMocha" = @{
        CD_CanvasBackground             = "#1E1E2E"
        CD_CanvasBorderBrush            = "#313244"
        CD_CanvasGridLineBrush          = "#28283E"
        CD_ClassBoxBackground           = "#2A2A3C"
        CD_ClassBoxBorderBrush          = "#89B4FA"
        CD_ClassBoxHeaderBackground     = "#1E1E2E"
        CD_ClassBoxSectionDivider       = "#313244"
        CD_ClassNameForeground          = "#89DCEB"
        CD_StereotypeForeground         = "#CBA6F7"
        CD_MemberTextForeground         = "#CDD6F4"
        CD_FieldForeground              = "#89DCEB"
        CD_PropertyForeground           = "#94E2D5"
        CD_MethodForeground             = "#F9E2AF"
        CD_EventForeground              = "#FAB387"
        CD_InheritanceArrowBrush        = "#89B4FA"
        CD_AssociationArrowBrush        = "#89DCEB"
        CD_DependencyArrowBrush         = "#585B70"
        CD_AggregationArrowBrush        = "#94E2D5"
        CD_CompositionArrowBrush        = "#FAB387"
        CD_RelationshipLabelForeground  = "#BAC2DE"
    }

    "CatppuccinLatte" = @{
        CD_CanvasBackground             = "#EFF1F5"
        CD_CanvasBorderBrush            = "#CCD0DA"
        CD_CanvasGridLineBrush          = "#E6E9EF"
        CD_ClassBoxBackground           = "#FFFFFF"
        CD_ClassBoxBorderBrush          = "#1E66F5"
        CD_ClassBoxHeaderBackground     = "#E6E9EF"
        CD_ClassBoxSectionDivider       = "#CCD0DA"
        CD_ClassBoxSelectedBorderBrush  = "#1E66F5"
        CD_ClassBoxHoverBorderBrush     = "#04A5E5"
        CD_ClassNameForeground          = "#1E66F5"
        CD_StereotypeForeground         = "#8839EF"
        CD_MemberTextForeground         = "#4C4F69"
        CD_FieldForeground              = "#209FB5"
        CD_PropertyForeground           = "#179299"
        CD_MethodForeground             = "#DF8E1D"
        CD_EventForeground              = "#FE640B"
        CD_InheritanceArrowBrush        = "#1E66F5"
        CD_AssociationArrowBrush        = "#04A5E5"
        CD_DependencyArrowBrush         = "#9CA0B0"
        CD_AggregationArrowBrush        = "#179299"
        CD_CompositionArrowBrush        = "#FE640B"
        CD_RelationshipLabelForeground  = "#5C5F77"
    }

    "DarkGlass" = @{
        CD_CanvasBackground             = "#101418"
        CD_CanvasBorderBrush            = "#1E2428"
        CD_CanvasGridLineBrush          = "#181C20"
        CD_ClassBoxBackground           = "#151A20"
        CD_ClassBoxBorderBrush          = "#2E8BE0"
        CD_ClassBoxHeaderBackground     = "#101418"
        CD_ClassBoxSectionDivider       = "#1E2C38"
        CD_ClassNameForeground          = "#60C8FF"
        CD_StereotypeForeground         = "#A0BFDF"
        CD_MemberTextForeground         = "#D8E8F8"
        CD_FieldForeground              = "#80CFFF"
        CD_PropertyForeground           = "#60DFB8"
        CD_MethodForeground             = "#F0D060"
        CD_EventForeground              = "#F08060"
        CD_InheritanceArrowBrush        = "#2E8BE0"
        CD_AssociationArrowBrush        = "#60C8FF"
        CD_DependencyArrowBrush         = "#4A5A6A"
        CD_AggregationArrowBrush        = "#60DFB8"
        CD_CompositionArrowBrush        = "#F08060"
        CD_RelationshipLabelForeground  = "#A8C8E8"
    }

    "Forest" = @{
        CD_CanvasBackground             = "#1A2218"
        CD_CanvasBorderBrush            = "#2C3828"
        CD_CanvasGridLineBrush          = "#222C20"
        CD_ClassBoxBackground           = "#243020"
        CD_ClassBoxBorderBrush          = "#6AAF5A"
        CD_ClassBoxHeaderBackground     = "#1A2218"
        CD_ClassBoxSectionDivider       = "#2C3828"
        CD_ClassNameForeground          = "#8EDD78"
        CD_StereotypeForeground         = "#A8C890"
        CD_MemberTextForeground         = "#DCECD0"
        CD_FieldForeground              = "#7EC8B0"
        CD_PropertyForeground           = "#80D890"
        CD_MethodForeground             = "#E8D870"
        CD_EventForeground              = "#E8A050"
        CD_InheritanceArrowBrush        = "#6AAF5A"
        CD_AssociationArrowBrush        = "#7EC8B0"
        CD_DependencyArrowBrush         = "#5A6A50"
        CD_AggregationArrowBrush        = "#80D890"
        CD_CompositionArrowBrush        = "#E8A050"
        CD_RelationshipLabelForeground  = "#A8C890"
    }

    "HighContrast" = @{
        CD_CanvasBackground             = "#000000"
        CD_CanvasBorderBrush            = "#FFFFFF"
        CD_CanvasGridLineBrush          = "#222222"
        CD_ClassBoxBackground           = "#000000"
        CD_ClassBoxBorderBrush          = "#FFFFFF"
        CD_ClassBoxHeaderBackground     = "#111111"
        CD_ClassBoxSectionDivider       = "#555555"
        CD_ClassBoxSelectedBorderBrush  = "#FFFF00"
        CD_ClassBoxHoverBorderBrush     = "#00FFFF"
        CD_ClassNameForeground          = "#FFFFFF"
        CD_StereotypeForeground         = "#AAAAAA"
        CD_MemberTextForeground         = "#FFFFFF"
        CD_FieldForeground              = "#00FFFF"
        CD_PropertyForeground           = "#00FF00"
        CD_MethodForeground             = "#FFFF00"
        CD_EventForeground              = "#FF8000"
        CD_InheritanceArrowBrush        = "#FFFFFF"
        CD_AssociationArrowBrush        = "#00FFFF"
        CD_DependencyArrowBrush         = "#AAAAAA"
        CD_AggregationArrowBrush        = "#00FF00"
        CD_CompositionArrowBrush        = "#FF8000"
        CD_RelationshipLabelForeground  = "#FFFFFF"
    }

    "VS2022Dark" = @{
        CD_CanvasBackground             = "#1E1E1E"
        CD_CanvasBorderBrush            = "#2D2D30"
        CD_CanvasGridLineBrush          = "#252526"
        CD_ClassBoxBackground           = "#2D2D30"
        CD_ClassBoxBorderBrush          = "#4A4A4F"
        CD_ClassBoxHeaderBackground     = "#252526"
        CD_ClassBoxSectionDivider       = "#3F3F46"
        CD_ClassNameForeground          = "#4FC1FF"
        CD_StereotypeForeground         = "#9B9B9B"
        CD_MemberTextForeground         = "#E0E0E0"
        CD_FieldForeground              = "#9CDCFE"
        CD_PropertyForeground           = "#4EC9B0"
        CD_MethodForeground             = "#DCDCAA"
        CD_EventForeground              = "#CE9178"
        CD_InheritanceArrowBrush        = "#569CD6"
        CD_AssociationArrowBrush        = "#9CDCFE"
        CD_DependencyArrowBrush         = "#858585"
        CD_AggregationArrowBrush        = "#4EC9B0"
        CD_CompositionArrowBrush        = "#CE9178"
        CD_RelationshipLabelForeground  = "#C8C8C8"
    }

    "VisualStudio" = @{
        # Visual Studio light-ish theme
        CD_CanvasBackground             = "#F5F5F5"
        CD_CanvasBorderBrush            = "#C8C8C8"
        CD_CanvasGridLineBrush          = "#DCDCDC"
        CD_ClassBoxBackground           = "#FFFFFF"
        CD_ClassBoxBorderBrush          = "#0078D7"
        CD_ClassBoxHeaderBackground     = "#EBF3FB"
        CD_ClassBoxSectionDivider       = "#C8D8E8"
        CD_ClassBoxSelectedBorderBrush  = "#0078D7"
        CD_ClassBoxHoverBorderBrush     = "#1C97EA"
        CD_ClassNameForeground          = "#0052A3"
        CD_StereotypeForeground         = "#6040A0"
        CD_MemberTextForeground         = "#1A1A1A"
        CD_FieldForeground              = "#0070C0"
        CD_PropertyForeground           = "#008080"
        CD_MethodForeground             = "#7030A0"
        CD_EventForeground              = "#C55A11"
        CD_InheritanceArrowBrush        = "#0078D7"
        CD_AssociationArrowBrush        = "#0070C0"
        CD_DependencyArrowBrush         = "#808080"
        CD_AggregationArrowBrush        = "#008080"
        CD_CompositionArrowBrush        = "#C55A11"
        CD_RelationshipLabelForeground  = "#3A3A3A"
    }

    "Office" = @{
        CD_CanvasBackground             = "#F3F3F3"
        CD_CanvasBorderBrush            = "#D0D0D0"
        CD_CanvasGridLineBrush          = "#E0E0E0"
        CD_ClassBoxBackground           = "#FFFFFF"
        CD_ClassBoxBorderBrush          = "#2E74B5"
        CD_ClassBoxHeaderBackground     = "#DEEBF7"
        CD_ClassBoxSectionDivider       = "#BDD7EE"
        CD_ClassBoxSelectedBorderBrush  = "#2E74B5"
        CD_ClassBoxHoverBorderBrush     = "#538135"
        CD_ClassNameForeground          = "#1F3864"
        CD_StereotypeForeground         = "#5A3A7A"
        CD_MemberTextForeground         = "#1A1A1A"
        CD_FieldForeground              = "#1F78B4"
        CD_PropertyForeground           = "#006060"
        CD_MethodForeground             = "#7030A0"
        CD_EventForeground              = "#C55A11"
        CD_InheritanceArrowBrush        = "#2E74B5"
        CD_AssociationArrowBrush        = "#1F78B4"
        CD_DependencyArrowBrush         = "#808080"
        CD_AggregationArrowBrush        = "#538135"
        CD_CompositionArrowBrush        = "#C55A11"
        CD_RelationshipLabelForeground  = "#3A3A3A"
    }

    "Minimal" = @{
        CD_CanvasBackground             = "#FAFAFA"
        CD_CanvasBorderBrush            = "#E0E0E0"
        CD_CanvasGridLineBrush          = "#ECECEC"
        CD_ClassBoxBackground           = "#FFFFFF"
        CD_ClassBoxBorderBrush          = "#BDBDBD"
        CD_ClassBoxHeaderBackground     = "#F5F5F5"
        CD_ClassBoxSectionDivider       = "#E0E0E0"
        CD_ClassBoxSelectedBorderBrush  = "#1976D2"
        CD_ClassBoxHoverBorderBrush     = "#42A5F5"
        CD_ClassNameForeground          = "#212121"
        CD_StereotypeForeground         = "#757575"
        CD_MemberTextForeground         = "#424242"
        CD_FieldForeground              = "#1565C0"
        CD_PropertyForeground           = "#00695C"
        CD_MethodForeground             = "#6A1B9A"
        CD_EventForeground              = "#E65100"
        CD_InheritanceArrowBrush        = "#1976D2"
        CD_AssociationArrowBrush        = "#1565C0"
        CD_DependencyArrowBrush         = "#9E9E9E"
        CD_AggregationArrowBrush        = "#00695C"
        CD_CompositionArrowBrush        = "#E65100"
        CD_RelationshipLabelForeground  = "#424242"
    }
}

function Update-ColorsXaml {
    param([string]$FilePath, [hashtable]$Tokens)

    $content = Get-Content $FilePath -Raw -Encoding UTF8
    $changed = 0

    foreach ($key in $Tokens.Keys) {
        $value = $Tokens[$key]
        # Match: Color="#XXXXXX" where the key appears right before
        $pattern = '(?<=x:Key="' + [regex]::Escape($key) + '"[^>]*Color=")#[0-9A-Fa-f]{6,8}(?=")'
        if ($content -match $pattern) {
            $content = $content -replace $pattern, $value
            $changed++
        }
    }

    if ($changed -gt 0) {
        [System.IO.File]::WriteAllText($FilePath, $content, [System.Text.UTF8Encoding]::new($false))
        Write-Host "  Updated $changed tokens in: $FilePath"
    } else {
        Write-Host "  No matches in: $FilePath"
    }
}

# --- Docking themes (Dark + Light) ---
Write-Host "`n=== Docking themes ===" -ForegroundColor Cyan
foreach ($rel in $themeTokens.Keys) {
    $full = Join-Path $dockingRoot $rel
    if (Test-Path $full) {
        Update-ColorsXaml -FilePath $full -Tokens $themeTokens[$rel]
    } else {
        Write-Warning "Not found: $full"
    }
}

# --- Shell themes ---
Write-Host "`n=== Shell themes ===" -ForegroundColor Cyan
$shellRoot = $PSScriptRoot  # This script lives in Shell/WpfHexEditor.Shell/Themes
foreach ($themeFolder in $shellThemeTokens.Keys) {
    $full = Join-Path $shellRoot "$themeFolder\Colors.xaml"
    if (Test-Path $full) {
        Update-ColorsXaml -FilePath $full -Tokens $shellThemeTokens[$themeFolder]
    } else {
        Write-Warning "Not found: $full"
    }
}

Write-Host "`nDone." -ForegroundColor Green
