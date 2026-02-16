# Plan de Portage du HexEditor Control vers Avalonia

## 📋 Résumé Exécutif

Ce plan détaille la stratégie pour porter le WPFHexaEditor Control vers Avalonia UI, permettant ainsi un support cross-platform (Windows, Linux, macOS) tout en maintenant la version WPF existante.

**Approche recommandée :** Abstraction Progressive avec Shared Core
**Durée estimée :** 6-8 semaines
**Code portable existant :** 85% (~30,000 lignes)
**Code nécessitant adaptation :** 15% (~5,000 lignes)

---

## 🎯 Objectifs

1. **Support multi-plateforme** : Permettre l'utilisation du contrôle sur Windows, Linux et macOS via Avalonia
2. **Maintien de la version WPF** : Garantir la compatibilité et les performances de la version WPF existante
3. **Code partagé maximal** : Réutiliser au maximum la logique métier et les services (85% du code)
4. **Architecture propre** : Créer une abstraction minimale et maintenable
5. **Performance identique** : Assurer des performances équivalentes sur les deux plateformes

---

## 📊 Analyse de la Codebase Actuelle

### Structure du Projet (40,940 lignes, 127 fichiers)

```
Sources/WPFHexaEditor/
├── Core/                    (~13,050 lignes) - ✅ 100% PORTABLE
│   ├── Bytes/              (9,522 lignes) - Logique de manipulation des bytes
│   ├── CharacterTable/     (1,891 lignes) - Tables de caractères TBL
│   ├── Interfaces/         (580 lignes) - Contrats métier
│   └── MethodExtension/    (1,057 lignes) - Extensions .NET
│
├── Services/               (~4,305 lignes) - ✅ 98% PORTABLE
│   ├── UndoRedoService.cs
│   ├── SearchService.cs
│   ├── SelectionService.cs
│   ├── ByteDataService.cs
│   ├── StreamService.cs
│   └── 10+ autres services métier
│
├── ViewModels/             (~2,500 lignes) - ✅ 95% PORTABLE
│   ├── HexEditorViewModel.cs (INotifyPropertyChanged)
│   └── HexBoxViewModel.cs
│
├── Controls/               (~8,500 lignes) - ⚠️ 30% NÉCESSITE ADAPTATION
│   ├── HexEditor.xaml.cs   (1,500+ lignes) - DependencyProperty, Events
│   ├── HexViewport.cs      (535 lignes OnRender) - DrawingContext custom
│   ├── HexBox.xaml.cs      (150 lignes) - DependencyProperty
│   ├── BarChartPanel.cs    (171 lignes OnRender) - Histogramme de fréquence
│   ├── ScrollMarkerPanel.cs (291 lignes) - Marqueurs de scroll
│   └── Caret.cs            (264 lignes) - Curseur clignotant
│
├── Commands/               (~200 lignes) - ⚠️ NÉCESSITE LÉGÈRE ADAPTATION
│   └── RelayCommand.cs     (CommandManager.RequerySuggested)
│
├── Converters/             (~800 lignes) - ⚠️ NÉCESSITE ADAPTATION
│   ├── BoolToSelectionBrushConverter.cs
│   ├── ActionToBrushConverter.cs
│   └── 15+ autres converters
│
└── Dialog/                 (~3,000 lignes) - ⚠️ NÉCESSITE ADAPTATION
    ├── FindReplaceWindow.xaml
    ├── GotoWindow.xaml
    └── Autres fenêtres de dialogue
```

### Dépendances WPF Critiques Identifiées

| Dépendance | Occurrences | Impact | Priorité |
|------------|-------------|--------|----------|
| **DrawingContext (OnRender)** | 10 contrôles | CRITIQUE | P0 |
| **DependencyProperty** | 458 usages | ÉLEVÉ | P1 |
| **System.Windows.Media.Color/Brush** | ~200 usages | ÉLEVÉ | P0 |
| **KeyEventArgs / MouseButtonEventArgs** | ~50 usages | MOYEN | P1 |
| **DispatcherTimer** | 2 usages | MOYEN | P1 |
| **CommandManager.RequerySuggested** | 1 usage | FAIBLE | P2 |
| **IValueConverter** | 15 converters | MOYEN | P2 |
| **FindName() / Visual Tree** | ~5 usages | FAIBLE | P3 |

---

## 🏗️ Architecture Proposée : Abstraction Progressive

### Vue d'Ensemble

```
┌─────────────────────────────────────────────────────────────┐
│                    APPLICATION LAYER                         │
│  ┌─────────────────────┐      ┌─────────────────────┐      │
│  │  WPF Sample App     │      │ Avalonia Sample App │      │
│  └──────────┬──────────┘      └──────────┬──────────┘      │
└─────────────┼──────────────────────────────┼────────────────┘
              │                              │
┌─────────────┼──────────────────────────────┼────────────────┐
│             ▼                              ▼                 │
│  ┌──────────────────┐          ┌──────────────────┐        │
│  │ WpfHexaEditor.Wpf│          │WpfHexaEditor     │        │
│  │                  │          │  .Avalonia       │        │
│  │ - HexEditor.xaml │          │ - HexEditor.axaml│        │
│  │ - HexViewport.cs │          │ - HexViewport.cs │        │
│  │ - WPF Platform   │          │ - Avalonia       │        │
│  │   Implementations│          │   Platform Impl. │        │
│  └────────┬─────────┘          └─────────┬────────┘        │
│           │                              │                  │
│           └──────────────┬───────────────┘                  │
│                          ▼                                   │
│         ┌────────────────────────────────┐                  │
│         │   WpfHexaEditor.Core           │                  │
│         │   (Platform-Agnostic)          │                  │
│         │                                │                  │
│         │ ┌────────────────────────────┐ │                  │
│         │ │ Platform Abstractions      │ │                  │
│         │ │ - IDrawingContext          │ │                  │
│         │ │ - PlatformColor/Brush      │ │                  │
│         │ │ - PlatformKey/Input        │ │                  │
│         │ │ - IPlatformTimer           │ │                  │
│         │ └────────────────────────────┘ │                  │
│         │                                │                  │
│         │ ┌────────────────────────────┐ │                  │
│         │ │ Business Logic (Portable)  │ │                  │
│         │ │ - Core/Bytes (13,050 LOC) │ │                  │
│         │ │ - Services (4,305 LOC)    │ │                  │
│         │ │ - ViewModels (2,500 LOC)  │ │                  │
│         │ │ - Models                   │ │                  │
│         │ │ - Events                   │ │                  │
│         │ └────────────────────────────┘ │                  │
│         └────────────────────────────────┘                  │
│                  SHARED CORE LAYER                           │
└─────────────────────────────────────────────────────────────┘
```

### Principe Clé : Minimal Abstraction

**Philosophie :** Abstraire uniquement ce qui est strictement nécessaire pour supporter les deux plateformes, en gardant une API simple et performante.

**Ce qu'on abstrait :**
- ✅ Rendering (DrawingContext) - CRITIQUE
- ✅ Colors/Brushes - CRITIQUE
- ✅ Input (Key/Mouse) - IMPORTANT
- ✅ Timer - IMPORTANT

**Ce qu'on N'abstrait PAS :**
- ❌ DependencyProperty / AvaloniaProperty (on les garde séparés)
- ❌ XAML (versions séparées .xaml / .axaml)
- ❌ Converters (versions séparées ou remplacement Avalonia)
- ❌ Dialogs (implémentations séparées)

---

## 📁 Structure de Projet Proposée

```
WpfHexEditorControl/
├── Sources/
│   │
│   ├── WpfHexaEditor.Core/              # ⭐ NOUVEAU - Core portable
│   │   ├── WpfHexaEditor.Core.csproj    (netstandard2.0)
│   │   │
│   │   ├── Platform/                     # Abstractions plateforme
│   │   │   ├── Rendering/
│   │   │   │   ├── IDrawingContext.cs
│   │   │   │   ├── IFormattedText.cs
│   │   │   │   ├── ITypeface.cs
│   │   │   │   └── PlatformGeometry.cs  (Rect, Point, Size structs)
│   │   │   │
│   │   │   ├── Media/
│   │   │   │   ├── PlatformColor.cs     (struct ARGB)
│   │   │   │   ├── IBrush.cs
│   │   │   │   ├── IPen.cs
│   │   │   │   ├── PlatformSolidColorBrush.cs
│   │   │   │   └── PlatformBrushes.cs   (static common brushes)
│   │   │   │
│   │   │   ├── Input/
│   │   │   │   ├── PlatformKey.cs       (enum)
│   │   │   │   ├── PlatformModifierKeys.cs (flags enum)
│   │   │   │   ├── PlatformMouseButton.cs
│   │   │   │   ├── PlatformKeyEventArgs.cs
│   │   │   │   └── KeyConverter.cs
│   │   │   │
│   │   │   ├── Threading/
│   │   │   │   └── IPlatformTimer.cs
│   │   │   │
│   │   │   └── Controls/
│   │   │       └── PlatformControlBase.cs (base optionnelle)
│   │   │
│   │   ├── Core/                         # ✅ Déjà portable (déplacé)
│   │   │   ├── Bytes/
│   │   │   │   ├── BaseByte.cs
│   │   │   │   ├── ByteModified.cs
│   │   │   │   ├── ByteProvider/
│   │   │   │   └── ...
│   │   │   ├── CharacterTable/
│   │   │   │   ├── TBLStream.cs
│   │   │   │   └── DTE.cs
│   │   │   ├── Interfaces/
│   │   │   └── MethodExtension/
│   │   │
│   │   ├── Services/                     # ✅ Déjà portable (déplacé)
│   │   │   ├── UndoRedoService.cs
│   │   │   ├── SearchService.cs
│   │   │   ├── SelectionService.cs
│   │   │   └── ...
│   │   │
│   │   ├── ViewModels/                   # ✅ Déjà portable (déplacé)
│   │   │   ├── HexEditorViewModel.cs
│   │   │   └── HexBoxViewModel.cs
│   │   │
│   │   ├── Models/                       # ✅ Déjà portable (déplacé)
│   │   │   ├── HexLine.cs
│   │   │   ├── ByteData.cs
│   │   │   └── ...
│   │   │
│   │   └── Events/                       # ✅ Déjà portable (déplacé)
│   │       ├── ByteModifiedEventArgs.cs
│   │       ├── PositionChangedEventArgs.cs
│   │       └── ...
│   │
│   ├── WpfHexaEditor/                    # ⚠️ RENOMMÉ EN WpfHexaEditor.Wpf
│   │   ├── WpfHexaEditor.Wpf.csproj     (net48;net8.0-windows)
│   │   │   DefineConstants: WPF
│   │   │   UseWPF: true
│   │   │
│   │   ├── Platform/                     # Implémentations WPF
│   │   │   ├── Rendering/
│   │   │   │   ├── WpfDrawingContext.cs
│   │   │   │   ├── WpfFormattedText.cs
│   │   │   │   └── WpfTypeface.cs
│   │   │   │
│   │   │   ├── Media/
│   │   │   │   ├── WpfBrush.cs
│   │   │   │   └── WpfPen.cs
│   │   │   │
│   │   │   ├── Input/
│   │   │   │   └── WpfKeyConverter.cs
│   │   │   │
│   │   │   └── Threading/
│   │   │       └── WpfDispatcherTimer.cs
│   │   │
│   │   ├── Controls/                     # Contrôles WPF
│   │   │   ├── HexEditor.xaml
│   │   │   ├── HexEditor.xaml.cs         (adapté pour abstractions)
│   │   │   ├── HexViewport.cs            (OnRender -> IDrawingContext)
│   │   │   ├── HexBox.xaml
│   │   │   ├── HexBox.xaml.cs
│   │   │   ├── BarChartPanel.cs
│   │   │   ├── ScrollMarkerPanel.cs
│   │   │   └── Caret.cs
│   │   │
│   │   ├── Converters/                   # IValueConverter WPF
│   │   │   ├── BoolToSelectionBrushConverter.cs
│   │   │   └── ...
│   │   │
│   │   ├── Dialog/                       # Dialogues WPF
│   │   │   ├── FindReplaceWindow.xaml
│   │   │   └── ...
│   │   │
│   │   └── Commands/                     # Commands WPF
│   │       ├── RelayCommand.cs           (#if WPF)
│   │       └── RelayCommand_T.cs
│   │
│   ├── WpfHexaEditor.Avalonia/           # ⭐ NOUVEAU - Version Avalonia
│   │   ├── WpfHexaEditor.Avalonia.csproj (net8.0)
│   │   │   DefineConstants: AVALONIA
│   │   │   PackageReference: Avalonia 11.0+
│   │   │
│   │   ├── Platform/                     # Implémentations Avalonia
│   │   │   ├── Rendering/
│   │   │   │   ├── AvaloniaDrawingContext.cs
│   │   │   │   ├── AvaloniaFormattedText.cs
│   │   │   │   └── AvaloniaTypeface.cs
│   │   │   │
│   │   │   ├── Media/
│   │   │   │   ├── AvaloniaBrush.cs
│   │   │   │   └── AvaloniaPen.cs
│   │   │   │
│   │   │   ├── Input/
│   │   │   │   └── AvaloniaKeyConverter.cs
│   │   │   │
│   │   │   └── Threading/
│   │   │       └── AvaloniaDispatcherTimer.cs
│   │   │
│   │   ├── Controls/                     # Contrôles Avalonia
│   │   │   ├── HexEditor.axaml           (XAML Avalonia)
│   │   │   ├── HexEditor.axaml.cs        (code-behind adapté)
│   │   │   ├── HexViewport.cs            (Render -> IDrawingContext)
│   │   │   ├── HexBox.axaml
│   │   │   ├── HexBox.axaml.cs
│   │   │   ├── BarChartPanel.cs
│   │   │   ├── ScrollMarkerPanel.cs
│   │   │   └── Caret.cs
│   │   │
│   │   ├── Converters/                   # Avalonia converters
│   │   │   ├── BoolToSelectionBrushConverter.cs
│   │   │   └── ...
│   │   │
│   │   ├── Views/                        # Views Avalonia
│   │   │   ├── FindReplaceWindow.axaml
│   │   │   └── ...
│   │   │
│   │   └── Commands/                     # Commands Avalonia
│   │       ├── RelayCommand.cs           (#if AVALONIA)
│   │       └── RelayCommand_T.cs
│   │
│   └── Samples/
│       ├── WpfHexEditor.Sample.Main/     # ✅ Existant WPF
│       └── AvaloniaHexEditor.Sample/     # ⭐ NOUVEAU - Sample Avalonia
│
└── AVALONIA_PORTAGE_PLAN.md              # Ce fichier
```

---

## 🔧 Abstractions Techniques Détaillées

### 1. Abstraction du Rendering (Priorité P0)

**Fichiers à créer :**

#### `WpfHexaEditor.Core/Platform/Rendering/IDrawingContext.cs`
```csharp
namespace WpfHexaEditor.Platform.Rendering
{
    public interface IDrawingContext : IDisposable
    {
        void DrawRectangle(IBrush brush, IPen pen, PlatformRect rect);
        void DrawText(IFormattedText text, PlatformPoint origin);
        void DrawLine(IPen pen, PlatformPoint start, PlatformPoint end);
        void DrawEllipse(IBrush brush, IPen pen, PlatformPoint center, double radiusX, double radiusY);
    }

    public interface IFormattedText
    {
        string Text { get; }
        double Width { get; }
        double Height { get; }
    }
}
```

#### `WpfHexaEditor.Core/Platform/Rendering/PlatformGeometry.cs`
```csharp
namespace WpfHexaEditor.Platform.Rendering
{
    public struct PlatformRect
    {
        public double X, Y, Width, Height;
        public PlatformRect(double x, double y, double width, double height)
        { X = x; Y = y; Width = width; Height = height; }
    }

    public struct PlatformPoint
    {
        public double X, Y;
        public PlatformPoint(double x, double y) { X = x; Y = y; }
    }

    public struct PlatformSize
    {
        public double Width, Height;
        public PlatformSize(double width, double height) { Width = width; Height = height; }
    }
}
```

**Impact :**
- 10 fichiers à modifier (HexViewport, BarChartPanel, Caret, etc.)
- ~500 lignes de code de rendu à adapter
- Performance : Overhead négligeable (<2ns par appel)

---

### 2. Abstraction des Couleurs/Brushes (Priorité P0)

#### `WpfHexaEditor.Core/Platform/Media/PlatformColor.cs`
```csharp
namespace WpfHexaEditor.Platform.Media
{
    public struct PlatformColor : IEquatable<PlatformColor>
    {
        public byte A, R, G, B;

        public static PlatformColor FromArgb(byte a, byte r, byte g, byte b)
            => new() { A = a, R = r, G = g, B = b };

        public static PlatformColor FromRgb(byte r, byte g, byte b)
            => FromArgb(255, r, g, b);

        // Conversions implicites
#if WPF
        public static implicit operator System.Windows.Media.Color(PlatformColor c)
            => System.Windows.Media.Color.FromArgb(c.A, c.R, c.G, c.B);
#elif AVALONIA
        public static implicit operator Avalonia.Media.Color(PlatformColor c)
            => Avalonia.Media.Color.FromArgb(c.A, c.R, c.G, c.B);
#endif
    }

    public interface IBrush
    {
        bool IsFrozen { get; }
        void Freeze();
    }

    public class PlatformSolidColorBrush : IBrush
    {
        private static readonly Dictionary<PlatformColor, PlatformSolidColorBrush> _cache = new();

        public PlatformColor Color { get; }
        public bool IsFrozen { get; private set; }

        // Implémentation avec cache pour performance
        public static PlatformSolidColorBrush GetFrozen(PlatformColor color)
        {
            if (_cache.TryGetValue(color, out var cached))
                return cached;

            var brush = new PlatformSolidColorBrush(color);
            brush.Freeze();
            _cache[color] = brush;
            return brush;
        }
    }
}
```

**Impact :**
- ~200 usages de Color/Brush à migrer
- Performance : Amélioration grâce au cache de brushes frozen

---

### 3. Abstraction de l'Input (Priorité P1)

#### `WpfHexaEditor.Core/Platform/Input/PlatformKey.cs`
```csharp
namespace WpfHexaEditor.Platform.Input
{
    public enum PlatformKey
    {
        None = 0,
        A, B, C, D, E, F, G, H, I, J, K, L, M,
        N, O, P, Q, R, S, T, U, V, W, X, Y, Z,
        D0, D1, D2, D3, D4, D5, D6, D7, D8, D9,
        Left, Right, Up, Down,
        Home, End, PageUp, PageDown,
        Enter, Escape, Tab, Back, Delete, Space,
        F1, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12
    }

    [Flags]
    public enum PlatformModifierKeys
    {
        None = 0,
        Alt = 1,
        Control = 2,
        Shift = 4,
        Windows = 8
    }

    public static class KeyConverter
    {
#if WPF
        public static PlatformKey ToPlatformKey(System.Windows.Input.Key key)
        {
            return key switch
            {
                System.Windows.Input.Key.A => PlatformKey.A,
                System.Windows.Input.Key.Delete => PlatformKey.Delete,
                // ... mapping complet
                _ => PlatformKey.None
            };
        }
#elif AVALONIA
        public static PlatformKey ToPlatformKey(Avalonia.Input.Key key)
        {
            return key switch
            {
                Avalonia.Input.Key.A => PlatformKey.A,
                Avalonia.Input.Key.Delete => PlatformKey.Delete,
                // ... mapping complet
                _ => PlatformKey.None
            };
        }
#endif
    }
}
```

**Impact :**
- ~50 event handlers à adapter
- Pattern : Convertir en PlatformKey au point d'entrée, logique métier reste identique

---

### 4. Abstraction du Timer (Priorité P1)

#### `WpfHexaEditor.Core/Platform/Threading/IPlatformTimer.cs`
```csharp
namespace WpfHexaEditor.Platform.Threading
{
    public interface IPlatformTimer : IDisposable
    {
        TimeSpan Interval { get; set; }
        bool IsEnabled { get; set; }

        event EventHandler Tick;

        void Start();
        void Stop();
    }

    public static class PlatformTimer
    {
        public static IPlatformTimer Create() =>
#if WPF
            new WpfDispatcherTimer();
#elif AVALONIA
            new AvaloniaDispatcherTimer();
#endif
    }
}
```

**Impact :**
- 2 usages (auto-scroll HexEditor, clignotement Caret)
- Migration simple

---

## 📅 Plan d'Implémentation par Phases

### **Phase 1 : Préparation et Infrastructure (Semaine 1-2)**

**Objectif :** Créer la structure de projet et les abstractions de base

#### Tâches :
1. **Créer WpfHexaEditor.Core project**
   - Créer WpfHexaEditor.Core.csproj (netstandard2.0)
   - Configurer la solution pour multi-targeting

2. **Déplacer le code portable vers Core**
   - Déplacer Core/Bytes/ (13,050 lignes)
   - Déplacer Services/ (4,305 lignes)
   - Déplacer ViewModels/ (2,500 lignes)
   - Déplacer Models/ et Events/
   - Mettre à jour les namespaces
   - Résoudre les dépendances circulaires

3. **Créer les abstractions Platform**
   - Créer Platform/Rendering/
     - IDrawingContext.cs
     - IFormattedText.cs
     - PlatformGeometry.cs (Rect, Point, Size)

   - Créer Platform/Media/
     - PlatformColor.cs
     - IBrush.cs, IPen.cs
     - PlatformSolidColorBrush.cs
     - PlatformBrushes.cs (static helpers)

   - Créer Platform/Input/
     - PlatformKey.cs (enum)
     - PlatformModifierKeys.cs
     - KeyConverter.cs

   - Créer Platform/Threading/
     - IPlatformTimer.cs
     - PlatformTimer.cs (factory)

4. **Tests de compilation**
   - Vérifier que WpfHexaEditor.Core compile sans erreur
   - Documenter les abstractions créées

**Livrables :**
- ✅ WpfHexaEditor.Core project créé et compilable
- ✅ ~20,000 lignes de code portable isolées
- ✅ 4 abstractions Platform définies
- ✅ Documentation des interfaces

---

### **Phase 2 : Implémentation WPF des Abstractions (Semaine 3)**

**Objectif :** Adapter la version WPF existante pour utiliser les abstractions

#### Tâches :
1. **Renommer le projet WPF**
   - Renommer WpfHexaEditor → WpfHexaEditor.Wpf
   - Ajouter référence vers WpfHexaEditor.Core
   - Ajouter DefineConstants: WPF

2. **Implémenter Platform/Rendering pour WPF**
   - WpfDrawingContext.cs (wrapper DrawingContext)
   - WpfFormattedText.cs (wrapper FormattedText)
   - WpfTypeface.cs

3. **Implémenter Platform/Media pour WPF**
   - WpfBrush.cs (wrapper SolidColorBrush)
   - WpfPen.cs (wrapper Pen)
   - Tester conversions implicites PlatformColor ↔ System.Windows.Media.Color

4. **Implémenter Platform/Input pour WPF**
   - WpfKeyConverter.cs (mapping Key → PlatformKey)
   - WpfModifierKeysHelper.cs

5. **Implémenter Platform/Threading pour WPF**
   - WpfDispatcherTimer.cs (wrapper DispatcherTimer)

**Livrables :**
- ✅ 5 implémentations WPF des abstractions Platform
- ✅ Tous les wrappers testés unitairement
- ✅ Performance baseline mesurée

---

### **Phase 3 : Migration des Contrôles WPF vers Abstractions (Semaine 4-5)**

**Objectif :** Adapter les contrôles WPF pour utiliser les abstractions au lieu des APIs WPF directes

#### Tâches (par ordre de priorité) :

1. **Migrer HexViewport.cs** (CRITIQUE)
   - Fichier : `Controls/HexViewport.cs` (535 lignes OnRender)
   - Modifier `OnRender(DrawingContext dc)` pour wrapper vers `IDrawingContext`
   - Remplacer tous les `dc.DrawRectangle()` par versions abstraites
   - Remplacer `System.Windows.Media.Color` par `PlatformColor`
   - Remplacer `SolidColorBrush` par `PlatformSolidColorBrush`
   - Tester le rendu (validation visuelle)

2. **Migrer BarChartPanel.cs**
   - Fichier : `Controls/BarChartPanel.cs` (171 lignes OnRender)
   - Adapter OnRender vers IDrawingContext
   - Tester l'histogramme de fréquence

3. **Migrer Caret.cs**
   - Fichier : `Core/Caret.cs` (264 lignes OnRender)
   - Adapter OnRender vers IDrawingContext
   - Migrer DispatcherTimer vers IPlatformTimer
   - Tester le clignotement

4. **Migrer ScrollMarkerPanel.cs**
   - Fichier : `Controls/ScrollMarkerPanel.cs` (291 lignes)
   - Adapter OnRender vers IDrawingContext

5. **Migrer HexEditor.xaml.cs** (COMPLEXE)
   - Fichier : `HexEditor.xaml.cs` (1,500+ lignes)
   - Migrer auto-scroll DispatcherTimer vers IPlatformTimer
   - Adapter les event handlers clavier (KeyEventArgs → PlatformKeyEventArgs)
   - Garder les DependencyProperty tels quels (pas d'abstraction)
   - Tester toutes les fonctionnalités

6. **Migrer HexBox.xaml.cs**
   - Fichier : `Controls/HexBox.xaml.cs` (150 lignes)
   - Adapter les event handlers
   - Garder les DependencyProperty

7. **Migrer BaseByte.cs, FastTextLine.cs, StringByte.cs**
   - Ces classes ont aussi des OnRender à migrer

8. **Adapter RelayCommand.cs**
   - Fichier : `Commands/RelayCommand.cs`
   - Ajouter `#if WPF` pour CommandManager.RequerySuggested
   - Préparer version Avalonia (événement manuel)

**Tests :**
- ✅ Validation visuelle complète de l'interface WPF
- ✅ Tests de performance (rendu, scrolling, sélection)
- ✅ Tests fonctionnels (édition, recherche, undo/redo)
- ✅ Pas de régression par rapport à la version originale

**Livrables :**
- ✅ Version WPF 100% fonctionnelle utilisant les abstractions
- ✅ 0 régression de fonctionnalité
- ✅ Performance identique ou meilleure

---

### **Phase 4 : Création de la Version Avalonia (Semaine 6-7)**

**Objectif :** Créer le projet Avalonia et implémenter les abstractions Platform

#### Tâches :

1. **Créer WpfHexaEditor.Avalonia project**
   - Créer WpfHexaEditor.Avalonia.csproj (net8.0)
   - Ajouter PackageReference Avalonia 11.0+
   - Ajouter DefineConstants: AVALONIA
   - Ajouter référence vers WpfHexaEditor.Core

2. **Implémenter Platform/Rendering pour Avalonia**
   - AvaloniaDrawingContext.cs (wrapper Avalonia.Media.DrawingContext)
   - AvaloniaFormattedText.cs (wrapper Avalonia.Media.FormattedText)
   - AvaloniaTypeface.cs
   - **Attention :** Avalonia DrawingContext.Dispose() est nécessaire

3. **Implémenter Platform/Media pour Avalonia**
   - AvaloniaBrush.cs (wrapper Avalonia.Media.ISolidColorBrush)
   - AvaloniaPen.cs (wrapper Avalonia.Media.Pen)
   - Tester conversions PlatformColor ↔ Avalonia.Media.Color

4. **Implémenter Platform/Input pour Avalonia**
   - AvaloniaKeyConverter.cs (mapping Avalonia.Input.Key → PlatformKey)
   - AvaloniaModifierKeysHelper.cs

5. **Implémenter Platform/Threading pour Avalonia**
   - AvaloniaDispatcherTimer.cs (wrapper Avalonia.Threading.DispatcherTimer)

**Livrables :**
- ✅ WpfHexaEditor.Avalonia project créé
- ✅ 5 implémentations Avalonia des abstractions Platform
- ✅ Tests unitaires des implémentations

---

### **Phase 5 : Portage des Contrôles vers Avalonia (Semaine 7-8)**

**Objectif :** Porter les contrôles WPF vers Avalonia

#### Tâches :

1. **Porter HexViewport.cs vers Avalonia**
   - Copier `HexViewport.cs` dans WpfHexaEditor.Avalonia/Controls/
   - Changer `FrameworkElement` → `Avalonia.Controls.Control`
   - Changer `OnRender()` → `Render()`
   - Le code OnRenderCore() utilisant IDrawingContext est déjà portable !
   - Tester le rendu de base

2. **Porter BarChartPanel.cs**
   - Copier et adapter la classe
   - Tester l'histogramme

3. **Porter Caret.cs**
   - Copier et adapter
   - Tester le clignotement

4. **Porter ScrollMarkerPanel.cs**
   - Copier et adapter

5. **Créer HexEditor.axaml (XAML Avalonia)**
   - Créer `Controls/HexEditor.axaml`
   - Adapter le XAML WPF vers syntaxe Avalonia
   - Différences principales :
     - `xmlns:av="https://github.com/avaloniaui"`
     - `Window.Resources` → `Window.Styles`
     - `Style TargetType` syntaxe différente
     - Pas de `x:Static` (utiliser resources)

6. **Porter HexEditor.axaml.cs**
   - Copier `HexEditor.xaml.cs` → `HexEditor.axaml.cs`
   - Remplacer `DependencyProperty` par `StyledProperty<T>` Avalonia
   - Syntaxe différente :
     ```csharp
     // WPF
     public static readonly DependencyProperty AllowZoomProperty =
         DependencyProperty.Register(nameof(AllowZoom), typeof(bool), ...);

     // Avalonia
     public static readonly StyledProperty<bool> AllowZoomProperty =
         AvaloniaProperty.Register<HexEditor, bool>(nameof(AllowZoom), defaultValue: true);
     ```
   - Adapter FindName() → FindControl<T>()
   - Le reste du code utilisant les abstractions fonctionne tel quel !

7. **Porter HexBox.axaml + HexBox.axaml.cs**
   - Créer les fichiers Avalonia
   - Adapter DependencyProperty → StyledProperty

8. **Porter les Converters**
   - Avalonia a `IValueConverter` similaire
   - Copier et adapter les 15 converters
   - Ajuster TryFindResource si nécessaire

9. **Porter les Dialogs**
   - Créer FindReplaceWindow.axaml
   - Créer GotoWindow.axaml
   - Adapter le code-behind

**Livrables :**
- ✅ Contrôle HexEditor fonctionnel sous Avalonia
- ✅ Toutes les fonctionnalités de base opérationnelles
- ✅ Rendu correct sur Windows/Linux/macOS

---

### **Phase 6 : Création d'un Sample Avalonia et Tests (Semaine 8)**

**Objectif :** Créer une application de démonstration Avalonia et tester le portage

#### Tâches :

1. **Créer AvaloniaHexEditor.Sample**
   - Créer projet Avalonia Desktop Application
   - Ajouter référence vers WpfHexaEditor.Avalonia
   - Créer MainWindow.axaml avec HexEditor control

2. **Tests multi-plateformes**
   - Tester sur Windows 11
   - Tester sur Ubuntu 22.04 / Fedora
   - Tester sur macOS (si disponible)
   - Vérifier le rendu sur chaque plateforme

3. **Tests fonctionnels complets**
   - Ouvrir/éditer des fichiers
   - Recherche et remplacement
   - Sélection multi-byte
   - Undo/Redo
   - Copier/coller
   - Thèmes (si supporté)
   - Performance sur gros fichiers (>100 MB)

4. **Documentation**
   - Créer AVALONIA_USAGE.md
   - Documenter les différences entre WPF et Avalonia
   - Exemples d'intégration

**Livrables :**
- ✅ Sample Avalonia fonctionnel
- ✅ Tests réussis sur 2+ plateformes
- ✅ Documentation utilisateur

---

### **Phase 7 : Polish et Documentation (Semaine 9)**

**Objectif :** Finaliser le projet, documenter et préparer la release

#### Tâches :

1. **Performance tuning**
   - Profiler Avalonia vs WPF
   - Optimiser les points chauds
   - Mesurer et comparer les métriques

2. **Gestion des thèmes**
   - Adapter les thèmes existants pour Avalonia
   - Tester Cyberpunk, Light, Dark, etc.

3. **Packaging NuGet**
   - Créer WpfHexaEditor.Core.nupkg
   - Créer WpfHexaEditor.Wpf.nupkg
   - Créer WpfHexaEditor.Avalonia.nupkg
   - Configurer multi-targeting

4. **Documentation complète**
   - README.md mis à jour
   - MIGRATION_GUIDE.md
   - API documentation
   - CHANGELOG.md

5. **CI/CD**
   - Configurer GitHub Actions pour build multi-plateforme
   - Tests automatisés WPF et Avalonia
   - Publication NuGet automatique

**Livrables :**
- ✅ 3 packages NuGet publiés
- ✅ Documentation complète
- ✅ CI/CD configuré
- ✅ Release v3.0.0 avec support Avalonia

---

## 📊 Fichiers Critiques à Modifier

### Priorité P0 (Bloquer si non fait)

| Fichier | Lignes | Description | Changements |
|---------|--------|-------------|-------------|
| `Controls/HexViewport.cs` | 535 | Viewport principal avec rendu custom | OnRender → IDrawingContext (150 LOC modifiées) |
| `Controls/BarChartPanel.cs` | 171 | Histogramme de fréquence | OnRender → IDrawingContext (50 LOC) |
| `Core/Caret.cs` | 264 | Curseur clignotant | OnRender + Timer (40 LOC) |
| `Controls/ScrollMarkerPanel.cs` | 291 | Marqueurs de recherche | OnRender (30 LOC) |

### Priorité P1 (Important)

| Fichier | Lignes | Description | Changements |
|---------|--------|-------------|-------------|
| `HexEditor.xaml.cs` | 1,500+ | Contrôle principal | Timer, Input events (100 LOC) |
| `Controls/HexBox.xaml.cs` | 150 | Spinner hex | Input events (20 LOC) |
| `Commands/RelayCommand.cs` | 100 | Pattern Command | #if WPF/AVALONIA (10 LOC) |

### Priorité P2 (Nice to have)

| Fichier | Lignes | Description | Changements |
|---------|--------|-------------|-------------|
| `Converters/*.cs` | ~800 | 15+ value converters | Copier et adapter pour Avalonia |
| `Dialog/*.xaml.cs` | ~3,000 | Fenêtres de dialogue | Recréer en Avalonia |
| `Core/BaseByte.cs` | ~200 | OnRender custom | OnRender → IDrawingContext (30 LOC) |

**Total estimé de modifications : ~500 lignes d'abstraction + ~430 lignes d'adaptation = ~1,000 lignes modifiées sur 40,940 (~2.5%)**

---

## ⚠️ Risques et Mitigation

### Risque 1 : Différences de rendu entre WPF et Avalonia
**Impact :** MOYEN
**Probabilité :** ÉLEVÉE

**Description :** Avalonia et WPF ont des différences subtiles dans le rendu (anti-aliasing, subpixel rendering, font metrics)

**Mitigation :**
- Tests visuels systématiques sur chaque plateforme
- Screenshots de référence pour comparaison
- Ajustements de positionnement si nécessaire (épsilon de tolérance)

---

### Risque 2 : Performance dégradée sur Avalonia
**Impact :** ÉLEVÉ
**Probabilité :** MOYENNE

**Description :** Avalonia pourrait être plus lent que WPF pour le rendu custom intensif

**Mitigation :**
- Profiling early (Phase 4)
- Optimisations ciblées (caching, frozen brushes)
- Fallback vers rendu simplifié si nécessaire
- Tests de performance sur gros fichiers (>100 MB)

---

### Risque 3 : Complexité de maintenance de deux codebases
**Impact :** MOYEN
**Probabilité :** ÉLEVÉE

**Description :** Maintenir WPF et Avalonia en parallèle double la surface de test

**Mitigation :**
- Maximiser le code partagé dans Core (~85%)
- Tests automatisés pour les deux plateformes
- CI/CD qui build et test les deux versions
- Documentation claire des différences

---

### Risque 4 : Breaking changes Avalonia
**Impact :** FAIBLE
**Probabilité :** FAIBLE

**Description :** Avalonia 11 → 12 pourrait introduire breaking changes

**Mitigation :**
- Utiliser Avalonia 11 LTS (stable)
- Verrouiller les versions dans .csproj
- Suivre les release notes Avalonia

---

### Risque 5 : DependencyProperty vs StyledProperty incompatibilités
**Impact :** MOYEN
**Probabilité :** MOYENNE

**Description :** Certains patterns WPF (coercion, metadata flags) n'ont pas d'équivalent direct Avalonia

**Mitigation :**
- Ne PAS abstraire les propriétés (garder séparées)
- Implémenter manuellement la logique de coercion dans setters
- Documenter les différences

---

## 📈 Métriques de Succès

### Critères de Validation

| Critère | Cible | Mesure |
|---------|-------|--------|
| **Code partagé** | >80% | 85% du code dans Core |
| **Performance WPF** | 100% baseline | Pas de régression |
| **Performance Avalonia** | >80% de WPF | Scrolling, rendu |
| **Couverture fonctionnelle** | 100% | Toutes features portées |
| **Plateformes supportées** | 3 | Windows, Linux, macOS |
| **Tests automatisés** | >70% coverage | Core + services |
| **Documentation** | Complète | README, guides, API docs |

---

## 🎯 Conclusion et Prochaines Étapes

### Résumé

Ce plan propose une approche **progressive et pragmatique** pour porter le WPFHexaEditor vers Avalonia :

1. ✅ **85% du code est déjà portable** (Core, Services, ViewModels)
2. ✅ **Abstractions minimales** pour le rendu, couleurs, input, timer
3. ✅ **WPF reste la version de référence** (pas de régression)
4. ✅ **Avalonia bénéficie du travail d'architecture V2** (MVVM, services)
5. ✅ **Durée réaliste** : 8-9 semaines pour un développeur expérimenté

### Recommandation

**Approuver et commencer par Phase 1** : Créer WpfHexaEditor.Core et déplacer le code portable. C'est une étape à faible risque qui prépare le terrain pour le reste.

### Questions Ouvertes

1. **Priorité des plateformes** : Windows only d'abord, ou Linux/macOS immédiatement ?
2. **Compatibilité .NET Framework** : Garder net48 pour WPF ou migrer vers net8.0 only ?
3. **Breaking changes** : Accepter des breaking changes dans l'API publique pour simplifier ?
4. **Thèmes Avalonia** : Porter tous les thèmes existants ou uniquement Light/Dark ?

---

## 📚 Références

- [Avalonia Documentation](https://docs.avaloniaui.net/)
- [Avalonia Samples](https://github.com/AvaloniaUI/Avalonia.Samples)
- [WPF to Avalonia Migration Guide](https://docs.avaloniaui.net/docs/next/guides/migration-from-wpf)
- [Avalonia Performance Tips](https://docs.avaloniaui.net/docs/guides/optimization)

---

**Document créé :** 2026-02-16
**Version :** 1.0
**Auteur :** Plan généré par Claude Code
**Statut :** 🟡 En attente d'approbation
