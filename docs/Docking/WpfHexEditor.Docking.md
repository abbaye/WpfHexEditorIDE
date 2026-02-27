# WpfHexeditor.Docking

## Table des matières

1. [Introduction](#introduction)
2. [Structure du projet](#structure-du-projet)
3. [Architecture Core](#architecture-core)
   - [DockNode](#docknode)
   - [DockSplitNode](#docksplitnode)
   - [DockGroupNode](#dockgroupnode)
   - [DocumentHostNode](#documenthostnode)
   - [DockItem](#dockitem)
   - [DockLayoutRoot](#docklayoutroot)
4. [DockEngine](#dockengine)
   - [Fonctionnalités](#fonctionnalites)
   - [Locked Layout](#locked-layout)
   - [Transactions et NormalizeTree](#transactions-et-normalizetree)
5. [WpfHexeditor.Docking.Wpf](#wpfhexeditordockingwpf)
   - [DockControl](#dockcontrol)
   - [Rendering Strategy](#rendering-strategy)
   - [Overlay Dock & Drag](#overlay-dock--drag)
6. [Serialization](#serialization)
   - [DTOs](#dtos)
   - [Persistence](#persistence)
7. [Themes](#themes)
8. [API Publique](#api-publique)
9. [Règles et contraintes Core](#regles-et-contraintes-core)
10. [Phase de développement recommandée](#phase-de-developpement-recommandee)

---

## Introduction

WpfHexeditor.Docking est une librairie de docking Visual Studio-like conçue pour être intégrée directement dans l'IDE WpfHexEditor. Cette librairie est totalement indépendante, sans dépendances externes, et offre une gestion complète de l'organisation de fenêtres et panels.

Objectifs principaux :

- Docking, Floating et Auto-hide de panels
- Multi-DocumentHost avec un MainDocumentHost central obligatoire
- Splits infinis imbriqués
- Mode "Locked Layout" pour protéger la structure
- Sérialisation des layouts (JSON ou binaire)
- API publique simple pour l'IDE

---

## Structure du projet

```text
WpfHexeditor.Docking
├── Core
├── Wpf
├── Serialization
└── Themes (Dark, Light, VS2022, VS2026)
```

- **Core** : Logique et modèle, aucune dépendance WPF  
- **Wpf** : Projection visuelle et rendu, dépend uniquement de Core  
- **Serialization** : Conversion DockNode <-> DTO pour sauvegarde et restauration  
- **Themes** : ResourceDictionary et styles (optionnel, light/dark)  

---

## Architecture Core

### Diagramme Global

```mermaid
tree
    DockLayoutRoot
        MainDocumentHost
        DockSplitNode
            DockGroupNode (Left Panels)
            DockSplitNode
                MainDocumentHost (central)
                DockGroupNode (Right Panels)
```

### DockNode

Base de tous les éléments de docking.

### DockSplitNode

Split horizontal ou vertical, ratio entre enfants.

### DockGroupNode

Conteneur de panels tabulés.

### DocumentHostNode

Host pour documents avec MainDocumentHost obligatoire.

### DockItem

Panel ou document individuel.

### DockLayoutRoot

Root du layout, garantit l’existence du MainDocumentHost.

### Diagramme Split et Group

```mermaid
tree
    DockSplitNode
        DockGroupNode (Left Panel)
            DockItem A
            DockItem B
        DockSplitNode
            DocumentHostNode (Main)
                DockItem Doc1
                DockItem Doc2
            DockGroupNode (Right Panel)
                DockItem C
```

---

## DockEngine

Fonctionnalités : manipulation de DockNode, multi-DocumentHost, NormalizeTree, locked layouts, transactions.

### Locked Layout

```csharp
[Flags]
public enum DockLockMode
{
    None = 0,
    PreventSplitting = 1,
    PreventUndocking = 2,
    PreventClosing = 4,
    Full = 7
}
```

### Transactions et NormalizeTree

Permet d’éviter les recalculs fréquents et de garder l’arbre cohérent.

---

## WpfHexeditor.Docking.Wpf

### DockControl

Control principal exposant DockNode Root.

### Rendering Strategy

Projection :

- DockSplitNode → Grid + GridSplitter  
- DockGroupNode → TabControl  
- DocumentHostNode → DocumentTabHost  

### Overlay Dock & Drag

- Hit-testing  
- Overlay pour preview docking  
- Calcul du drop target et modification du DockTree  

---

## Serialization

DTOs et persistence pour sauvegarde et restauration du layout, versionable et adaptable.

---

## Themes

ResourceDictionary WPF, light/dark, optionnel.

---

## API Publique

Interface pour IDE pour ajouter panels, documents, split, float, close, load/save layout.

---

## Règles et contraintes Core

1. Toujours au moins un DocumentHost  
2. MainDocumentHost ne peut pas être supprimé  
3. RootNode non null  
4. DockSplitNode au moins 2 enfants  
5. Appeler NormalizeTree() après mutation  

---

## Phase de développement recommandée

1. DockEngine complet (Core)  
2. DockControl WPF  
3. Overlay Dock et drag preview  
4. Serialization/DTOs  
5. Themes et styles  
6. Locked Layout  
7. Fonctionnalités avancées : Ctrl+Tab, MRU, animations  

---

Notes : Core indépendant de WPF, support multi-DocumentHost, nested splits infinis, MainDocumentHost central obligatoire.