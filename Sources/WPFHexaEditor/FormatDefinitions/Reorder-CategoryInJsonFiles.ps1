# Script PowerShell pour repositionner la propriete "category" apres "description"
# dans tous les fichiers JSON

$rootPath = Split-Path -Parent $MyInvocation.MyCommand.Path
$categories = Get-ChildItem -Path $rootPath -Directory

Write-Host "Reorganisation des proprietes JSON..." -ForegroundColor Cyan
Write-Host ""

$totalFiles = 0
$updatedFiles = 0
$errorFiles = 0

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

            # Creer un nouveau hashtable ordonne avec les proprietes dans le bon ordre
            $orderedJson = [ordered]@{}

            # Ordre desire: formatName, version, extensions, description, category, puis le reste
            $propertyOrder = @(
                'formatName',
                'version',
                'extensions',
                'description',
                'category',
                'author',
                'detection',
                'variables',
                'blocks',
                'structures',
                'conditionalBlocks',
                'loops'
            )

            # Ajouter les proprietes dans l'ordre specifie
            foreach ($prop in $propertyOrder) {
                if ($null -ne $json.$prop) {
                    $orderedJson[$prop] = $json.$prop
                }
            }

            # Ajouter toutes les autres proprietes qui ne sont pas dans la liste
            $json.PSObject.Properties | ForEach-Object {
                if (-not $orderedJson.Contains($_.Name)) {
                    $orderedJson[$_.Name] = $_.Value
                }
            }

            # S'assurer que category est definie
            if ([string]::IsNullOrWhiteSpace($orderedJson['category'])) {
                $orderedJson['category'] = $categoryName
            }

            # Convertir en JSON avec indentation
            $newContent = $orderedJson | ConvertTo-Json -Depth 100

            # Sauvegarder le fichier
            $newContent | Set-Content -Path $file.FullName -Encoding UTF8 -NoNewline

            Write-Host "  [OK] $($file.Name)" -ForegroundColor Green
            $updatedFiles++
        }
        catch {
            Write-Host "  [ERROR] $($file.Name) - Erreur: $($_.Exception.Message)" -ForegroundColor Red
            $errorFiles++
        }
    }

    Write-Host ""
}

Write-Host "Resume:" -ForegroundColor Cyan
Write-Host "  Total de fichiers: $totalFiles" -ForegroundColor White
Write-Host "  Fichiers mis a jour: $updatedFiles" -ForegroundColor Green
Write-Host "  Erreurs: $errorFiles" -ForegroundColor Red
Write-Host ""
Write-Host "Termine!" -ForegroundColor Green
