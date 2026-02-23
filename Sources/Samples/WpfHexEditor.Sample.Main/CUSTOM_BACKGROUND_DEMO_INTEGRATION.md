# CustomBackgroundDemo - Guide d'intégration

## Composant créé

Un UserControl complet de démonstration des CustomBackgroundBlocks a été créé :
- **XAML** : `Views/Components/CustomBackgroundDemo.xaml`
- **Code** : `Views/Components/CustomBackgroundDemo.xaml.cs`

## Fonctionnalités

✅ **Actions rapides** : Ajouter des blocs prédéfinis (Rouge, Bleu, Vert, Jaune)
✅ **Bloc aléatoire** : Générer un bloc à position/couleur aléatoire
✅ **Détection automatique** : Reconnaît ZIP, PNG, PDF, JPEG, EXE
✅ **Liste interactive** : Affiche tous les blocs actifs avec bouton de suppression
✅ **Événements** : Se synchronise automatiquement avec les changements
✅ **Barre de statut** : Feedback temps réel

## Intégration dans MainWindow.xaml

### Étape 1 : Ajouter dans le XAML

Trouvez une section appropriée dans `MainWindow.xaml` (par exemple dans un TabControl ou DockPanel) et ajoutez :

```xml
<!-- Dans vos xmlns declarations en haut -->
xmlns:components="clr-namespace:WpfHexEditor.Sample.Main.Views.Components"

<!-- Où vous voulez afficher la démo (exemple: dans un DockPanel) -->
<DockPanel>
    <!-- À gauche ou à droite du HexEditor -->
    <components:CustomBackgroundDemo x:Name="CustomBackgroundDemoPanel"
                                      DockPanel.Dock="Right"
                                      Width="350"/>

    <!-- Votre HexEditor existant -->
    <wpfHexaEditor:HexEditor x:Name="HexEditorControl" />
</DockPanel>
```

**OU dans un TabControl** (si vous avez déjà des onglets) :

```xml
<TabItem Header="🎨 Highlights">
    <components:CustomBackgroundDemo x:Name="CustomBackgroundDemoPanel"/>
</TabItem>
```

### Étape 2 : Connecter dans MainWindow.xaml.cs

Dans le constructeur `MainWindow()`, après l'initialisation du HexEditor, ajoutez :

```csharp
// Dans le constructeur après HexEditorSettingsPanel.HexEditorControl = HexEditorControl;
if (CustomBackgroundDemoPanel != null)
{
    CustomBackgroundDemoPanel.SetHexEditor(HexEditorControl);
}
```

### Étape 3 : Tester

1. **Compiler** le projet Sample.Main
2. **Ouvrir** un fichier (ZIP, PNG, PDF, ou n'importe quoi)
3. **Cliquer** sur "🔍 Detect File Format" → Les signatures seront automatiquement détectées !
4. **Ajouter** des blocs manuellement avec les boutons colorés
5. **Observer** les highlights dans le HexEditor

## Exemple d'utilisation programmatique

Si vous voulez ajouter des blocs depuis votre propre code :

```csharp
// Ajouter un bloc simple
HexEditorControl.AddCustomBackgroundBlock(
    new CustomBackgroundBlock(
        start: 0,
        length: 16,
        color: Brushes.Red,
        description: "File Header",
        opacity: 0.4
    )
);

// Utiliser le service pour des opérations avancées
var service = HexEditorControl.CustomBackgroundService;

// Vérifier les chevauchements
if (!service.WouldOverlap(100, 50))
{
    service.AddBlock(100, 50, Brushes.Green, "Safe to add");
}

// Obtenir tous les blocs dans une plage
var blocksInRange = service.GetBlocksInRange(0, 1000);

// S'abonner aux événements
HexEditorControl.CustomBackgroundBlockChanged += (s, e) =>
{
    Console.WriteLine($"Blocks changed: {e.ChangeType}, Total: {e.TotalBlockCount}");
};
```

## Format de fichiers détectés

Le bouton "🔍 Detect File Format" reconnaît automatiquement :

| Format | Signature | Description |
|--------|-----------|-------------|
| **ZIP** | `50 4B 03 04` | Archives ZIP/JAR/APK |
| **PNG** | `89 50 4E 47 0D 0A 1A 0A` | Images PNG |
| **PDF** | `25 50 44 46` | Documents PDF |
| **JPEG** | `FF D8` | Images JPEG |
| **EXE** | `4D 5A` | Exécutables Windows (PE) |

## Personnalisation

### Modifier les couleurs

Dans `CustomBackgroundDemo.xaml.cs`, méthodes `Add*Block_Click()` :

```csharp
var block = new CustomBackgroundBlock(
    start: 0,
    length: 16,
    color: new SolidColorBrush(Color.FromRgb(255, 200, 200)), // ← Changez ici
    description: "Votre description",
    opacity: 0.4  // ← Ajustez la transparence
);
```

### Ajouter des détections de formats

Dans `DetectFormat_Click()`, ajoutez vos propres signatures :

```csharp
else if (header.Length >= 4 &&
         header[0] == 0x52 && header[1] == 0x61 && header[2] == 0x72 && header[3] == 0x21)
{
    // RAR archive
    _hexEditor.AddCustomBackgroundBlock(new CustomBackgroundBlock(
        0, 4, new SolidColorBrush(Color.FromRgb(150, 100, 255)),
        "RAR Archive Signature", 0.5));

    ShowStatus("✅ Detected RAR archive!");
}
```

## Intégration avec Phase 4 (Format Detection JSON)

Lorsque la Phase 4 sera implémentée, ce composant pourra être étendu pour :
- Charger des définitions de formats depuis des fichiers JSON
- Afficher un menu déroulant des formats disponibles
- Appliquer automatiquement les blocs définis dans les scripts

## Support

Pour des questions ou améliorations, consultez :
- [CustomBackgroundBlock.cs](../../WPFHexaEditor/Core/CustomBackgroundBlock.cs) - Classe de base
- [CustomBackgroundService.cs](../../WPFHexaEditor/Services/CustomBackgroundService.cs) - Service de gestion
- [Tests unitaires](../../WPFHexaEditor.Tests/CustomBackgroundBlock_Tests.cs) - Exemples d'utilisation
