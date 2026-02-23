# Script PowerShell pour ajouter la propriete "category" dans tous les fichiers JSON
# Base sur la structure des dossiers FormatDefinitions

$rootPath = Split-Path -Parent $MyInvocation.MyCommand.Path
$categories = Get-ChildItem -Path $rootPath -Directory

Write-Host "Analyse des categories..." -ForegroundColor Cyan
Write-Host ""

$totalFiles = 0
$updatedFiles = 0
$skippedFiles = 0

foreach ($category in $categories) {
    $categoryName = $category.Name

    # Ignorer les dossiers systeme
    if ($categoryName -eq ".git" -or $categoryName -eq "bin" -or $categoryName -eq "obj") {
        continue
    }

    Write-Host "Categorie: $categoryName" -ForegroundColor Yellow

    # Trouver tous les fichiers JSON dans ce dossier
    $jsonFiles = Get-ChildItem -Path $category.FullName -Filter "*.json" -File

    foreach ($file in $jsonFiles) {
        $totalFiles++

        try {
            # Lire le contenu du fichier
            $content = Get-Content -Path $file.FullName -Raw -Encoding UTF8

            # Parser le JSON
            $json = $content | ConvertFrom-Json

            # Verifier si la propriete "category" existe deja
            if ($null -eq $json.category -or [string]::IsNullOrWhiteSpace($json.category)) {
                # Ajouter la propriete "category"
                $json | Add-Member -MemberType NoteProperty -Name "category" -Value $categoryName -Force

                # Convertir en JSON avec indentation
                $newContent = $json | ConvertTo-Json -Depth 100

                # Sauvegarder le fichier
                $newContent | Set-Content -Path $file.FullName -Encoding UTF8 -NoNewline

                Write-Host "  [OK] $($file.Name) - Categorie '$categoryName' ajoutee" -ForegroundColor Green
                $updatedFiles++
            }
            else {
                Write-Host "  [SKIP] $($file.Name) - Categorie deja presente: '$($json.category)'" -ForegroundColor Gray
                $skippedFiles++
            }
        }
        catch {
            Write-Host "  [ERROR] $($file.Name) - Erreur: $($_.Exception.Message)" -ForegroundColor Red
        }
    }

    Write-Host ""
}

Write-Host "Resume:" -ForegroundColor Cyan
Write-Host "  Total de fichiers: $totalFiles" -ForegroundColor White
Write-Host "  Fichiers mis a jour: $updatedFiles" -ForegroundColor Green
Write-Host "  Fichiers ignores: $skippedFiles" -ForegroundColor Gray
Write-Host ""
Write-Host "Termine!" -ForegroundColor Green
